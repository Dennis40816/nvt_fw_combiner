using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolves postbuild branch and command data for legacy combiner transforms.</summary>
public static class LegacyCombinerPostbuildPlanner
{
    /// <summary>Creates a command plan from an IC profile and user-selected IC number context.</summary>
    public static LegacyCombinerPostbuildCommandPlan CreatePlan(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection? icNumberSelection)
    {
        ArgumentNullException.ThrowIfNull(profile);

        LegacyCombinerPostbuildBranch branch = ResolveBranch(icNumberSelection);
        IReadOnlyList<LegacyCombinerPostbuildCommand> commands = branch switch
        {
            LegacyCombinerPostbuildBranch.SingleChip => profile.SingleCommands,
            LegacyCombinerPostbuildBranch.TwoChip => profile.TwoChipCommands ?? profile.CascadeCommands,
            LegacyCombinerPostbuildBranch.ThreeChip => profile.ThreeChipCommands ?? profile.CascadeCommands,
            LegacyCombinerPostbuildBranch.Cascade => profile.CascadeCommands,
            _ => throw new ArgumentOutOfRangeException(nameof(icNumberSelection), "Unsupported postbuild branch."),
        };
        return new LegacyCombinerPostbuildCommandPlan(profile, branch, commands);
    }

    /// <summary>Returns staged file block arguments in deterministic order.</summary>
    public static IReadOnlyList<LegacyCombinerBlockArgument> GetStagedFileBlocks(
        LegacyCombinerPostbuildCommandPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return [
            .. plan.Commands
                .SelectMany(command => command.Blocks)
                .Where(block => block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile)
                .OrderBy(block => block.SourceFileName, StringComparer.Ordinal)
                .ThenBy(block => block.SourceOffset)
                .ThenBy(block => block.FirmwareRange.Start),
        ];
    }

    private static LegacyCombinerPostbuildBranch ResolveBranch(IcNumberSelection? selection)
    {
        if (selection is null)
        {
            return LegacyCombinerPostbuildBranch.SingleChip;
        }

        if (selection.Mode == IcNumberInputMode.SingleSelector)
        {
            return LegacyCombinerPostbuildBranch.SingleChip;
        }

        if (selection.Mode == IcNumberInputMode.NumericSelector)
        {
            return ResolveNumericBranch(selection.Parts[^1]);
        }

        string? lastPart = selection.Parts.Count == 0 ? null : selection.Parts[^1];
        return string.Equals(lastPart, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(lastPart, "single", StringComparison.OrdinalIgnoreCase)
                ? LegacyCombinerPostbuildBranch.SingleChip
                : LegacyCombinerPostbuildBranch.Cascade;
    }

    private static LegacyCombinerPostbuildBranch ResolveNumericBranch(string value)
    {
        return int.TryParse(value, out int icCount)
            ? icCount switch
            {
                1 => LegacyCombinerPostbuildBranch.SingleChip,
                2 => LegacyCombinerPostbuildBranch.TwoChip,
                3 => LegacyCombinerPostbuildBranch.ThreeChip,
                >= 4 => LegacyCombinerPostbuildBranch.Cascade,
                _ => throw new ArgumentException("Numeric IC number selection must be positive."),
            }
            : throw new ArgumentException("Numeric IC number selection must be an integer.");
    }
}
