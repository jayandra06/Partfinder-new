using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class PartsViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchemaService;
    private readonly IPartsDataService _partsDataService;
    private CancellationTokenSource? _loadCts;
    private int _offset;

    public PartsViewModel(ITemplateSchemaService templateSchemaService, IPartsDataService partsDataService)
    {
        _templateSchemaService = templateSchemaService;
        _partsDataService = partsDataService;
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; } = [];
    public ObservableCollection<GridColumnDefinition> DynamicColumns { get; } = [];
    public ObservableCollection<PartRecord> Records { get; } = [];

    private PartTemplateDefinition? _selectedTemplate;
    public PartTemplateDefinition? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value) && value is not null)
            {
                _ = RefreshAsync();
            }
        }
    }

    private bool _hasMoreRows;
    public bool HasMoreRows
    {
        get => _hasMoreRows;
        set => SetProperty(ref _hasMoreRows, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _templateSchemaService.GetTemplatesAsync(cancellationToken);
        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }

        SelectedTemplate = Templates.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _offset = 0;
        Records.Clear();
        DynamicColumns.Clear();

        foreach (var field in SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder))
        {
            DynamicColumns.Add(new GridColumnDefinition { Key = field.Key, Header = field.Label });
        }

        await LoadNextPageAsync(_loadCts.Token);
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextPage))]
    private async Task LoadMoreAsync()
    {
        if (_loadCts is null)
        {
            _loadCts = new CancellationTokenSource();
        }

        await LoadNextPageAsync(_loadCts.Token);
    }

    private bool CanLoadNextPage() => HasMoreRows && !IsLoading;

    private async Task LoadNextPageAsync(CancellationToken cancellationToken)
    {
        if (SelectedTemplate is null || IsLoading)
        {
            return;
        }

        IsLoading = true;
        LoadMoreCommand.NotifyCanExecuteChanged();

        try
        {
            var page = await _partsDataService.GetPageAsync(SelectedTemplate.Id, _offset, 200, cancellationToken);
            foreach (var item in page.Records)
            {
                Records.Add(item);
            }

            _offset += page.Records.Count;
            HasMoreRows = page.HasMore;
        }
        finally
        {
            IsLoading = false;
            LoadMoreCommand.NotifyCanExecuteChanged();
        }
    }
}
