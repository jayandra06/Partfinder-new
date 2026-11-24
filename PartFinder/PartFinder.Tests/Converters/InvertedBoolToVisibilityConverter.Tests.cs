using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Converters;

namespace PartFinder.Tests.Converters;

[TestClass]
public sealed class InvertedBoolToVisibilityConverterTests
{
    private readonly InvertedBoolToVisibilityConverter _converter = new();

    [TestMethod]
    public void True_IsCollapsed()
    {
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert(true, typeof(Visibility), null!, "en"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow("true")]
    public void FalseOrNonBool_IsVisible(object value)
    {
        Assert.AreEqual(Visibility.Visible, _converter.Convert(value, typeof(Visibility), null!, "en"));
    }

    [TestMethod]
    public void ConvertBack_ReturnsWhetherVisible()
    {
        Assert.AreEqual(true, _converter.ConvertBack(Visibility.Visible, typeof(bool), null!, "en"));
        Assert.AreEqual(false, _converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, "en"));
    }
}
