using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
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
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            ResolveCalls++;
            throw new InvalidOperationException("Retained exact inspection must not recompile.");
        }
    }
}
