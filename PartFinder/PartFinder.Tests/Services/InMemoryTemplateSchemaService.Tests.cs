using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class InMemoryTemplateSchemaServiceTests
{
    private readonly InMemoryTemplateSchemaService _service = new();

    [TestMethod]
    public async Task GetTemplatesAsync_ReturnsTwoPublishedTemplates()
    {
        var templates = await _service.GetTemplatesAsync();

        CollectionAssert.AreEqual(new[] { "template-a", "template-b" }, templates.Select(t => t.Id).ToArray());
        Assert.IsTrue(templates.All(t => t.IsPublished));
    }

    [TestMethod]
    public async Task GetTemplateAsync_ReturnsFieldsInDisplayOrder()
    {
        var template = await _service.GetTemplateAsync("template-b");

        Assert.IsNotNull(template);
        CollectionAssert.AreEqual(
            new[] { "part_no", "price", "SKU", "stock_level" },
            template.Fields.OrderBy(f => f.DisplayOrder).Select(f => f.Key).ToArray());
        Assert.AreEqual(TemplateFieldType.Number, template.Fields.Single(f => f.Key == "stock_level").Type);
    }

    [TestMethod]
    public async Task GetTemplateAsync_UnknownId_ReturnsNull()
    {
        Assert.IsNull(await _service.GetTemplateAsync("nope"));
    }

    [TestMethod]
    public async Task RequiredFields_ArePartNoAndPrice()
    {
        var template = await _service.GetTemplateAsync("template-a");

        CollectionAssert.AreEquivalent(
            new[] { "part_no", "price" },
            template!.Fields.Where(f => f.IsRequired).Select(f => f.Key).ToArray());
    }

    [TestMethod]
    public async Task SaveAndDelete_AreNotSupported()
    {
        var template = (await _service.GetTemplateAsync("template-a"))!;

        await Assert.ThrowsExceptionAsync<NotSupportedException>(() => _service.SaveTemplateAsync(template));
        await Assert.ThrowsExceptionAsync<NotSupportedException>(() => _service.DeleteTemplateAsync("template-a"));
    }
}
