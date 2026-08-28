using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class ReportWindowedListViewModel : ObservableObject
{
    private readonly IReadOnlyList<object> _allItems;
    private readonly ResettableObjectCollection _items = [];
    private readonly int _pageSize;
    private readonly ShellLanguage _language;
    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;

    private ReportWindowedListViewModel(
        IReadOnlyList<object> allItems,
        int pageSize,
        ShellLanguage language,
        bool loadInitialPage)
    {
        ArgumentNullException.ThrowIfNull(allItems);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        _allItems = allItems;
        _pageSize = pageSize;
        _language = language;
        Items = new ReadOnlyObservableCollection<object>(_items);
        _previousPageCommand = new RelayCommand(ShowPreviousPage, () => HasPreviousPage);
        _nextPageCommand = new RelayCommand(ShowNextPage, () => HasNextPage);
        if (loadInitialPage && TotalCount > 0)
        {
            ShowPage(0);
        }
    }

    public ReadOnlyObservableCollection<object> Items { get; }

    public int TotalCount => _allItems.Count;

    public int VisibleCount => Items.Count;

    public int PageIndex { get; private set; }

    public int PageCount => TotalCount == 0 ? 0 : checked(((TotalCount - 1) / _pageSize) + 1);

    public bool HasPreviousPage => PageIndex > 0;

    public bool HasNextPage => VisibleCount == 0 ? PageCount > 0 : PageIndex + 1 < PageCount;

    public bool HasMultiplePages => PageCount > 1;

    public string PageStatus
    {
        get
        {
            if (VisibleCount == 0)
            {
                return _language == ShellLanguage.ChineseTraditional ? "沒有項目" : "No items";
            }

            int first = checked((PageIndex * _pageSize) + 1);
            int last = checked(first + VisibleCount - 1);
            return _language == ShellLanguage.ChineseTraditional
                ? $"顯示第 {first}-{last} 筆，共 {TotalCount} 筆"
                : $"Showing {first}-{last} of {TotalCount}";
        }
    }

    public string PreviousPageLabel => _language == ShellLanguage.ChineseTraditional ? "上一頁" : "Previous page";

    public string NextPageLabel => _language == ShellLanguage.ChineseTraditional ? "下一頁" : "Next page";

    public IRelayCommand PreviousPageCommand => _previousPageCommand;

    public IRelayCommand NextPageCommand => _nextPageCommand;

    internal static ReportWindowedListViewModel Create<T>(
        IReadOnlyList<T> items,
        int pageSize,
        ShellLanguage language,
        bool loadInitialPage = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new ReportWindowedListViewModel(
            new ObjectReadOnlyList<T>(items),
            pageSize,
            language,
            loadInitialPage);
    }

    internal void ShowItemAt(int index)
    {
        if ((uint)index >= (uint)TotalCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        int pageIndex = index / _pageSize;
        if (VisibleCount == 0 || PageIndex != pageIndex)
        {
            ShowPage(pageIndex);
        }
    }

    private void ShowPreviousPage()
    {
        if (HasPreviousPage)
        {
            ShowPage(PageIndex - 1);
        }
    }

    private void ShowNextPage()
    {
        if (HasNextPage)
        {
            ShowPage(VisibleCount == 0 ? 0 : PageIndex + 1);
        }
    }

    private void ShowPage(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        int start = checked(pageIndex * _pageSize);
        int endExclusive = Math.Min(checked(start + _pageSize), TotalCount);
        object[] rows = new object[endExclusive - start];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = _allItems[start + index];
        }

        PageIndex = pageIndex;
        _items.ReplaceAll(rows);
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(PageIndex));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PageStatus));
        _previousPageCommand.NotifyCanExecuteChanged();
        _nextPageCommand.NotifyCanExecuteChanged();
    }

    private sealed class ResettableObjectCollection : ObservableCollection<object>
    {
        internal void ReplaceAll(IEnumerable<object> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            CheckReentrancy();
            Items.Clear();
            foreach (object item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

}
