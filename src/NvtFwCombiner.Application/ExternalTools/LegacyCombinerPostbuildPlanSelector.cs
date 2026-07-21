using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Typed IC-count selector for one distinct legacy postbuild command plan.</summary>
public sealed class LegacyCombinerPostbuildPlanSelector
{
    /// <summary>Creates and validates one plan selector.</summary>
    public LegacyCombinerPostbuildPlanSelector(
        LegacyCombinerPostbuildPlanSelectorKind kind,
        LegacyCombinerPostbuildBranch branch,
        int? count = null,
        int? minimumCount = null,
        int? maximumCount = null)
    {
        ValidateShape(kind, branch, count, minimumCount, maximumCount);

        Kind = kind;
        Branch = branch;
        MinimumCount = kind switch
        {
            LegacyCombinerPostbuildPlanSelectorKind.SingleChip => 1,
            LegacyCombinerPostbuildPlanSelectorKind.GenericCascade => 2,
            LegacyCombinerPostbuildPlanSelectorKind.ExactCount => count!.Value,
            LegacyCombinerPostbuildPlanSelectorKind.CountRange => minimumCount!.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        MaximumCount = kind switch
        {
            LegacyCombinerPostbuildPlanSelectorKind.SingleChip => 1,
            LegacyCombinerPostbuildPlanSelectorKind.GenericCascade => int.MaxValue,
            LegacyCombinerPostbuildPlanSelectorKind.ExactCount => count!.Value,
            LegacyCombinerPostbuildPlanSelectorKind.CountRange => maximumCount!.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Token = kind switch
        {
            LegacyCombinerPostbuildPlanSelectorKind.SingleChip => IcNumberSelectionTokens.SingleChip,
            LegacyCombinerPostbuildPlanSelectorKind.GenericCascade => IcNumberSelectionTokens.Cascade,
            LegacyCombinerPostbuildPlanSelectorKind.ExactCount =>
                MinimumCount.ToString(CultureInfo.InvariantCulture),
            LegacyCombinerPostbuildPlanSelectorKind.CountRange =>
                $"cascade_{MinimumCount.ToString(CultureInfo.InvariantCulture)}to{MaximumCount.ToString(CultureInfo.InvariantCulture)}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        DisplayLabel = kind switch
        {
            LegacyCombinerPostbuildPlanSelectorKind.SingleChip => "1 IC",
            LegacyCombinerPostbuildPlanSelectorKind.GenericCascade => "Cascade",
            LegacyCombinerPostbuildPlanSelectorKind.ExactCount => $"{MinimumCount.ToString(CultureInfo.InvariantCulture)} IC",
            LegacyCombinerPostbuildPlanSelectorKind.CountRange =>
                $"{MinimumCount.ToString(CultureInfo.InvariantCulture)}–{MaximumCount.ToString(CultureInfo.InvariantCulture)} IC",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>Selector shape declared by owner-provided command plans.</summary>
    public LegacyCombinerPostbuildPlanSelectorKind Kind { get; }

    /// <summary>Command branch selected by this count shape.</summary>
    public LegacyCombinerPostbuildBranch Branch { get; }

    /// <summary>Inclusive minimum chip count.</summary>
    public int MinimumCount { get; }

    /// <summary>Inclusive maximum chip count.</summary>
    public int MaximumCount { get; }

    /// <summary>Stable UI/CLI token for this selector.</summary>
    public string Token { get; }

    /// <summary>Concise display label projected to UI adapters.</summary>
    public string DisplayLabel { get; }

    /// <summary>Returns whether a user selection resolves to this plan.</summary>
    public bool Matches(IcNumberSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Parts.Count == 0)
        {
            return false;
        }

        string token = selection.Parts[^1].Trim();
        return string.Equals(token, Token, StringComparison.OrdinalIgnoreCase) ||
            (TryParsePositiveCount(token, out int count) &&
             count >= MinimumCount &&
             count <= MaximumCount);
    }

    /// <summary>Returns whether FWConfig's reported chip count is compatible with this selector.</summary>
    public bool MatchesReportedChipCount(int count)
    {
        return Kind == LegacyCombinerPostbuildPlanSelectorKind.SingleChip
            ? count is 0 or 1
            : count >= MinimumCount && count <= MaximumCount;
    }

    private static void ValidateShape(
        LegacyCombinerPostbuildPlanSelectorKind kind,
        LegacyCombinerPostbuildBranch branch,
        int? count,
        int? minimumCount,
        int? maximumCount)
    {
        bool valid = kind switch
        {
            LegacyCombinerPostbuildPlanSelectorKind.SingleChip =>
                branch == LegacyCombinerPostbuildBranch.SingleChip &&
                count is null && minimumCount is null && maximumCount is null,
            LegacyCombinerPostbuildPlanSelectorKind.GenericCascade =>
                branch == LegacyCombinerPostbuildBranch.Cascade &&
                count is null && minimumCount is null && maximumCount is null,
            LegacyCombinerPostbuildPlanSelectorKind.ExactCount =>
                (count is 2 && branch == LegacyCombinerPostbuildBranch.TwoChip &&
                minimumCount is null && maximumCount is null) ||
                (count is 3 && branch == LegacyCombinerPostbuildBranch.ThreeChip &&
                minimumCount is null && maximumCount is null),
            LegacyCombinerPostbuildPlanSelectorKind.CountRange =>
                branch == LegacyCombinerPostbuildBranch.Cascade &&
                count is null && minimumCount >= 2 && maximumCount > minimumCount,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("Postbuild plan selector kind, branch, and count bounds are inconsistent.");
        }
    }

    private static bool TryParsePositiveCount(string value, out int count)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count > 0;
    }
}

/// <summary>Supported plan-selection dimensions.</summary>
public enum LegacyCombinerPostbuildPlanSelectorKind
{
    /// <summary>Single-chip plan.</summary>
    SingleChip,

    /// <summary>One generic plan for every supported multi-chip count.</summary>
    GenericCascade,

    /// <summary>One distinct plan for an exact supported count.</summary>
    ExactCount,

    /// <summary>One distinct plan for an inclusive supported count range.</summary>
    CountRange,
}
