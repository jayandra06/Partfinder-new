using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Converters;
using PartFinder.Models;

namespace PartFinder.Tests.Converters;

[TestClass]
public sealed class TemplateFieldTypeToInputHintConverterTests
{
    private readonly TemplateFieldTypeToInputHintConverter _converter = new();

    [TestMethod]
    [DataRow(TemplateFieldType.Number, "e.g. 100")]
    [DataRow(TemplateFieldType.Decimal, "e.g. 99.95")]
    [DataRow(TemplateFieldType.Date, "e.g. 2026-04-29")]
    [DataRow(TemplateFieldType.Boolean, "Yes / No")]
    [DataRow(TemplateFieldType.Dropdown, "Select value")]
    [DataRow(TemplateFieldType.RecordLink, "Linked record")]
    [DataRow(TemplateFieldType.Text, "Column label")]
    public void EachFieldType_HasHint(TemplateFieldType type, string expected)
    {
        Assert.AreEqual(expected, _converter.Convert(type, typeof(string), null!, "en"));
    }

    [TestMethod]
    public void NonFieldTypeValue_FallsBackToColumnLabel()
    {
        Assert.AreEqual("Column label", _converter.Convert("Number", typeof(string), null!, "en"));
    }

    [TestMethod]
    public void EveryEnumValue_ProducesNonEmptyHint()
    {
        foreach (var type in Enum.GetValues<TemplateFieldType>())
        {
            var hint = _converter.Convert(type, typeof(string), null!, "en") as string;
            Assert.IsFalse(string.IsNullOrWhiteSpace(hint), type.ToString());
        }
    }

    [TestMethod]
    public void ConvertBack_ReturnsText()
    {
        Assert.AreEqual(TemplateFieldType.Text, _converter.ConvertBack("e.g. 100", typeof(TemplateFieldType), null!, "en"));
    }
}
