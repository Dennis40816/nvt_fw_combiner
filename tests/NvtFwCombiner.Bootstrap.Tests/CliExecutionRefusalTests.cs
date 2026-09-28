using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// CLI regressions for an accepted run whose catalog or tool configuration is reloaded after the
/// handler's readiness check and before the run is admitted (owner decision 68): every build route
/// prints the typed pre-run refusal and exits 1, and a plain invariant failure still propagates.
/// </summary>
public sealed class CliExecutionRefusalTests
{
    /// <summary>A runtime reload after the CtrlRAM readiness check ends in a typed refusal, not an unhandled exception.</summary>
    [Fact]
    public async Task CtrlRamReplaceRuntimeReloadAfterReadinessIsATypedRefusal()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-ctrlram");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var execution = new BeforeExecution(host.CompositionExecution, async () =>
            _ = await host.ExternalEnvironmentLoader.LoadToCompletionAsync(
                progress: null,
                TestContext.Current.CancellationToken));
        ReplaceCliCommandTests.Nt51926SelectiveVnRegression fixture =
            ReplaceCliCommandTests.LoadNt51926SelectiveVnRegression();
        string outputPath = workspace.PathFor("output.bin");
        string reportPath = workspace.PathFor("report.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await ReplaceCliCommandHandler.RunAsync(
            CreateServices(host, execution),
            host.LocalFiles,
            "ctrlram-replace",
            [
                "build", "--profile", "NT51926", "--ic-num", "cascade",
                "--base", CanonicalGoldenTestData.ArtifactPath(fixture.BaseArtifact),
                "--ctrlram", $"replace-ctrlram-vn={CanonicalGoldenTestData.ArtifactPath(fixture.VnArtifact)}",
                "--output", outputPath, "--report", reportPath,
            ],
            output,
            error,
            TestContext.Current.CancellationToken);

        AssertTypedRefusal(exitCode, output, error, execution, outputPath, reportPath, ExperienceIds.CtrlRamReplace);
        Assert.Contains("runtime generation changed from", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A tool-configuration change after the CtrlRAM readiness check leaves the environment without a
    /// runtime (generation 0, no processor); the run ends in a typed refusal, not an argument failure.
    /// </summary>
    [Fact]
    public async Task CtrlRamReplaceToolConfigurationChangeAfterReadinessIsATypedRefusal()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-ctrlram-config");
        var toolchain = new PublishingToolchainSession(1);
        CompositionHostServices host = CompositionHostServices.Create(
            new ExternalProcessorEnvironmentLoader(toolchain),
            loadPolicy: null,
            localStateDirectory: IsolatedLocalState.CreateDirectory(),
            toolchainConfiguration: toolchain);
        Assert.True((await host.ExternalEnvironmentLoader.LoadToCompletionAsync(
            progress: null,
            TestContext.Current.CancellationToken)).Succeeded);
        var execution = new BeforeExecution(host.CompositionExecution, () =>
        {
            toolchain.Publish(2);
            Assert.Equal(0, host.ExternalEnvironment.AcquireCurrent().Generation);
            return Task.CompletedTask;
        });
        ReplaceCliCommandTests.Nt51926SelectiveVnRegression fixture =
            ReplaceCliCommandTests.LoadNt51926SelectiveVnRegression();
        string outputPath = workspace.PathFor("output.bin");
        string reportPath = workspace.PathFor("report.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await ReplaceCliCommandHandler.RunAsync(
            CreateServices(host, execution),
            host.LocalFiles,
            "ctrlram-replace",
            [
                "build", "--profile", "NT51926", "--ic-num", "cascade",
                "--base", CanonicalGoldenTestData.ArtifactPath(fixture.BaseArtifact),
                "--ctrlram", $"replace-ctrlram-vn={CanonicalGoldenTestData.ArtifactPath(fixture.VnArtifact)}",
                "--output", outputPath, "--report", reportPath,
            ],
            output,
            error,
            TestContext.Current.CancellationToken);

        AssertTypedRefusal(exitCode, output, error, execution, outputPath, reportPath, ExperienceIds.CtrlRamReplace);
        Assert.Contains("is no longer available", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A catalog reload after the AB readiness check is refused by the AB format admission, an existing
    /// typed pre-run refusal; the narrowed AB catch still prints it in the AB error format with exit 1.
    /// </summary>
    [Fact]
    public async Task AbMergeCatalogReloadAfterReadinessIsATypedRefusal()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-ab");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var execution = new BeforeExecution(host.CompositionExecution, () =>
        {
            Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
            return Task.CompletedTask;
        });
        string outputPath = workspace.PathFor("output.bin");
        string reportPath = workspace.PathFor("report.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await AbMergeCliCommandHandler.RunAsync(
            CreateServices(host, execution),
            host.LocalFiles,
            CreateAbBuildArguments(workspace, outputPath, reportPath),
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.True(exitCode == 1, error + Environment.NewLine + output);
        Assert.Equal(1, execution.Calls);
        Assert.StartsWith("error: AB_FORMAT_SESSION_STALE: ", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(output.ToString());
        Assert.False(File.Exists(outputPath));
        Assert.False(File.Exists(reportPath));
    }

    /// <summary>A plain invariant failure from AB execution propagates instead of becoming exit code 1.</summary>
    [Fact]
    public async Task AbMergeUntypedExecutionFailureIsNotSwallowed()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-ab-untyped");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var injected = new InvalidOperationException("Injected execution invariant failure.");
        var execution = new BeforeExecution(host.CompositionExecution, () => throw injected);
        string outputPath = workspace.PathFor("output.bin");
        using var output = new StringWriter();
        using var error = new StringWriter();

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AbMergeCliCommandHandler.RunAsync(
                CreateServices(host, execution),
                host.LocalFiles,
                CreateAbBuildArguments(workspace, outputPath, workspace.PathFor("report.json")),
                output,
                error,
                TestContext.Current.CancellationToken));

        Assert.Same(injected, failure);
        Assert.Empty(error.ToString());
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>A catalog reload after General Replace preparation ends in a typed refusal, not an unhandled exception.</summary>
    [Fact]
    public async Task GeneralReplaceCatalogReloadAfterPreparationIsATypedRefusal()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-general-replace");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var execution = new BeforeExecution(host.CompositionExecution, () =>
        {
            Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
            return Task.CompletedTask;
        });
        string outputPath = workspace.PathFor("output.bin");
        string reportPath = workspace.PathFor("report.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await ReplaceCliCommandHandler.RunAsync(
            CreateServices(host, execution),
            host.LocalFiles,
            "general-replace",
            [
                "build", "--profile", "NT51926", "--ic-num", "single",
                "--base", workspace.Write(
                    "reference.bin",
                    File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output"))),
                "--mapping", $"0x3E020+0x2={workspace.Write("source.bin", [0xBE, 0xEF])}",
                "--output", outputPath, "--report", reportPath,
            ],
            output,
            error,
            TestContext.Current.CancellationToken);

        AssertTypedRefusal(exitCode, output, error, execution, outputPath, reportPath, ExperienceIds.GeneralReplace);
    }

    /// <summary>A catalog reload after General Merge preparation ends in a typed refusal, not an unhandled exception.</summary>
    [Fact]
    public async Task GeneralMergeCatalogReloadAfterPreparationIsATypedRefusal()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-general-merge");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var execution = new BeforeExecution(host.CompositionExecution, () =>
        {
            Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
            return Task.CompletedTask;
        });
        string outputPath = workspace.PathFor("output.bin");
        string reportPath = workspace.PathFor("report.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MergeCliCommandHandler.RunAsync(
            CreateServices(host, execution),
            [
                "build", "--profile", "NT51950", "--size", "0x4",
                "--mapping", $"0x0+0x1+0x2={workspace.Write("source.bin", [0x10, 0x11])}",
                "--output", outputPath, "--report", reportPath,
            ],
            output,
            error,
            TestContext.Current.CancellationToken);

        AssertTypedRefusal(exitCode, output, error, execution, outputPath, reportPath, ExperienceIds.GeneralMerge);
    }

    /// <summary>A catalog reload after Standard Merge preparation ends in a typed refusal, not an unhandled exception.</summary>
    [Fact]
    public async Task StandardMergeCatalogReloadAfterPreparationIsATypedRefusal()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-standard-merge");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var execution = new BeforeExecution(host.CompositionExecution, () =>
        {
            Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
            return Task.CompletedTask;
        });
        byte[] dp = new byte[0x40000];
        dp[0x3E000 + 20 + 1] = 0xB6;
        dp[0x3E000 + 20 + 2] = 0xD4;
        byte[] tp = new byte[0x3C000];
        tp[0] = 0xA7;
        tp[1] = 0x58;
        tp[17] = 0xC9;
        tp[0x17] = 1;
        "\0NVT"u8.CopyTo(tp.AsSpan(0xFFC));
        string outputPath = workspace.PathFor("output.bin");
        string reportPath = workspace.PathFor("report.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunStandardMergeAsync(
            CreateServices(host, execution),
            host.LocalFiles,
            [
                "build", "--profile", "NT51923",
                "--dp", workspace.Write("dp.bin", dp),
                "--tp", workspace.Write("tp.bin", tp),
                "--output", outputPath, "--report", reportPath,
            ],
            output,
            error,
            TestContext.Current.CancellationToken);

        AssertTypedRefusal(exitCode, output, error, execution, outputPath, reportPath, ExperienceIds.StandardMerge);
    }

    /// <summary>A plain invariant failure from execution still propagates; only the typed refusal is mapped.</summary>
    [Fact]
    public async Task UntypedExecutionFailureIsNotSwallowed()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-refusal-untyped");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var injected = new InvalidOperationException("Injected execution invariant failure.");
        var execution = new BeforeExecution(host.CompositionExecution, () => throw injected);
        using var output = new StringWriter();
        using var error = new StringWriter();

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MergeCliCommandHandler.RunAsync(
                CreateServices(host, execution),
                [
                    "build", "--profile", "NT51950", "--size", "0x4",
                    "--mapping", $"0x0+0x1+0x2={workspace.Write("source.bin", [0x10, 0x11])}",
                    "--output", workspace.PathFor("output.bin"),
                ],
                output,
                error,
                TestContext.Current.CancellationToken));

        Assert.Same(injected, failure);
        Assert.Empty(error.ToString());
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
    }

    private static void AssertTypedRefusal(
        int exitCode,
        StringWriter output,
        StringWriter error,
        BeforeExecution execution,
        string outputPath,
        string reportPath,
        string workflowId)
    {
        Assert.True(exitCode == 1, error + Environment.NewLine + output);
        Assert.Equal(1, execution.Calls);
        Assert.StartsWith("Issues:", error.ToString(), StringComparison.Ordinal);
        Assert.Contains(
            $"  {CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale} [{workflowId}]: ",
            error.ToString(),
            StringComparison.Ordinal);
        Assert.Empty(output.ToString());
        Assert.False(File.Exists(outputPath));
        Assert.False(File.Exists(reportPath));
    }

    private static string[] CreateAbBuildArguments(TempWorkspace workspace, string outputPath, string reportPath)
    {
        return
        [
            "build", "--profile", "NT51929",
            "--dp-ab", workspace.Write("dp-ab.bin", new byte[0x80000]),
            "--tp-a", workspace.Write("tp-a.bin", CreateAbTp()),
            "--tp-b", workspace.Write("tp-b.bin", CreateAbTp()),
            "--output", outputPath, "--report", reportPath,
        ];
    }

    private static byte[] CreateAbTp()
    {
        byte[] tp = new byte[0x40000];
        tp[0] = 0xA7;
        tp[1] = 0x58;
        tp[0x17] = 1;
        "\0NVT"u8.CopyTo(tp.AsSpan(0xFFC));
        return tp;
    }

    private static async Task<CompositionHostServices> CreateLoadedHostAsync()
    {
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        _ = await host.ExternalEnvironmentLoader.LoadToCompletionAsync(
            progress: null,
            TestContext.Current.CancellationToken);
        return host;
    }

    private static CliCompositionServices CreateServices(
        CompositionHostServices host,
        ICompositionExecution execution)
    {
        return new CliCompositionServices(
            host.CompositionCapabilityExperience,
            host.SavedRuleAuthoring,
            host.StandardMergeAuthoring,
            host.AbMergeAuthoring,
            host.CtrlRamAuthoring,
            host.GeneralAuthoring,
            host.CompositionOutputNaming,
            execution);
    }

    /// <summary>Changes the host's runtime or publication after the CLI readiness check, then executes.</summary>
    private sealed class BeforeExecution(ICompositionExecution inner, Func<Task> before) : ICompositionExecution
    {
        internal int Calls { get; private set; }

        public async ValueTask<CompositionRunResult> ExecuteAsync(
            AcceptedCompositionExecutionRequest request,
            CompositionRunProgressFeed progress,
            CancellationToken cancellationToken)
        {
            Calls++;
            await before();
            return await inner.ExecuteAsync(request, progress, cancellationToken);
        }
    }

    /// <summary>A bundled-runtime tool configuration whose next publication the test chooses.</summary>
    private sealed class PublishingToolchainSession(long generation) : IToolchainRuntimeConfigurationSession
    {
        public ToolchainRuntimeConfigurationSnapshot Current { get; private set; } = Snapshot(generation);

        internal void Publish(long nextGeneration)
        {
            Current = Snapshot(nextGeneration);
        }

        public ValueTask<ToolchainRuntimeConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ToolchainRuntimeConfigurationOperationResult(Current, true, []));
        }

        public ValueTask<ToolchainRuntimeConfigurationOperationResult> SaveAsync(
            ToolchainRuntimeSelection selection,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ToolchainRuntimeConfigurationOperationResult(Current, false, []));
        }

        public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private static ToolchainRuntimeConfigurationSnapshot Snapshot(long value)
        {
            var selection = new ToolchainRuntimeSelection(ToolchainRuntimeSource.Bundled);
            return new(value, ToolchainRuntimeConfigurationStatus.Current, selection, selection, []);
        }
    }
}
