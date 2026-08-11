using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolved postbuild command plan for one run.</summary>
public sealed class LegacyCombinerPostbuildCommandPlan
{
    /// <summary>Binds an exact topology fact to a profile-compiled command shape.</summary>
    internal LegacyCombinerPostbuildCommandPlan(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildProfile.CompiledPlanTemplate template,
        int topologyCount,
        IReadOnlyList<LegacyCombinerPostbuildCommand> commands,
        ExternalProcessorProtocolPlan protocolPlan)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(protocolPlan);
        Profile = profile;
        Template = template;
        Selector = template.Selector;
        TopologyCount = topologyCount;
        Commands = commands;
        ProtocolPlan = protocolPlan;
    }

    /// <summary>Profile selected for this run.</summary>
    public LegacyCombinerPostbuildProfile Profile { get; }

    /// <summary>Typed topology selector resolved for this run.</summary>
    public LegacyCombinerPostbuildPlanSelector Selector { get; }

    /// <summary>Single, cascade, or distinct count command branch selected for this run.</summary>
    public LegacyCombinerPostbuildBranch Branch => Selector.Branch;

    /// <summary>Exact IC Count used to lower count-dependent runtime facts.</summary>
    public int TopologyCount { get; }

    /// <summary>Exact immutable protocol plan compiled from the selected command shape.</summary>
    public ExternalProcessorProtocolPlan ProtocolPlan { get; }

    /// <summary>
    /// Resolved dependency on canonical FWConfig <c>Chip_Num</c>. A masked DiffDLM
    /// policy consumes the value; count-invariant plans only surface zero as a warning.
    /// </summary>
    public FirmwareConfigChipCountRequirement ChipCountRequirement =>
        Branch == LegacyCombinerPostbuildBranch.Cascade &&
        Profile.DiffDlmPolicy is not null
            ? FirmwareConfigChipCountRequirement.RequiredPositive
            : FirmwareConfigChipCountRequirement.WarningIfZero;

    /// <summary>Process commands in execution order.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> Commands { get; }

    internal LegacyCombinerPostbuildProfile.CompiledPlanTemplate Template { get; }
}
