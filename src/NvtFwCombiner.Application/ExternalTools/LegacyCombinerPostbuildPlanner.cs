using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolves postbuild branch and command data for legacy combiner transforms.</summary>
public static class LegacyCombinerPostbuildPlanner
{
    /// <summary>External processor parameter key carrying the approved postbuild command ids for one run.</summary>
    public const string CommandIdsParameterName = "legacy-combiner.command-ids";

    /// <summary>Creates a command plan from an IC profile and user-selected IC number context.</summary>
    public static LegacyCombinerPostbuildCommandPlan CreatePlan(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection? icNumberSelection)
    {
        ArgumentNullException.ThrowIfNull(profile);

        LegacyCombinerPostbuildBranch branch = ResolveBranch(profile, icNumberSelection);
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

    /// <summary>Filters a resolved command plan to approved command ids while preserving postbuild order.</summary>
    public static LegacyCombinerPostbuildCommandPlan FilterPlan(
        LegacyCombinerPostbuildCommandPlan plan,
        IReadOnlyList<string> commandIds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(commandIds);
        if (commandIds.Count == 0)
        {
            throw new ArgumentException("Postbuild command id filter must not be empty.", nameof(commandIds));
        }

        HashSet<string> requested = new(StringComparer.Ordinal);
        foreach (string commandId in commandIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
            if (!requested.Add(commandId))
            {
                throw new ArgumentException(
                    $"Postbuild command id '{commandId}' is requested more than once.",
                    nameof(commandIds));
            }
        }

        LegacyCombinerPostbuildCommand[] filtered =
        [
            .. plan.Commands.Where(command => requested.Contains(command.CommandId)),
        ];
        if (filtered.Length != requested.Count)
        {
            string missing = string.Join(
                ", ",
                requested.Except(filtered.Select(command => command.CommandId), StringComparer.Ordinal));
            throw new ArgumentException(
                $"Postbuild command id filter includes unknown command ids: {missing}.",
                nameof(commandIds));
        }

        return new LegacyCombinerPostbuildCommandPlan(plan.Profile, plan.Branch, filtered);
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

    private static LegacyCombinerPostbuildBranch ResolveBranch(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection? selection)
    {
        if (selection is null)
        {
            return LegacyCombinerPostbuildBranch.SingleChip;
        }

        string token = LegacyCombinerPostbuildBranchRule.NormalizeToken(selection.Parts[^1]);
        return profile.BranchRules.TryGetValue(token, out LegacyCombinerPostbuildBranch branch)
            ? branch
            : throw new ArgumentException(
                $"IC number selection '{selection.Parts[^1]}' is not supported by postbuild profile '{profile.ProcessorId}'.");
    }
}
