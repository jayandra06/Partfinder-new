using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class TemplateCatalogFiltersTests
{
    private static PartTemplateDefinition T(string id, bool isCatalog, params string[] labels) => new()
    {
        Id = id,
        Name = id,
        IsMasterDataCatalog = isCatalog,
        Fields = labels.Select((l, i) => new TemplateFieldDefinition { Key = "k" + i, Label = l, DisplayOrder = i }).ToList(),
    };

    private static readonly IReadOnlyList<PartTemplateDefinition> All =
    [
        T("cat-a", true, "Brand"),
        T("org-a", false, "Part No"),
        T("cat-b", true, "Vendor"),
        T("org-b", false, "SKU"),
    ];

    [TestMethod]
    public void MasterDataCatalogs_KeepsOnlyCatalogsInOrder()
    {
        CollectionAssert.AreEqual(
            new[] { "cat-a", "cat-b" },
            TemplateCatalogFilters.MasterDataCatalogs(All).Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public void OrganizationTemplates_ExcludesCatalogs()
    {
        CollectionAssert.AreEqual(
            new[] { "org-a", "org-b" },
            TemplateCatalogFilters.OrganizationTemplates(All).Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public void Filters_PartitionTheInput()
    {
        var total = TemplateCatalogFilters.MasterDataCatalogs(All).Count
                    + TemplateCatalogFilters.OrganizationTemplates(All).Count;

        Assert.AreEqual(All.Count, total);
    }

    [TestMethod]
    public void HasSharedColumnLabels_IgnoresCase()
    {
        Assert.IsTrue(TemplateCatalogFilters.HasSharedColumnLabels(
            T("p", false, "Part No", "Brand"),
            T("l", true, "brand")));
    }

    [TestMethod]
    public void HasSharedColumnLabels_FalseWhenDisjoint()
    {
        Assert.IsFalse(TemplateCatalogFilters.HasSharedColumnLabels(
            T("p", false, "Part No"),
            T("l", true, "Vendor")));
    }
}
