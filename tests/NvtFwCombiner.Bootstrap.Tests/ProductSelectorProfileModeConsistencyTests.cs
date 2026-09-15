using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks every released selector token to a compatible compiled policy.</summary>
public sealed class ProductSelectorProfileModeConsistencyTests
{
    /// <summary>All operator-visible 1.0.0 modes consume only workflow-scoped selector facts.</summary>
    [Fact]
    public void ProductAuthoringSelectorTokensMatchTheirCompiledWorkflowPolicies()
    {
        CanonicalTestContext host = BootstrapTestHost.ProductCanonical;
        CanonicalCapabilityCatalogSnapshot snapshot = host.Catalog.GetCurrentSnapshot();
        CapabilitySelectorPublication selector = snapshot.SelectorPublication;
        Assert.NotEmpty(selector.IcIds);

        foreach (string icId in selector.IcIds)
        {
            IReadOnlyList<string> workflows = selector.GetAuthorableWorkflowIds(icId);
            Assert.DoesNotContain(ExperienceIds.DpReplace, workflows);
            if (workflows.Contains(ExperienceIds.StandardMerge, StringComparer.Ordinal))
            {
                AssertSelectorFreeMerge(snapshot, icId, ExperienceIds.StandardMerge);
            }
            if (workflows.Contains(ExperienceIds.GeneralMerge, StringComparer.Ordinal))
            {
                AssertSelectorFreeMerge(snapshot, icId, ExperienceIds.GeneralMerge);
            }
            if (workflows.Contains(ExperienceIds.AbMerge, StringComparer.Ordinal))
            {
                AssertAbChoices(host, snapshot, selector, icId);
            }
            if (workflows.Contains(ExperienceIds.CtrlRamReplace, StringComparer.Ordinal))
            {
                AssertCtrlRamChoices(host, snapshot, selector, icId);
            }
            if (workflows.Contains(ExperienceIds.GeneralReplace, StringComparer.Ordinal))
            {
                AssertCompiledReplaceChoices(snapshot, selector, icId, ExperienceIds.GeneralReplace);
            }
        }
    }

    private static void AssertSelectorFreeMerge(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string icId,
        string workflowId)
    {
        ResolvedCapability[] capabilities = AuthorableCapabilities(snapshot, icId, workflowId);
        Assert.All(
            capabilities,
            static capability => Assert.Null(
                capability.CompiledComposition.V2Details.IcNumberInputMode));
    }

    private static void AssertAbChoices(
        CanonicalTestContext host,
        CanonicalCapabilityCatalogSnapshot snapshot,
        CapabilitySelectorPublication selector,
        string icId)
    {
        _ = AuthorableCapabilities(snapshot, icId, ExperienceIds.AbMerge);
        IReadOnlyList<CapabilityTopologyChoice> choices = selector.GetAbMergeTopologyChoices(icId);
        // The accepted AB contract has two NT51950 axes; other AB ICs are
        // selector-free. Missing published choices must not pass vacuously.
        string[] expectedTokens = icId == "NT51950" ? ["cascade", "single"] : [];
        Assert.Equal(expectedTokens,
            choices.Select(static choice => choice.Token).Order(StringComparer.Ordinal));
        foreach (CapabilityTopologyChoice choice in choices)
        {
            // One selector choice can now disclose more than one format-specific AB route.
            // Compile every exact publication rather than reviving the intentionally ambiguous
            // IC-plus-topology convenience lookup.
            ResolvedCapabilityRoute[] routes =
            [
                .. snapshot.DynamicRoutes.Where(route =>
                    route.Identity.IcId == icId && route.Identity.WorkflowId == ExperienceIds.AbMerge &&
                    route.Authoring.Value == CapabilityAuthoringAvailability.Available &&
                    route.AbMergeTopologyChoice == choice),
            ];
            Assert.NotEmpty(routes);
            foreach (ResolvedCapabilityRoute route in routes)
            {
                Assert.True(host.Compiler.TryCompilePublishedDynamicCapability(route.Identity, null, null,
                    out CompiledComposition? composition, out _, out IReadOnlyList<CompositionIssue> issues,
                    route.AbMergeTopologyChoice!.Selection),
                    string.Join(',', issues.Select(static issue => issue.Code)));
                CompiledComposition compiled = Assert.IsType<CompiledComposition>(composition);
                Assert.True(compiled.V2Details.Provenance.ResolvedMap.ImageMap.Applicability
                    .TopologyRequirement.Matches(choice.Selection));
                Assert.Null(compiled.V2Details.IcNumberInputMode);
            }
        }
    }

    private static void AssertCtrlRamChoices(
        CanonicalTestContext host,
        CanonicalCapabilityCatalogSnapshot snapshot,
        CapabilitySelectorPublication selector,
        string icId)
    {
        IReadOnlyList<CapabilityNumberChoice> choices = selector.GetNumberSelectionChoices(
            icId,
            ExperienceIds.CtrlRamReplace);
        Assert.NotEmpty(choices);
        ResolvedCapability[] capabilities = AuthorableCapabilities(
            snapshot,
            icId,
            ExperienceIds.CtrlRamReplace);
        foreach (ResolvedCapability capability in capabilities)
        {
            Assert.Contains(
                choices,
                choice => IcNumberSelection.FromToken(choice.Token).Mode ==
                    capability.CompiledComposition.V2Details.IcNumberInputMode);
        }
        foreach (CapabilityNumberChoice choice in choices)
        {
            IcNumberSelection selection = IcNumberSelection.FromToken(choice.Token);
            Assert.True(IcNumberChoicePolicy.IsNumberSelectionSupported(
                selection,
                BuiltInPostbuildProfileCatalog.GetProfiles(icId)));
            CtrlRamInspectionDisplay display = host.CtrlRamAuthoring.GetDiscoveryDisplay(
                icId,
                choice.Token);
            Assert.Equal(choice.Token, display.NumberToken);
        }
    }

    private static void AssertCompiledReplaceChoices(
        CanonicalCapabilityCatalogSnapshot snapshot,
        CapabilitySelectorPublication selector,
        string icId,
        string workflowId)
    {
        IReadOnlyList<CapabilityNumberChoice> choices =
            selector.GetNumberSelectionChoices(icId, workflowId);
        Assert.NotEmpty(choices);
        _ = AuthorableCapabilities(snapshot, icId, workflowId);
        ResolvedCapabilityRoute[] dynamicRoutes =
        [
            .. snapshot.DynamicRoutes
                .Where(route =>
                    StringComparer.Ordinal.Equals(route.Identity.IcId, icId) &&
                    StringComparer.Ordinal.Equals(route.Identity.WorkflowId, workflowId) &&
                    route.Authoring.Value == CapabilityAuthoringAvailability.Available)
        ];
        Assert.NotEmpty(dynamicRoutes);
        foreach (ResolvedCapabilityRoute route in dynamicRoutes)
        {
            CapabilityNumberChoice choice = Assert.IsType<CapabilityNumberChoice>(
                route.NumberChoice);
            Assert.Contains(choice, choices);
        }
        foreach (CapabilityNumberChoice choice in choices)
        {
            Assert.Contains(dynamicRoutes, route => route.NumberChoice == choice);
        }
    }

    private static ResolvedCapability[] AuthorableCapabilities(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string icId,
        string workflowId)
    {
        ResolvedCapability[] capabilities =
        [
            .. snapshot.Capabilities.Where(capability =>
                StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(capability.Identity.WorkflowId, workflowId) &&
                capability.Authoring.Value == CapabilityAuthoringAvailability.Available),
        ];
        bool hasDynamic = snapshot.DynamicRoutes.Any(route =>
            StringComparer.Ordinal.Equals(route.Identity.IcId, icId) &&
            StringComparer.Ordinal.Equals(route.Identity.WorkflowId, workflowId) &&
            route.Authoring.Value == CapabilityAuthoringAvailability.Available);
        Assert.True(capabilities.Length > 0 || hasDynamic, $"{icId}/{workflowId} has no authorable route.");
        return capabilities;
    }

}
