using System.Reflection;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks external production Registry-locator precedence without validating paths twice.</summary>
public sealed class UpdateSourceRegistryLocatorTests
{
    /// <summary>The release default is the ordered owner-approved filesystem replica pair.</summary>
    [Fact]
    public void ProductionDefaultsMatchApprovedPrimaryAndBackupRegistryFiles()
    {
        Assert.Equal(
            [
                @"G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-source-registry.json",
                @"G:\AUTO\Tool\NVT_FW_Combiner\update-source-registry.json",
            ],
            UpdateSourceRegistryLocator.ProductionDefaults);
    }

    /// <summary>An explicit host option is one diagnostic locator and avoids inherited/default authority.</summary>
    [Theory]
    [InlineData(@"G:\fixed\registry.json")]
    [InlineData(@"relative\must-fail-in-the-registry-adapter.json")]
    [InlineData("")]
    public void ExplicitLocatorAlwaysWins(string explicitLocator)
    {
        int reads = 0;
        IReadOnlyList<string> resolved = UpdateSourceRegistryLocator.ResolveAll(
            explicitLocatorSupplied: true,
            explicitLocator,
            _ =>
            {
                reads++;
                return @"G:\environment\registry.json";
            });

        Assert.Equal([explicitLocator], resolved);
        Assert.Equal(0, reads);
    }

    /// <summary>The external environment overrides the release pair only when configured.</summary>
    [Theory]
    [InlineData(@"G:\external\registry.json")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EnvironmentIsOneOptionalOverride(string? configured)
    {
        string? observedName = null;
        IReadOnlyList<string> resolved = UpdateSourceRegistryLocator.ResolveAll(
            explicitLocatorSupplied: false,
            explicitLocator: null,
            name =>
            {
                observedName = name;
                return configured;
            });

        Assert.Equal(UpdateSourceRegistryLocator.EnvironmentVariableName, observedName);
        Assert.Equal(
            string.IsNullOrWhiteSpace(configured)
                ? UpdateSourceRegistryLocator.ProductionDefaults
                : [configured],
            resolved);
    }

    /// <summary>An impossible explicit-null host state fails before another authority is consulted.</summary>
    [Fact]
    public void ExplicitNullStateFailsClosed()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            UpdateSourceRegistryLocator.ResolveAll(
                explicitLocatorSupplied: true,
                explicitLocator: null,
                _ => @"G:\environment\registry.json"));
    }

    /// <summary>A single explicit locator cannot bypass the bounded physical-read wrapper.</summary>
    [Fact]
    public void SingleLocatorCompositionUsesReplicatedTimeoutBoundary()
    {
        using VersionManagementExperience experience =
            Assert.IsType<VersionManagementExperience>(
                CompositionHostServices.CreateVersionManagementExperience(
                    "1.0.0",
                    managedRoot: Path.Combine(Path.GetTempPath(), "nfc-managed-composition-probe"),
                    statePath: Path.Combine(Path.GetTempPath(), "nfc-state-composition-probe.json"),
                    updateSourceRegistryPaths: [@"G:\blocked\update-source-registry.json"]));
        FieldInfo registryField = typeof(VersionManagementExperience).GetField(
            "_sourceRegistry",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Version Registry composition field is missing.");

        _ = Assert.IsType<ReplicatedUpdateSourceRegistry>(registryField.GetValue(experience));
    }
}
