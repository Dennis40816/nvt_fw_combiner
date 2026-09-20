using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Explicit trusted family facts survive retirement of a workflow without granting runtime authority.</summary>
public sealed class CanonicalFamilyDisclosureSourceTests
{
    private static readonly string[] PerfectMembers = ["NT51919", "NT51929", "NT51932"];
    private const string Provider = "nt51919-nt51929-nt51932-shared-facts";

    /// <summary>Removing real DP definitions preserves the same Perfect summary and every runtime projection.</summary>
    [Fact]
    public void ExplicitDisclosurePreservesPerfectWithoutDpDefinitions()
    {
        CanonicalCapabilityDisclosure? baseline = null;
        CanonicalCapabilityCatalogSource source = CreateSource((definitions, dynamicDefinitions) =>
        {
            Assert.Contains(definitions, definition => definition.Identity.WorkflowId == ExperienceIds.DpReplace);
            baseline = CanonicalCapabilityDisclosureInventory.Create(definitions, dynamicDefinitions);
            CanonicalCapabilityDefinition[] withoutDp = [.. definitions.Where(definition => definition.Identity.WorkflowId != ExperienceIds.DpReplace)];
            CanonicalCapabilityDisclosure actual = CanonicalCapabilityDisclosureInventory.Create(withoutDp, dynamicDefinitions);
            foreach (string ic in PerfectMembers)
            {
                Assert.Equal(CapabilityFamilyRelationship.PerfectAlias, actual.GetFamilySummary(ic).Relationship);
                Assert.Equal(baseline.GetFamilySummary(ic), actual.GetFamilySummary(ic));
            }
            foreach (string workflow in new[] { ExperienceIds.StandardMerge, ExperienceIds.AbMerge, ExperienceIds.DpReplace })
            {
                Assert.Equivalent(baseline.GetProfileSummaries(workflow), actual.GetProfileSummaries(workflow), strict: true);
            }
            Assert.Equal(CapabilityFamilyRelationship.Standalone, actual.GetFamilySummary("NT99999").Relationship);
            return actual;
        });
        CapabilityCatalogLoadResult loaded = source.Load(TestContext.Current.CancellationToken);
        Assert.True(loaded.Succeeded);
        Assert.NotNull(baseline);
        CapabilityCatalogLoadResult original = CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(TestContext.Current.CancellationToken);
        Assert.True(original.Succeeded);
        Assert.Equal(original.Candidate!.Definitions.Select(definition => definition.Identity),
            loaded.Candidate!.Definitions.Select(definition => definition.Identity));
        Assert.Equal(original.Candidate.DynamicDefinitions.Select(definition => definition.Identity),
            loaded.Candidate.DynamicDefinitions.Select(definition => definition.Identity));
    }

    /// <summary>Metadata authority alone contributes no disclosure; only the explicitly listed exact family does.</summary>
    [Fact]
    public void MetadataProviderWithoutOptInContributesNoDisclosure()
    {
        ProfileBundlePackageTrustEntry provider = BuiltInV2BundleRegistry.TrustIndex.Bundles.Single(bundle => bundle.BundleDirectory == Provider);
        Assert.Empty(provider.RuntimeRegistrations);
        _ = Assert.Single(provider.MetadataProviderFamilies);
        FirmwareFamilyResolutionDefinition family = Assert.Single(CanonicalCapabilityDisclosureInventory.ResolveDisclosureFamilies([provider]));
        Assert.Equal("nt51929-nt51932", family.FamilyId);
        Assert.Equal("1.3.1", family.FamilyVersion);
        Assert.Equal("d2499758dd19908422f857e5b7a68c24c47ac57961418da82d10dec2f039f3e8", family.FamilyContentHash);
        IReadOnlyList<FirmwareFamilyResolutionDefinition> absent = CanonicalCapabilityDisclosureInventory.ResolveDisclosureFamilies(
            [provider with { FamilyDisclosureFamilies = [] }]);
        Assert.Empty(absent);
        CanonicalCapabilityCatalogSource source = CreateSource((definitions, dynamicDefinitions) =>
        {
            CanonicalCapabilityDefinition[] withoutDp = [.. definitions.Where(definition => definition.Identity.WorkflowId != ExperienceIds.DpReplace)];
            CanonicalCapabilityDisclosure actual = CanonicalCapabilityDisclosureInventory.Create(withoutDp, dynamicDefinitions, absent);
            foreach (string ic in PerfectMembers)
            {
                Assert.NotEqual(CapabilityFamilyRelationship.PerfectAlias, actual.GetFamilySummary(ic).Relationship);
            }
            return actual;
        });
        Assert.True(source.Load(TestContext.Current.CancellationToken).Succeeded);
    }

    /// <summary>Missing or contradictory family facts reject a real reload and retain the previous publication.</summary>
    [Theory]
    [InlineData("wrong-family")]
    [InlineData("wrong-version")]
    [InlineData("wrong-owning-bundle")]
    [InlineData("different-hash")]
    [InlineData("different-reason")]
    [InlineData("different-evidence")]
    [InlineData("overlapping-perfect")]
    public void InvalidDisclosureCandidateRetainsPublishedCatalog(string mutation)
    {
        bool reject = false;
        CanonicalCapabilityCatalogSource source = CreateSource((definitions, dynamicDefinitions) =>
        {
            if (!reject)
            {
                return CanonicalCapabilityDisclosureInventory.Create(definitions, dynamicDefinitions);
            }
            ProfileBundlePackageTrustEntry provider = BuiltInV2BundleRegistry.TrustIndex.Bundles.Single(bundle => bundle.BundleDirectory == Provider);
            if (mutation is "wrong-family" or "wrong-version" or "wrong-owning-bundle")
            {
                provider = provider with
                {
                    BundleDirectory = mutation == "wrong-owning-bundle" ? "nt51927-standard-merge" : provider.BundleDirectory,
                    FamilyDisclosureFamilies = [new(mutation == "wrong-family" ? "absent-family" : "nt51929-nt51932",
                        mutation == "wrong-version" ? "99.0.0" : "1.3.1")],
                };
                return CanonicalCapabilityDisclosureInventory.Create(definitions, dynamicDefinitions,
                    CanonicalCapabilityDisclosureInventory.ResolveDisclosureFamilies([provider]));
            }
            FirmwareFamilyResolutionDefinition family = Assert.Single(CanonicalCapabilityDisclosureInventory.ResolveDisclosureFamilies([provider]));
            PerfectFamilyRelationship perfect = Assert.Single(family.FamilyRelationships.OfType<PerfectFamilyRelationship>());
            FirmwareFamilyRelationship[] relationships = mutation is "different-reason" or "different-evidence"
                ? [new PerfectFamilyRelationship(perfect.RelationshipId, perfect.MemberIds,
                    mutation == "different-reason" ? "Contradictory candidate reason" : perfect.Reason,
                    mutation == "different-evidence" ? ["candidate-evidence"] : perfect.EvidenceRefs)]
                : [.. family.FamilyRelationships];
            var conflicting = new FirmwareFamilyResolutionDefinition(
                mutation == "overlapping-perfect" ? "conflicting-family" : family.FamilyId,
                family.FamilyVersion, mutation == "different-hash" ? new string('a', 64) : family.FamilyContentHash,
                family.ImageMaps, family.MetadataSets, family.CapabilityBindings, relationships, family.AbFormatPolicy);
            return CanonicalCapabilityDisclosureInventory.Create(definitions, dynamicDefinitions, [family, conflicting]);
        });
        var catalog = new CanonicalCapabilityCatalog(source);
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        CanonicalCapabilityCatalogSnapshot published = catalog.GetCurrentSnapshot();
        reject = true;
        CapabilityCatalogReloadResult failed = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.False(failed.Succeeded);
        Assert.Contains(failed.Issues, issue => issue.Code == CapabilityCatalogIssueCodes.SourceInvalid);
        Assert.Same(published, catalog.GetCurrentSnapshot());
        reject = false;
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        Assert.NotSame(published, catalog.GetCurrentSnapshot());
    }

    private static CanonicalCapabilityCatalogSource CreateSource(Func<IReadOnlyList<CanonicalCapabilityDefinition>,
        IReadOnlyList<CanonicalDynamicCapabilityDefinition>, CanonicalCapabilityDisclosure> disclosure)
    {
        return new CanonicalCapabilityCatalogSource(BuiltInCanonicalCapabilityPolicy.Load,
            CanonicalDynamicRouteInventory.IsDynamic, CanonicalCompiledRouteInventory.Resolve,
            CanonicalDynamicRouteInventory.CreateResolver, disclosure, CanonicalFullImageMetadataInventory.Create);
    }
}
