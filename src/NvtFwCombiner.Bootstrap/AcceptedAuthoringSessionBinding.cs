using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Validates one exact accepted authoring session before creating immutable runtime bindings.</summary>
internal static class AcceptedAuthoringSessionBinding
{
    internal static ResolvedCapability RequireCapability(
        ActiveSessionSnapshot session,
        string workflowId,
        string icId,
        AuthoringDerivedResultKind resultKind)
    {
        ArgumentNullException.ThrowIfNull(session);
        ResolvedCapability? capability = session.GetAcceptedCapability(resultKind);
        return capability is not null &&
            StringComparer.Ordinal.Equals(session.WorkflowId, workflowId) &&
            StringComparer.Ordinal.Equals(
                capability.Identity.IcId,
                Profiles.IcIdentifier.Normalize(icId)) &&
            (resultKind != AuthoringDerivedResultKind.Inspection || session.HasCurrentInputInspection)
            ? capability
            : throw new InvalidOperationException(
                "The run requires one exact current accepted authoring compilation.");
    }

    internal static InputArtifactBinding Create(
        CompiledComposition compiledComposition,
        string addressSpaceId,
        string selectedPath,
        ActiveSessionSnapshot acceptedSession,
        string? slotDefinitionId = null)
    {
        ArgumentNullException.ThrowIfNull(acceptedSession);
        string definitionId = ResolveSlotDefinitionId(
            compiledComposition.V2Details.InputContract.SpaceBindings,
            addressSpaceId,
            slotDefinitionId);
        AuthoringSlotState slot = acceptedSession.Slots.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.DefinitionId, definitionId)) ??
            throw new InvalidOperationException(
                $"The accepted session does not contain input slot '{definitionId}'.");
        string fullPath = Path.GetFullPath(selectedPath);
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        bool pathMatches = slot.SelectedPath is { } acceptedPath &&
            string.Equals(Path.GetFullPath(acceptedPath), fullPath, pathComparison);
        FileStamp stamp = pathMatches &&
            slot.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning &&
            slot.FileStamp is { } acceptedStamp
                ? acceptedStamp
                : throw new InvalidOperationException(
                    $"Input slot '{definitionId}' does not match its accepted inspected file.");

        return CompiledCompositionInputBindingFactory.Create(
            compiledComposition,
            addressSpaceId,
            fullPath,
            stamp);
    }

    internal static string ResolveSlotDefinitionId(
        IReadOnlyList<CompiledInputSpaceBinding> bindings,
        string addressSpaceId,
        string? explicitSlotDefinitionId = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        return explicitSlotDefinitionId ?? bindings.Single(binding =>
            StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId)).SlotId;
    }
}
