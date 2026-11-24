using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Helpers;

namespace PartFinder.Tests.Helpers;

[TestClass]
public sealed class CarouselDepthCalculatorTests
{
    private const double Tolerance = 1e-9;

    [TestMethod]
    public void FrontCard_IsFullyVisibleAndUnshifted()
    {
        Assert.AreEqual(1.0, CarouselDepthCalculator.GetOpacity(0), Tolerance);
        Assert.AreEqual(1.0, CarouselDepthCalculator.GetScale(0), Tolerance);
        Assert.AreEqual(0.0, CarouselDepthCalculator.GetBlurRadius(0), Tolerance);
        Assert.AreEqual(0.0, CarouselDepthCalculator.GetYOffset(0), Tolerance);
    }

    [TestMethod]
    [DataRow(1, 0.85, 0.95, 1.5, 12.0)]
    [DataRow(2, 0.70, 0.90, 3.0, 24.0)]
    [DataRow(4, 0.40, 0.80, 6.0, 48.0)]
    public void DeeperCards_FadeShrinkBlurAndShift(int depth, double opacity, double scale, double blur, double y)
    {
        Assert.AreEqual(opacity, CarouselDepthCalculator.GetOpacity(depth), Tolerance);
        Assert.AreEqual(scale, CarouselDepthCalculator.GetScale(depth), Tolerance);
        Assert.AreEqual(blur, CarouselDepthCalculator.GetBlurRadius(depth), Tolerance);
        Assert.AreEqual(y, CarouselDepthCalculator.GetYOffset(depth), Tolerance);
    }

    [TestMethod]
    public void Opacity_IsClampedAtZero()
    {
        Assert.AreEqual(0.0, CarouselDepthCalculator.GetOpacity(7), Tolerance);
        Assert.AreEqual(0.0, CarouselDepthCalculator.GetOpacity(50), Tolerance);
    }

    [TestMethod]
    public void Scale_IsClampedAtZero()
    {
        Assert.AreEqual(0.0, CarouselDepthCalculator.GetScale(20), Tolerance);
        Assert.AreEqual(0.0, CarouselDepthCalculator.GetScale(25), Tolerance);
    }

    [TestMethod]
    public void Opacity_DecreasesMonotonically()
    {
        for (var d = 0; d < 10; d++)
        {
            Assert.IsTrue(CarouselDepthCalculator.GetOpacity(d + 1) <= CarouselDepthCalculator.GetOpacity(d));
        }
    }
}
