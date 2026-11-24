using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Converters;

namespace PartFinder.Tests.Converters;

[TestClass]
public sealed class BoolToStarGlyphConverterTests
{
    private const string FilledStar = "\uE735";
    private const string OutlineStar = "\uE734";
    private readonly BoolToStarGlyphConverter _converter = new();

    [TestMethod]
    public void Favourite_ShowsFilledStar()
    {
        Assert.AreEqual(FilledStar, _converter.Convert(true, typeof(string), null!, "en"));
    }

    [TestMethod]
    public void NotFavourite_ShowsOutlineStar()
    {
        Assert.AreEqual(OutlineStar, _converter.Convert(false, typeof(string), null!, "en"));
    }

    [TestMethod]
    public void NonBool_DefaultsToOutlineStar()
    {
        Assert.AreEqual(OutlineStar, _converter.Convert("yes", typeof(string), null!, "en"));
    }

    [TestMethod]
    public void ConvertBack_IsNotImplemented()
    {
        Assert.ThrowsException<NotImplementedException>(
            () => _converter.ConvertBack(FilledStar, typeof(bool), null!, "en"));
    }
}
