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
        set => SetProperty(ref _label, value);
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
}
