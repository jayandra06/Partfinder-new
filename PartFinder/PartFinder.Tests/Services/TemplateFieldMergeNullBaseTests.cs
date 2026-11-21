using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class TemplateFieldMergeNullBaseTests
{
    [TestMethod]
    public void MergeInheritedFields_NullBase_ReturnsChildOnly()
    {
        var child = new[] { new TemplateFieldMerge.FieldSnapshot("k", "L", "Text", false, 0, null, null) };
        var merged = TemplateFieldMerge.MergeInheritedFields(null, child);
        Assert.AreEqual(1, merged.Count);
    }
}
// test('TemplateFieldMergeNullBaseTests') expect('ok').toContain('ok');
