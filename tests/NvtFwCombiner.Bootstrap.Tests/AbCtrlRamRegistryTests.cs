using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Production registry admission needs definitions only, never firmware fixtures.</summary>
public sealed class AbCtrlRamRegistryTests(ITestOutputHelper output)
{
    /// <summary>Partial-family candidates join only the existing same-IC AB and local full-flash profiles.</summary>
    [Theory]
    [InlineData("NT51950", "1-ic", "nt51950-ab-merge-512k", 0x40000, 0x40000)]
    [InlineData("NT51950", "2-ic", "nt51950-ab-merge-1024k", 0x80000, 0x40000)]
    [InlineData("NT51951", "1-ic", "nt51951-ab-merge-1024k", 0x80000, 0x80000)]
    [InlineData("NT51951", "2-ic", "nt51951-ab-merge-1024k", 0x80000, 0x80000)]
    public void PartialFamilyReusesExactExistingParents(string icId, string count, string mapId,
        int bankCapacity, int localCapacity)
    {
        BankReplaceRouteBinding binding = Assert.IsType<BankReplaceRouteBinding>(
            CanonicalDynamicRouteInventory.FindBankReplaceBinding(icId, count));
        Assert.Equal(mapId, binding.Definition.Layout.MapId);
        Assert.Equal(bankCapacity, binding.Definition.BankCapacityBytes);
        Assert.Equal(new ByteRange(0, localCapacity), binding.Definition.LocalBankRange);
        Assert.Equal(icId, binding.Definition.Local.MemberId);
        Assert.Equal(binding.Local.Route.ProfileId, binding.Definition.Local.ProfileId);
        output.WriteLine($"{binding.Identity.RouteId} {CanonicalDynamicRouteInventory.Resolve(binding.Identity).CapabilityFingerprint}");
        Assert.Equal("nt51950-ab-merge", binding.Layout.BundleContentHash ==
            BuiltInV2BundleRegistry.All["nt51950-ab-merge"].ContentHash
                ? "nt51950-ab-merge" : "unexpected");
    }

    /// <summary>Every Perfect-family member exposes the same two candidate bank shapes.</summary>
    [Theory]
    [InlineData("NT51919", "1-ic", "nt51919-ab-merge-512k")]
    [InlineData("NT51919", "2-8-ic", "nt51919-ab-merge-512k")]
    [InlineData("NT51929", "1-ic", "nt51929-ab-merge-512k")]
    [InlineData("NT51929", "2-8-ic", "nt51929-ab-merge-512k")]
    [InlineData("NT51932", "1-ic", "nt51932-ab-merge-512k")]
    [InlineData("NT51932", "2-8-ic", "nt51932-ab-merge-512k")]
    public void PerfectFamilySingleAndCascadeHaveCandidateAbReplaceRoutes(string icId, string count, string mapId)
    {
        var identity = new CapabilityRouteIdentity(icId, ExperienceIds.CtrlRamReplace, count, mapId);
        CanonicalDynamicRoute definition = CanonicalDynamicRouteInventory.Resolve(identity);
        output.WriteLine($"{identity.RouteId} {definition.CapabilityFingerprint}");
        CapabilityRouteResolutionResult resolution = Catalog().ResolveDynamicRoute(identity.RouteId);
        Assert.True(resolution.Succeeded, resolution.Issue?.Message);
        Assert.Equal(CapabilityPublicationStatus.Candidate, resolution.Route!.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, resolution.Route.Evidence.Value);
    }

    /// <summary>Definition expansion is deterministic and exposes only genuine trusted parent identities.</summary>
    [Fact]
    public void DefinitionExpansionRequiresNoFirmwareArtifact()
    {
        BankReplaceRouteBinding binding = CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51929", "1-ic")!;
        BankReferenceReplaceDefinition definition = binding.Definition;
        CapabilityRouteIdentity identity = binding.Identity;
        CanonicalDynamicRoute route = CanonicalDynamicRouteInventory.Resolve(identity);
        Assert.Equal("nt51929-ab-merge", definition.Layout.ProfileId);
        Assert.Equal("nt51929-ctrlram-replace-fw200-single", definition.Local.ProfileId);
        Assert.Equal(definition.ContentHash, route.CompilationContract.TrustedDefinitionSha256);
        Assert.Equal(route.CapabilityFingerprint, CanonicalDynamicRouteInventory.CreateResolver()(identity).CapabilityFingerprint);
        output.WriteLine("Registry route: " + identity.RouteId);
        output.WriteLine("Registry fingerprint: " + route.CapabilityFingerprint);
    }

    /// <summary>The official catalog publishes a distinct AB candidate without inheriting Single certification.</summary>
    [Fact]
    public void CatalogPublishesAbCandidateFromDefinitionsOnly()
    {
        CanonicalCapabilityCatalog catalog = Catalog();
        var identity = new CapabilityRouteIdentity("NT51929", ExperienceIds.CtrlRamReplace, "1-ic", "nt51929-ab-merge-512k");
        CapabilityRouteResolutionResult resolution = catalog.ResolveDynamicRoute(identity.RouteId);
        Assert.True(resolution.Succeeded, resolution.Issue?.Message);
        ResolvedCapabilityRoute route = resolution.Route!;
        Assert.Equal(CapabilityPublicationStatus.Candidate, route.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, route.Evidence.Value);
        Assert.Equal(CapabilityAuthoringAvailability.Available, route.Authoring.Value);
        Assert.Equal(BankReferenceReplaceDefinition.CompilerSemanticId, route.CompilationContract.CompilerSemanticId);
        ResolvedCapabilityRoute single = catalog.ResolveDynamicRoute(new CapabilityRouteIdentity("NT51929", ExperienceIds.CtrlRamReplace,
            "1-ic", "nt51929-ctrlram-fw200-single-full-flash").RouteId).Route!;
        Assert.Equal(CapabilityPublicationStatus.Supported, single.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.DirectGolden, single.Evidence.Value);
        Assert.NotEqual(single.CapabilityFingerprint, route.CapabilityFingerprint);
    }

    /// <summary>The real compiler parents bind to the artifact-free registry definition for every bank selection.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ActualCompilationMatchesOfficialDefinition(bool a, bool b)
    {
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("ab-merge", "NT51929", "expected-output", "t05-d06"));
        CompiledComposition compiled = AbCtrlRamRuntimeWiringTests.Compile(reference, a, b, editVersions: true);
        BankReplaceRouteBinding binding = CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51929", "1-ic")!;
        ResolvedCapabilityRoute route = Catalog().ResolveDynamicRoute(binding.Identity.RouteId).Route!;
        ResolvedCapability capability = route.BindCompilation(compiled, runtimeReferenceProof: AbCtrlRamRuntimeWiringTests.Proof(compiled));
        Assert.Equal(binding.Definition.ContentHash,
            capability.CompiledComposition.V2Details.Provenance.BankReplaceDefinition!.ContentHash);
        Assert.Equal(CapabilityPublicationStatus.Candidate, capability.Publication.Value);
        Assert.NotNull(capability.RuntimeReferenceProof);
    }

    /// <summary>Wrong parent catalogs fail; every parent hash remains part of the stable definition identity.</summary>
    [Fact]
    public void MissingParentAndChangedIdentityNeverReuseAnAdmission()
    {
        TrustedProfileBundleCatalog ab = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51919-nt51929-nt51932-ab-merge", "892af5d0f1ff0094bb96a0e30ffad3b6c2cf18451a6705623c2ca97206422c6b");
        BankReplaceRouteBinding binding = CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51929", "1-ic")!;
        BankReferenceReplaceDefinition definition = binding.Definition;
        _ = Assert.Throws<ArgumentException>(() => ab.CreateBankReplaceDefinition(ab,
            "NT51929", definition.Layout.ProfileId, definition.Layout.ProfileVersion, definition.Layout.MapId,
            definition.Local.ProfileId, definition.Local.ProfileVersion, definition.Local.MapId));
        TrustedProfileBundleCatalog local32 = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51932-ctrlram-replace-candidate",
            CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51932", "1-ic")!.Definition.Local.Bundle.ContentHash);
        BankReferenceReplaceDefinition otherMember = CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51932", "1-ic")!.Definition;
        Assert.Null(ab.TryCreateBankReplaceAdmission(local32, "NT51929", "NT51932",
            definition.Layout.ProfileId, definition.Layout.ProfileVersion, definition.Layout.MapId,
            otherMember.Local.ProfileId, otherMember.Local.ProfileVersion, otherMember.Local.MapId));
        Assert.Null(ab.TryCreateBankReplaceAdmission(ab, "NT51929", "NT51929",
            definition.Layout.ProfileId, definition.Layout.ProfileVersion, definition.Layout.MapId,
            definition.Layout.ProfileId, definition.Layout.ProfileVersion, definition.Layout.MapId));
        TrustedProfileBundleCatalog local29 = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            binding.Local.Route.BundleId, definition.Local.Bundle.ContentHash);
        BankReferenceReplaceDefinition cascade = CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51929", "2-8-ic")!.Definition;
        _ = Assert.Throws<ArgumentException>(() => ab.CreateBankReplaceDefinition(local29,
            "NT51929", definition.Layout.ProfileId, definition.Layout.ProfileVersion, definition.Layout.MapId,
            definition.Local.ProfileId, definition.Local.ProfileVersion, cascade.Local.MapId));
        BankReferenceDefinitionSource local = definition.Local;
        var changed = new BankReferenceDefinitionSource(local.ProfileId, local.ProfileVersion, local.Bundle,
            new ProfileBundleEntryIdentity(local.Entry.EntryId, new string('a', 64)), local.FamilyHash, local.MemberId, local.MapId, local.CapacityBytes);
        Assert.NotEqual(definition.ContentHash, new BankReferenceReplaceDefinition(definition.Layout, changed).ContentHash);
    }

    /// <summary>A missing or ambiguous local definition reports a failed reload and retains the last good publication.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrokenParentReportsTypedReloadFailureWithoutFallback(bool duplicate)
    {
        CanonicalCtrlRamDefinition[] definitions = [.. CtrlRamV2RouteRegistry.All.SelectMany(CanonicalDynamicRouteInventory.CreateCtrlRamDefinitions)];
        var catalog = new CanonicalCapabilityCatalog(new CanonicalCapabilityCatalogSource(
            BuiltInCanonicalCapabilityPolicy.Load, CanonicalDynamicRouteInventory.IsDynamic, CanonicalCompiledRouteInventory.Resolve,
            () => CanonicalDynamicRouteInventory.CreateResolver(() => definitions),
            CanonicalCapabilityDisclosureInventory.Create, CanonicalFullImageMetadataInventory.Create));
        CapabilityCatalogReloadResult first = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded);
        CanonicalCtrlRamDefinition parent = definitions.Single(static definition => definition.Route.ProfileId == "nt51929-ctrlram-replace-fw200-single");
        definitions = duplicate ? [.. definitions, parent] : [.. definitions.Where(candidate => !ReferenceEquals(candidate, parent))];
        CapabilityCatalogReloadResult failed = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.False(failed.Succeeded);
        Assert.True(failed.RetainedLastKnownGood);
        Assert.Equal(CapabilityCatalogIssueCodes.SourceInvalid, Assert.Single(failed.Issues).Code);
        Assert.Same(first.Snapshot, catalog.GetCurrentSnapshot());
    }

    private static CanonicalCapabilityCatalog Catalog()
    {
        var catalog = new CanonicalCapabilityCatalog(new CanonicalCapabilityCatalogSource(
            BuiltInCanonicalCapabilityPolicy.Load, CanonicalDynamicRouteInventory.IsDynamic,
            CanonicalCompiledRouteInventory.Resolve, CanonicalDynamicRouteInventory.CreateResolver,
            CanonicalCapabilityDisclosureInventory.Create, CanonicalFullImageMetadataInventory.Create));
        CapabilityCatalogReloadResult load = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, string.Join("; ", load.Issues.Select(static issue => issue.Message)));
        return catalog;
    }
}
