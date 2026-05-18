using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class WorksheetRelationUiTextTests
{
    [TestMethod]
    public void GetSharedColumnNames_ReturnsIntersection()
    {
        var primary = new[] { "Brand", "Model", "Part No" };
        var lookup = new[] { "brand", "Vendor", "Part No" };

        var shared = WorksheetRelationUiText.GetSharedColumnNames(primary, lookup);

        CollectionAssert.AreEquivalent(new[] { "Brand", "Part No" }, shared.ToArray());
    }

    [TestMethod]
    public void GetMatchColumnHint_MultipleColumns_ListsAll()
    {
        var hint = WorksheetRelationUiText.GetMatchColumnHint(["Brand", "Part No"]);

        StringAssert.Contains(hint, "Brand");
        StringAssert.Contains(hint, "Part No");
        StringAssert.Contains(hint, "all of these");
    }

    [TestMethod]
    public void GetMatchColumnHint_WhenEmpty_DescribesRequirement()
    {
        var hint = WorksheetRelationUiText.GetMatchColumnHint([]);

        StringAssert.Contains(hint, "one or more");
    }
}
