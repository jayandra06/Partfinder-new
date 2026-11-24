using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;

namespace PartFinder.Tests.Models;

[TestClass]
public sealed class PartTemplateDefinitionTests
{
    private static PartTemplateDefinition WithFields(int count) => new()
    {
        Id = "t",
        Name = "Brake Pads",
        Fields = Enumerable.Range(0, count)
            .Select(i => new TemplateFieldDefinition { Key = "k" + i, Label = "L" + i })
            .ToList(),
    };

    [TestMethod]
    [DataRow(0, "0 columns")]
    [DataRow(1, "1 column")]
    [DataRow(5, "5 columns")]
    public void ColumnCountLabel_Pluralises(int count, string expected)
    {
        Assert.AreEqual(expected, WithFields(count).ColumnCountLabel);
    }

    [TestMethod]
    public void ToString_ReturnsName()
    {
        Assert.AreEqual("Brake Pads", WithFields(1).ToString());
    }

    [TestMethod]
    public void Defaults_AreMasterDataCatalogWithoutBase()
    {
        var template = WithFields(1);

        Assert.IsTrue(template.IsMasterDataCatalog);
        Assert.IsNull(template.BaseTemplateId);
        Assert.IsFalse(template.IsPublished);
        Assert.AreEqual(0, template.Version);
    }

    [TestMethod]
    public void FieldDefinition_DefaultsToOptionalText()
    {
        var field = new TemplateFieldDefinition { Key = "k", Label = "L" };

        Assert.AreEqual(TemplateFieldType.Text, field.Type);
        Assert.IsFalse(field.IsRequired);
        Assert.IsNull(field.LinkedTemplateId);
    }
}
