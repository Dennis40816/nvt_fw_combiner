using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
    private static ResolvedCapabilityRoute CreateRoute(string workflowId)
    {
        var identity = new CapabilityRouteIdentity(
            "NT-HEADLESS",
            workflowId,
            "none",
            $"{workflowId}-map");
        var contract = new CanonicalCapabilityCompilationContract(
            $"synthetic-{workflowId}",
            "1.0.0",
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            [$"{workflowId}-map"],
            CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId);
        string fingerprint = CapabilityDefinitionFingerprint.Compute(
            identity,
            contract.ProfileId,
            contract.ProfileVersion,
            contract.TrustedDefinitionSha256,
            contract.AllowedMapVariantIds,
            contract.CompilerSemanticId,
            contract.SemanticBindingIds);
        var definition = new CanonicalDynamicCapabilityDefinition(
            identity,
            fingerprint,
            contract,
            Decision(identity, fingerprint, CapabilityAuthoringAvailability.Available),
            Decision(identity, fingerprint, CapabilityPublicationStatus.TestOnly),
            Decision(identity, fingerprint, CapabilityEvidenceStatus.SyntheticOracle));
        return new ResolvedCapabilityRoute(
            definition,
            new ResolutionToken("headless-pending-publication"));
    }

    private static PinnedCapabilityDecision<T> Decision<T>(
        CapabilityRouteIdentity identity,
        T value)
        where T : struct, Enum
    {
        return Decision(identity, CapabilityFingerprint, value);
    }

    private static PinnedCapabilityDecision<T> Decision<T>(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        T value)
        where T : struct, Enum
    {
        return new PinnedCapabilityDecision<T>(
            $"headless-{typeof(T).Name}",
            identity.RouteId,
            capabilityFingerprint,
            value,
            "synthetic-headless-contract");
    }
}
