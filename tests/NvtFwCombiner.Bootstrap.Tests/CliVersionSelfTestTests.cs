using NvtFwCombiner.Application.VersionManagement;

using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Protects the CLI wrapper over the exact Version-page self-test use case.</summary>
public sealed class CliVersionSelfTestTests
{
    /// <summary>The command reaches the production Registry adapter and preserves its typed issue.</summary>
    [Fact]
    public async Task VersionSelfTestUsesProductionRegistryAdapter()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["version-self-test", "--registry", "http://unsafe.example/registry.json"],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.True(GoldenDiagnostics.Contains("Registry: UnsafeLocator", result.Output, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
        Assert.True(GoldenDiagnostics.Contains(
            "Registry replica 1: Role=Primary; Issue=UnsafeLocator; Revision=; Selected=False",
            result.Output,
            StringComparison.Ordinal), "Captured text did not match the expected marker check.");
        Assert.True(StringComparer.Ordinal.Equals(string.Empty, result.Error), "Captured text differs from the expected text.");
        Assert.True(!GoldenDiagnostics.Contains("unsafe.example", result.Output, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
    }

    /// <summary>The adapter prints typed admission results without exposing private source roots.</summary>
    [Fact]
    public async Task VersionSelfTestPrintsTypedAdmissionWithoutPrivatePaths()
    {
        const string privateRoot = @"G:\private\packages";
        var selfTest = new VersionEnvironmentSelfTestResult(
            UpdateSourceRegistryLoadIssue.None,
            [
                new VersionEnvironmentSelfTestAttempt(
                    privateRoot,
                    UpdateSourceRegistryEntryStatus.Latest,
                    UpdateCatalogLoadIssue.None,
                    ManagedVersionInstallIssue.None,
                    ManagedAppVersion.Parse("1.0.1"),
                    isVerified: true),
            ],
            [
                new(1, UpdateSourceRegistryLoadIssue.None, 8, IsSelected: true),
                new(2, UpdateSourceRegistryLoadIssue.None, 7, IsSelected: false),
            ],
            acceptedRegistryRevision: 8);
        using var output = new StringWriter();

        int exitCode = await CliApplication.RunVersionEnvironmentSelfTestAsync(
            _ => ValueTask.FromResult(selfTest),
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.True(GoldenDiagnostics.Contains("Registry: None", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("Accepted Registry revision: 8", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains(
            "Role=Primary; Issue=None; Revision=8; Selected=True",
            output.ToString(),
            StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains(
            "Role=Backup 1; Issue=None; Revision=7; Selected=False",
            output.ToString(),
            StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains(
            "Status=Latest; Catalog=None; Package=None; Newest=1.0.1; Verified=True",
            output.ToString(),
            StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.False(GoldenDiagnostics.Contains(privateRoot, output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
    }

    /// <summary>Self-test rejects a selected replica below durable anti-rollback authority.</summary>
    [Fact]
    public async Task VersionSelfTestReturnsFailureForRegistryRollback()
    {
        var selfTest = new VersionEnvironmentSelfTestResult(
            UpdateSourceRegistryLoadIssue.None,
            [],
            [new(1, UpdateSourceRegistryLoadIssue.None, 7, IsSelected: true)],
            UpdateSourceRegistryIssue.RevisionRollback,
            acceptedRegistryRevision: 8);
        using var output = new StringWriter();

        int exitCode = await CliApplication.RunVersionEnvironmentSelfTestAsync(
            _ => ValueTask.FromResult(selfTest),
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.True(GoldenDiagnostics.Contains("Registry authority: RevisionRollback", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
        Assert.True(GoldenDiagnostics.Contains("Accepted Registry revision: 8", output.ToString(), StringComparison.Ordinal), GoldenDiagnostics.TextSummary(output.ToString()));
    }

    /// <summary>Malformed command arguments fail before any network or filesystem access.</summary>
    [Fact]
    public async Task VersionSelfTestRejectsMalformedArguments()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["version-self-test", "--unknown"],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.True(StringComparer.Ordinal.Equals(string.Empty, result.Output), "Captured text differs from the expected text.");
        Assert.True(GoldenDiagnostics.Contains("usage: nvt_fw_combiner version-self-test", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
    }
}
