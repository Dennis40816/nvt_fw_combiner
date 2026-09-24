using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Display-only counterparts remain exact, fingerprint-bound and independent of Report.</summary>
public sealed class CtrlRamContextAdmissionTests
{
    /// <summary>All six declarations reuse existing same-IC maps, including the declared family member.</summary>
    [Theory]
    [InlineData("NT51919", "single-chip", "nt51919-standard-merge-256k")]
    [InlineData("NT51919", "cascade", "nt51919-standard-merge-256k")]
    [InlineData("NT51950", "single-chip", "nt51950-standard-merge-256k")]
    [InlineData("NT51950", "cascade", "nt51950-standard-merge-512k")]
    [InlineData("NT51951", "single-chip", "nt51951-standard-merge-512k")]
    [InlineData("NT51951", "cascade", "nt51951-standard-merge-512k")]
    public void ExactExistingMapIsAdmitted(string ic, string branch, string mapId)
    {
        ProfileBundleRuntimeRegistration registration = Registration(ic, branch);
        BuiltInV2Registration standard = BuiltInV2RegistrationRegistry.StandardMergeByIc[ic];
        MemoryLayoutContextMap context = Assert.IsType<MemoryLayoutContextMap>(
            CtrlRamV2RouteRegistry.ValidateMemoryLayoutContext(registration, standard));
        Assert.Equal(mapId, context.Map.MapId);
        Assert.Equal(ic, context.IcId);
        Assert.Contains(ic, context.Map.Applicability.MemberIds);
        Assert.Equal(standard.ProfileId, context.ProfileId);
        Assert.Equal(standard.ProfileVersion, context.ProfileVersion);
        Assert.Equal(standard.BundleContentHash, context.TrustedDefinitionSha256);
        Assert.Null(registration.ReportMetadataMapId);
        Assert.Empty(CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(registration, standard).Entries);
    }

    /// <summary>No capacity-based/cross-IC fallback or conflicting Report authority can enter publication.</summary>
    [Fact]
    public void InvalidCounterpartsAreRejected()
    {
        ProfileBundleRuntimeRegistration registration = Registration("NT51919", "single-chip");
        BuiltInV2Registration standard = BuiltInV2RegistrationRegistry.StandardMergeByIc[registration.IcId];
        foreach (string mapId in new[] { "unknown-map", "nt51929-standard-merge-256k", "nt51950-standard-merge-256k" })
        {
            _ = Assert.Throws<InvalidDataException>(() => CtrlRamV2RouteRegistry.ValidateMemoryLayoutContext(
                registration with { MemoryLayoutContextMapId = mapId }, standard));
        }
        _ = Assert.Throws<InvalidDataException>(() => CtrlRamV2RouteRegistry.ValidateMemoryLayoutContext(registration, null));
        _ = Assert.Throws<InvalidDataException>(() => CtrlRamV2RouteRegistry.ValidateMemoryLayoutContext(
            registration, BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51929"]));
        _ = Assert.Throws<InvalidDataException>(() => CtrlRamV2RouteRegistry.ValidateMemoryLayoutContext(
            registration with { ReportMetadataMapId = "another-map" }, standard));
        Assert.Null(CtrlRamV2RouteRegistry.ValidateMemoryLayoutContext(
            registration with { MemoryLayoutContextMapId = null }, standard));
    }

    /// <summary>All ten affected dynamic definitions retain the context and exact policy pins.</summary>
    [Fact]
    public void ContextFingerprintsMatchPinnedPolicy()
    {
        CanonicalCapabilityPolicyRoute[] policies = [.. BuiltInCanonicalCapabilityPolicy.Load().Routes.Where(row =>
            row.Identity.WorkflowId == ExperienceIds.CtrlRamReplace &&
            (row.Identity.IcId is "NT51919" or "NT51950" or "NT51951") &&
            !row.Identity.MapVariant.Contains("-ab-merge-", StringComparison.Ordinal))];
        Assert.Equal(10, policies.Length);
        Assert.All(policies, policy =>
        {
            CanonicalDynamicRoute route = CanonicalDynamicRouteInventory.Resolve(policy.Identity);
            MemoryLayoutContextMap context = Assert.IsType<MemoryLayoutContextMap>(route.MemoryLayoutContext);
            Assert.Equal(policy.Identity.IcId, context.IcId);
            Assert.All(context.SemanticBindingIds, binding => Assert.Contains(binding, route.CompilationContract.SemanticBindingIds));
            Assert.True(policy.CapabilityFingerprint == route.CapabilityFingerprint,
                $"{policy.Identity.RouteId}: {policy.CapabilityFingerprint} -> {route.CapabilityFingerprint}");
            var definition = new CanonicalDynamicCapabilityDefinition(policy.Identity, route.CapabilityFingerprint,
                route.CompilationContract, policy.Authoring, policy.Publication, policy.Evidence, memoryLayoutContext: context);
            Assert.Same(context, definition.MemoryLayoutContext);
            _ = Assert.Throws<ArgumentException>(() => new CanonicalDynamicCapabilityDefinition(policy.Identity, route.CapabilityFingerprint,
                route.CompilationContract, policy.Authoring, policy.Publication, policy.Evidence));
        });
    }

    private static ProfileBundleRuntimeRegistration Registration(string ic, string branch)
    {
        return BuiltInV2BundleRegistry.TrustIndex.Bundles.SelectMany(static bundle => bundle.RuntimeRegistrations)
            .Single(row => row.WorkflowId == ExperienceIds.CtrlRamReplace && row.IcId == ic && row.PostbuildBranch == branch);
    }
}
