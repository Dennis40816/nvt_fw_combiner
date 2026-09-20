using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Captured effective format; generation is provenance, not a freshness comparison key.</summary>
public sealed record CompositionOutputFormatSummary(
    string FormatId, string DisplayName, long ConfigurationGeneration, string ConfigurationSourceSha256);

/// <summary>One real accepted input binding, independent of delivery-file deduplication.</summary>
public sealed record CompositionOutputInputSummary(
    string BindingId, string SlotId, string SourceFileName, long SizeBytes,
    IReadOnlyList<long> ExpectedLengths, EventBufferFormatObservation? EventBufferFormat,
    AuthoringSlotLifecycle? InspectionLifecycle, string? InspectionIssueCode)
{
    /// <summary>Complete accepted input identity, shared with the bundle source manifest.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Existing path-free inspection evidence for localized warning presentation.</summary>
    public CompiledInputArtifactInspectionResult? Inspection { get; init; }

    /// <summary>Existing non-blocking observation diagnostics, including version metadata warnings.</summary>
    public IReadOnlyList<CompiledInputArtifactInspectionAdvisory> InspectionAdvisories { get; init; } = [];
}

/// <summary>Path-free immutable confirmation facts. Output length is never the sum of input or bundle sizes.</summary>
public sealed record CompositionOutputConfirmationSummary(
    string IcId, string WorkflowId, TopologySelection? Topology, long OutputLengthBytes,
    CompositionOutputFormatSummary? Format, IReadOnlyList<CompositionOutputInputSummary> Inputs,
    bool HasGeneratedInputs)
{
    /// <summary>Accepted Replace selector, retained without converting selector tokens into inferred topology.</summary>
    public IcNumberSelection? IcNumber { get; init; }

    /// <summary>Compiled map constraint; a cascade selection's count is a bound, not an observed exact count.</summary>
    public TopologyRequirement? TopologyRequirement { get; init; }
}

internal static class CompositionOutputConfirmationProjector
{
    internal static CompositionOutputConfirmationSummary Create(ActiveSessionSnapshot session,
        ICompositionArtifactIdentityPolicy identityPolicy, AbMergeFormatSelection? format,
        CompositionOutputPreparation output)
    {
        ResolvedCapability capability = session.ExactCapability ??
            throw new InvalidOperationException("Confirmation requires an exact accepted capability.");
        CompiledComposition compiled = capability.CompiledComposition;
        var inputs = new List<CompositionOutputInputSummary>();
        bool generated = output.OutputName.Issues.Any(static issue => issue.Code == "output-naming.dummy-dp");
        foreach (CompositionOutputBundleSourceCandidate candidate in CompositionOutputBundleSourcePlanner.CreateCandidates(session))
        {
            if (VirtualArtifactLocator.IsVirtual(candidate.ArtifactLocator)) { generated |= capability.GeneralExecutionPlan is null; continue; }
            AuthoringInputSlotStatus? status = session.InputSlotStatuses.SingleOrDefault(input =>
                input.AddressSpaceId == candidate.BindingId);
            IReadOnlyList<long> expected = status?.Inspection?.ExpectedOuterLengths ?? [];
            inputs.Add(new(candidate.BindingId, candidate.SlotId,
                identityPolicy.Resolve(candidate.ArtifactLocator).OriginalFileName, candidate.FileStamp.AcceptedLength,
                Array.AsReadOnly(expected.ToArray()),
                AbMergeAuthoringExperience.ProjectEventBufferFormat(candidate.BindingId, format),
                status?.InspectionLifecycle, status?.InspectionIssueCode)
            {
                Sha256 = candidate.FileStamp.Sha256,
                Inspection = status?.Inspection,
                InspectionAdvisories = Array.AsReadOnly(status?.InspectionAdvisories.ToArray() ?? []),
            });
        }
        return new(capability.Identity.IcId, session.WorkflowId,
            compiled.V2Details.Provenance.Context is MapBoundV2CompilationContext map ? map.ResolvedMap.TopologySelection : null,
            compiled.Plan.AddressSpaces.Single(space => space.AddressSpaceId == compiled.Plan.OutputSpaceId).Length,
            format is null ? null : new(format.FormatId, format.DisplayName,
                format.ConfigurationGeneration, format.ConfigurationSourceSha256),
            Array.AsReadOnly(inputs.ToArray()), generated)
        {
            TopologyRequirement = compiled.V2Details.Provenance.Context is MapBoundV2CompilationContext context
                ? context.ResolvedMap.ImageMap.Applicability.TopologyRequirement : null,
            IcNumber = capability.CtrlRamExecutionPlan?.IcNumberSelection ??
                capability.GeneralExecutionPlan?.IcNumberSelection,
        };
    }
}
