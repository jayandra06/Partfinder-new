using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class MockPartsDataServiceTests
{
    private readonly MockPartsDataService _service = new();

    [TestMethod]
    public async Task FirstPage_HasDeterministicRows()
    {
        var (records, hasMore) = await _service.GetPageAsync("tpl", 0, 3);

        Assert.IsTrue(hasMore);
        CollectionAssert.AreEqual(new[] { "tpl-0", "tpl-1", "tpl-2" }, records.Select(r => r.Id).ToArray());
        Assert.AreEqual("PN-000000", records[0].Values["part_no"]);
        Assert.AreEqual("QC required", records[0].Values["remarks"]);
        Assert.AreEqual(string.Empty, records[1].Values["remarks"]);
        Assert.AreEqual(20.35m, records[1].Values["price"]);
        Assert.AreEqual("KF-1002", records[2].Values["KF_number"]);
    }

    [TestMethod]
    public async Task LastPage_IsTruncatedAndHasNoMore()
    {
        var (records, hasMore) = await _service.GetPageAsync("tpl", 99_998, 5);

        Assert.IsFalse(hasMore);
        CollectionAssert.AreEqual(new[] { "tpl-99998", "tpl-99999" }, records.Select(r => r.Id).ToArray());
    }

    [TestMethod]
    public async Task CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => _service.GetPageAsync("tpl", 0, 10, cts.Token));
    }
}
