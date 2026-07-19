using System.Collections.Specialized;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regression coverage for the fixed-size report navigator window.</summary>
public sealed class ReportWindowedListViewModelTests
{
    /// <summary>Page navigation replaces retained rows and never grows beyond the declared window.</summary>
    [Fact]
    public void NavigationReplacesTheCurrentFixedSizeWindow()
    {
        int[] source = [.. Enumerable.Range(0, 130)];
        var navigator = ReportWindowedListViewModel.Create(
            source,
            pageSize: 64,
            ShellLanguage.English);
        var collectionChanges = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)navigator.Items).CollectionChanged +=
            (_, args) => collectionChanges.Add(args.Action);

        Assert.Equal(0, navigator.PageIndex);
        Assert.Equal(3, navigator.PageCount);
        Assert.True(navigator.HasMultiplePages);
        Assert.Equal(64, navigator.VisibleCount);
        Assert.Equal(0, navigator.Items[0]);
        Assert.Equal("Showing 1-64 of 130", navigator.PageStatus);
        Assert.False(navigator.PreviousPageCommand.CanExecute(null));
        Assert.True(navigator.NextPageCommand.CanExecute(null));

        navigator.NextPageCommand.Execute(null);

        Assert.Equal(1, navigator.PageIndex);
        Assert.Equal(64, navigator.VisibleCount);
        Assert.Equal(64, navigator.Items[0]);
        Assert.Equal("Showing 65-128 of 130", navigator.PageStatus);
        Assert.Equal([NotifyCollectionChangedAction.Reset], collectionChanges);

        collectionChanges.Clear();
        navigator.NextPageCommand.Execute(null);

        Assert.Equal(2, navigator.PageIndex);
        Assert.Equal(2, navigator.VisibleCount);
        Assert.Equal(128, navigator.Items[0]);
        Assert.Equal("Showing 129-130 of 130", navigator.PageStatus);
        Assert.Equal([NotifyCollectionChangedAction.Reset], collectionChanges);
        Assert.True(navigator.PreviousPageCommand.CanExecute(null));
        Assert.False(navigator.NextPageCommand.CanExecute(null));

        navigator.PreviousPageCommand.Execute(null);

        Assert.Equal(1, navigator.PageIndex);
        Assert.Equal(64, navigator.VisibleCount);
        Assert.Equal(64, navigator.Items[0]);
    }

    /// <summary>Direct item navigation jumps to the containing window without retaining preceding pages.</summary>
    [Fact]
    public void DirectItemNavigationShowsOnlyTheContainingWindow()
    {
        int projectedRowCount = 0;
        var source = new FactoryReadOnlyList<int>(
            10_000,
            index =>
            {
                projectedRowCount++;
                return index;
            });
        var navigator = ReportWindowedListViewModel.Create(
            source,
            pageSize: 64,
            ShellLanguage.English);

        Assert.Equal(64, projectedRowCount);

        navigator.ShowItemAt(9_999);

        Assert.Equal(156, navigator.PageIndex);
        Assert.Equal(16, navigator.VisibleCount);
        Assert.Equal(9_984, navigator.Items[0]);
        Assert.Equal(9_999, navigator.Items[^1]);
        Assert.Equal("Showing 9985-10000 of 10000", navigator.PageStatus);
        Assert.Equal(80, projectedRowCount);

        navigator.ShowItemAt(9_998);

        Assert.Equal(80, projectedRowCount);
    }

    /// <summary>Window controls expose bilingual labels without requiring shell-owned translations.</summary>
    [Fact]
    public void NavigationLabelsFollowTheSelectedShellLanguage()
    {
        var navigator = ReportWindowedListViewModel.Create(
            ["row"],
            pageSize: 64,
            ShellLanguage.ChineseTraditional,
            loadInitialPage: false);

        Assert.Equal("上一頁", navigator.PreviousPageLabel);
        Assert.Equal("下一頁", navigator.NextPageLabel);
        Assert.Equal("沒有項目", navigator.PageStatus);
        Assert.Equal(0, navigator.VisibleCount);
        Assert.False(navigator.HasMultiplePages);
        Assert.True(navigator.NextPageCommand.CanExecute(null));

        navigator.NextPageCommand.Execute(null);

        Assert.Equal("顯示第 1-1 筆，共 1 筆", navigator.PageStatus);
        Assert.Equal(1, navigator.VisibleCount);
        Assert.False(navigator.NextPageCommand.CanExecute(null));
    }
}
