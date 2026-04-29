using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PartFinder.ViewModels;

public sealed class ColumnLabelDraft : ObservableObject
{
    private string _label = string.Empty;
    private TemplateFieldType _fieldType = TemplateFieldType.Text;
    private string? _linkedTemplateId;
    private string _dropdownOptionsText = string.Empty;
    private bool _isSyncingOptions;
    private string _pendingDropdownOption = string.Empty;

    public ColumnLabelDraft()
    {
        DropdownOptions.CollectionChanged += OnDropdownOptionsChanged;
    }

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

    /// <summary>Comma-separated options used when <see cref="FieldType"/> is Dropdown.</summary>
    public string DropdownOptionsText
    {
        get => _dropdownOptionsText;
        set
        {
            if (SetProperty(ref _dropdownOptionsText, value))
            {
                SyncDropdownOptionsFromText();
                OnPropertyChanged(nameof(SampleValue1));
                OnPropertyChanged(nameof(SampleValue2));
            }
        }
    }

    public ObservableCollection<string> DropdownOptions { get; } = [];

    public string PendingDropdownOption
    {
        get => _pendingDropdownOption;
        set => SetProperty(ref _pendingDropdownOption, value);
    }

    public void TryAddPendingDropdownOption()
    {
        var option = PendingDropdownOption.Trim();
        if (option.Length == 0)
        {
            return;
        }

        if (DropdownOptions.Any(x => string.Equals(x, option, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        DropdownOptions.Add(option);
        PendingDropdownOption = string.Empty;
    }

    public void RemoveDropdownOption(string option)
    {
        var match = DropdownOptions.FirstOrDefault(x => string.Equals(x, option, StringComparison.Ordinal));
        if (match is not null)
        {
            DropdownOptions.Remove(match);
        }
    }

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
                TemplateFieldType.Dropdown => GetDropdownPreview(defaultValue: "Option A"),
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
                TemplateFieldType.Dropdown => GetDropdownPreview(defaultValue: "Option B", second: true),
                TemplateFieldType.RecordLink => "Another Item",
                _ => $"Another {Label}"
            };
        }
    }

    private string GetDropdownPreview(string defaultValue, bool second = false)
    {
        var options = DropdownOptions.ToList();
        if (options.Count == 0)
        {
            return defaultValue;
        }

        if (second && options.Count > 1)
        {
            return options[1];
        }

        return options[0];
    }

    private void SyncDropdownOptionsFromText()
    {
        if (_isSyncingOptions)
        {
            return;
        }

        _isSyncingOptions = true;
        try
        {
            var parsed = DropdownOptionsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            DropdownOptions.Clear();
            foreach (var option in parsed)
            {
                DropdownOptions.Add(option);
            }
        }
        finally
        {
            _isSyncingOptions = false;
        }
    }

    private void OnDropdownOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isSyncingOptions)
        {
            return;
        }

        _isSyncingOptions = true;
        try
        {
            DropdownOptionsText = string.Join(", ", DropdownOptions);
        }
        finally
        {
            _isSyncingOptions = false;
        }

        OnPropertyChanged(nameof(SampleValue1));
        OnPropertyChanged(nameof(SampleValue2));
    }
}
