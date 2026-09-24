using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Actual product publication has no retired execution authority.</summary>
public sealed class DpRuntimeRetirementTests
{
    /// <summary>Every current IC rejects the retired route while surviving workflows stay published.</summary>
    [Fact]
    public void ProductCatalogRejectsDpAcrossAllIcsAndPreservesSurvivingRoutes()
    {
        var host = new IsolatedBootstrapTestHost();
        CapabilityCatalogReloadResult reload = host.Catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(reload.Succeeded);
        CanonicalCapabilityCatalogSnapshot snapshot = Assert.IsType<CanonicalCapabilityCatalogSnapshot>(reload.Snapshot);
        Assert.DoesNotContain(snapshot.Capabilities, static capability => capability.Identity.WorkflowId == ExperienceIds.DpReplace);
        Assert.DoesNotContain(snapshot.DynamicRoutes, static route => route.Identity.WorkflowId == ExperienceIds.DpReplace);
        foreach (string icId in snapshot.SelectorPublication.IcIds)
        {
            Assert.False(snapshot.SelectorPublication.IsWorkflowAuthorable(icId, ExperienceIds.DpReplace));
            CapabilityResolutionResult rejected = host.Catalog.ResolveUniqueRoute(icId, ExperienceIds.DpReplace, "1-ic", 0x40000);
            Assert.False(rejected.Succeeded);
            Assert.Null(rejected.Capability);
            Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, rejected.Issue!.Code);
        }
        Assert.True(snapshot.SelectorPublication.IsWorkflowAuthorable("NT51926", ExperienceIds.GeneralReplace));
        Assert.True(snapshot.SelectorPublication.IsWorkflowAuthorable("NT51926", ExperienceIds.CtrlRamReplace));
        Assert.True(snapshot.SelectorPublication.IsWorkflowAuthorable("NT51926", ExperienceIds.StandardMerge));
        Assert.True(snapshot.SelectorPublication.IsWorkflowAuthorable("NT51950", ExperienceIds.AbMerge));
    }
}
