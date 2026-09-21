using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Production registry admission needs definitions only, never firmware fixtures.</summary>
public sealed class AbCtrlRamRegistryTests(ITestOutputHelper output)
{
    /// <summary>Definition expansion is deterministic and exposes only genuine trusted parent identities.</summary>
    [Fact]
    public void DefinitionExpansionRequiresNoFirmwareArtifact()
    {
        BankReferenceReplaceDefinition definition = CanonicalDynamicRouteInventory.CreateBankReplaceDefinition();
        CapabilityRouteIdentity identity = CanonicalDynamicRouteInventory.BankReplaceIdentity;
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
        ResolvedCapabilityRoute route = Catalog().ResolveDynamicRoute(CanonicalDynamicRouteInventory.BankReplaceIdentity.RouteId).Route!;
        ResolvedCapability capability = route.BindCompilation(compiled, runtimeReferenceProof: AbCtrlRamRuntimeWiringTests.Proof(compiled));
        Assert.Equal(CanonicalDynamicRouteInventory.CreateBankReplaceDefinition().ContentHash,
            capability.CompiledComposition.V2Details.Provenance.BankReplaceDefinition!.ContentHash);
        Assert.Equal(CapabilityPublicationStatus.Candidate, capability.Publication.Value);
        Assert.NotNull(capability.RuntimeReferenceProof);
    }

    /// <summary>Wrong parent catalogs fail; every parent hash remains part of the stable definition identity.</summary>
    [Fact]
    public void MissingParentAndChangedIdentityNeverReuseAnAdmission()
    {
        TrustedProfileBundleCatalog ab = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51919-nt51929-nt51932-ab-merge", "5acf2fd4d0757d7b757bf7491ff2f268d07cf70a76588f528d36b616e1e5eed0");
        _ = Assert.Throws<ArgumentException>(() => ab.CreateBankReplaceDefinition(ab));
        BankReferenceReplaceDefinition definition = CanonicalDynamicRouteInventory.CreateBankReplaceDefinition();
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
