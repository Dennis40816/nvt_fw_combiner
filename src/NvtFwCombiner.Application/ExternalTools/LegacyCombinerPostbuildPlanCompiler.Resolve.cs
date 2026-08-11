using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolves postbuild branch and command data for legacy combiner transforms.</summary>
public static partial class LegacyCombinerPostbuildPlanCompiler
{
    internal static IReadOnlyList<LegacyCombinerPostbuildCommand> ResolveCommands(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildPlanSelector selector,
        int topologyCount)
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
        if (!selector.MatchesReportedChipCount(topologyCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(topologyCount),
                topologyCount,
                "Topology count is outside the selected postbuild plan.");
        }

        LegacyCombinerDiffDlmPolicy? policy = branch == LegacyCombinerPostbuildBranch.Cascade
            ? profile.DiffDlmPolicy
            : null;
        IReadOnlyList<LegacyCombinerPostbuildCommand> resolvedCommands = policy is null
            ? commands
            : ExpandDiffDlmPolicy(commands, policy, topologyCount);
        return resolvedCommands;
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

    private static System.Collections.ObjectModel.ReadOnlyCollection<LegacyCombinerPostbuildCommand> ExpandDiffDlmPolicy(
        IReadOnlyList<LegacyCombinerPostbuildCommand> commands,
        LegacyCombinerDiffDlmPolicy policy,
        int topologyCount)
    {
        int templateCount = commands
            .SelectMany(static command => command.Blocks)
            .Count(policy.IsTemplateBlock);
        return templateCount != 1
            ? throw new ArgumentException(
                $"DiffDLM policy '{policy.PolicyId}' requires exactly one maximum-envelope template block.")
            : Array.AsReadOnly(
            [
                .. commands.Select(command =>
                {
                    var blocks = new List<LegacyCombinerBlockArgument>();
                    foreach (LegacyCombinerBlockArgument block in command.Blocks)
                    {
                        if (policy.IsTemplateBlock(block))
                        {
                            blocks.AddRange(policy.Expand(block, topologyCount));
                        }
                        else
                        {
                            blocks.Add(block);
                        }
                    }

                    return new LegacyCombinerPostbuildCommand(
                        command.CommandId,
                        command.Family,
                        command.ModeArgument,
                        command.CrcArgument,
                        blocks);
                }),
            ]);
    }
}
