using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Bounded report-detail window that materializes more UI rows only when requested.</summary>
public sealed class ReportPagedListViewModel : ObservableObject
{
    private readonly IReadOnlyList<object> _allItems;
    private readonly ObservableCollection<object> _items = [];
    private readonly int _pageSize;
    private readonly ShellLanguage _language;
    private readonly RelayCommand _loadMoreCommand;

    private ReportPagedListViewModel(
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
        _loadMoreCommand = new RelayCommand(LoadMore, () => HasMoreItems);
        if (loadInitialPage)
        {
            LoadMore();
        }
    }

    /// <summary>Rows currently materialized for Avalonia controls.</summary>
    public ReadOnlyObservableCollection<object> Items { get; }

    /// <summary>Total rows retained by the complete report projection.</summary>
    public int TotalCount => _allItems.Count;

    /// <summary>Rows currently visible to controls.</summary>
    public int VisibleCount => Items.Count;

    /// <summary>Rows still hidden behind the explicit paging action.</summary>
    public int RemainingCount => TotalCount - VisibleCount;

    /// <summary>True when another bounded row page is available.</summary>
    public bool HasMoreItems => RemainingCount > 0;

    /// <summary>Localized bounded-page status for sighted and assistive review.</summary>
    public string PageStatus => _language == ShellLanguage.ChineseTraditional
        ? $"已顯示 {VisibleCount}/{TotalCount} 筆"
        : $"Showing {VisibleCount}/{TotalCount}";

    /// <summary>Localized label for the next bounded page.</summary>
    public string LoadMoreLabel
    {
        get
        {
            if (!HasMoreItems)
            {
                return _language == ShellLanguage.ChineseTraditional
                    ? "已載入全部項目"
                    : "All items loaded";
            }

            int nextCount = Math.Min(_pageSize, RemainingCount);
            return _language == ShellLanguage.ChineseTraditional
                ? $"再載入 {nextCount} 筆（尚餘 {RemainingCount} 筆）"
                : $"Load {nextCount} more ({RemainingCount} remaining)";
        }
    }

    /// <summary>Loads one more bounded row page.</summary>
    public IRelayCommand LoadMoreCommand => _loadMoreCommand;

    internal static ReportPagedListViewModel Create<T>(
        IReadOnlyList<T> items,
        int pageSize,
        ShellLanguage language,
        bool loadInitialPage = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new ReportPagedListViewModel(
            new ObjectReadOnlyList<T>(items),
            pageSize,
            language,
            loadInitialPage);
    }

    internal void EnsureInitialPage()
    {
        if (VisibleCount == 0 && TotalCount > 0)
        {
            LoadMore();
        }
    }

    private void LoadMore()
    {
        int endExclusive = Math.Min(checked(VisibleCount + _pageSize), TotalCount);
        for (int index = VisibleCount; index < endExclusive; index++)
        {
            _items.Add(_allItems[index]);
        }

        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(RemainingCount));
        OnPropertyChanged(nameof(HasMoreItems));
        OnPropertyChanged(nameof(PageStatus));
        OnPropertyChanged(nameof(LoadMoreLabel));
        _loadMoreCommand.NotifyCanExecuteChanged();
    }

    private sealed class ObjectReadOnlyList<T>(IReadOnlyList<T> items) : IReadOnlyList<object>
    {
        private readonly IReadOnlyList<T> _items = items ?? throw new ArgumentNullException(nameof(items));

        public int Count => _items.Count;

        public object this[int index] => _items[index]!;

        public IEnumerator<object> GetEnumerator()
        {
            for (int index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
