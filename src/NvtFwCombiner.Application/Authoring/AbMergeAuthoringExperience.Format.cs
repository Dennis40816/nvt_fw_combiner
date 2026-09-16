using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class AbMergeAuthoringExperience
{
    private sealed record FormatResolution(ResolvedCapabilityRoute DiscoveryRoute,
        CanonicalAbAuthoringDefinition Definition, ResolvedCapability? Capability,
        AbMergeFormatSelection? Format, IReadOnlyList<CompositionIssue> Issues);

    private (ResolvedCapabilityRoute Route, CanonicalAbAuthoringDefinition Definition) GetAbDeclarations(
        string icId, TopologySelection? topology)
    {
        string member = IcIdentifier.Normalize(icId);
        ResolvedCapabilityRoute[] routes = [.. _catalog.GetCurrentSnapshot().DynamicRoutes.Where(route =>
            route.Identity.WorkflowId == ExperienceIds.AbMerge && route.Identity.IcId == member &&
            route.Authoring.Value == CapabilityAuthoringAvailability.Available &&
            (route.AbMergeTopologyChoice is { } choice
                ? topology is not null && choice.Selection.ChipCount == 1 == (topology.ChipCount == 1)
                : topology is null)).OrderBy(static route => route.Identity.RouteId, StringComparer.Ordinal)];
        if (routes.Length == 0)
        {
            throw new InvalidOperationException("No current AB declaration matches the selected IC and topology.");
        }
        CanonicalAbAuthoringDefinition? first = null;
        foreach (ResolvedCapabilityRoute route in routes)
        {
            if (!_compiler.TryGetAbAuthoringDefinition(route, out CanonicalAbAuthoringDefinition? next,
                    out IReadOnlyList<CompositionIssue> issues))
            {
                throw new InvalidOperationException(string.Join(" | ", issues.Select(static issue => issue.Message)));
            }
            if (first is not null && !HasEquivalentAbInputs(first, next))
            {
                throw new InvalidOperationException("AB format variants disagree on their trusted input declarations.");
            }
            first ??= next;
        }
        // A deterministic declaration source is not an output-map choice.
        return (routes[0], first!);
    }

    private static bool HasEquivalentAbInputs(CanonicalAbAuthoringDefinition first, CanonicalAbAuthoringDefinition next)
    {
        return first.Family.FamilyId == next.Family.FamilyId && first.Family.FamilyVersion == next.Family.FamilyVersion &&
            first.Family.FamilyContentHash == next.Family.FamilyContentHash && first.InputBindings.SequenceEqual(next.InputBindings) &&
            first.TpAInputBinding == next.TpAInputBinding && first.TpBInputBinding == next.TpBInputBinding &&
            first.SelectionGroupMemberSlotIds.SequenceEqual(next.SelectionGroupMemberSlotIds, StringComparer.Ordinal);
    }

    private async ValueTask<FormatResolution> ResolveAbFormatAsync(string icId, TopologySelection? topology,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> captured, AbMergeDpMode dpMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (ResolvedCapabilityRoute route, CanonicalAbAuthoringDefinition definition) = GetAbDeclarations(icId, topology);
        EventBufferFormatConfigurationState? state = null;
        if (definition.Family.AbFormatPolicy is not null)
        {
            if (_getConfiguration is not null)
            {
                IEventBufferFormatConfigurationSession configuration =
                    await _getConfiguration(cancellationToken).ConfigureAwait(false);
                state = (await configuration.ReloadAsync(cancellationToken).ConfigureAwait(false)).State;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return ResolveCapturedFormat(route, definition, topology, captured, dpMode, state);
    }

    private FormatResolution ResolveCapturedFormat(ResolvedCapabilityRoute declarationRoute,
        CanonicalAbAuthoringDefinition definition, TopologySelection? topology,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs, AbMergeDpMode dpMode,
        EventBufferFormatConfigurationState? state)
    {
        if (!Enum.IsDefined(dpMode)) { throw new ArgumentOutOfRangeException(nameof(dpMode)); }
        CompiledAuthoringSelectedInput[] normalized = CompiledAuthoringWorkflowService.NormalizeSelectedInputs(
            [.. inputs], definition.InputBindings);
        if (normalized.Select(static input => input.SlotId).Distinct(StringComparer.Ordinal).Count() != normalized.Length ||
            normalized.Any(input => !definition.InputBindings.Any(binding => binding.SlotId == input.SlotId) ||
                (dpMode == AbMergeDpMode.Dummy && definition.SelectionGroupMemberSlotIds.Contains(input.SlotId, StringComparer.Ordinal))))
        {
            return new(declarationRoute, definition, null, null,
                [new CompositionIssue("AB_FORMAT_INPUT_INVALID", "The selected inputs do not match the declared AB input mode.")]);
        }
        if (!_compiler.TryGetAbAuthoringDefinition(declarationRoute, out CanonicalAbAuthoringDefinition? currentDefinition, out _) ||
            !HasEquivalentAbInputs(definition, currentDefinition))
        {
            return StaleAbPublication(declarationRoute, definition);
        }
        AbMergeFormatSelection? format = null;
        ResolvedCapabilityRoute route = declarationRoute;
        if (definition.Family.AbFormatPolicy is not null)
        {
            FirmwareBinInspectionArtifact[] artifacts = [.. normalized.Where(static input => input.Bytes is { Length: > 0 })
                .Select(input => new FirmwareBinInspectionArtifact(
                    definition.InputBindings.Single(binding => binding.SlotId == input.SlotId).AddressSpaceId,
                    input.Bytes!.Value))];
            AbMergeFormatAdmissionResult admitted = AbMergeFormatAdmission.Assess(definition.Family,
                declarationRoute.Identity.IcId, state, topology, artifacts);
            if (!admitted.Succeeded)
            {
                return new(declarationRoute, definition, null, null, admitted.Issues);
            }
            format = admitted.Selection!;
            ResolvedCapabilityRoute[] matches = [.. _catalog.GetCurrentSnapshot().DynamicRoutes.Where(candidate =>
                candidate.Identity.WorkflowId == ExperienceIds.AbMerge &&
                candidate.Identity.IcId == declarationRoute.Identity.IcId &&
                candidate.Authoring.Value == CapabilityAuthoringAvailability.Available &&
                candidate.CompilationContract.AllowedMapVariantIds.Contains(format.MapId, StringComparer.Ordinal))];
            if (matches.Length != 1)
            {
                return new(declarationRoute, definition, null, format,
                    [new CompositionIssue("AB_FORMAT_ROUTE_UNAVAILABLE", "The detected format has no unique current output route.")]);
            }
            route = matches[0];
        }
        if (route.ResolutionToken != declarationRoute.ResolutionToken ||
            !_compiler.TryGetAbAuthoringDefinition(route, out CanonicalAbAuthoringDefinition? targetDefinition, out _) ||
            !HasEquivalentAbInputs(definition, targetDefinition))
        {
            return StaleAbPublication(declarationRoute, definition);
        }
        _ = _compiler.TryCompilePublishedDynamicCapability(route.Identity, null,
                dpMode == AbMergeDpMode.Dummy ? [] : definition.SelectionGroupMemberSlotIds,
                out _, out ResolvedCapability? capability, out IReadOnlyList<CompositionIssue> issues, topology);
        return _catalog.GetCurrentSnapshot().ResolutionToken != declarationRoute.ResolutionToken ||
            (capability is not null && capability.ResolutionToken != declarationRoute.ResolutionToken)
            ? StaleAbPublication(declarationRoute, definition)
            : capability is null || issues.Count != 0
            ? new(declarationRoute, definition, null, format, issues)
            : format is not null && capability.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.MapId != format.MapId
            ? new(declarationRoute, definition, null, format,
                [new CompositionIssue("AB_FORMAT_ROUTE_MISMATCH", "The compiled output differs from the admitted format.")])
            : new(declarationRoute, definition, capability, format, []);
    }

    private static FormatResolution StaleAbPublication(ResolvedCapabilityRoute route, CanonicalAbAuthoringDefinition definition)
    {
        return new(route, definition, null, null,
            [new CompositionIssue("AB_FORMAT_PUBLICATION_STALE", "The AB profile publication changed during format capture. Inspect the inputs again.")]);
    }

    private static CompiledAuthoringSelectedInput[] CaptureAbInputs(IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        bool invalid = inputs.Any(static input => input is null || string.IsNullOrWhiteSpace(input.SlotId) ||
                string.IsNullOrWhiteSpace(input.SelectedPathHint));
        return invalid ? throw new ArgumentException("AB inputs require slot identifiers and explicit paths.", nameof(inputs))
            : [.. inputs.Select(static input => input with
        {
            Bytes = input.Bytes is { } bytes ? new ReadOnlyMemory<byte>(bytes.ToArray()) : (ReadOnlyMemory<byte>?)null,
        })];
    }

    private static CompiledAuthoringSelectionSnapshot DeclarationSelection(ResolvedCapabilityRoute route,
        CanonicalAbAuthoringDefinition definition, IReadOnlyCollection<string> selectedSlotIds,
        AbMergeDpMode dpMode, IReadOnlyList<CompositionIssue>? issues = null)
    {
        CompiledAuthoringInputBinding[] bindings = [.. definition.InputBindings.Where(binding =>
            dpMode != AbMergeDpMode.Dummy || !definition.SelectionGroupMemberSlotIds.Contains(binding.SlotId, StringComparer.Ordinal))];
        return new(AuthoringCapabilityCatalogSnapshot.FromDynamicRoute(route, bindings.Select(static binding => binding.SlotId)),
            [.. bindings.Select(binding => new InputSelectionMemberReadiness(binding.SlotId,
                selectedSlotIds.Contains(binding.SlotId, StringComparer.Ordinal), ResolvedChildReadiness.PendingInput,
                CanSelect: true, Reason: null, NextAction: null, IsRequired: true))], bindings, issues ?? []);
    }

    /// <summary>Captures config and bytes before exact compilation; no fallback to a previously accepted format.</summary>
    public async ValueTask<CompiledAuthoringSessionPreparation> PrepareSessionAsync(AuthoringSessionState session,
        string icId, string? topologyToken, IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs,
        AbMergeDpMode dpMode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        CompiledAuthoringSelectedInput[] captured = CaptureAbInputs(inputs);
        TopologySelection? topology = _compiler.ResolveAbMergeTopologySelection(icId, topologyToken);
        FormatResolution resolved = await ResolveAbFormatAsync(icId, topology, captured, dpMode, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (resolved.Capability is null)
        {
            CompiledAuthoringSelectionSnapshot selection = DeclarationSelection(resolved.DiscoveryRoute, resolved.Definition,
                [.. captured.Select(static input => input.SlotId)], dpMode, resolved.Issues);
            AuthoringSessionTransitionResult invalidated = session.Activate(selection.Catalog);
            return new(invalidated.Snapshot, selection, null, invalidated.Issue);
        }
        return new CompiledAuthoringWorkflowService(new AbMergeAuthoringResolver(topology, _compiler, dpMode, resolved.Capability))
            .PrepareExactSession(icId, session, captured);
    }

    /// <inheritdoc />
    public async ValueTask<CompiledAuthoringSessionPreparation?> ReapplyAcceptedInputsAsync(
        AuthoringSessionState session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        ActiveSessionSnapshot? original = session.CurrentSnapshot;
        ResolvedCapability? accepted = original?.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection);
        if (original is null || (!original.HasCurrentInputInspection &&
            !original.InputSlotStatuses.Any(static status => status.CapturedSource is not null))) { return null; }
        ResolvedCapabilityRoute? sourceRoute = _catalog.ResolveDynamicRoute(original.SelectedRouteId).Route;
        if (original.WorkflowId != ExperienceIds.AbMerge ||
            sourceRoute is null || sourceRoute.ResolutionToken != original.ResolutionToken ||
            sourceRoute.CapabilityFingerprint != original.CapabilityFingerprint ||
            (accepted is not null && _catalog.ResolveCurrentCompilation(accepted.CompiledComposition, accepted) is null))
        {
            throw new InvalidOperationException("AB_FORMAT_SESSION_STALE: The retained AB inputs no longer have current profile authority.");
        }
        TopologySelection? topology = accepted is not null
            ? CapabilityPublicationCoherence.GetAcceptedAbMergeTopologySelection(accepted)
            : sourceRoute.AbMergeTopologyChoice?.Selection;
        (ResolvedCapabilityRoute route, CanonicalAbAuthoringDefinition definition) = GetAbDeclarations(original.SelectedIc, topology);
        if (definition.Family.AbFormatPolicy is null) { return null; }
        AbMergeDpMode mode = GetDeclaredAbDpMode(original.Slots.Select(static slot => slot.DefinitionId), definition);
        CompiledAuthoringSelectedInput[] inputs = accepted is not null
            ? CaptureAcceptedAbInputs(original)
            : CaptureAbInputs([.. original.InputSlotStatuses.Select(static status =>
                new CompiledAuthoringSelectedInput(status.SlotId, status.SelectedPathHint!, status.CapturedSource?.AcceptedBytes))]);
        CompiledAuthoringSelectionSnapshot declaration = DeclarationSelection(route, definition,
            [.. inputs.Select(static input => input.SlotId)], mode);
        AuthoringSessionTransitionResult begun = session.TryBeginAcceptedInputReinspection(original);
        if (!begun.Succeeded) { return new(begun.Snapshot, declaration, null, begun.Issue); }
        ActiveSessionSnapshot expected = begun.Snapshot!;
        FormatResolution resolution = await ResolveAbFormatAsync(original.SelectedIc, topology, inputs, mode, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (resolution.Capability is not { } capability)
        {
            return new(expected, declaration with { Issues = resolution.Issues }, null, null);
        }
        CompiledAuthoringInspectionBatch batch = new CompiledAuthoringWorkflowService(
            new AbMergeAuthoringResolver(topology, _compiler, mode, capability))
            .InspectBatch(original.SelectedIc, expected.AuthoringRevision, inputs, capability);
        var selection = new CompiledAuthoringSelectionSnapshot(batch.Catalog,
            [.. batch.Statuses.Values.Select(static status => status.SelectionReadiness)], declaration.InputBindings, batch.Issues);
        if (batch.Issues.Count != 0 || batch.Statuses.Values.Any(static status => status.BlocksBuild))
        {
            return new(expected, selection, batch, new AuthoringSessionIssue(AuthoringSessionIssueCodes.InvalidPublication,
                "The retained inputs do not satisfy the updated AB output contract.", ExperienceIds.AbMerge));
        }
        cancellationToken.ThrowIfCancellationRequested();
        AuthoringSessionTransitionResult adopted = session.TryAdoptExactSlotFileInspectionBatch(expected, batch.Catalog, [.. batch.Statuses.Values]);
        if (adopted.Succeeded && adopted.Snapshot is { } current)
        {
            batch = new(AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(current.ExactCapability!),
                current.InputSlotStatuses.ToDictionary(static status => status.SlotId, StringComparer.Ordinal), batch.Issues, current.MetadataInspection);
            selection = selection with { Catalog = batch.Catalog, Slots = [.. current.InputSlotStatuses.Select(static status => status.SelectionReadiness)] };
        }
        return new(adopted.Snapshot, selection, batch, adopted.Issue)
        {
            AbMergeFacts = adopted.Succeeded
                ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, AbMergeInputFacts>(
                    batch.Statuses.ToDictionary(static pair => pair.Key,
                        pair => ProjectAbInputFacts(pair.Value, resolution), StringComparer.Ordinal))
                : System.Collections.Frozen.FrozenDictionary<string, AbMergeInputFacts>.Empty,
        };
    }
}
