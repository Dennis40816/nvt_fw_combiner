using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolved postbuild command plan for one run.</summary>
public sealed class LegacyCombinerPostbuildCommandPlan
{
    private readonly LegacyCombinerPostbuildCommand[] _commands;

    /// <summary>Creates a resolved postbuild command plan.</summary>
    public LegacyCombinerPostbuildCommandPlan(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildPlanSelector selector,
        IEnumerable<LegacyCombinerPostbuildCommand> commands,
        int? topologyCount = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(commands);

        _commands = [.. commands];
        if (_commands.Length == 0)
        {
            throw new ArgumentException("Resolved postbuild plan must contain at least one command.", nameof(commands));
        }

        Profile = profile;
        Selector = selector;
        TopologyCount = topologyCount ?? selector.MinimumCount;
        if (!selector.MatchesReportedChipCount(TopologyCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(topologyCount),
                TopologyCount,
                "Resolved topology count is outside the selected postbuild plan.");
        }
    }

    /// <summary>Profile selected for this run.</summary>
    public LegacyCombinerPostbuildProfile Profile { get; }

    /// <summary>Typed topology selector resolved for this run.</summary>
    public LegacyCombinerPostbuildPlanSelector Selector { get; }

    /// <summary>Single, cascade, or distinct count command branch selected for this run.</summary>
    public LegacyCombinerPostbuildBranch Branch => Selector.Branch;

    /// <summary>Exact IC Count used to lower count-dependent runtime facts.</summary>
    public int TopologyCount { get; }

    /// <summary>
    /// Resolved dependency on canonical FWConfig <c>Chip_Num</c>. A Dynamic DiffDLM
    /// policy consumes the value; count-invariant plans only surface zero as a warning.
    /// </summary>
    public FirmwareConfigChipCountRequirement ChipCountRequirement =>
        Branch == LegacyCombinerPostbuildBranch.Cascade &&
        Profile.DiffDlmPolicy is not null
            ? FirmwareConfigChipCountRequirement.RequiredPositive
            : FirmwareConfigChipCountRequirement.WarningIfZero;

    /// <summary>Process commands in execution order.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> Commands => _commands;
}
