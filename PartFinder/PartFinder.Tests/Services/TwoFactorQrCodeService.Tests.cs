using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class TwoFactorQrCodeServiceTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [TestMethod]
    public void BuildTotpSetupUri_TrimsEscapesAndUppercasesSecret()
    {
        var uri = TwoFactorQrCodeService.BuildTotpSetupUri(" Part Finder ", " admin@acme.com ", " jbswy3dpehpk3pxp ");

        Assert.AreEqual(
            "otpauth://totp/Part%20Finder:admin%40acme.com?secret=JBSWY3DPEHPK3PXP&issuer=Part%20Finder",
            uri);
    }

    [TestMethod]
    public void BuildTotpSetupUri_ProducesAParsableOtpauthUri()
    {
        var uri = new Uri(TwoFactorQrCodeService.BuildTotpSetupUri("PartFinder", "user", "abc"));

        Assert.AreEqual("otpauth", uri.Scheme);
        Assert.AreEqual("totp", uri.Host);
        StringAssert.Contains(uri.Query, "secret=ABC");
        StringAssert.Contains(uri.Query, "issuer=PartFinder");
    }

    [TestMethod]
    public void RenderPng_ReturnsPngBytes()
    {
        var png = TwoFactorQrCodeService.RenderPng("otpauth://totp/PartFinder:user?secret=ABC&issuer=PartFinder");

        CollectionAssert.AreEqual(PngSignature, png.Take(8).ToArray());
    }

    [TestMethod]
    public void RenderPng_LargerModulesProduceLargerImages()
    {
        const string uri = "otpauth://totp/PartFinder:user?secret=ABC&issuer=PartFinder";

        var small = TwoFactorQrCodeService.RenderPng(uri, 2);
        var large = TwoFactorQrCodeService.RenderPng(uri, 10);

        Assert.IsTrue(ReadPngWidth(large) > ReadPngWidth(small));
        Assert.AreEqual(ReadPngWidth(small) * 5, ReadPngWidth(large));
    }

    private static int ReadPngWidth(byte[] png) =>
        (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
}
