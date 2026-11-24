using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Converters;
using PartFinder.Models;

namespace PartFinder.Tests.Converters;

[TestClass]
public sealed class TemplateFieldTypeEqualsToVisibilityConverterTests
{
    private readonly TemplateFieldTypeEqualsToVisibilityConverter _converter = new();

    [TestMethod]
    [DataRow("Dropdown")]
    [DataRow("dropdown")]
    public void MatchingType_IsVisible(string parameter)
    {
        Assert.AreEqual(Visibility.Visible, _converter.Convert(TemplateFieldType.Dropdown, typeof(Visibility), parameter, "en"));
    }

    [TestMethod]
    public void DifferentType_IsCollapsed()
    {
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert(TemplateFieldType.Text, typeof(Visibility), "Dropdown", "en"));
    }

    [TestMethod]
    public void UnknownTypeName_IsCollapsed()
    {
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert(TemplateFieldType.Text, typeof(Visibility), "Colour", "en"));
    }

    [TestMethod]
    public void WrongValueOrParameterTypes_AreCollapsed()
    {
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert("Dropdown", typeof(Visibility), "Dropdown", "en"));
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert(TemplateFieldType.Dropdown, typeof(Visibility), 3, "en"));
    }

    [TestMethod]
    public void ConvertBack_ReturnsText()
    {
        Assert.AreEqual(TemplateFieldType.Text, _converter.ConvertBack(Visibility.Visible, typeof(TemplateFieldType), "Dropdown", "en"));
    }
}
