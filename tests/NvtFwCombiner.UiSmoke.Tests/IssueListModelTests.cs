// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Issue list model tests.</summary>
public sealed class IssueListModelTests
{
    /// <summary>Snapshot owns every collection and keeps host order.</summary>
    [Fact]
    public void SnapshotOwnsEveryCollectionAndKeepsHostOrder()
    {
        var chips = new List<string> { "opaque host location" };
        var details = new List<string> { "opaque host detail" };
        var first = new IssueListItem("warning", IssueListSeverity.Warning, "Host sentence.", chips, 2, details);
        IssueListItem second = IssueListTestHost.Item("error", IssueListSeverity.Error);
        var items = new List<IssueListItem> { first, second };
        var model = new IssueListModel(items);

        chips.Clear();
        details.Clear();
        items.Clear();

        Assert.Equal([first, second], model.Issues);
        Assert.Equal(["opaque host location"], first.LocationChips);
        Assert.Equal(["opaque host detail"], first.DetailLines);
        Assert.True(Assert.IsType<IList<IssueListItem>>(model.Issues, exactMatch: false).IsReadOnly);
        Assert.True(Assert.IsType<IList<string>>(first.LocationChips, exactMatch: false).IsReadOnly);
        Assert.True(Assert.IsType<IList<string>>(first.DetailLines, exactMatch: false).IsReadOnly);
    }

    /// <summary>Counts sum occurrences without int overflow.</summary>
    [Fact]
    public void CountsSumOccurrencesWithoutIntOverflow()
    {
        var model = new IssueListModel(
        [
            IssueListTestHost.Item("error-a", IssueListSeverity.Error, int.MaxValue),
            IssueListTestHost.Item("warning-a", count: 3),
            IssueListTestHost.Item("error-b", IssueListSeverity.Error, 2),
        ]);

        Assert.Equal((long)int.MaxValue + 2, model.ErrorCount);
        Assert.Equal(3, model.WarningCount);
    }

    /// <summary>Null issue collection is rejected.</summary>
    [Fact]
    public void NullIssueCollectionIsRejected()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new IssueListModel(null!));
    }

    /// <summary>Null issue is rejected.</summary>
    [Fact]
    public void NullIssueIsRejected()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new IssueListModel([null!]));
    }

    /// <summary>Duplicate keys are rejected without merging.</summary>
    [Fact]
    public void DuplicateKeysAreRejectedWithoutMerging()
    {
        _ = Assert.Throws<ArgumentException>(() => new IssueListModel(
        [
            IssueListTestHost.Item("same", IssueListSeverity.Error),
            IssueListTestHost.Item("same"),
        ]));
    }

    /// <summary>Missing key is rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingKeyIsRejected(string? key)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => IssueListTestHost.Item(key!));
    }

    /// <summary>Missing sentence is rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingSentenceIsRejected(string? sentence)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => IssueListTestHost.Item("key", sentence: sentence!));
    }

    /// <summary>Occurrence count below one is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OccurrenceCountBelowOneIsRejected(int count)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => IssueListTestHost.Item("key", count: count));
    }

    /// <summary>Unknown severity is rejected.</summary>
    [Fact]
    public void UnknownSeverityIsRejected()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => IssueListTestHost.Item("key", (IssueListSeverity)42));
    }

    /// <summary>Null chips and details are rejected.</summary>
    [Fact]
    public void NullChipsAndDetailsAreRejected()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new IssueListItem("key", IssueListSeverity.Error, "Sentence.", null!, 1, []));
        _ = Assert.Throws<ArgumentNullException>(() => new IssueListItem("key", IssueListSeverity.Error, "Sentence.", [], 1, null!));
    }

    /// <summary>Null chip and detail elements are rejected.</summary>
    [Fact]
    public void NullChipAndDetailElementsAreRejected()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new IssueListItem("key", IssueListSeverity.Warning, "Sentence.", [null!], 1, []));
        _ = Assert.Throws<ArgumentNullException>(() => new IssueListItem("key", IssueListSeverity.Warning, "Sentence.", [], 1, [null!]));
    }
}
