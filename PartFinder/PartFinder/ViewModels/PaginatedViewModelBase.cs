using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public abstract partial class PaginatedViewModelBase : ViewModelBase
{
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _filteredItemCount;

    public string PaginationSummary => FilteredItemCount == 0
        ? "No rows to display"
        : $"Page {CurrentPage} of {TotalPages} ({FilteredItemCount} rows)";

    public bool CanGoToPreviousPage => CurrentPage > 1;

    public bool CanGoToNextPage => CurrentPage < TotalPages;

    protected void ResetPaginationPage() => CurrentPage = 1;

    protected void CommitFilterPage<T>(IReadOnlyList<T> filtered, ObservableCollection<T> target)
    {
        FilteredItemCount = filtered.Count;
        TotalPages = TablePagination.TotalPagesFor(filtered.Count);
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        if (CurrentPage < 1)
        {
            CurrentPage = 1;
        }

        RefreshPage(target, filtered);
        NotifyPaginationState();
    }

    protected void RefreshPage<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in TablePagination.PageSlice(source, CurrentPage))
        {
            target.Add(item);
        }
    }

    protected abstract void OnPaginationRefreshRequested();

    partial void OnCurrentPageChanged(int value)
    {
        OnPaginationRefreshRequested();
        NotifyPaginationState();
    }

    partial void OnTotalPagesChanged(int value) => NotifyPaginationState();

    partial void OnFilteredItemCountChanged(int value) => OnPropertyChanged(nameof(PaginationSummary));

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void GoToPreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void GoToNextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
        }
    }

    private void NotifyPaginationState()
    {
        OnPropertyChanged(nameof(PaginationSummary));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
        GoToPreviousPageCommand.NotifyCanExecuteChanged();
        GoToNextPageCommand.NotifyCanExecuteChanged();
    }
}
