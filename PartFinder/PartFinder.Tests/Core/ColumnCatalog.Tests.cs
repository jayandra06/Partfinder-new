using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Core;

[TestClass]
public sealed class ColumnCatalogTests
{
    [TestMethod]
    public void StandardColumns_HaveUniqueKeysAndLabels()
    {
        var keys = ColumnCatalog.StandardColumns.Select(c => c.Key).ToList();
        var labels = ColumnCatalog.StandardColumns.Select(c => c.Label).ToList();

        Assert.AreEqual(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.AreEqual(labels.Count, labels.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [TestMethod]
    public void StandardColumns_UseKnownCategories()
    {
        var categories = ColumnCatalog.StandardColumns
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "Part identity", "Vehicle", "Vendor" }, categories);
    }

    [TestMethod]
    public void GetMissingByLabel_NoExistingLabels_ReturnsWholeCatalogInOrder()
    {
        var missing = ColumnCatalog.GetMissingByLabel([]);

        CollectionAssert.AreEqual(ColumnCatalog.StandardColumns.ToList(), missing.ToList());
    }

    [TestMethod]
    public void GetMissingByLabel_MatchesLabelsCaseInsensitively()
    {
        var missing = ColumnCatalog.GetMissingByLabel(["BRAND", "part no"]);

        Assert.AreEqual(ColumnCatalog.StandardColumns.Count - 2, missing.Count);
        Assert.IsFalse(missing.Any(m => m.Key == "brand"));
        Assert.IsFalse(missing.Any(m => m.Key == "part_no"));
    }

    [TestMethod]
    public void GetMissingByLabel_MatchesOnLabelNotKey()
    {
        var missing = ColumnCatalog.GetMissingByLabel(["vendor_phone"]);

        Assert.IsTrue(missing.Any(m => m.Key == "vendor_phone"));
    }

    [TestMethod]
    public void GetMissingByLabel_AllLabelsPresent_ReturnsEmpty()
    {
        var missing = ColumnCatalog.GetMissingByLabel(ColumnCatalog.StandardColumns.Select(c => c.Label));

        Assert.AreEqual(0, missing.Count);
    }

    [TestMethod]
    public void GetMissingByLabel_UnknownLabelsAreIgnored()
    {
        var missing = ColumnCatalog.GetMissingByLabel(["Colour", "Weight"]);

        Assert.AreEqual(ColumnCatalog.StandardColumns.Count, missing.Count);
    }
}
