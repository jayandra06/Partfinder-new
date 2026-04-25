using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using QRCoder;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class QrCodeManagerViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IPartsDataService _partsData;

    public QrCodeManagerViewModel(ITemplateSchemaService templateSchema, IPartsDataService partsData)
    {
        _templateSchema = templateSchema;
        _partsData = partsData;
        Templates = [];
        Parts = [];
        GeneratedCodes = [];
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; }
    public ObservableCollection<PartRecord> Parts { get; }
    public ObservableCollection<QrCodeItem> GeneratedCodes { get; }

    [ObservableProperty] private PartTemplateDefinition? _selectedTemplate;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _previewQrText = string.Empty;
    [ObservableProperty] private byte[]? _previewQrImageBytes;
    [ObservableProperty] private int _totalGenerated;

    partial void OnSelectedTemplateChanged(PartTemplateDefinition? value)
    {
        if (value is not null)
        {
            _ = LoadPartsForTemplateAsync(value.Id);
        }
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var templates = await _templateSchema.GetTemplatesAsync().ConfigureAwait(true);
            Templates.Clear();
            foreach (var t in templates)
            {
                Templates.Add(t);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPartsForTemplateAsync(string templateId)
    {
        IsLoading = true;
        StatusMessage = "Loading parts...";
        try
        {
            var (rows, _) = await _partsData.GetPageAsync(templateId, 0, 200).ConfigureAwait(true);
            Parts.Clear();
            foreach (var row in rows)
            {
                Parts.Add(row);
            }

            StatusMessage = $"{Parts.Count} part(s) loaded.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void GenerateSingleQr(PartRecord? record)
    {
        if (record is null || SelectedTemplate is null)
        {
            return;
        }

        var payload = BuildPayload(record);
        var imageBytes = RenderQrPng(payload);

        PreviewQrText = payload;
        PreviewQrImageBytes = imageBytes;

        var existing = GeneratedCodes.FirstOrDefault(c => c.RowId == record.Id);
        if (existing is not null)
        {
            GeneratedCodes.Remove(existing);
        }

        GeneratedCodes.Insert(0, new QrCodeItem(
            record.Id,
            GetPrimaryLabel(record),
            payload,
            imageBytes));

        TotalGenerated = GeneratedCodes.Count;
        StatusMessage = $"QR generated for {GetPrimaryLabel(record)}";
    }

    [RelayCommand]
    private void GenerateAllQr()
    {
        if (SelectedTemplate is null || Parts.Count == 0)
        {
            return;
        }

        GeneratedCodes.Clear();
        foreach (var record in Parts)
        {
            var payload = BuildPayload(record);
            var imageBytes = RenderQrPng(payload);
            GeneratedCodes.Add(new QrCodeItem(
                record.Id,
                GetPrimaryLabel(record),
                payload,
                imageBytes));
        }

        TotalGenerated = GeneratedCodes.Count;
        StatusMessage = $"{TotalGenerated} QR codes generated.";

        if (GeneratedCodes.Count > 0)
        {
            PreviewQrText = GeneratedCodes[0].Payload;
            PreviewQrImageBytes = GeneratedCodes[0].ImageBytes;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        GeneratedCodes.Clear();
        PreviewQrText = string.Empty;
        PreviewQrImageBytes = null;
        TotalGenerated = 0;
        StatusMessage = "Cleared.";
    }

    private string BuildPayload(PartRecord record)
    {
        var lines = new List<string>
        {
            $"PartFinder|{SelectedTemplate?.Name ?? "Unknown"}",
            $"RowId:{record.Id}",
        };

        if (SelectedTemplate is not null)
        {
            foreach (var field in SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder).Take(6))
            {
                var val = record.Values.TryGetValue(field.Key, out var v) ? v?.ToString() : string.Empty;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    lines.Add($"{field.Label}:{val}");
                }
            }
        }

        return string.Join("\n", lines);
    }

    private string GetPrimaryLabel(PartRecord record)
    {
        if (SelectedTemplate is null)
        {
            return record.Id;
        }

        var firstField = SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder).FirstOrDefault();
        if (firstField is null)
        {
            return record.Id;
        }

        return record.Values.TryGetValue(firstField.Key, out var v)
            ? v?.ToString() ?? record.Id
            : record.Id;
    }

    private static byte[] RenderQrPng(string payload, int pixelsPerModule = 8)
    {
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}

public sealed class QrCodeItem
{
    public QrCodeItem(string rowId, string label, string payload, byte[] imageBytes)
    {
        RowId = rowId;
        Label = label;
        Payload = payload;
        ImageBytes = imageBytes;
    }

    public string RowId { get; }
    public string Label { get; }
    public string Payload { get; }
    public byte[] ImageBytes { get; }
}
