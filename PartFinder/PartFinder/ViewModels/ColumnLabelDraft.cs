using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;

namespace PartFinder.ViewModels;

public sealed class ColumnLabelDraft : ObservableObject
{
    private string _label = string.Empty;
    private TemplateFieldType _fieldType = TemplateFieldType.Text;
    private string? _linkedTemplateId;

    public string Label
    {
        get => _label;
        set
        {
            if (SetProperty(ref _label, value))
            {
                OnPropertyChanged(nameof(SampleValue1));
                OnPropertyChanged(nameof(SampleValue2));
            }
        }
    }

    public TemplateFieldType FieldType
    {
        get => _fieldType;
        set
        {
            if (SetProperty(ref _fieldType, value))
            {
                if (_fieldType != TemplateFieldType.RecordLink)
                {
                    LinkedTemplateId = null;
                }
                OnPropertyChanged(nameof(IsLinkColumn));
                OnPropertyChanged(nameof(SampleValue1));
                OnPropertyChanged(nameof(SampleValue2));
            }
        }
    }

    public bool IsLinkColumn
    {
        get => _fieldType == TemplateFieldType.RecordLink;
        set
        {
            if (value)
            {
                FieldType = TemplateFieldType.RecordLink;
            }
            else
            {
                LinkedTemplateId = null;
                FieldType = TemplateFieldType.Text;
            }
        }
    }

    /// <summary>Target template id when <see cref="FieldType"/> is <see cref="TemplateFieldType.RecordLink"/>.</summary>
    public string? LinkedTemplateId
    {
        get => _linkedTemplateId;
        set => SetProperty(ref _linkedTemplateId, value);
    }

    /// <summary>When editing an existing template, preserve the stored field key for this row.</summary>
    public string? StableKey { get; set; }

    public string SampleValue1
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Label)) return "---";
            return FieldType switch
            {
                TemplateFieldType.Number => "42",
                TemplateFieldType.Decimal => "12.50",
                TemplateFieldType.Date => "2024-10-01",
                TemplateFieldType.Boolean => "Yes",
                TemplateFieldType.Dropdown => "Option A",
                TemplateFieldType.RecordLink => "Linked Item",
                _ => $"Sample {Label}"
            };
        }
    }

    public string SampleValue2
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Label)) return "---";
            return FieldType switch
            {
                TemplateFieldType.Number => "10",
                TemplateFieldType.Decimal => "5.75",
                TemplateFieldType.Date => "2024-11-15",
                TemplateFieldType.Boolean => "No",
                TemplateFieldType.Dropdown => "Option B",
                TemplateFieldType.RecordLink => "Another Item",
                _ => $"Another {Label}"
            };
        }
    }
}
