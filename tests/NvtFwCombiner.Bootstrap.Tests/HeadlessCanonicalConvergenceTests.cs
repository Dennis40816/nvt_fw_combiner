using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Pins the executable headless route denominator during canonical convergence.</summary>
public sealed class HeadlessCanonicalConvergenceTests
{
    /// <summary>The exact route inventory must remain materializable before it can be migrated.</summary>
    [Fact]
    public void CurrentSupportMatrixMaterializesExactRouteInventory()
    {
        var catalog = new CanonicalCapabilityCatalog(
            new CanonicalCapabilityCatalogMigrationSource());
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        Assert.True(
            reload.Succeeded,
            string.Join("; ", reload.Issues.Select(static issue => issue.Message)));

        string[] expected =
        [
            .. CurrentSupportMatrixCatalog.Create().Rows
                .Where(static row => row.Route.ExecutionAdmitted)
                .Select(static row => FormatAxes(
                    row.Route.Identity.IcId,
                    row.Route.Identity.WorkflowId,
                    row.Route.Identity.IcCountVariant,
                    row.Route.Identity.MapVariant))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        string[] actual =
        [
            .. reload.Snapshot!.Capabilities
                .Select(static capability => capability.Identity)
                .Concat(reload.Snapshot.DynamicRoutes.Select(
                    static route => route.Identity))
                .Select(static identity => FormatAxes(
                    identity.IcId,
                    identity.WorkflowId,
                    identity.IcCountVariant,
                    identity.MapVariant))
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"Expected {expected.Length} admitted routes but canonical catalog has {actual.Length}." +
            Environment.NewLine +
            string.Join(Environment.NewLine, expected));
    }

    private static string FormatAxes(
        string icId,
        string workflowId,
        string icCountVariant,
        string mapVariant)
    {
        return $"{icId}|{workflowId}|{icCountVariant}|{mapVariant}";
    }
}
