using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PartFinder.Helpers;

namespace PartFinder.Services;

public sealed class WmsApiClient
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(10),
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = 10 * 1024 * 1024,
    };
    private static readonly JsonSerializerOptions ReadJson = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 32,
    };
    private static readonly JsonSerializerOptions WriteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AdminSessionStore _session;
    private readonly ILocalSetupContext _setup;

    public WmsApiClient(AdminSessionStore session, ILocalSetupContext setup)
    {
        _session = session;
        _setup = setup;
    }

    public async Task<(bool Ok, string? Error, WmsHealthDto? Data)> GetHealthAsync(
        CancellationToken ct = default)
    {
        var (ok, err, dto) = await SendCoreAsync<WmsHealthDto>(
                HttpMethod.Get,
                "/wms/v1/health",
                null,
                ct)
            .ConfigureAwait(false);
        return ok ? (true, null, dto) : (false, err, null);
    }

    public Task<(bool Ok, string? Error, WmsTransactionProbeDto? Data)> ProbeTransactionsAsync(
        CancellationToken ct = default) =>
        GetAsync<WmsTransactionProbeDto>("/wms/v1/diagnostics/transactions", ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsWarehouseDto> Items)> ListWarehousesAsync(
        CancellationToken ct = default) =>
        ListAsync<WmsWarehouseDto>("/wms/v1/warehouses", ct);

    public Task<(bool Ok, string? Error, WmsWarehouseDto? Data)> CreateWarehouseAsync(
        CreateWmsWarehouseRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsWarehouseDto>("/wms/v1/warehouses", request, ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsStockOwnerDto> Items)> ListStockOwnersAsync(
        CancellationToken ct = default) =>
        ListAsync<WmsStockOwnerDto>("/wms/v1/stock-owners", ct);

    public Task<(bool Ok, string? Error, WmsStockOwnerDto? Data)> CreateStockOwnerAsync(
        CreateWmsStockOwnerRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsStockOwnerDto>("/wms/v1/stock-owners", request, ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsZoneDto> Items)> ListZonesAsync(
        string warehouseId,
        CancellationToken ct = default) =>
        ListAsync<WmsZoneDto>($"/wms/v1/zones?warehouseId={Uri.EscapeDataString(warehouseId)}", ct);

    public Task<(bool Ok, string? Error, WmsZoneDto? Data)> CreateZoneAsync(
        CreateWmsZoneRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsZoneDto>("/wms/v1/zones", request, ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsLocationDto> Items)> ListLocationsAsync(
        string warehouseId,
        CancellationToken ct = default) =>
        ListAsync<WmsLocationDto>(
            $"/wms/v1/locations?warehouseId={Uri.EscapeDataString(warehouseId)}",
            ct);

    public Task<(bool Ok, string? Error, WmsLocationDto? Data)> CreateLocationAsync(
        CreateWmsLocationRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsLocationDto>("/wms/v1/locations", request, ct);

    public Task<(bool Ok, string? Error, WmsPagedBalancesResult? Data)> ListBalancesAsync(
        string? warehouseId = null,
        string? ownerId = null,
        string? skuId = null,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default)
    {
        var parts = new List<string> { $"skip={skip}", $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(warehouseId))
            parts.Add($"warehouseId={Uri.EscapeDataString(warehouseId)}");
        if (!string.IsNullOrWhiteSpace(ownerId))
            parts.Add($"ownerId={Uri.EscapeDataString(ownerId)}");
        if (!string.IsNullOrWhiteSpace(skuId))
            parts.Add($"skuId={Uri.EscapeDataString(skuId)}");
        return GetAsync<WmsPagedBalancesResult>($"/wms/v1/inventory/balances?{string.Join("&", parts)}", ct);
    }

    public Task<(bool Ok, string? Error, WmsPagedTransactionsResult? Data)> ListTransactionsAsync(
        string? warehouseId = null,
        string? ownerId = null,
        string? skuId = null,
        int skip = 0,
        int limit = 50,
        CancellationToken ct = default)
    {
        var parts = new List<string> { $"skip={skip}", $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(warehouseId))
            parts.Add($"warehouseId={Uri.EscapeDataString(warehouseId)}");
        if (!string.IsNullOrWhiteSpace(ownerId))
            parts.Add($"ownerId={Uri.EscapeDataString(ownerId)}");
        if (!string.IsNullOrWhiteSpace(skuId))
            parts.Add($"skuId={Uri.EscapeDataString(skuId)}");
        return GetAsync<WmsPagedTransactionsResult>($"/wms/v1/inventory/transactions?{string.Join("&", parts)}", ct);
    }

    public Task<(bool Ok, string? Error, WmsOpeningBalanceDryRunResult? Data)> OpeningBalanceDryRunAsync(
        CancellationToken ct = default) =>
        GetAsync<WmsOpeningBalanceDryRunResult>("/wms/v1/ledger/opening-balance-dry-run", ct);

    public Task<(bool Ok, string? Error, WmsCatalogStatusDto? Data)> GetCatalogStatusAsync(
        CancellationToken ct = default) =>
        GetAsync<WmsCatalogStatusDto>("/wms/v1/catalog/status", ct);

    public Task<(bool Ok, string? Error, WmsSkuListResult? Data)> ListSkusAsync(
        string? status = null,
        CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(status)
            ? "/wms/v1/skus"
            : $"/wms/v1/skus?status={Uri.EscapeDataString(status)}";
        return GetAsync<WmsSkuListResult>(q, ct);
    }

    public Task<(bool Ok, string? Error, WmsSkuDetailDto? Data)> GetSkuAsync(string skuId, CancellationToken ct = default) =>
        GetAsync<WmsSkuDetailDto>($"/wms/v1/skus/{Uri.EscapeDataString(skuId)}", ct);

    public Task<(bool Ok, string? Error, WmsSkuDto? Data)> CreateSkuAsync(
        CreateWmsSkuRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsSkuDto>("/wms/v1/skus", request, ct);

    public Task<(bool Ok, string? Error, WmsSkuDto? Data)> UpdateSkuAsync(
        string skuId,
        UpdateWmsSkuRequest request,
        CancellationToken ct = default) =>
        PatchAsync<WmsSkuDto>($"/wms/v1/skus/{Uri.EscapeDataString(skuId)}", request, ct);

    public Task<(bool Ok, string? Error, WmsSkuIdentifierDto? Data)> AddSkuIdentifierAsync(
        string skuId,
        AddWmsSkuIdentifierRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsSkuIdentifierDto>($"/wms/v1/skus/{Uri.EscapeDataString(skuId)}/identifiers", request, ct);

    public Task<(bool Ok, string? Error, WmsIdentifierCheckResult? Data)> CheckSkuIdentifierAsync(
        AddWmsSkuIdentifierRequest request,
        string? excludeSkuId = null,
        CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(excludeSkuId)
            ? "/wms/v1/skus/identifiers/check"
            : $"/wms/v1/skus/identifiers/check?excludeSkuId={Uri.EscapeDataString(excludeSkuId)}";
        return PostAsync<WmsIdentifierCheckResult>(q, request, ct);
    }

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsUomDto> Items)> ListUomAsync(CancellationToken ct = default) =>
        ListAsync<WmsUomDto>("/wms/v1/uom", ct);

    public Task<(bool Ok, string? Error, WmsUomDto? Data)> CreateUomAsync(
        CreateWmsUomRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsUomDto>("/wms/v1/uom", request, ct);

    public async Task<(bool Ok, string? Error, IReadOnlyList<WmsExpectedReceiptRowDto> Items)> ListExpectedReceiptsAsync(
        string? warehouseId = null,
        CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(warehouseId)
            ? "/wms/v1/receiving/expected"
            : $"/wms/v1/receiving/expected?warehouseId={Uri.EscapeDataString(warehouseId)}";
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsExpectedReceiptRowDto>>>(
            HttpMethod.Get, q, null, ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<WmsExpectedReceiptRowDto>());
        return (true, null, env.Data.Items);
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<WmsGoodsReceiptRowDto> Items)> ListGoodsReceiptsAsync(
        string? warehouseId = null,
        CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(warehouseId)
            ? "/wms/v1/receiving/goods-receipts"
            : $"/wms/v1/receiving/goods-receipts?warehouseId={Uri.EscapeDataString(warehouseId)}";
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsGoodsReceiptRowDto>>>(
            HttpMethod.Get, q, null, ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<WmsGoodsReceiptRowDto>());
        return (true, null, env.Data.Items);
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<WmsReceiptLineRowDto> Items)> ListReceiptLinesAsync(
        string goodsReceiptId,
        CancellationToken ct = default)
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsReceiptLineRowDto>>>(
            HttpMethod.Get,
            $"/wms/v1/receiving/goods-receipts/{Uri.EscapeDataString(goodsReceiptId)}/lines",
            null,
            ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<WmsReceiptLineRowDto>());
        return (true, null, env.Data.Items);
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<WmsQcRowDto> Items)> ListQcAsync(
        string? status,
        CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(status)
            ? "/wms/v1/receiving/qc"
            : $"/wms/v1/receiving/qc?status={Uri.EscapeDataString(status)}";
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsQcRowDto>>>(
            HttpMethod.Get, q, null, ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<WmsQcRowDto>());
        return (true, null, env.Data.Items);
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<WmsPutawayRowDto> Items)> ListPutawayTasksAsync(
        string? warehouseId,
        CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(warehouseId)
            ? "/wms/v1/receiving/putaway"
            : $"/wms/v1/receiving/putaway?warehouseId={Uri.EscapeDataString(warehouseId)}";
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsPutawayRowDto>>>(
            HttpMethod.Get, q, null, ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<WmsPutawayRowDto>());
        return (true, null, env.Data.Items);
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<string> Items)> ListReceivingHistoryAsync(
        CancellationToken ct = default)
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsReceivingAuditRowDto>>>(
            HttpMethod.Get, "/wms/v1/receiving/history", null, ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<string>());
        return (true, null, env.Data.Items.Select(i => $"{i.Action} {i.EntityId} ({i.ActorEmail})").ToList());
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<WmsReceiptLineRowDto> Items)> ListReceivingDiscrepanciesAsync(
        CancellationToken ct = default)
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsReceiptLineRowDto>>>(
            HttpMethod.Get, "/wms/v1/receiving/discrepancies", null, ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<WmsReceiptLineRowDto>());
        return (true, null, env.Data.Items);
    }

    public Task<(bool Ok, string? Error, WmsSessionProfileDto? Data)> GetSessionProfileAsync(
        CancellationToken ct = default) =>
        GetAsync<WmsSessionProfileDto>("/wms/v1/session/profile", ct);

    public Task<(bool Ok, string? Error, WmsCreateExpectedReceiptResult? Data)> CreateExpectedReceiptAsync(
        CreateWmsExpectedReceiptRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsCreateExpectedReceiptResult>("/wms/v1/receiving/expected", request, ct);

    public Task<(bool Ok, string? Error, WmsAddExpectedLinesResult? Data)> AddExpectedReceiptLinesAsync(
        string expectedReceiptId,
        AddWmsExpectedLinesRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsAddExpectedLinesResult>(
            $"/wms/v1/receiving/expected/{Uri.EscapeDataString(expectedReceiptId)}/lines",
            request,
            ct);

    public Task<(bool Ok, string? Error, WmsConfirmGoodsReceiptResult? Data)> ConfirmGoodsReceiptAsync(
        ConfirmWmsGoodsReceiptRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsConfirmGoodsReceiptResult>("/wms/v1/receiving/goods-receipts", request, ct);

    public Task<(bool Ok, string? Error, WmsQcDecisionResult? Data)> ApplyQcDecisionAsync(
        string receiptLineId,
        WmsQcDecisionRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsQcDecisionResult>(
            $"/wms/v1/receiving/qc/{Uri.EscapeDataString(receiptLineId)}",
            request,
            ct);

    public Task<(bool Ok, string? Error, WmsPutawayCompleteResult? Data)> CompletePutawayAsync(
        string putawayTaskId,
        CompleteWmsPutawayRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsPutawayCompleteResult>(
            $"/wms/v1/receiving/putaway/{Uri.EscapeDataString(putawayTaskId)}/complete",
            request,
            ct);

    public async Task<(bool Ok, string? Error, IReadOnlyList<WmsOperatorGrantDto> Items)> ListOperatorGrantsAsync(
        CancellationToken ct = default)
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsReceivingListEnvelope<WmsOperatorGrantDto>>>(
            HttpMethod.Get, "/wms/v1/operator-grants", null, ct).ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null) return (false, err, Array.Empty<WmsOperatorGrantDto>());
        return (true, null, env.Data.Items);
    }

    public Task<(bool Ok, string? Error, WmsOperatorGrantDto? Data)> UpsertOperatorGrantAsync(
        UpsertWmsOperatorGrantRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsOperatorGrantDto>("/wms/v1/operator-grants", request, ct);

    public Task<(bool Ok, string? Error, WmsStagingGateEvidenceDto? Data)> GetStagingGateLatestAsync(
        CancellationToken ct = default) =>
        GetAsync<WmsStagingGateEvidenceDto>("/wms/v1/staging-gate/latest", ct);

    public Task<(bool Ok, string? Error, WmsStagingGateEvidenceDto? Data)> RecordStagingPreflightAsync(
        string? tenantLabel = null,
        CancellationToken ct = default) =>
        PostAsync<WmsStagingGateEvidenceDto>(
            "/wms/v1/staging-gate/preflight",
            string.IsNullOrWhiteSpace(tenantLabel) ? new { } : new { tenantLabel = tenantLabel.Trim() },
            ct);

    public Task<(bool Ok, string? Error, WmsDispositionResult? Data)> ConfirmDispositionAsync(
        string receiptLineId,
        WmsDispositionRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsDispositionResult>(
            $"/wms/v1/receiving/disposition/{Uri.EscapeDataString(receiptLineId)}",
            request,
            ct);

    private async Task<(bool Ok, string? Error, T? Data)> PatchAsync<T>(
        string path,
        object body,
        CancellationToken ct)
        where T : class
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<T>>(new HttpMethod("PATCH"), path, body, ct)
            .ConfigureAwait(false);
        return ok ? (true, null, env?.Data) : (false, err, default);
    }

    private async Task<(bool Ok, string? Error, IReadOnlyList<T> Items)> ListAsync<T>(
        string path,
        CancellationToken ct)
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsListEnvelope<T>>>(
                HttpMethod.Get,
                path,
                null,
                ct)
            .ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null)
        {
            return (false, err, Array.Empty<T>());
        }

        return (true, null, env.Data.Items);
    }

    private async Task<(bool Ok, string? Error, T? Data)> GetAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<T>>(HttpMethod.Get, path, null, ct)
            .ConfigureAwait(false);
        return ok ? (true, null, env?.Data) : (false, err, default);
    }

    private async Task<(bool Ok, string? Error, T? Data)> PostAsync<T>(
        string path,
        object body,
        CancellationToken ct)
        where T : class
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<T>>(HttpMethod.Post, path, body, ct)
            .ConfigureAwait(false);
        return ok ? (true, null, env?.Data) : (false, err, default);
    }

    private async Task<(bool Ok, string? Error, T? Data)> SendCoreAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct)
    {
        _session.Load();
        _setup.Refresh();
        var orgId = _setup.OrgCode?.Trim();
        var token = _session.AccessToken?.Trim();

        if (string.IsNullOrWhiteSpace(orgId))
        {
            return (false, "Missing organisation context.", default);
        }

        var baseUrl = LicenseApiClient.GetBaseUrl().TrimEnd('/');
        using var req = new HttpRequestMessage(method, $"{baseUrl}/api{path}");
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        req.Headers.Add("X-Org-Id", orgId);
        if (body is not null)
        {
            req.Content = new StringContent(
                JsonSerializer.Serialize(body, WriteJson),
                Encoding.UTF8,
                "application/json");
        }

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, default);
        }

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            return (false, ApiErrorFormatter.ToUserMessage(text), default);
        }

        try
        {
            var dto = JsonSerializer.Deserialize<T>(text, ReadJson);
            return (true, null, dto);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, default);
        }
    }

    private sealed class WmsEnvelope<T>
    {
        public T? Data { get; set; }
        public bool Success { get; set; }
    }

    private sealed class WmsListEnvelope<T>
    {
        public T[]? Items { get; set; }
        public int Total { get; set; }
    }
}

public sealed class WmsHealthDto
{
    public bool Ok { get; set; }
    public string OrgCode { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public bool StockMutations { get; set; }
    public bool StagingStockPosting { get; set; }
    public bool LiveStockPosting { get; set; }
}

public sealed class WmsSkuDto
{
    public string SkuId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PrimaryUom { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class WmsSkuListResult
{
    public WmsSkuDto[]? Items { get; set; }
    public int Total { get; set; }
}

public sealed class WmsBalanceRowDto
{
    public string SkuId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public double OnHand { get; set; }
    public double Reserved { get; set; }
    public double Available { get; set; }
    public string PrimaryUom { get; set; } = string.Empty;
}

public sealed class WmsPagedBalancesResult
{
    public WmsBalanceRowDto[]? Items { get; set; }
    public int Total { get; set; }
}

public sealed class WmsTransactionRowDto
{
    public string TxnId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class WmsPagedTransactionsResult
{
    public WmsTransactionRowDto[]? Items { get; set; }
    public int Total { get; set; }
}

public sealed class WmsOpeningBalanceDryRunResult
{
    public int TotalSourceRows { get; set; }
    public int MappedCount { get; set; }
    public int UnmappedCount { get; set; }
    public int DuplicateCandidateCount { get; set; }
    public int InvalidQuantityCount { get; set; }
    public string? Disclaimer { get; set; }
    public WmsDryRunUnmappedRow[]? Unmapped { get; set; }
}

public sealed class WmsDryRunUnmappedRow
{
    public string LegacyInventoryId { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public double Quantity { get; set; }
}

public sealed class WmsTransactionProbeDto
{
    public bool Supported { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class WmsWarehouseDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CreateWmsWarehouseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public string? Address { get; set; }
}

public sealed class WmsStockOwnerDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CreateWmsStockOwnerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class WmsZoneDto
{
    public string Id { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CreateWmsZoneRequest
{
    public string WarehouseId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class WmsLocationDto
{
    public string Id { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string? ZoneId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsPickable { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateWmsLocationRequest
{
    public string WarehouseId { get; set; } = string.Empty;
    public string? ZoneId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "storage";
    public bool IsPickable { get; set; }
}

public sealed class WmsCatalogStatusDto
{
    public string Phase { get; set; } = string.Empty;
    public bool StagingStockPosting { get; set; }
    public bool LiveStockPosting { get; set; }
}

public sealed class WmsSkuDetailDto
{
    public string SkuId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PrimaryUom { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WmsSkuIdentifierDto[]? Identifiers { get; set; }
}

public sealed class WmsSkuIdentifierDto
{
    public string Namespace { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class CreateWmsSkuRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PrimaryUom { get; set; } = "EA";
}

public sealed class UpdateWmsSkuRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? PrimaryUom { get; set; }
}

public sealed class AddWmsSkuIdentifierRequest
{
    public string Namespace { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class WmsIdentifierCheckResult
{
    public string? ScopeKey { get; set; }
    public object? Conflict { get; set; }
}

public sealed class WmsUomDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}

public sealed class CreateWmsUomRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public sealed class WmsReceivingListEnvelope<T>
{
    public T[]? Items { get; set; }
    public int Total { get; set; }
}

public sealed class WmsExpectedReceiptRowDto
{
    public string ExpectedReceiptId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ExternalPoId { get; set; }
}

public sealed class WmsGoodsReceiptRowDto
{
    public string GoodsReceiptId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class WmsReceiptLineRowDto
{
    public string ReceiptLineId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public double QtyReceived { get; set; }
    public string QcStatus { get; set; } = string.Empty;
    public bool Damaged { get; set; }
    public bool Shortage { get; set; }
    public bool OverDelivery { get; set; }
}

public sealed class WmsQcRowDto
{
    public string QcId { get; set; } = string.Empty;
    public string ReceiptLineId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class WmsPutawayRowDto
{
    public string PutawayTaskId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string FromLocationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class WmsReceivingAuditRowDto
{
    public string Action { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
}

public sealed class WmsSessionProfileDto
{
    public string Email { get; set; } = string.Empty;
    public bool IsWmsAdmin { get; set; }
    public WmsOperatorPermissionsDto OperatorPermissions { get; set; } = new();
    public string[] AllowedWarehouseIds { get; set; } = [];
    public string[] AllowedOwnerIds { get; set; } = [];
}

public sealed class WmsOperatorPermissionsDto
{
    public bool Receiving { get; set; }
    public bool Qc { get; set; }
    public bool Putaway { get; set; }
    public bool Supervise { get; set; }
}

public sealed class CreateWmsExpectedReceiptRequest
{
    public string OwnerId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string? ExternalPoId { get; set; }
    public string? SupplierDeliveryNote { get; set; }
    public string? SupplierId { get; set; }
    public string? Notes { get; set; }
}

public sealed class WmsCreateExpectedReceiptResult
{
    public string ExpectedReceiptId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class AddWmsExpectedLinesRequest
{
    public WmsExpectedLineInput[] Lines { get; set; } = [];
}

public sealed class WmsExpectedLineInput
{
    public string SkuId { get; set; } = string.Empty;
    public double QtyExpected { get; set; }
    public string? PrimaryUom { get; set; }
}

public sealed class WmsAddExpectedLinesResult
{
    public string ExpectedReceiptId { get; set; } = string.Empty;
    public int LineCount { get; set; }
}

public sealed class ConfirmWmsGoodsReceiptRequest
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string ReceivingLocationId { get; set; } = string.Empty;
    public string? ExpectedReceiptId { get; set; }
    public string? ExternalPoId { get; set; }
    public string? SupplierDeliveryNote { get; set; }
    public WmsGoodsReceiptLineInput[] Lines { get; set; } = [];
}

public sealed class WmsGoodsReceiptLineInput
{
    public string SkuId { get; set; } = string.Empty;
    public double QtyReceived { get; set; }
    public string? LotId { get; set; }
    public string? SerialId { get; set; }
    public bool Damaged { get; set; }
    public bool Shortage { get; set; }
    public bool OverDelivery { get; set; }
}

public sealed class WmsConfirmGoodsReceiptResult
{
    public string GoodsReceiptId { get; set; } = string.Empty;
    public bool Duplicate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class WmsQcDecisionRequest
{
    public string Status { get; set; } = "partial";
    public double? QtyAccepted { get; set; }
    public double? QtyRejected { get; set; }
    public string? Note { get; set; }
    public string? ApprovalNote { get; set; }
}

public sealed class WmsQcDecisionResult
{
    public string ReceiptLineId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double QtyAccepted { get; set; }
    public double QtyRejected { get; set; }
}

public sealed class CompleteWmsPutawayRequest
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ToLocationId { get; set; } = string.Empty;
}

public sealed class WmsPutawayCompleteResult
{
    public string PutawayTaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class WmsOperatorGrantDto
{
    public string Email { get; set; } = string.Empty;
    public string[] WarehouseIds { get; set; } = [];
    public string[] OwnerIds { get; set; } = [];
    public WmsOperatorPermissionsDto Permissions { get; set; } = new();
}

public sealed class UpsertWmsOperatorGrantRequest
{
    public string Email { get; set; } = string.Empty;
    public string[] WarehouseIds { get; set; } = [];
    public string[] OwnerIds { get; set; } = [];
    public WmsOperatorPermissionsDto Permissions { get; set; } = new();
}

public sealed class WmsStagingGateEvidenceDto
{
    public string? OrgCode { get; set; }
    public string? OrganizationName { get; set; }
    public string? TenantDatabase { get; set; }
    public string? ReplicaSetName { get; set; }
    public bool Supported { get; set; }
    public bool CommitProbe { get; set; }
    public bool RollbackProbe { get; set; }
    public bool StagingStockPosting { get; set; }
    public bool LiveStockPosting { get; set; }
    public string? DeploymentTier { get; set; }
    public bool PostingGateOpen { get; set; }
    public string? Detail { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class WmsDispositionRequest
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string DispositionReason { get; set; } = string.Empty;
    public string DispositionType { get; set; } = "return_to_supplier";
}

public sealed class WmsDispositionResult
{
    public string ReceiptLineId { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string DispositionType { get; set; } = string.Empty;
}

public static class WmsSkuIdentifierNamespaces
{
    public static readonly string[] All =
    {
        "manufacturerPn",
        "supplierPn",
        "erpMaterial",
        "barcode",
        "masterDataRow",
        "partsRecord",
        "legacyInventory",
        "ownerExternalProduct",
    };
}