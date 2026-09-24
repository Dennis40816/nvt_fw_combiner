using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Real trusted declarations can be inspected before compiling any output.</summary>
public sealed class AbMergeAuthoringDefinitionTests
{
    /// <summary>Profile IO during exact lookup must not escape the declaration query's typed boundary.</summary>
    [Fact]
    public void MissingBundleDuringRegistrationLookupReturnsIssueWithoutPartialDefinition()
    {
        using var workspace = TempWorkspace.Create("ab-declaration-missing-bundle");
        var bundle = new BuiltInV2Bundle(workspace.Root, "1.0.0", new string('a', 64), "test-anchor");
        var registration = new BuiltInV2Registration("NT51950", "missing-profile", "1.0.0", "missing-map-set",
            bundle, CompositionKind.Merge, ExperienceIds.AbMerge);
        var adapter = new BuiltInV2DynamicCompilationAdapter((ic, maps) =>
            BuiltInV2RegistrationRegistry.FindAbMergeRegistration([registration], ic, maps));
        Assert.False(adapter.TryGetAbAuthoringDefinition(
            new CapabilityRouteIdentity("NT51950", ExperienceIds.AbMerge, "1-ic", "missing-map-set"),
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues));
        Assert.Null(definition);
        Assert.Equal("profile.v2.builtin-bundle-load-failed", Assert.Single(issues).Code);
    }

    /// <summary>Declaration membership matches real normal compilation without projecting output geometry.</summary>
    [Theory]
    [InlineData("NT51950", "1-ic", "nt51950-ab-merge-maps", true)]
    [InlineData("NT51950", "2-plus-ic", "nt51950-ab-cascade-maps", true)]
    [InlineData("NT51951", "selector-free", "nt51951-ab-merge-1024k", true)]
    [InlineData("NT51929", "selector-free", "nt51929-ab-merge-512k", false)]
    public void TrustedDeclarationMatchesCompiledInputMembership(string ic, string count, string maps, bool hasPolicy)
    {
        var catalog = new CanonicalCapabilityCatalog(CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        ResolvedCapabilityRoute route = Assert.Single(catalog.GetCurrentSnapshot().DynamicRoutes,
            candidate => candidate.Identity.IcId == ic && candidate.Identity.WorkflowId == ExperienceIds.AbMerge &&
                candidate.Identity.IcCountVariant == count && candidate.Identity.MapVariant == maps);
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, new BuiltInV2DynamicCompilationAdapter());
        Assert.True(compiler.TryGetAbAuthoringDefinition(route, out CanonicalAbAuthoringDefinition? declaration,
            out IReadOnlyList<CompositionIssue> issues));
        Assert.Empty(issues);
        Assert.NotNull(declaration);
        Assert.Equal(route.CompilationContract.ProfileId, declaration.ProfileId);
        Assert.Equal(route.CompilationContract.ProfileVersion, declaration.ProfileVersion);
        Assert.Equal(route.CompilationContract.TrustedDefinitionSha256, declaration.BundleContentHash);
        Assert.All(declaration.InputBindings, binding =>
        {
            Assert.Null(binding.RequiredEndExclusive);
            Assert.Null(binding.ExpectedOuterLengths);
        });
        AuthoringCapabilityCatalogSnapshot discovery = AuthoringCapabilityCatalogSnapshot.FromDynamicRoute(route,
            declaration.InputBindings.Select(binding => binding.SlotId));
        Assert.Null(Assert.Single(discovery.Routes).ExactCapability);
        Assert.False(discovery.Routes[0].ExecutionAdmitted);
        Assert.Equal(hasPolicy, declaration.TpAInputBinding is not null);
        Assert.Equal(hasPolicy, declaration.TpBInputBinding is not null);

        Assert.True(compiler.TryCompilePublishedDynamicCapability(route.Identity, null, null,
            out CompiledComposition? normal, out _, out issues, route.AbMergeTopologyChoice?.Selection));
        Assert.Empty(issues);
        Assert.NotNull(normal);
        Assert.Equal(normal.V2Details.InputContract.SpaceBindings.Select(binding => (binding.SlotId, binding.AddressSpaceId)).Order(),
            declaration.InputBindings.Select(binding => (binding.SlotId, binding.AddressSpaceId)).Order());
        Assert.Equal(route.CompilationContract.SemanticBindingIds, declaration.SelectionGroupMemberSlotIds);
    }

    /// <summary>Candidate declarations are available without pretending a new executable route is published.</summary>
    [Theory]
    [InlineData("nt51950-ab-merge-desay", "0.2.1")]
    [InlineData("nt51951-ab-merge-desay", "0.2.1")]
    [InlineData("nt51950-ab-merge-cascade", "0.4.0")]
    public void CandidateDeclarationNeedsNoExecutableMap(string profileId, string profileVersion)
    {
        var bundle = new BuiltInV2Bundle("nt51950-ab-merge", "1.1.10-ab-dp-envelope.1",
            "18b43352606ca744f499e328d5778c3b9e08307a97fd122ac38fd8762d37c8d1",
            "built-in-profile-bundle-v2");
        bool loaded = bundle.TryGetAbAuthoringDefinition(profileId, profileVersion,
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues);
        Assert.True(loaded, string.Join(" | ", issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        Assert.NotNull(definition);
        Assert.Equal(profileId, definition.ProfileId);
        Assert.NotNull(definition.TpAInputBinding);
        Assert.NotNull(definition.TpBInputBinding);
        Assert.NotEmpty(definition.SelectionGroupMemberSlotIds);
        Assert.All(definition.InputBindings, binding => Assert.Null(binding.RequiredEndExclusive));
    }

    /// <summary>A declared primary policy cannot silently become a policy-free legacy profile.</summary>
    [Fact]
    public void PolicyWithMissingPrimaryProfileBindingIsRejected()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", "nt51950-ab-merge", "profiles", "nt51950-ab-merge.json");
        CompositionProfileDocument document = JsonSerializer.Deserialize(File.ReadAllBytes(path),
            ProfileBundleSemanticJsonContext.Default.CompositionProfileDocument)!;
        document = document with
        {
            MetadataBindings = [.. document.MetadataBindings.Where(binding => binding.StructureId != "tp-b-primary-firmware-config")],
        };
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps")!;
        CompositionProfileDefinition profile = CompositionProfileNormalizer.Normalize(document);
        _ = Assert.Throws<InvalidOperationException>(() => BuiltInV2Bundle.ProjectAbAuthoringDefinition(
            profile, registration.GetFirmwareFamily(), registration.BundleContentHash));
    }

    /// <summary>The actual normalized profile projection joins metadata space to slot, not a naming coincidence.</summary>
    [Fact]
    public void PrimaryProjectionFollowsProfileSpaceToDifferentlyNamedSlot()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", "nt51950-ab-merge", "profiles", "nt51950-ab-merge.json");
        CompositionProfileDocument document = JsonSerializer.Deserialize(File.ReadAllBytes(path),
            ProfileBundleSemanticJsonContext.Default.CompositionProfileDocument)!;
        document = document with
        {
            InputSlots = [.. document.InputSlots.Select(slot => slot.SlotId == "tp-a-input" ? slot with { SlotId = "selected-a" } : slot)],
            Spaces = [.. document.Spaces.Select(space => space.SlotId == "tp-a-input" ? space with { SlotId = "selected-a" } : space)],
        };
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps")!;
        CanonicalAbAuthoringDefinition definition = BuiltInV2Bundle.ProjectAbAuthoringDefinition(
            CompositionProfileNormalizer.Normalize(document), registration.GetFirmwareFamily(), registration.BundleContentHash);
        Assert.NotNull(definition.TpAInputBinding);
        Assert.Equal("selected-a", definition.TpAInputBinding.SlotId);
        Assert.Equal("tp-a-input", definition.TpAInputBinding.AddressSpaceId);
        Assert.Same(Assert.Single(definition.InputBindings, binding => binding.SlotId == "selected-a"), definition.TpAInputBinding);
    }
}
