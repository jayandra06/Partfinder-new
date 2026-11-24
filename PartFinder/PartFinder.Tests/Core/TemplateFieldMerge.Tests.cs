using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;
using Field = PartFinder.Core.TemplateFieldMerge.FieldSnapshot;

namespace PartFinder.Tests.Core;

[TestClass]
public sealed class TemplateFieldMergeTests
{
    private static Field F(string key, string label, int order, string type = "Text") =>
        new(key, label, type, false, order, null, null);

    [TestMethod]
    public void EmptyBase_ReturnsChildListUnchanged()
    {
        var child = new[] { F("a", "A", 0) };

        var merged = TemplateFieldMerge.MergeInheritedFields(Array.Empty<Field>(), child);

        Assert.AreSame(child, merged);
    }

    [TestMethod]
    public void ChildOverridesBaseByKey_CaseInsensitive()
    {
        var baseFields = new[] { F("brand", "Brand", 0) };
        var child = new[] { F("BRAND", "Make", 0, "Dropdown") };

        var merged = TemplateFieldMerge.MergeInheritedFields(baseFields, child);

        Assert.AreEqual(1, merged.Count);
        Assert.AreEqual("Make", merged[0].Label);
        Assert.AreEqual("Dropdown", merged[0].Type);
    }

    [TestMethod]
    public void Result_IsOrderedByDisplayOrderThenLabel()
    {
        var baseFields = new[] { F("z", "Zeta", 2), F("a", "Alpha", 0) };
        var child = new[] { F("m", "beta", 1), F("n", "Able", 1) };

        var merged = TemplateFieldMerge.MergeInheritedFields(baseFields, child);

        CollectionAssert.AreEqual(
            new[] { "Alpha", "Able", "beta", "Zeta" },
            merged.Select(f => f.Label).ToArray());
    }

    [TestMethod]
    public void BaseOnlyAndChildOnlyFields_AreBothKept()
    {
        var baseFields = new[] { F("brand", "Brand", 0), F("model", "Model", 1) };
        var child = new[] { F("vendor", "Vendor", 2) };

        var merged = TemplateFieldMerge.MergeInheritedFields(baseFields, child);

        CollectionAssert.AreEquivalent(
            new[] { "brand", "model", "vendor" },
            merged.Select(f => f.Key).ToArray());
    }

    [TestMethod]
    public void LinkMetadata_ComesFromOverridingChild()
    {
        var baseFields = new[] { new Field("vendor", "Vendor", "Text", false, 0, null, null) };
        var child = new[] { new Field("vendor", "Vendor", "RecordLink", true, 0, "vendors", "name") };

        var merged = TemplateFieldMerge.MergeInheritedFields(baseFields, child);

        Assert.AreEqual("vendors", merged[0].LinkedTemplateId);
        Assert.AreEqual("name", merged[0].LinkedDisplayFieldKey);
        Assert.IsTrue(merged[0].IsRequired);
    }
}
