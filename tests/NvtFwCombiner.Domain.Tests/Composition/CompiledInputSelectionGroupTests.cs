using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Verifies immutable compiled input-selection invariants.</summary>
public sealed class CompiledInputSelectionGroupTests
{
    /// <summary>Compiled groups snapshot, sort, and expose resolved state.</summary>
    [Fact]
    public void SnapshotsSortedSelectionStateAndNotApplicableReasons()
    {
        string[] members = ["ldc-input", "initial-code-input"];
        string[] applicable = ["initial-code-input"];
        string[] selected = ["initial-code-input"];
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ldc-input"] = "Reference length does not include LDC",
        };

        CompiledInputSelectionGroup group = CreateGroup(
            "dp-replacement-selection",
            members,
            applicable,
            selected,
            minimumSelected: 1,
            maximumSelected: 1,
            reasons);

        members[0] = "mutated";
        applicable[0] = "mutated";
        selected[0] = "mutated";
        reasons["ldc-input"] = "mutated";

        Assert.Equal(
            ["initial-code-input", "ldc-input"],
            group.MemberSlotIds);
        Assert.Equal(["initial-code-input"], group.ApplicableMemberSlotIds);
        Assert.Equal(["initial-code-input"], group.SelectedSlotIds);
        Assert.Equal(
            "Reference length does not include LDC",
            group.NotApplicableReasons["ldc-input"]);
        Assert.Equal(1, group.MinimumSelected);
        Assert.Equal(1, group.MaximumSelected);
    }

    /// <summary>A compiled group requires a stable non-empty identity.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectsMissingGroupId(string groupId)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                groupId,
                ["slot"],
                ["slot"],
                ["slot"],
                minimumSelected: 1,
                maximumSelected: 1));
    }

    /// <summary>Null and noncanonical identities retain their exact failure contracts.</summary>
    [Fact]
    public void RejectsInvalidGroupIdsWithExactExceptionTypes()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            CreateGroup(null!, ["slot"], ["slot"], ["slot"], 1, 1));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup("Group", ["slot"], ["slot"], ["slot"], 1, 1));
    }

    /// <summary>Canonical members must exist and remain ordinally unique.</summary>
    [Fact]
    public void RejectsMissingEmptyOrDuplicateMemberIds()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            CreateGroup(
                "group",
                null!,
                [],
                [],
                minimumSelected: 0,
                maximumSelected: 0));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                [],
                [],
                [],
                minimumSelected: 0,
                maximumSelected: 0));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot", "slot"],
                ["slot"],
                ["slot"],
                minimumSelected: 1,
                maximumSelected: 1));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot", " "],
                ["slot"],
                ["slot"],
                minimumSelected: 1,
                maximumSelected: 1));
    }

    /// <summary>Applicability and selection remain subsets of declared members.</summary>
    [Fact]
    public void RejectsInvalidApplicabilityAndSelectionSubsets()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot"],
                ["other"],
                [],
                minimumSelected: 0,
                maximumSelected: 1));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot", "other"],
                ["slot"],
                ["other"],
                minimumSelected: 0,
                maximumSelected: 1));
    }

    /// <summary>Selected-count bounds match the applicable and selected sets.</summary>
    [Fact]
    public void RejectsInvalidSelectedCountBounds()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot"],
                ["slot"],
                [],
                minimumSelected: -1,
                maximumSelected: 0));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot"],
                ["slot"],
                [],
                minimumSelected: 1,
                maximumSelected: 0));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot"],
                ["slot"],
                [],
                minimumSelected: 0,
                maximumSelected: 2));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot"],
                ["slot"],
                [],
                minimumSelected: 1,
                maximumSelected: 1));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot", "other"],
                ["slot", "other"],
                ["slot", "other"],
                minimumSelected: 0,
                maximumSelected: 1));
    }

    /// <summary>Reasons name only declared members unavailable in the resolved map.</summary>
    [Theory]
    [InlineData("", "reason")]
    [InlineData("slot", "")]
    [InlineData("other", "reason")]
    [InlineData("slot", "reason")]
    public void RejectsInvalidNotApplicableReasons(
        string reasonSlotId,
        string reason)
    {
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [reasonSlotId] = reason,
        };

        _ = Assert.Throws<ArgumentException>(() =>
            CreateGroup(
                "group",
                ["slot", "ldc"],
                ["slot"],
                ["slot"],
                minimumSelected: 1,
                maximumSelected: 1,
                reasons));
    }

    private static CompiledInputSelectionGroup CreateGroup(
        string groupId,
        IEnumerable<string> memberSlotIds,
        IEnumerable<string> applicableMemberSlotIds,
        IEnumerable<string> selectedSlotIds,
        int minimumSelected,
        int maximumSelected,
        IReadOnlyDictionary<string, string>? notApplicableReasons = null)
    {
        return new CompiledInputSelectionGroup(
            new InputSelectionGroupDefinition(
                groupId,
                memberSlotIds,
                minimumSelected,
                maximumSelected),
            applicableMemberSlotIds,
            selectedSlotIds,
            maximumSelected,
            notApplicableReasons);
    }
}
