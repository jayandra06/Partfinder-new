using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Helpers;

namespace PartFinder.Tests.Helpers;

[TestClass]
public sealed class BritishEnglishTests
{
    [TestMethod]
    [DataRow("Organization", "Organisation")]
    [DataRow("organizations", "organisations")]
    [DataRow("License", "Licence")]
    [DataRow("licenses", "licences")]
    [DataRow("Favorites", "Favourites")]
    [DataRow("Catalog", "Catalogue")]
    [DataRow("catalogs", "catalogues")]
    [DataRow("Synchronize", "Synchronise")]
    [DataRow("analyze", "analyse")]
    [DataRow("Unauthorized", "Unauthorised")]
    [DataRow("authorized", "authorised")]
    [DataRow("Defense", "Defence")]
    [DataRow("Customize", "Customise")]
    [DataRow("customized", "customised")]
    [DataRow("Recognized", "Recognised")]
    [DataRow("modeling", "modelling")]
    [DataRow("Canceled", "Cancelled")]
    public void ForDisplay_ConvertsUsSpellings(string us, string uk)
    {
        Assert.AreEqual(uk, BritishEnglish.ForDisplay(us));
    }

    [TestMethod]
    public void ForDisplay_ConvertsWithinSentences()
    {
        Assert.AreEqual(
            "Your organisation licence was cancelled.",
            BritishEnglish.ForDisplay("Your organization license was canceled."));
    }

    [TestMethod]
    public void ForDisplay_LeavesOtherTextAlone()
    {
        Assert.AreEqual("Part number 42 saved", BritishEnglish.ForDisplay("Part number 42 saved"));
    }

    [TestMethod]
    public void ForDisplay_AlreadyBritish_IsStable()
    {
        const string text = "Organisation catalogue licence";

        Assert.AreEqual(text, BritishEnglish.ForDisplay(text));
    }

    [TestMethod]
    [DataRow("catalogs")]
    [DataRow("Catalogs")]
    [DataRow("catalogue")]
    [DataRow("Favourites")]
    public void ForDisplay_IsIdempotent(string text)
    {
        var once = BritishEnglish.ForDisplay(text);

        Assert.AreEqual(once, BritishEnglish.ForDisplay(once));
    }

    [TestMethod]
    public void ForDisplay_NullOrEmpty_ReturnedAsIs()
    {
        Assert.AreEqual(string.Empty, BritishEnglish.ForDisplay(string.Empty));
        Assert.IsNull(BritishEnglish.ForDisplay(null!));
    }
}
