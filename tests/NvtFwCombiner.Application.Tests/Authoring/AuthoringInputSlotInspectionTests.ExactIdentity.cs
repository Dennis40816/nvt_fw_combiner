using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
    /// <summary>Picker admission is separate from inspecting an already-selected ready artifact.</summary>
    [Fact]
    public void SelectedReadyArtifactCanBeInspectedWhenPickerTransitionIsDisabled()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        InputSelectionMemberReadiness selected = ReadySelection() with
        {
            CanSelect = false,
        };

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(11),
            selected,
            SourceSpace,
            new byte[4]);

        Assert.Equal(AuthoringSlotLifecycle.Verified, result.InspectionLifecycle);
        Assert.False(result.CanSelect);
    }

    /// <summary>One Application use case normalizes address-space aliases and owns exact immutable publication.</summary>
    [Fact]
    public void PrepareExactSessionPublishesOneCurrentImmutableBatch()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        var service = new CompiledAuthoringWorkflowService(
            new FixedCapabilityResolver(capability, prerequisiteSlotId: null));
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        byte[] sourceBytes = [.. Enumerable.Repeat((byte)0x5A, 4)];
        CompiledAuthoringInputBinding[] bindings =
        [
            .. capability.CompiledComposition.V2Details.InputContract.SpaceBindings
                .Select(static binding => new CompiledAuthoringInputBinding(
                    binding.SlotId,
                    binding.AddressSpaceId)),
        ];

        CompiledAuthoringSessionPreparation prepared = service.PrepareExactSession(
            "NT-HEADLESS",
            session,
            [
                .. bindings.Select(binding => new CompiledAuthoringSelectedInput(
                    binding.AddressSpaceId,
                    $"{binding.AddressSpaceId}.bin",
                    sourceBytes)),
            ]);

        Assert.True(
            prepared.Succeeded,
            $"session={prepared.SessionIssue?.Code}:{prepared.SessionIssue?.Message}; selection={string.Join(',', prepared.Selection.Issues.Select(static issue => issue.Code))}; inspection={string.Join(',', prepared.Inspection?.Issues.Select(static issue => issue.Code) ?? [])}; current={prepared.Snapshot?.HasCurrentInputInspection}");
        Assert.Null(prepared.SessionIssue);
        Assert.Empty(prepared.Issues);
        ActiveSessionSnapshot snapshot = Assert.IsType<ActiveSessionSnapshot>(prepared.Snapshot);
        Assert.True(snapshot.HasCurrentInputInspection);
        Assert.Same(capability, snapshot.ExactCapability);
        Assert.Equal(
            bindings.Select(static binding => binding.SlotId),
            snapshot.Slots.Select(static slot => slot.DefinitionId));
        _ = Assert.Single(snapshot.DerivedPublications);
        Assert.All(snapshot.InputSlotStatuses, static status => Assert.True(status.IsTerminal));
        MetadataInspectionSnapshot metadata = Assert.IsType<MetadataInspectionSnapshot>(
            snapshot.MetadataInspection);
        Assert.Equal(snapshot.AuthoringRevision.Value, metadata.AuthoringRevision);
        Assert.Equal(
            snapshot.InputSlotStatuses.Select(static status => status.AddressSpaceId)
                .Order(StringComparer.Ordinal),
            metadata.ArtifactIdentities.Select(static identity => identity.ArtifactId));
        var naming =
            AcceptedOutputNamingInspection.Accept(snapshot);
        Assert.Same(metadata, naming.Snapshot);
        Assert.Equal(snapshot.AuthoringRevision.Value, naming.AuthoringRevision);
        AuthoringPublicationLease validationLease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation);
        Assert.True(session.TryPublish(
            validationLease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "validation-result")).Succeeded);
        Assert.Same(metadata, session.CurrentSnapshot!.MetadataInspection);
        AuthoringSessionTransitionResult readinessRefresh = session.Activate(prepared.Selection);
        Assert.True(readinessRefresh.Succeeded);
        Assert.Same(metadata, readinessRefresh.Snapshot!.MetadataInspection);
        Assert.True(readinessRefresh.Snapshot.HasCurrentInputInspection);
        Assert.Same(
            metadata,
            AcceptedOutputNamingInspection.Accept(readinessRefresh.Snapshot).Snapshot);

        Array.Fill(sourceBytes, (byte)0x00);
        Assert.All(snapshot.InputSlotStatuses, static status =>
            Assert.DoesNotContain((byte)0x00, status.AcceptedBytes!.Value.ToArray()));
        Assert.All(metadata.ArtifactIdentities, static identity =>
            Assert.NotEqual(new string('0', 64), identity.Sha256));
    }

    /// <summary>A retained exact batch never rediscovers or recompiles its workflow.</summary>
    [Fact]
    public void RetainedExactInspectionSkipsResolverCompilation()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        var resolver = new CountingRejectingResolver(ExperienceIds.StandardMerge);
        var service = new CompiledAuthoringWorkflowService(resolver);
        CompiledInputSlotRequirement slot = Assert.Single(
            capability.CompiledComposition.V2Details.InputContract.Slots);

        CompiledAuthoringInspectionBatch result = service.InspectBatch(
            "NT-HEADLESS",
            new AuthoringRevision(6),
            [new CompiledAuthoringSelectedInput(slot.SlotId, "source.bin", new byte[4])],
            capability);

        Assert.Empty(result.Issues);
        Assert.Same(capability, Assert.Single(result.Catalog.Routes).ExactCapability);
        Assert.Equal(0, resolver.DiscoverCalls);
        Assert.Equal(0, resolver.ResolveCalls);
        Assert.All(result.Statuses.Values, static status => Assert.True(status.IsTerminal));
    }

    /// <summary>Reprojecting the same exact capability is an identity-preserving no-op.</summary>
    [Fact]
    public void SessionRetainsTheSameExactCapabilityInstance()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        Assert.True(session.Activate(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability)).Succeeded);
        ActiveSessionSnapshot first = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult repeated = session.Activate(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability));

        Assert.True(repeated.Succeeded);
        Assert.Same(first, repeated.Snapshot);
        Assert.Same(capability, repeated.Snapshot!.ExactCapability);
    }

    /// <summary>Equal fingerprint strings cannot substitute a different compiled object.</summary>
    [Fact]
    public void SessionRejectsDifferentCapabilityInstanceWithEqualFingerprints()
    {
        ResolvedCapability accepted = CreateCapability(ExperienceIds.StandardMerge);
        ResolvedCapability duplicate = CreateCapability(ExperienceIds.StandardMerge);
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        Assert.True(session.Activate(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(accepted)).Succeeded);

        AuthoringSessionTransitionResult replacement = session.Activate(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(duplicate));

        Assert.False(replacement.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.InvalidPublication, replacement.Issue!.Code);
        Assert.Same(accepted, session.CurrentSnapshot!.ExactCapability);
    }

    private sealed class CountingRejectingResolver(string workflowId)
        : ICompiledAuthoringWorkflowResolver
    {
        public string WorkflowId { get; } = workflowId;

        public int DiscoverCalls { get; private set; }

        public int ResolveCalls { get; private set; }

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            DiscoverCalls++;
            throw new InvalidOperationException("Retained exact inspection must not rediscover.");
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            AuthoringRevision authoringRevision,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            ResolveCalls++;
            throw new InvalidOperationException("Retained exact inspection must not recompile.");
        }
    }

    private sealed class FixedCapabilityResolver(
        ResolvedCapability capability,
        string? prerequisiteSlotId) : ICompiledAuthoringWorkflowResolver
    {
        public string WorkflowId => capability.Identity.WorkflowId;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            return new CompiledAuthoringWorkflowDiscovery(
                capability,
                [
                    .. capability.CompiledComposition.V2Details.InputContract.Slots
                        .Select(static slot => slot.SlotId),
                ],
                prerequisiteSlotId);
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            AuthoringRevision authoringRevision,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            return new CompiledAuthoringWorkflowResolution(capability, []);
        }
    }
}
