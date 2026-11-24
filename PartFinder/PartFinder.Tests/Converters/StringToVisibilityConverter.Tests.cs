using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Converters;

namespace PartFinder.Tests.Converters;

[TestClass]
public sealed class StringToVisibilityConverterTests
{
    private readonly StringToVisibilityConverter _converter = new();

    [TestMethod]
    public void NonEmptyString_IsVisible()
    {
        Assert.AreEqual(Visibility.Visible, _converter.Convert("Error!", typeof(Visibility), null!, "en"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(42)]
    public void BlankOrNonString_IsCollapsed(object value)
    {
        Assert.AreEqual(Visibility.Collapsed, _converter.Convert(value, typeof(Visibility), null!, "en"));
    }

    [TestMethod]
    public void ConvertBack_IsNotSupported()
    {
        Assert.ThrowsException<NotSupportedException>(
            () => _converter.ConvertBack(Visibility.Visible, typeof(string), null!, "en"));
    }
}
