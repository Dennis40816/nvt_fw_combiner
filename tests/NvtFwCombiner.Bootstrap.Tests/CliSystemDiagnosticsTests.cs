using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Protects the CLI adapter over the shared typed System Information contract.</summary>
public sealed class CliSystemDiagnosticsTests
{
    /// <summary>Doctor explicitly reloads the shared catalog and reports its typed lifecycle state.</summary>
    [Fact]
    public async Task DoctorUsesCanonicalSystemInformationLifecycle()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunAsync(
            ["doctor"],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Catalog state: Current", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("CLI assembly version:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("repository bootstrap is healthy", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>A cold typed catalog diagnostic is printed and returns the composition-failed exit.</summary>
    [Fact]
    public async Task DoctorReturnsFailureForTypedColdCatalogBlocker()
    {
        using var output = new StringWriter();
        SystemInformationService service = CreateService(
            CanonicalSupportMatrixCatalogState.ColdStartBlocked,
            matrix: null);

        int exitCode = await CliApplication.RunDoctorAsync(
            service,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Catalog state: ColdStartBlocked", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("The capability catalog is unavailable", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Correct the catalog source and reload", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A retained LKG typed warning is printed without disabling execution.</summary>
    [Fact]
    public async Task DoctorReportsLastKnownGoodWarningWithoutFailureExit()
    {
        using var output = new StringWriter();
        SystemInformationService service = CreateService(
            CanonicalSupportMatrixCatalogState.LastKnownGood,
            new CanonicalSupportMatrixSnapshot(
                "canonical-capability-policy",
                "1.5.0",
                new string('a', 64),
                new ResolutionToken("catalog:cli-lkg"),
                []));

        int exitCode = await CliApplication.RunDoctorAsync(
            service,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Catalog state: LastKnownGood", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("last-known-good publication remains active", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Review the catalog source and reload", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Doctor maps cancellation through the same stable CLI failure contract.</summary>
    [Fact]
    public async Task DoctorCancellationReturnsSoftwareErrorInsteadOfEscaping()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cancellation.Cancel();

        int exitCode = await CliApplication.RunAsync(
            ["doctor"],
            output,
            error,
            cancellation.Token);

        Assert.Equal(70, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("operation canceled", error.ToString(), StringComparison.Ordinal);
    }

    private static SystemInformationService CreateService(
        CanonicalSupportMatrixCatalogState state,
        CanonicalSupportMatrixSnapshot? matrix)
    {
        var catalog = new StubCatalog(state, matrix);
        return new SystemInformationService(
            "0.10.3-test",
            catalog,
            catalog,
            new StubRuntimeProbe(),
            new StubClock());
    }

    private sealed class StubCatalog(
        CanonicalSupportMatrixCatalogState state,
        CanonicalSupportMatrixSnapshot? matrix) :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        public CanonicalSupportMatrixQueryResult Query()
        {
            return new CanonicalSupportMatrixQueryResult(state, matrix, []);
        }

        public void Reload(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class StubRuntimeProbe : ISystemRuntimeProbe
    {
        public SystemRuntimeFacts Probe()
        {
            return new SystemRuntimeFacts(".NET test", "Windows test", "x64");
        }
    }

    private sealed class StubClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }
}
