using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Pins the executable headless route denominator during canonical convergence.</summary>
public sealed class HeadlessCanonicalConvergenceTests
{
    /// <summary>The exact route inventory must remain materializable before it can be migrated.</summary>
    [Fact]
    public void CurrentSupportMatrixMaterializesExactRouteInventory()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        Assert.True(
            reload.Succeeded,
            string.Join("; ", reload.Issues.Select(static issue => issue.Message)));

        string[] expected = ReadFrozenRouteAxes();
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

        (string[] missing, string[] unexpected) = Compare(expected, actual);
        Assert.Equal(89, expected.Length);
        Assert.Equal(expected.Length, expected.Distinct(StringComparer.Ordinal).Count());
        Assert.True(
            missing.Length == 0 && unexpected.Length == 0,
            $"Frozen denominator has {expected.Length} routes; catalog has {actual.Length}." +
            Environment.NewLine +
            $"Missing: {string.Join(", ", missing)}" + Environment.NewLine +
            $"Unexpected: {string.Join(", ", unexpected)}");
    }

    /// <summary>The independent denominator reports one dropped route rather than adapting to it.</summary>
    [Fact]
    public void FrozenRouteInventoryDetectsOneOmission()
    {
        string[] expected = ReadFrozenRouteAxes();
        string[] omitted = [.. expected.Skip(1)];

        (string[] missing, string[] unexpected) = Compare(expected, omitted);

        Assert.Equal([expected[0]], missing);
        Assert.Empty(unexpected);
    }

    private static string[] ReadFrozenRouteAxes()
    {
        return
        [
            .. File.ReadAllLines(RepositoryPaths.FromRepositoryRoot(
                    "tests",
                    "NvtFwCombiner.Bootstrap.Tests",
                    "Fixtures",
                    "canonical-route-axes-v1.txt"))
                .Where(static line =>
                    !string.IsNullOrWhiteSpace(line) &&
                    !line.StartsWith('#'))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static (string[] Missing, string[] Unexpected) Compare(
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        HashSet<string> expectedSet = [.. expected];
        HashSet<string> actualSet = [.. actual];
        return (
            [.. expectedSet.Except(actualSet).Order(StringComparer.Ordinal)],
            [.. actualSet.Except(expectedSet).Order(StringComparer.Ordinal)]);
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
