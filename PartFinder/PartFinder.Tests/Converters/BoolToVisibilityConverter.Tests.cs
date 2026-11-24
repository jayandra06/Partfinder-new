using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Converters;

namespace PartFinder.Tests.Converters;

[TestClass]
public sealed class BoolToVisibilityConverterTests
{
    private readonly BoolToVisibilityConverter _converter = new();

    [TestMethod]
    public void True_IsVisible()
    {
        Assert.AreEqual(Visibility.Visible, _converter.Convert(true, typeof(Visibility), null!, "en"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow("true")]
    [DataRow(1)]
    public void FalseOrNonBool_IsCollapsed(object value)
    {
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert(value, typeof(Visibility), null!, "en"));
    }

    [TestMethod]
    public void Null_IsCollapsed()
    {
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert(null!, typeof(Visibility), null!, "en"));
    }

    [TestMethod]
    public void ConvertBack_MapsVisibilityToBool()
    {
        Assert.AreEqual(true, _converter.ConvertBack(Visibility.Visible, typeof(bool), null!, "en"));
        Assert.AreEqual(false, _converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, "en"));
        Assert.AreEqual(false, _converter.ConvertBack("Visible", typeof(bool), null!, "en"));
    }
}
