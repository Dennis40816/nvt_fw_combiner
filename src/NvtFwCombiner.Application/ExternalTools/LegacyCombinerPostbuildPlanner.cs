using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolves postbuild branch and command data for legacy combiner transforms.</summary>
public static partial class LegacyCombinerPostbuildPlanner
{
    /// <summary>Creates a command plan from an IC profile and user-selected IC number context.</summary>
    public static LegacyCombinerPostbuildCommandPlan CreatePlan(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection? icNumberSelection)
    {
        ArgumentNullException.ThrowIfNull(profile);

        LegacyCombinerPostbuildPlanSelector selector = ResolveSelector(profile, icNumberSelection);
        return CreatePlan(profile, selector);
    }

    /// <summary>Creates a command plan from one exact selector owned by the supplied profile.</summary>
    public static LegacyCombinerPostbuildCommandPlan CreatePlan(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildPlanSelector selector)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(selector);
        if (!profile.PlanSelectors.Contains(selector))
        {
            throw new ArgumentException(
                "The selected postbuild plan selector must belong to the supplied profile.",
                nameof(selector));
        }

        LegacyCombinerPostbuildBranch branch = selector.Branch;
        IReadOnlyList<LegacyCombinerPostbuildCommand> commands = branch switch
        {
            LegacyCombinerPostbuildBranch.SingleChip => profile.SingleCommands,
            LegacyCombinerPostbuildBranch.TwoChip => profile.TwoChipCommands!,
            LegacyCombinerPostbuildBranch.ThreeChip => profile.ThreeChipCommands!,
            LegacyCombinerPostbuildBranch.Cascade => profile.CascadeCommands,
            _ => throw new ArgumentOutOfRangeException(nameof(selector), "Unsupported postbuild branch."),
        };
        return new LegacyCombinerPostbuildCommandPlan(profile, selector, commands);
    }

    /// <summary>Returns staged BIN block arguments in deterministic order.</summary>
    public static IReadOnlyList<LegacyCombinerBlockArgument> GetStagedFileBlocks(
        LegacyCombinerPostbuildCommandPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return [
            .. plan.Commands
                .SelectMany(command => command.Blocks)
                .Where(block => block.SourceKind is LegacyCombinerBlockSourceKind.StagedFile or
                    LegacyCombinerBlockSourceKind.StagedArtifact)
                .OrderBy(block => block.SourceFileName, StringComparer.Ordinal)
                .ThenBy(block => block.SourceOffset)
                .ThenBy(block => block.FirmwareRange.Start),
        ];
    }

    /// <summary>Calculates the minimum firmware image capacity needed by a postbuild plan.</summary>
    public static long CalculateRequiredCapacity(
        LegacyCombinerPostbuildCommandPlan plan,
        IEnumerable<ByteRange> requiredTargetRanges)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(requiredTargetRanges);

        long requiredCapacity = 1;
        foreach (ByteRange range in requiredTargetRanges)
        {
            requiredCapacity = Math.Max(requiredCapacity, range.EndExclusive);
        }

        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                requiredCapacity = Math.Max(requiredCapacity, block.FirmwareRange.EndExclusive);
                if (block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage)
                {
                    requiredCapacity = Math.Max(
                        requiredCapacity,
                        checked(block.SourceOffset + block.FirmwareRange.Length));
                }
            }
        }

        return requiredCapacity;
    }

    private static LegacyCombinerPostbuildPlanSelector ResolveSelector(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection? selection)
    {
        if (selection is null)
        {
            return profile.PlanSelectors.Single(static selector =>
                selector.Kind == LegacyCombinerPostbuildPlanSelectorKind.SingleChip);
        }

        LegacyCombinerPostbuildPlanSelector[] matches =
        [
            .. profile.PlanSelectors.Where(selector => selector.Matches(selection)),
        ];
        return matches.Length == 1
            ? matches[0]
            : throw new ArgumentException(
                $"IC number selection '{(selection.Parts.Count == 0 ? "<empty>" : selection.Parts[^1])}' is not supported by postbuild profile '{profile.ProcessorId}'.");
    }
}
