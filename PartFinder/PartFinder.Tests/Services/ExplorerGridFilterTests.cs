using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class ExplorerGridFilterTests
{
    [TestMethod]
    public void ComputeHealth_FormatsSummary()
    {
        var health = ExplorerGridFilter.ComputeHealth(totalRows: 10, matchedRows: 7, relationCount: 2);

        StringAssert.Contains(health.SummaryText, "7/10");
        StringAssert.Contains(health.SummaryText, "70%");
    }

    [TestMethod]
    public void RowMatchesLinkFilter_MatchedOnly()
    {
        Assert.IsTrue(
            ExplorerGridFilter.RowMatchesLinkFilter(ExplorerRowMatchFilter.MatchedOnly, true, true));
        Assert.IsFalse(
            ExplorerGridFilter.RowMatchesLinkFilter(ExplorerRowMatchFilter.MatchedOnly, true, false));
    }
}
