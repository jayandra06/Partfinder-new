using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests;

[TestClass]
public sealed class WmsWorkbookFilterTests
{
    [TestMethod]
    public void FilterBySearch_is_case_insensitive()
    {
        var rows = new[]
        {
            new WmsSkuDto { SkuId = "ABC", DisplayName = "Widget" },
            new WmsSkuDto { SkuId = "XYZ", DisplayName = "Gadget" },
        };

        var filtered = WmsWorkbookViewModelFilterShim.FilterBySearch(
            rows,
            "widget",
            x => $"{x.SkuId} {x.DisplayName}").ToList();

        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("ABC", filtered[0].SkuId);
    }
}

internal static class WmsWorkbookViewModelFilterShim
{
    public static IEnumerable<T> FilterBySearch<T>(
        IEnumerable<T> source,
        string search,
        Func<T, string> text) =>
        string.IsNullOrWhiteSpace(search.Trim())
            ? source
            : source.Where(x => text(x).Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
}