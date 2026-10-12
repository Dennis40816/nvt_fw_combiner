using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Protects the CLI adapter over the shared typed System Information contract.</summary>
public sealed class CliSystemDiagnosticsTests
{
    /// <summary>The CLI renders the shared fallback warning without turning JSON admission into a Build failure.</summary>
    [Fact]
    public async Task DoctorReportsTypedJsonAdmissionWarningWithoutFailureExit()
    {
        using var output = new StringWriter();
        SystemInformationService service = CreateService(CanonicalSupportMatrixCatalogState.Current,
            new CanonicalSupportMatrixSnapshot("test", "1", new string('a', 64), new ResolutionToken("test"), []),
            new AdmissionStatus());
        int exit = await CliApplication.RunDoctorAsync(service, output, TestContext.Current.CancellationToken);
        Assert.Equal(0, exit);
        Assert.True(GoldenDiagnostics.Contains("JSON admission is in use", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("restart the application", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
    }

    private sealed class AdmissionStatus : IBuiltInProfileAdmissionStatus
    {
        public BuiltInProfileAdmission? Current { get; } = new(BuiltInProfileAdmissionSource.Json, BuiltInProfileAdmissionRejectionReason.Format);
    }

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
            static () => IsolatedLocalState.CreateDirectory("cli-doctor"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.True(GoldenDiagnostics.Contains("Catalog state: Current", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("CLI assembly version:", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.False(GoldenDiagnostics.Contains("repository bootstrap is healthy", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(string.Equals(string.Empty, error.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(error.ToString()));
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
        Assert.True(GoldenDiagnostics.Contains("Catalog state: ColdStartBlocked", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("The capability catalog is unavailable", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("Correct the catalog source and reload", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
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
        Assert.True(GoldenDiagnostics.Contains("Catalog state: LastKnownGood", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("last-known-good publication remains active", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("Review the catalog source and reload", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
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
            static () => IsolatedLocalState.CreateDirectory("cli-doctor"),
            cancellation.Token);

        Assert.Equal(70, exitCode);
        Assert.True(string.Equals(string.Empty, output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("operation canceled", error.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(error.ToString()));
    }

    private static SystemInformationService CreateService(
        CanonicalSupportMatrixCatalogState state,
        CanonicalSupportMatrixSnapshot? matrix,
        IBuiltInProfileAdmissionStatus? admissionStatus = null)
    {
        var catalog = new StubCatalog(state, matrix);
        return new SystemInformationService(
            "0.10.3-test",
            catalog,
            catalog,
            new ExternalProcessorEnvironmentLoader(static (_, _) =>
                throw new NotSupportedException()),
            new StubRuntimeProbe(),
            new StubClock(), admissionStatus: admissionStatus);
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
