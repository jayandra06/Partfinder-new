using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public class TemplatesViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchemaService;

    public TemplatesViewModel(ITemplateSchemaService templateSchemaService)
    {
        _templateSchemaService = templateSchemaService;
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; } = [];

    private PartTemplateDefinition? _selectedTemplate;
    public PartTemplateDefinition? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _templateSchemaService.GetTemplatesAsync(cancellationToken);
        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }

        SelectedTemplate = Templates.FirstOrDefault();
    }
}
