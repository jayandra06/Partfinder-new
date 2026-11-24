using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class MasterDataSourceFieldEncodingTests
{
    [TestMethod]
    public void TryParse_ValidPattern_ReturnsCatalogAndField()
    {
        var ok = MasterDataSourceFieldEncoding.TryParse("pfsrc|cat-1|brand", out var catalogId, out var fieldKey);

        Assert.IsTrue(ok);
        Assert.AreEqual("cat-1", catalogId);
        Assert.AreEqual("brand", fieldKey);
    }

    [TestMethod]
    public void TryParse_FieldKeyMayContainSeparator()
    {
        Assert.IsTrue(MasterDataSourceFieldEncoding.TryParse("pfsrc|cat|a|b", out _, out var fieldKey));
        Assert.AreEqual("a|b", fieldKey);
    }

    [TestMethod]
    [DataRow((string?)null)]
    [DataRow("")]
    [DataRow("^[A-Z]+$")]
    [DataRow("PFSRC|cat|brand")]
    [DataRow("pfsrc|cat")]
    [DataRow("pfsrc||brand")]
    [DataRow("pfsrc|cat| ")]
    public void TryParse_InvalidPatterns_ReturnFalseAndEmptyOutputs(string? pattern)
    {
        var ok = MasterDataSourceFieldEncoding.TryParse(pattern, out var catalogId, out var fieldKey);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, catalogId);
        Assert.AreEqual(string.Empty, fieldKey);
    }

    [TestMethod]
    public void IsSourcedFromCatalog_MatchesCatalogIdExactly()
    {
        var field = new TemplateFieldDefinition
        {
            Key = "brand",
            Label = "Brand",
            ValidationPattern = MasterDataSourceFieldEncoding.SourcePrefix + "cat-1|brand",
        };

        Assert.IsTrue(MasterDataSourceFieldEncoding.IsSourcedFromCatalog(field, "cat-1"));
        Assert.IsFalse(MasterDataSourceFieldEncoding.IsSourcedFromCatalog(field, "CAT-1"));
        Assert.IsFalse(MasterDataSourceFieldEncoding.IsSourcedFromCatalog(field, "cat-2"));
    }

    [TestMethod]
    public void IsSourcedFromCatalog_FieldWithoutProvenance_IsFalse()
    {
        var field = new TemplateFieldDefinition { Key = "k", Label = "K" };

        Assert.IsFalse(MasterDataSourceFieldEncoding.IsSourcedFromCatalog(field, "cat-1"));
    }
}
