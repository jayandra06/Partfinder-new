using PartFinder.Models;

namespace PartFinder.Services;

public sealed class InMemoryTemplateSchemaService : ITemplateSchemaService
{
    private readonly IReadOnlyList<PartTemplateDefinition> _templates =
    [
        new PartTemplateDefinition
        {
            Id = "template-a",
            Name = "Template A",
            Version = 1,
            IsPublished = true,
            Fields =
            [
                new() { Key = "part_no", Label = "Part No", Type = TemplateFieldType.Text, IsRequired = true, DisplayOrder = 0 },
                new() { Key = "position_no", Label = "Position No", Type = TemplateFieldType.Text, IsRequired = false, DisplayOrder = 1 },
                new() { Key = "KF_number", Label = "KF Number", Type = TemplateFieldType.Text, IsRequired = false, DisplayOrder = 2 },
                new() { Key = "price", Label = "Price", Type = TemplateFieldType.Decimal, IsRequired = true, DisplayOrder = 3 },
                new() { Key = "remarks", Label = "Remarks", Type = TemplateFieldType.Text, IsRequired = false, DisplayOrder = 4 }
            ]
        },
        new PartTemplateDefinition
        {
            Id = "template-b",
            Name = "Template B",
            Version = 1,
            IsPublished = true,
            Fields =
            [
                new() { Key = "part_no", Label = "Part No", Type = TemplateFieldType.Text, IsRequired = true, DisplayOrder = 0 },
                new() { Key = "price", Label = "Price", Type = TemplateFieldType.Decimal, IsRequired = true, DisplayOrder = 1 },
                new() { Key = "SKU", Label = "SKU", Type = TemplateFieldType.Text, IsRequired = false, DisplayOrder = 2 },
                new() { Key = "stock_level", Label = "Stock Level", Type = TemplateFieldType.Number, IsRequired = false, DisplayOrder = 3 }
            ]
        }
    ];

    public Task<IReadOnlyList<PartTemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_templates);

    public Task<PartTemplateDefinition?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
        => Task.FromResult(_templates.FirstOrDefault(t => t.Id == templateId));

    public Task SaveTemplateAsync(PartTemplateDefinition template, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("In-memory template store is read-only.");
}
