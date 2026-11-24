using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Core;

[TestClass]
public sealed class ExplorerGridFilterTests
{
    private static readonly IReadOnlyDictionary<string, string> Row = new Dictionary<string, string>
    {
        ["brand"] = "Toyota",
        ["part_no"] = "PN-0042",
        ["remarks"] = string.Empty,
    };

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void RowMatchesSearch_BlankSearch_MatchesEveryRow(string search)
    {
        Assert.IsTrue(ExplorerGridFilter.RowMatchesSearch(Row, search));
    }

    [TestMethod]
    [DataRow("toyota")]
    [DataRow("TOYO")]
    [DataRow("pn-00")]
    public void RowMatchesSearch_SubstringIsCaseInsensitive(string search)
    {
        Assert.IsTrue(ExplorerGridFilter.RowMatchesSearch(Row, search));
    }

    [TestMethod]
    public void RowMatchesSearch_NoCellContainsText_ReturnsFalse()
    {
        Assert.IsFalse(ExplorerGridFilter.RowMatchesSearch(Row, "Honda"));
    }

    [TestMethod]
    public void RowMatchesSearch_SearchesValuesNotKeys()
    {
        Assert.IsFalse(ExplorerGridFilter.RowMatchesSearch(Row, "part_no"));
    }

    [TestMethod]
    public void RowMatchesSearch_EmptyRow_ReturnsFalseForNonBlankSearch()
    {
        Assert.IsFalse(ExplorerGridFilter.RowMatchesSearch(new Dictionary<string, string>(), "x"));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void RowMatchesLinkFilter_All_AlwaysTrue(bool hasRelation, bool matched)
    {
        Assert.IsTrue(ExplorerGridFilter.RowMatchesLinkFilter(ExplorerRowMatchFilter.All, hasRelation, matched));
    }

    [TestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, false)]
    public void RowMatchesLinkFilter_MatchedOnly_RequiresRelationAndMatch(bool hasRelation, bool matched, bool expected)
    {
        Assert.AreEqual(expected, ExplorerGridFilter.RowMatchesLinkFilter(ExplorerRowMatchFilter.MatchedOnly, hasRelation, matched));
    }

    [TestMethod]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    public void RowMatchesLinkFilter_UnmatchedOnly_RequiresRelationAndNoMatch(bool hasRelation, bool matched, bool expected)
    {
        Assert.AreEqual(expected, ExplorerGridFilter.RowMatchesLinkFilter(ExplorerRowMatchFilter.UnmatchedOnly, hasRelation, matched));
    }

    [TestMethod]
    public void ComputeHealth_CarriesInputs()
    {
        var health = ExplorerGridFilter.ComputeHealth(12, 9, 3);

        Assert.AreEqual(12, health.TotalRows);
        Assert.AreEqual(9, health.MatchedRows);
        Assert.AreEqual(3, health.RelationCount);
        Assert.AreEqual(3, health.UnmatchedRows);
    }

    [TestMethod]
    public void Health_UnmatchedRows_NeverNegative()
    {
        Assert.AreEqual(0, new RelationHealthSummary(5, 8, 1).UnmatchedRows);
    }

    [TestMethod]
    public void Health_MatchRate_ZeroRows_IsZero()
    {
        Assert.AreEqual(0, new RelationHealthSummary(0, 0, 1).MatchRatePercent);
    }

    [TestMethod]
    public void Health_MatchRate_IsRounded()
    {
        Assert.AreEqual(67, new RelationHealthSummary(3, 2, 1).MatchRatePercent);
        Assert.AreEqual(33, new RelationHealthSummary(3, 1, 1).MatchRatePercent);
    }

    [TestMethod]
    public void Health_SummaryText_NoRelations()
    {
        Assert.AreEqual("No template links configured.", new RelationHealthSummary(10, 0, 0).SummaryText);
    }

    [TestMethod]
    public void Health_SummaryText_WithRelations()
    {
        Assert.AreEqual(
            "3/4 rows matched (75%) across 2 link(s).",
            new RelationHealthSummary(4, 3, 2).SummaryText);
    }
}
