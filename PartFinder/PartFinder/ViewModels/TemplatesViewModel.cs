using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PartFinder.ViewModels;

public partial class TemplatesViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IShellNavCoordinator _shellNav;
    private readonly ILocalSetupContext _setupContext;
    private readonly IContextActionsService _contextActions;

    public TemplatesViewModel(
        ITemplateSchemaService templateSchema,
        IShellNavCoordinator shellNav,
        ILocalSetupContext setupContext,
        IContextActionsService contextActions)
    {
        _templateSchema = templateSchema;
        _shellNav = shellNav;
        _setupContext = setupContext;
        _contextActions = contextActions;
        ColumnLabels.Add(new ColumnLabelDraft());
        Templates.CollectionChanged += OnTemplatesCollectionChanged;
    }

    private void OnTemplatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyTemplateUiState();
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; } = [];

    public ObservableCollection<ColumnLabelDraft> ColumnLabels { get; } = [];

    public ObservableCollection<ContextActionListRow> ContextActionRows { get; } = [];

    private ContextActionListRow? _selectedContextAction;
    public ContextActionListRow? SelectedContextAction
    {
        get => _selectedContextAction;
        set => SetProperty(ref _selectedContextAction, value);
    }

    private PartTemplateDefinition? _selectedTemplate;
    public PartTemplateDefinition? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (!SetProperty(ref _selectedTemplate, value))
            {
                return;
            }

            SelectedContextAction = null;
            NotifyTemplateUiState();
            _ = ReloadContextActionsAsync();
        }
    }

    private bool _isCreatingTemplate;
    public bool IsCreatingTemplate
    {
        get => _isCreatingTemplate;
        set
        {
            if (!SetProperty(ref _isCreatingTemplate, value))
            {
                return;
            }

            NotifyTemplateUiState();
        }
    }

    private bool _isEditingExisting;
    public bool IsEditingExisting
    {
        get => _isEditingExisting;
        set
        {
            if (SetProperty(ref _isEditingExisting, value))
            {
                OnPropertyChanged(nameof(LinkTargetCandidates));
            }
        }
    }

    private string? _editingTemplateId;
    public string? EditingTemplateId
    {
        get => _editingTemplateId;
        set
        {
            if (SetProperty(ref _editingTemplateId, value))
            {
                OnPropertyChanged(nameof(LinkTargetCandidates));
            }
        }
    }

    public IEnumerable<PartTemplateDefinition> LinkTargetCandidates =>
        Templates.Where(
            t => !IsEditingExisting
                 || !string.Equals(t.Id, EditingTemplateId, StringComparison.Ordinal));

    public IReadOnlyList<TemplateFieldTypeOption> FieldTypeOptions { get; } =
    [
        new(TemplateFieldType.Text, "Text"),
        new(TemplateFieldType.Number, "Whole number"),
        new(TemplateFieldType.Decimal, "Decimal"),
        new(TemplateFieldType.Date, "Date"),
        new(TemplateFieldType.Boolean, "Yes / No"),
        new(TemplateFieldType.Dropdown, "Dropdown"),
        new(TemplateFieldType.RecordLink, "Link to another template"),
    ];

    public bool CanEditSelectedTemplate => !IsCreatingTemplate && SelectedTemplate is not null;

    public string EditorHeaderTitle => IsEditingExisting ? "Edit template" : "New template";

    private string _newTemplateName = string.Empty;
    public string NewTemplateName
    {
        get => _newTemplateName;
        set => SetProperty(ref _newTemplateName, value);
    }

    private bool _isTemplateNameReadOnly;
    public bool IsTemplateNameReadOnly
    {
        get => _isTemplateNameReadOnly;
        set => SetProperty(ref _isTemplateNameReadOnly, value);
    }

    private string _formError = string.Empty;
    public string FormError
    {
        get => _formError;
        set => SetProperty(ref _formError, value);
    }

    private bool _canAddAnotherTemplate;
    public bool CanAddAnotherTemplate
    {
        get => _canAddAnotherTemplate;
        set
        {
            if (!SetProperty(ref _canAddAnotherTemplate, value))
            {
                return;
            }

            NotifyTemplateUiState();
        }
    }

    private bool _hasTenantDb;

    public bool ShowDbMissingBanner => !_hasTenantDb;

    public bool ShowTemplateList => !IsCreatingTemplate;

    public bool ShowTemplateEditor => IsCreatingTemplate;

    public bool ShowTemplatePreviewPanel => !IsCreatingTemplate && SelectedTemplate is not null;

    public bool ShowSelectTemplatePromptPanel =>
        !IsCreatingTemplate && SelectedTemplate is null && Templates.Count > 0;

    public bool ShowContextActionsEditor =>
        !IsCreatingTemplate && SelectedTemplate is not null && _hasTenantDb;

    private void NotifyTemplateUiState()
    {
        OnPropertyChanged(nameof(ShowDbMissingBanner));
        OnPropertyChanged(nameof(ShowTemplateList));
        OnPropertyChanged(nameof(ShowTemplateEditor));
        OnPropertyChanged(nameof(ShowTemplatePreviewPanel));
        OnPropertyChanged(nameof(ShowSelectTemplatePromptPanel));
        OnPropertyChanged(nameof(ShowContextActionsEditor));
        OnPropertyChanged(nameof(CanEditSelectedTemplate));
        OnPropertyChanged(nameof(LinkTargetCandidates));
        OnPropertyChanged(nameof(EditorHeaderTitle));
    }

    public async Task ReloadContextActionsAsync(CancellationToken cancellationToken = default)
    {
        ContextActionRows.Clear();
        SelectedContextAction = null;
        if (SelectedTemplate is null || !_hasTenantDb)
        {
            return;
        }

        try
        {
            var actions = await _contextActions
                .GetForSourceTemplateAsync(SelectedTemplate.Id, cancellationToken)
                .ConfigureAwait(true);
            foreach (var a in actions.OrderBy(x => x.MenuLabel, StringComparer.OrdinalIgnoreCase))
            {
                var targetName = Templates.FirstOrDefault(t => t.Id == a.TargetTemplateId)?.Name ?? a.TargetTemplateId;
                ContextActionRows.Add(
                    new ContextActionListRow
                    {
                        Action = a,
                        TargetTemplateName = targetName,
                    });
            }
        }
        catch
        {
            // Leave list empty if DB unavailable.
        }
    }

    [RelayCommand]
    private async Task DeleteContextActionAsync()
    {
        if (SelectedContextAction is null)
        {
            return;
        }

        try
        {
            await _contextActions.DeleteAsync(SelectedContextAction.Action.Id).ConfigureAwait(true);
            ContextActionRows.Remove(SelectedContextAction);
            SelectedContextAction = null;
        }
        catch
        {
            // ignore
        }
    }

    public async Task SaveNewContextActionAsync(TemplateContextAction action, CancellationToken cancellationToken = default)
    {
        await _contextActions.SaveAsync(action, cancellationToken).ConfigureAwait(true);
        await ReloadContextActionsAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task<bool> InsertColumnIntoSelectedTemplateAsync(int insertAt, string columnName, CancellationToken cancellationToken = default)
    {
        if (SelectedTemplate is null)
        {
            return false;
        }

        var trimmed = columnName.Trim();
        if (trimmed.Length == 0)
        {
            FormError = "Column name cannot be empty.";
            return false;
        }

        var fields = SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var safeIndex = Math.Clamp(insertAt, 0, fields.Count);
        fields.Insert(
            safeIndex,
            new TemplateFieldDefinition
            {
                Key = MakeFieldKey(trimmed, safeIndex),
                Label = trimmed,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = safeIndex,
                ValidationPattern = null,
                Options = null,
                LinkedTemplateId = null,
                LinkedDisplayFieldKey = null,
            });

        var remapped = fields
            .Select((f, i) => new TemplateFieldDefinition
            {
                Key = string.IsNullOrWhiteSpace(f.Key) ? MakeFieldKey(f.Label, i) : f.Key,
                Label = f.Label,
                Type = f.Type,
                IsRequired = f.IsRequired,
                DisplayOrder = i,
                ValidationPattern = f.ValidationPattern,
                Options = f.Options,
                LinkedTemplateId = f.LinkedTemplateId,
                LinkedDisplayFieldKey = f.LinkedDisplayFieldKey,
            })
            .ToList();

        return await SavePatchedTemplateAsync(remapped, cancellationToken).ConfigureAwait(true);
    }

    public async Task<bool> RemoveColumnFromSelectedTemplateAsync(int index, CancellationToken cancellationToken = default)
    {
        if (SelectedTemplate is null)
        {
            return false;
        }

        var fields = SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        if (index < 0 || index >= fields.Count || fields.Count <= 1)
        {
            return false;
        }

        fields.RemoveAt(index);
        var remapped = fields
            .Select((f, i) => new TemplateFieldDefinition
            {
                Key = f.Key,
                Label = f.Label,
                Type = f.Type,
                IsRequired = f.IsRequired,
                DisplayOrder = i,
                ValidationPattern = f.ValidationPattern,
                Options = f.Options,
                LinkedTemplateId = f.LinkedTemplateId,
                LinkedDisplayFieldKey = f.LinkedDisplayFieldKey,
            })
            .ToList();

        return await SavePatchedTemplateAsync(remapped, cancellationToken).ConfigureAwait(true);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        FormError = string.Empty;
        _setupContext.Refresh();
        _hasTenantDb = _setupContext.TryGetTenantMongoUri(out _);

        IReadOnlyList<PartTemplateDefinition> templates;
        try
        {
            templates = await _templateSchema.GetTemplatesAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
            templates = Array.Empty<PartTemplateDefinition>();
        }

        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }

        var hasMaster = templates.Any(
            t => string.Equals(t.Name, MongoTemplateSchemaService.MasterDataTemplateName, StringComparison.OrdinalIgnoreCase));
        CanAddAnotherTemplate = hasMaster;

        SelectedTemplate = Templates.FirstOrDefault();

        if (!hasMaster && _hasTenantDb)
        {
            BeginMasterDataFlow();
        }
        else
        {
            IsCreatingTemplate = false;
        }

        NotifyTemplateUiState();
    }

    private void BeginMasterDataFlow()
    {
        IsEditingExisting = false;
        EditingTemplateId = null;
        IsCreatingTemplate = true;
        NewTemplateName = MongoTemplateSchemaService.MasterDataTemplateName;
        IsTemplateNameReadOnly = true;
        ColumnLabels.Clear();
        ColumnLabels.Add(new ColumnLabelDraft());
        SelectedTemplate = null;
        NotifyTemplateUiState();
    }

    [RelayCommand]
    private void AddColumn()
    {
        ColumnLabels.Add(new ColumnLabelDraft());
    }

    [RelayCommand]
    private void InsertColumnAfter(ColumnLabelDraft? draft)
    {
        if (draft is null)
        {
            ColumnLabels.Add(new ColumnLabelDraft());
            return;
        }

        var index = ColumnLabels.IndexOf(draft);
        if (index < 0)
        {
            ColumnLabels.Add(new ColumnLabelDraft());
            return;
        }

        ColumnLabels.Insert(index + 1, new ColumnLabelDraft());
    }

    [RelayCommand]
    private void InsertColumnBefore(ColumnLabelDraft? draft)
    {
        if (draft is null)
        {
            ColumnLabels.Insert(0, new ColumnLabelDraft());
            return;
        }

        var index = ColumnLabels.IndexOf(draft);
        if (index < 0)
        {
            ColumnLabels.Insert(0, new ColumnLabelDraft());
            return;
        }

        ColumnLabels.Insert(index, new ColumnLabelDraft());
    }

    [RelayCommand]
    private void RemoveColumn(ColumnLabelDraft? draft)
    {
        if (draft is null || ColumnLabels.Count <= 1)
        {
            return;
        }

        ColumnLabels.Remove(draft);
    }

    [RelayCommand]
    private void StartNewCustomTemplate()
    {
        if (!CanAddAnotherTemplate)
        {
            return;
        }

        FormError = string.Empty;
        IsEditingExisting = false;
        EditingTemplateId = null;
        IsCreatingTemplate = true;
        IsTemplateNameReadOnly = false;
        NewTemplateName = string.Empty;
        ColumnLabels.Clear();
        ColumnLabels.Add(new ColumnLabelDraft());
        SelectedTemplate = null;
        NotifyTemplateUiState();
    }

    [RelayCommand]
    private void BeginEditSelectedTemplate()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        FormError = string.Empty;
        IsEditingExisting = true;
        EditingTemplateId = SelectedTemplate.Id;
        IsCreatingTemplate = true;
        IsTemplateNameReadOnly = string.Equals(
            SelectedTemplate.Name,
            MongoTemplateSchemaService.MasterDataTemplateName,
            StringComparison.OrdinalIgnoreCase);
        NewTemplateName = SelectedTemplate.Name;
        ColumnLabels.Clear();
        foreach (var f in SelectedTemplate.Fields.OrderBy(x => x.DisplayOrder))
        {
            ColumnLabels.Add(
                new ColumnLabelDraft
                {
                    Label = f.Label,
                    FieldType = f.Type,
                    LinkedTemplateId = f.LinkedTemplateId,
                    StableKey = f.Key,
                });
        }

        if (ColumnLabels.Count == 0)
        {
            ColumnLabels.Add(new ColumnLabelDraft());
        }

        SelectedTemplate = null;
        NotifyTemplateUiState();
    }

    [RelayCommand]
    private void CancelCreate()
    {
        FormError = string.Empty;
        IsEditingExisting = false;
        EditingTemplateId = null;
        IsCreatingTemplate = false;
        SelectedTemplate = Templates.FirstOrDefault();

        var hasMaster = Templates.Any(
            t => string.Equals(t.Name, MongoTemplateSchemaService.MasterDataTemplateName, StringComparison.OrdinalIgnoreCase));
        if (!hasMaster && _hasTenantDb)
        {
            BeginMasterDataFlow();
        }
    }

    [RelayCommand]
    private async Task SaveTemplateAsync()
    {
        FormError = string.Empty;
        var name = NewTemplateName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            FormError = "Enter a template name.";
            return;
        }

        var hasMasterAlready = Templates.Any(
            t => string.Equals(t.Name, MongoTemplateSchemaService.MasterDataTemplateName, StringComparison.OrdinalIgnoreCase));

        if (!IsEditingExisting)
        {
            if (!hasMasterAlready
                && !string.Equals(name, MongoTemplateSchemaService.MasterDataTemplateName, StringComparison.OrdinalIgnoreCase))
            {
                FormError = "Your first template must be named Master Data.";
                return;
            }

            if (hasMasterAlready
                && string.Equals(name, MongoTemplateSchemaService.MasterDataTemplateName, StringComparison.OrdinalIgnoreCase))
            {
                FormError = "A template named Master Data already exists.";
                return;
            }

            if (Templates.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                FormError = "A template with this name already exists.";
                return;
            }
        }
        else
        {
            if (Templates.Any(
                    t => !string.Equals(t.Id, EditingTemplateId, StringComparison.Ordinal)
                         && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                FormError = "A template with this name already exists.";
                return;
            }
        }

        var nonEmptyCount = ColumnLabels.Count(c => c.Label.Trim().Length > 0);
        if (nonEmptyCount == 0)
        {
            FormError = "Add at least one column name.";
            return;
        }

        string templateId;
        var version = 1;
        if (IsEditingExisting)
        {
            templateId = EditingTemplateId!;
            var existing = Templates.FirstOrDefault(t => t.Id == templateId);
            if (existing is not null)
            {
                version = existing.Version + 1;
            }
        }
        else if (!hasMasterAlready)
        {
            templateId = MongoTemplateSchemaService.MasterDataTemplateId;
        }
        else
        {
            templateId = Guid.NewGuid().ToString("N");
        }

        var fields = new List<TemplateFieldDefinition>();
        var ord = 0;
        for (var i = 0; i < ColumnLabels.Count; i++)
        {
            var draft = ColumnLabels[i];
            var label = draft.Label.Trim();
            if (label.Length == 0)
            {
                continue;
            }

            var key = !string.IsNullOrWhiteSpace(draft.StableKey)
                ? draft.StableKey!
                : MakeFieldKey(label, ord);

            if (draft.FieldType == TemplateFieldType.RecordLink)
            {
                if (string.IsNullOrWhiteSpace(draft.LinkedTemplateId))
                {
                    FormError = $"For \"{label}\", choose which template to link to.";
                    return;
                }

                if (string.Equals(draft.LinkedTemplateId, templateId, StringComparison.Ordinal))
                {
                    FormError = "A column cannot link to the same template it belongs to.";
                    return;
                }
            }

            fields.Add(
                new TemplateFieldDefinition
                {
                    Key = key,
                    Label = label,
                    Type = draft.FieldType,
                    IsRequired = false,
                    DisplayOrder = ord++,
                    ValidationPattern = null,
                    Options = null,
                    LinkedTemplateId = draft.FieldType == TemplateFieldType.RecordLink ? draft.LinkedTemplateId : null,
                    LinkedDisplayFieldKey = null,
                });
        }

        var def = new PartTemplateDefinition
        {
            Id = templateId,
            Name = name,
            Version = version,
            IsPublished = true,
            Fields = fields,
        };

        try
        {
            await _templateSchema.SaveTemplateAsync(def).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
            return;
        }

        var firstMasterSave = !IsEditingExisting && !hasMasterAlready;
        IsEditingExisting = false;
        EditingTemplateId = null;
        await LoadAsync().ConfigureAwait(true);
        await _shellNav.NotifyTemplatesChangedAsync(openMasterDataPage: firstMasterSave).ConfigureAwait(true);
    }

    private static string MakeFieldKey(string label, int index)
    {
        var lowered = label.ToLowerInvariant();
        var chars = lowered.Where(static c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var baseKey = new string(chars);
        if (string.IsNullOrEmpty(baseKey))
        {
            baseKey = "column";
        }

        return $"{baseKey}_{index}";
    }

    private async Task<bool> SavePatchedTemplateAsync(IReadOnlyList<TemplateFieldDefinition> fields, CancellationToken cancellationToken)
    {
        if (SelectedTemplate is null)
        {
            return false;
        }

        FormError = string.Empty;
        var def = new PartTemplateDefinition
        {
            Id = SelectedTemplate.Id,
            Name = SelectedTemplate.Name,
            Version = SelectedTemplate.Version + 1,
            IsPublished = true,
            Fields = fields.ToList(),
        };

        try
        {
            await _templateSchema.SaveTemplateAsync(def, cancellationToken).ConfigureAwait(true);
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            SelectedTemplate = Templates.FirstOrDefault(t => t.Id == def.Id) ?? Templates.FirstOrDefault();
            return true;
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
            return false;
        }
    }
}

public sealed record TemplateFieldTypeOption(TemplateFieldType Value, string Label);
