using ClosedXML.Excel;
using System.Globalization;

namespace PartFinder.Services;

public sealed class BulkInventoryImportService
{
    private const int InsertBatchSize = 50;
    private const int MaxConsecutiveBlankRows = 250;

    private readonly MongoInventoryService _inventory;

    public BulkInventoryImportService(MongoInventoryService inventory)
    {
        _inventory = inventory;
    }

    public async Task<BulkInventoryImportResult> ImportFromFileAsync(
        string sourcePath,
        IProgress<BulkImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new BulkInventoryImportResult
            {
                Errors = ["Could not read the selected file. Try saving it locally and choose it again."],
            };
        }

        List<InventoryImportRow> rows;
        try
        {
            rows = await Task.Run(
                    () => IsCsvPath(sourcePath)
                        ? ParseCsv(sourcePath)
                        : ParseWorkbook(sourcePath),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new BulkInventoryImportResult
            {
                Errors = [$"Could not parse the file: {ex.Message}"],
            };
        }

        if (rows.Count == 0)
        {
            return new BulkInventoryImportResult
            {
                Errors =
                [
                    "No inventory rows found.",
                    "Row 1 must be headers. Required column: Part Name (or PartName).",
                    "Optional: Part ID, Category, Quantity, Min Stock, Unit Price, Location, Supplier, Notes.",
                ],
            };
        }

        var existing = await _inventory.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var byPartId = new Dictionary<string, InventoryDoc>(StringComparer.OrdinalIgnoreCase);
        var byPartName = new Dictionary<string, InventoryDoc>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in existing)
        {
            if (!string.IsNullOrWhiteSpace(doc.PartId) && !byPartId.ContainsKey(doc.PartId))
            {
                byPartId[doc.PartId.Trim()] = doc;
            }

            if (!string.IsNullOrWhiteSpace(doc.PartName) && !byPartName.ContainsKey(doc.PartName))
            {
                byPartName[doc.PartName.Trim()] = doc;
            }
        }

        var total = rows.Count;
        var completed = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();
        var pendingInserts = new List<InventoryDoc>();

        progress?.Report(new BulkImportProgress(0, total, "Starting upload…"));

        async Task FlushInsertsAsync()
        {
            if (pendingInserts.Count == 0)
            {
                return;
            }

            await _inventory.InsertManyAsync(pendingInserts, cancellationToken).ConfigureAwait(false);
            inserted += pendingInserts.Count;
            pendingInserts.Clear();
        }

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(row.PartName))
            {
                skipped++;
                errors.Add($"Row {row.SourceLine}: skipped — Part Name is required.");
                completed++;
                progress?.Report(new BulkImportProgress(completed, total, $"Row {row.SourceLine}"));
                continue;
            }

            var partName = row.PartName.Trim();
            InventoryDoc? match = null;
            if (!string.IsNullOrWhiteSpace(row.PartId)
                && byPartId.TryGetValue(row.PartId.Trim(), out var byId))
            {
                match = byId;
            }
            else if (byPartName.TryGetValue(partName, out var byName))
            {
                match = byName;
            }

            if (match is not null)
            {
                ApplyRowToDoc(match, row);
                try
                {
                    await _inventory.UpsertAsync(match, cancellationToken).ConfigureAwait(false);
                    updated++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"Row {row.SourceLine}: failed to update \"{partName}\" — {ex.Message}");
                }
            }
            else
            {
                var doc = CreateDocFromRow(row);
                pendingInserts.Add(doc);

                if (!string.IsNullOrWhiteSpace(doc.PartId))
                {
                    byPartId[doc.PartId.Trim()] = doc;
                }

                byPartName[partName] = doc;

                if (pendingInserts.Count >= InsertBatchSize)
                {
                    try
                    {
                        await FlushInsertsAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        var failed = pendingInserts.Count;
                        skipped += failed;
                        errors.Add($"Batch insert failed near row {row.SourceLine}: {ex.Message}");
                        pendingInserts.Clear();
                    }
                }
            }

            completed++;
            progress?.Report(new BulkImportProgress(completed, total, partName));
        }

        try
        {
            await FlushInsertsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            skipped += pendingInserts.Count;
            errors.Add($"Batch insert failed: {ex.Message}");
            pendingInserts.Clear();
        }

        progress?.Report(new BulkImportProgress(completed, total, "Finishing upload…"));

        return new BulkInventoryImportResult
        {
            TotalCount = total,
            UploadedCount = completed,
            InsertedCount = inserted,
            UpdatedCount = updated,
            SkippedCount = skipped,
            Errors = errors,
        };
    }

    private static InventoryDoc CreateDocFromRow(InventoryImportRow row) =>
        new()
        {
            MongoId = MongoDB.Bson.ObjectId.GenerateNewId(),
            PartName = row.PartName.Trim(),
            PartId = row.PartId?.Trim() ?? string.Empty,
            Category = row.Category?.Trim() ?? string.Empty,
            Supplier = row.Supplier?.Trim() ?? string.Empty,
            Location = row.Location?.Trim() ?? string.Empty,
            Quantity = row.Quantity ?? 0,
            MinStock = row.MinStock ?? 10,
            UnitPrice = row.UnitPrice ?? 0,
            Notes = row.Notes?.Trim() ?? string.Empty,
            LastUpdated = DateTime.UtcNow,
        };

    private static void ApplyRowToDoc(InventoryDoc doc, InventoryImportRow row)
    {
        doc.PartName = row.PartName.Trim();
        if (!string.IsNullOrWhiteSpace(row.PartId))
        {
            doc.PartId = row.PartId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.Category))
        {
            doc.Category = row.Category.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.Supplier))
        {
            doc.Supplier = row.Supplier.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.Location))
        {
            doc.Location = row.Location.Trim();
        }

        if (row.Quantity is int qty)
        {
            doc.Quantity = qty;
        }

        if (row.MinStock is int min)
        {
            doc.MinStock = min > 0 ? min : doc.MinStock;
        }

        if (row.UnitPrice is double price)
        {
            doc.UnitPrice = price;
        }

        if (!string.IsNullOrWhiteSpace(row.Notes))
        {
            doc.Notes = row.Notes.Trim();
        }

        doc.LastUpdated = DateTime.UtcNow;
    }

    private static List<InventoryImportRow> ParseWorkbook(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.FirstOrDefault(w =>
                         string.Equals(w.Name, "Inventory", StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheets.First();

        var used = sheet.RangeUsed();
        if (used is null)
        {
            return [];
        }

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        var headerRow = sheet.Row(firstRow);
        var headerCells = new List<string>();
        for (var c = firstCol; c <= lastCol; c++)
        {
            headerCells.Add(headerRow.Cell(c).GetFormattedString());
        }

        var columnMap = BuildColumnMapFromHeaders(headerCells);
        if (!columnMap.ContainsKey(InventoryField.PartName))
        {
            return [];
        }

        var rows = new List<InventoryImportRow>();
        var blankStreak = 0;
        for (var r = firstRow + 1; r <= lastRow; r++)
        {
            var cells = new string[lastCol - firstCol + 1];
            for (var c = firstCol; c <= lastCol; c++)
            {
                cells[c - firstCol] = sheet.Cell(r, c).GetFormattedString().Trim();
            }

            if (IsBlankDataRow(cells))
            {
                blankStreak++;
                if (blankStreak >= MaxConsecutiveBlankRows)
                {
                    break;
                }

                continue;
            }

            blankStreak = 0;
            var parsed = ParseDataRow(cells, columnMap, r);
            if (parsed is not null)
            {
                rows.Add(parsed);
            }
        }

        return rows;
    }

    private static List<InventoryImportRow> ParseCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2)
        {
            return [];
        }

        var headerFields = ParseCsvLine(lines[0]);
        var columnMap = BuildColumnMapFromHeaders(headerFields);
        if (!columnMap.ContainsKey(InventoryField.PartName))
        {
            return [];
        }

        var rows = new List<InventoryImportRow>();
        var blankStreak = 0;
        for (var i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (IsBlankDataRow(fields))
            {
                blankStreak++;
                if (blankStreak >= MaxConsecutiveBlankRows)
                {
                    break;
                }

                continue;
            }

            blankStreak = 0;
            var parsed = ParseDataRow(fields, columnMap, i + 1);
            if (parsed is not null)
            {
                rows.Add(parsed);
            }
        }

        return rows;
    }

    private static Dictionary<InventoryField, int> BuildColumnMapFromHeaders(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<InventoryField, int>();
        for (var i = 0; i < headers.Count; i++)
        {
            var header = NormalizeHeader(headers[i]);
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            var field = MatchField(header);
            if (field is not null && !map.ContainsKey(field.Value))
            {
                map[field.Value] = i;
            }
        }

        return map;
    }

    private static InventoryImportRow? ParseDataRow(
        IReadOnlyList<string> cells,
        Dictionary<InventoryField, int> columnMap,
        int sourceLine)
    {
        if (!columnMap.TryGetValue(InventoryField.PartName, out var nameIndex)
            || nameIndex < 0
            || nameIndex >= cells.Count
            || string.IsNullOrWhiteSpace(cells[nameIndex]))
        {
            return null;
        }

        string? Get(InventoryField field)
        {
            if (!columnMap.TryGetValue(field, out var idx) || idx < 0 || idx >= cells.Count)
            {
                return null;
            }

            var value = cells[idx].Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        int? ParseInt(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return i;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                return (int)Math.Round(d, MidpointRounding.AwayFromZero);
            }

            return null;
        }

        double? ParseDouble(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var cleaned = raw.Trim().TrimStart('$');
            return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d
                : null;
        }

        return new InventoryImportRow
        {
            SourceLine = sourceLine,
            PartName = Get(InventoryField.PartName)!,
            PartId = Get(InventoryField.PartId),
            Category = Get(InventoryField.Category),
            Supplier = Get(InventoryField.Supplier),
            Location = Get(InventoryField.Location),
            Notes = Get(InventoryField.Notes),
            Quantity = ParseInt(Get(InventoryField.Quantity)),
            MinStock = ParseInt(Get(InventoryField.MinStock)),
            UnitPrice = ParseDouble(Get(InventoryField.UnitPrice)),
        };
    }

    private static InventoryField? MatchField(string header)
    {
        return header switch
        {
            "partname" or "part name" or "name" or "part" => InventoryField.PartName,
            "partid" or "part id" or "sku" or "id" => InventoryField.PartId,
            "category" or "type" => InventoryField.Category,
            "quantity" or "qty" or "stock" or "onhand" or "on hand" => InventoryField.Quantity,
            "minstock" or "min stock" or "minimum stock" or "reorder level" or "threshold" => InventoryField.MinStock,
            "unitprice" or "unit price" or "price" or "cost" => InventoryField.UnitPrice,
            "location" or "warehouse" or "bin" => InventoryField.Location,
            "supplier" or "vendor" => InventoryField.Supplier,
            "notes" or "note" or "comments" => InventoryField.Notes,
            _ => null,
        };
    }

    private static string NormalizeHeader(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? string.Empty
            : new string(raw.Trim().ToLowerInvariant()
                .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                .ToArray())
                .Replace("  ", " ", StringComparison.Ordinal);

    private static bool IsBlankDataRow(IReadOnlyList<string> cells) =>
        cells.All(string.IsNullOrWhiteSpace);

    private static bool IsCsvPath(string path) =>
        path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }

    private enum InventoryField
    {
        PartName,
        PartId,
        Category,
        Quantity,
        MinStock,
        UnitPrice,
        Location,
        Supplier,
        Notes,
    }

    private sealed class InventoryImportRow
    {
        public required int SourceLine { get; init; }
        public required string PartName { get; init; }
        public string? PartId { get; init; }
        public string? Category { get; init; }
        public string? Supplier { get; init; }
        public string? Location { get; init; }
        public string? Notes { get; init; }
        public int? Quantity { get; init; }
        public int? MinStock { get; init; }
        public double? UnitPrice { get; init; }
    }
}

public sealed class BulkInventoryImportResult
{
    public int TotalCount { get; init; }
    public int UploadedCount { get; init; }
    public int InsertedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int SkippedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool HasWork => InsertedCount > 0 || UpdatedCount > 0;
}
