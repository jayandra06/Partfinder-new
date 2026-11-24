namespace PartFinder.ViewModels;

public static class TablePagination
{
    public const int PageSize = 10;

    public static int TotalPagesFor(int itemCount) =>
        Math.Max(1, (int)Math.Ceiling(itemCount / (double)PageSize));

    public static IEnumerable<T> PageSlice<T>(IReadOnlyList<T> items, int currentPage)
    {
        if (items.Count == 0)
        {
            yield break;
        }

        var page = Math.Clamp(currentPage, 1, TotalPagesFor(items.Count));
        var start = (page - 1) * PageSize;
        var end = Math.Min(start + PageSize, items.Count);
        for (var i = start; i < end; i++)
        {
            yield return items[i];
        }
    }
}
