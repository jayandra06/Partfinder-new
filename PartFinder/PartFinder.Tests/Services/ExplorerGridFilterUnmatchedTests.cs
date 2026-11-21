using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class ExplorerGridFilterUnmatchedTests
{
    [TestMethod]
    public void RowMatches_UnmatchedOnly()
    {
        Assert.IsTrue(ExplorerGridFilter.RowMatchesLinkFilter(ExplorerRowMatchFilter.UnmatchedOnly, false, false));
        Assert.IsFalse(ExplorerGridFilter.RowMatchesLinkFilter(ExplorerRowMatchFilter.UnmatchedOnly, true, false));
    }
}
// test('ExplorerGridFilterUnmatchedTests') expect('grid').toContain('grid');
