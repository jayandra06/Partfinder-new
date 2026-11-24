using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class ExplorerNavigationCoordinatorTests
{
    [TestMethod]
    public void RequestOpenTemplate_RaisesEventWithId()
    {
        var coordinator = new ExplorerNavigationCoordinator();
        var received = new List<string>();
        coordinator.OpenTemplateRequested += received.Add;

        coordinator.RequestOpenTemplate("template-7");

        CollectionAssert.AreEqual(new[] { "template-7" }, received);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void RequestOpenTemplate_BlankId_DoesNotRaise(string id)
    {
        var coordinator = new ExplorerNavigationCoordinator();
        var raised = false;
        coordinator.OpenTemplateRequested += _ => raised = true;

        coordinator.RequestOpenTemplate(id);

        Assert.IsFalse(raised);
    }

    [TestMethod]
    public void RequestOpenTemplate_WithoutSubscribers_DoesNotThrow()
    {
        new ExplorerNavigationCoordinator().RequestOpenTemplate("x");
    }

    [TestMethod]
    public void PublishHealth_StoresLatestSnapshot()
    {
        var coordinator = new ExplorerNavigationCoordinator();
        Assert.IsNull(coordinator.LastHealth);

        var first = new RelationHealthSnapshot("p", "Parts", 10, 5, 1);
        var second = new RelationHealthSnapshot("p", "Parts", 10, 8, 1);
        coordinator.PublishHealth(first);
        coordinator.PublishHealth(second);

        Assert.AreSame(second, coordinator.LastHealth);
    }

    [TestMethod]
    [DataRow(0, 0, 0)]
    [DataRow(10, 5, 50)]
    [DataRow(3, 2, 67)]
    public void Snapshot_MatchRatePercent(int total, int matched, int expected)
    {
        Assert.AreEqual(expected, new RelationHealthSnapshot("p", "P", total, matched, 1).MatchRatePercent);
    }
}
