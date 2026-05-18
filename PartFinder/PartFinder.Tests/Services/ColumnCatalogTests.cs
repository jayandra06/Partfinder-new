using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class ColumnCatalogTests
{
    [TestMethod]
    public void GetMissingByLabel_SkipsExisting()
    {
        var missing = ColumnCatalog.GetMissingByLabel(["Brand", "Vendor"]);

        Assert.IsTrue(missing.Any(m => string.Equals(m.Label, "Model", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(missing.Any(m => string.Equals(m.Label, "Brand", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void MergeInheritedFields_ChildOverridesBase()
    {
        var baseFields = new[]
        {
            new TemplateFieldMerge.FieldSnapshot("brand", "Brand", "Text", false, 0, null, null),
        };
        var childFields = new[]
        {
            new TemplateFieldMerge.FieldSnapshot("vendor", "Vendor", "Text", false, 1, null, null),
        };

        var merged = TemplateFieldMerge.MergeInheritedFields(baseFields, childFields);

        Assert.AreEqual(2, merged.Count);
        Assert.IsTrue(merged.Any(f => f.Key == "vendor"));
        Assert.IsTrue(merged.Any(f => f.Key == "brand"));
    }
}
