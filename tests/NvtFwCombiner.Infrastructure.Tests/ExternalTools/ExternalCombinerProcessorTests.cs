using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using SharedTempWorkspace = NvtFwCombiner.TestSupport.TempWorkspace;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests staged external combiner execution and host-side diff policy.</summary>
public sealed class ExternalCombinerProcessorTests
{
    /// <summary>Rejecting a pre-existing directory must not delete another run's evidence.</summary>
    [Fact]
    public async Task RejectedExistingStagingPreservesSentinelWithoutLaunchingTool()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Must not launch."));
        ExternalProcessorRequest request = Request();
        string directory = Path.Combine(workspace.StagingRoot, request.RunId);
        _ = Directory.CreateDirectory(directory);
        string sentinel = Path.Combine(directory, "sentinel.bin");
        byte[] expected = [7, 8, 9];
        await File.WriteAllBytesAsync(sentinel, expected, TestContext.Current.CancellationToken);

        ExternalProcessorResult result = await workspace.CreateProcessor(sha256, runner)
            .TransformAsync(request, CancellationToken.None);

        Assert.Equal("external-tool.staging.exists", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
        Assert.True(File.Exists(sentinel));
        Assert.Equal(expected, await File.ReadAllBytesAsync(sentinel, TestContext.Current.CancellationToken));
        Assert.Equal([sentinel], Directory.GetFiles(directory));
    }

    /// <summary>Verifies a transform that changes only declared bytes succeeds and reports changed ranges.</summary>
    [Fact]
    public async Task TransformAcceptsDeclaredChangedRange()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            Assert.DoesNotContain(startInfo.Arguments, argument => argument.Contains('{', StringComparison.Ordinal));
            Assert.Contains(Path.Combine(startInfo.WorkingDirectory, "work.bin"), startInfo.Arguments);
            Assert.Contains(Path.Combine(startInfo.WorkingDirectory, "output.bin"), startInfo.Arguments);
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 7, 0, 0]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);
        ExternalProcessorRequest request = Request(allowedWrites: [new ByteRange(1, 1)]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal([0, 7, 0, 0], result.OutputBytes.ToArray());
        ByteRange range = Assert.Single(result.ChangedRanges);
        Assert.Equal(new ByteRange(1, 1), range);
        ExternalProcessInvocation executedCommand = Assert.Single(result.ExecutedCommands);
        Assert.EndsWith(Path.Combine("legacy-combiner", "1.10", "combiner.exe"), executedCommand.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.Combine(workspace.StagingRoot, "run-synthetic"), executedCommand.WorkingDirectory);
        Assert.Equal(
            [
                "--input",
                Path.Combine(workspace.StagingRoot, "run-synthetic", "work.bin"),
                "--output",
                Path.Combine(workspace.StagingRoot, "run-synthetic", "output.bin"),
            ],
            executedCommand.Arguments);
        Assert.Empty(result.Issues);
    }

    /// <summary>Verifies unknown manifest bindings fail before any process can run.</summary>
    [Fact]
    public async Task TransformRejectsUnknownToolBinding()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("runner should not run"));
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);
        ExternalProcessorRequest request = new(
            "run-unknown",
            "processor-v1",
            "missing-binding",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.binding.unknown", issue.Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies executable SHA mismatch fails closed before process launch.</summary>
    [Fact]
    public async Task TransformRejectsExecutableShaMismatch()
    {
        using var workspace = TempWorkspace.Create();
        _ = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("runner should not run"));
        ExternalCombinerProcessor processor = workspace.CreateProcessor(new string('0', 64), runner);

        ExternalProcessorResult result = await processor.TransformAsync(Request(), CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.executable-sha.mismatch", issue.Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies mutations outside profile-declared write authority fail closed.</summary>
    [Fact]
    public async Task TransformRejectsOutOfRangeMutation()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 0, 8, 0]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);

        ExternalProcessorResult result = await processor.TransformAsync(
            Request(allowedWrites: [new ByteRange(1, 1)]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.write-range.violation", issue.Code);
    }

    /// <summary>Verifies changed output length is rejected before bytes are imported.</summary>
    [Fact]
    public async Task TransformRejectsLengthChangedOutput()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 1, 2, 3, 4]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);

        ExternalProcessorResult result = await processor.TransformAsync(Request(), CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.output-length.changed", issue.Code);
    }

    /// <summary>Verifies unexpected staging files are rejected.</summary>
    [Fact]
    public async Task TransformRejectsUnexpectedOutputFiles()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 7, 0, 0]);
            File.WriteAllText(Path.Combine(startInfo.WorkingDirectory, "debug.tmp"), "unexpected");
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);

        ExternalProcessorResult result = await processor.TransformAsync(Request(), CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.unexpected-output-file", issue.Code);
    }

    /// <summary>Verifies non-zero process exit fails closed.</summary>
    [Fact]
    public async Task TransformRejectsNonZeroExitCode()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => new ExternalProcessResult(7, false, string.Empty, "failed"));
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);

        ExternalProcessorResult result = await processor.TransformAsync(Request(), CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.process.failed", issue.Code);
        _ = Assert.Single(result.ExecutedCommands);
    }

    /// <summary>Verifies timeout exits fail closed.</summary>
    [Fact]
    public async Task TransformRejectsTimedOutProcess()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => new ExternalProcessResult(-1, true, string.Empty, "timeout"));
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);

        ExternalProcessorResult result = await processor.TransformAsync(Request(), CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.process.timeout", issue.Code);
    }

    /// <summary>Verifies named artifacts are host-staged, expanded without path input, and imported only through the declared output.</summary>
    [Fact]
    public async Task TransformMaterializesNamedArtifactsAndPreservesTheirBytes()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            string aBankPath = Path.Combine(startInfo.WorkingDirectory, "artifact-a-bank.bin");
            string bBankPath = Path.Combine(startInfo.WorkingDirectory, "artifact-b-bank.bin");
            Assert.Contains(aBankPath, startInfo.Arguments);
            Assert.Contains(bBankPath, startInfo.Arguments);
            Assert.Equal([0xA1, 0xA2], File.ReadAllBytes(aBankPath));
            Assert.Equal([0xB1, 0xB2], File.ReadAllBytes(bBankPath));
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 7, 0, 0]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(
            sha256,
            runner,
            [
                "--a", "{staging.artifact.a-bank}",
                "--b", "{staging.artifact.b-bank}",
                "--output", "{staging.outputBin}",
            ]);
        ExternalProcessorRequest request = new(
            "run-artifacts",
            "processor-v1",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)],
            stagedArtifacts:
            [
                new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2 }),
                new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2 }),
            ]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal([0, 7, 0, 0], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies a profile-selected Combiner invocation overrides only the tool package default argv and output mode.</summary>
    [Fact]
    public async Task TransformUsesProfileSelectedInvocationForNamedArtifacts()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            Assert.Equal(
                [
                    "NT51950BASED_MERGE_AB_MODE",
                    "CRC8",
                    Path.Combine(startInfo.WorkingDirectory, "artifact-a-bank.bin"),
                    Path.Combine(startInfo.WorkingDirectory, "artifact-b-bank.bin"),
                    Path.Combine(startInfo.WorkingDirectory, "output.bin"),
                    "0x40000",
                ],
                startInfo.Arguments);
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 7, 0, 0]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        var invocation = new ExternalCombinerInvocationProfile(
            "nt51950-ab",
            "legacy-combiner-1.10",
            "input-output-file",
            [
                "NT51950BASED_MERGE_AB_MODE",
                "CRC8",
                "{staging.artifact.a-bank}",
                "{staging.artifact.b-bank}",
                "{staging.outputBin}",
                "0x40000",
            ]);
        ExternalCombinerProcessor processor = workspace.CreateProcessor(
            sha256,
            runner,
            ["--package-default", "{staging.runDir}"],
            [invocation]);
        var request = new ExternalProcessorRequest(
            "run-profile-invocation",
            "nt51950-ab",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)],
            stagedArtifacts:
            [
                new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2 }),
                new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2 }),
            ]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal([0, 7, 0, 0], result.OutputBytes.ToArray());
        _ = Assert.Single(result.ExecutedCommands);
    }

    /// <summary>Verifies a profile-selected invocation cannot run with another tool binding.</summary>
    [Fact]
    public async Task TransformRejectsProfileSelectedInvocationWithMismatchedToolBinding()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("runner should not run"));
        var invocation = new ExternalCombinerInvocationProfile(
            "nt51950-ab",
            "other-tool-binding",
            "input-output-file",
            ["{staging.outputBin}"]);
        ExternalCombinerProcessor processor = workspace.CreateProcessor(
            sha256,
            runner,
            invocationProfiles: [invocation]);
        var request = new ExternalProcessorRequest(
            "run-profile-invocation-mismatch",
            "nt51950-ab",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.invocation.binding-mismatch", issue.Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies an unrecognized profile-selected staging mode fails before process launch.</summary>
    [Fact]
    public async Task TransformRejectsProfileSelectedInvocationWithInvalidInputMode()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("runner should not run"));
        var invocation = new ExternalCombinerInvocationProfile(
            "nt51950-ab",
            "legacy-combiner-1.10",
            "two-input-files",
            ["{staging.outputBin}"]);
        ExternalCombinerProcessor processor = workspace.CreateProcessor(
            sha256,
            runner,
            invocationProfiles: [invocation]);
        var request = new ExternalProcessorRequest(
            "run-profile-invocation-invalid-mode",
            "nt51950-ab",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.invocation.input-mode.invalid", issue.Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies the command authority cannot be mutated by a caller after construction.</summary>
    [Fact]
    public void ProfileSelectedInvocationArgumentsAreRuntimeImmutable()
    {
        var invocation = new ExternalCombinerInvocationProfile(
            "nt51950-ab",
            "legacy-combiner-1.10",
            "input-output-file",
            ["CRC8", "{staging.outputBin}"]);

        var arguments = (IList<string>)invocation.ArgumentTemplate;

        Assert.True(arguments.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => arguments[0] = "CRC32");
        Assert.Equal("CRC8", invocation.ArgumentTemplate[0]);
    }

    /// <summary>Verifies the host rejects a Combiner that changes a named source artifact.</summary>
    [Fact]
    public async Task TransformRejectsModifiedNamedArtifact()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "artifact-a-bank.bin"), [0xFF, 0xFF]);
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 7, 0, 0]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(
            sha256,
            runner,
            ["--a", "{staging.artifact.a-bank}", "--output", "{staging.outputBin}"]);
        ExternalProcessorRequest request = new(
            "run-modified-artifact",
            "processor-v1",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)],
            stagedArtifacts: [new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2 })]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staged-artifact.modified", issue.Code);
    }

    /// <summary>Verifies a manifest cannot execute when it references an artifact absent from the compiled request.</summary>
    [Fact]
    public async Task TransformRejectsUnknownNamedArtifactTokenBeforeProcessStart()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("runner should not run"));
        ExternalCombinerProcessor processor = workspace.CreateProcessor(
            sha256,
            runner,
            ["--a", "{staging.artifact.a-bank}", "--output", "{staging.outputBin}"]);
        ExternalProcessorRequest request = new(
            "run-missing-artifact",
            "processor-v1",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)],
            stagedArtifacts: [new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2 })]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staged-artifact.unknown", issue.Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies every compiled artifact must be consumed by the selected manifest before process launch.</summary>
    [Fact]
    public async Task TransformRejectsUnusedNamedArtifactTokenBeforeProcessStart()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("runner should not run"));
        ExternalCombinerProcessor processor = workspace.CreateProcessor(
            sha256,
            runner,
            ["--input", "{staging.workBin}", "--output", "{staging.outputBin}"]);
        ExternalProcessorRequest request = new(
            "run-unused-artifact",
            "processor-v1",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            [new ByteRange(1, 1)],
            stagedArtifacts: [new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2 })]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staged-artifact.unused", issue.Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>All terminal paths release only this invocation's owned staging.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task OwnedStagingIsCleanedAfterSuccessFailureOrCancellation(int outcome)
    {
        using var workspace = TempWorkspace.Create();
        using var cancellation = new CancellationTokenSource();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(startInfo =>
        {
            if (outcome == 2)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }

            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 0, 0, 0]);
            return new ExternalProcessResult(outcome, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(sha256, runner);
        if (outcome == 2)
        {
            _ = await Assert.ThrowsAsync<OperationCanceledException>(
                () => processor.TransformAsync(Request(), cancellation.Token).AsTask());
        }
        else
        {
            ExternalProcessorResult result = await processor.TransformAsync(Request(), cancellation.Token);
            Assert.Equal(outcome == 0, result.Succeeded);
        }

        Assert.Equal(1, runner.RunCount);
        Assert.Empty(Directory.GetFileSystemEntries(workspace.StagingRoot));
    }

    /// <summary>The selected runtime and pinned tool execute from one private directory for the whole invocation.</summary>
    [Fact]
    public async Task UserRuntimeDeploymentIsUsedAndCleanedAfterTransform()
    {
        using var workspace = TempWorkspace.Create();
        string executableSha256 = workspace.CreateToolExecutable();
        byte[] runtimeBytes = [0x4D, 0x5A, 0x14, 0x00];
        string runtimePath = Path.Combine(workspace.Root, "runtime", "vcruntime140.dll");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
        await File.WriteAllBytesAsync(runtimePath, runtimeBytes, TestContext.Current.CancellationToken);
        var selection = new ToolchainRuntimeSelection(
            ToolchainRuntimeSource.User,
            runtimePath,
            Convert.ToHexStringLower(SHA256.HashData(runtimeBytes)));
        var deployment = new ExternalRuntimeDeployment(
            new(7, ToolchainRuntimeConfigurationStatus.Current, selection, selection, []),
            new LocalFileStore(),
            Path.Combine(workspace.Root, "deployments"));
        string? privateDirectory = null;
        FakeProcessRunner runner = new(startInfo =>
        {
            privateDirectory = Path.GetDirectoryName(startInfo.ExecutablePath);
            Assert.NotEqual(Path.Combine(workspace.ToolRoot, "legacy-combiner", "1.10", "combiner.exe"), startInfo.ExecutablePath);
            Assert.Equal(runtimeBytes, File.ReadAllBytes(Path.Combine(privateDirectory!, "vcruntime140.dll")));
            File.WriteAllBytes(Path.Combine(startInfo.WorkingDirectory, "output.bin"), [0, 7, 0, 0]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        ExternalCombinerProcessor processor = workspace.CreateProcessor(executableSha256, runner, runtimeDeployment: deployment);

        ExternalProcessorResult result = await processor.TransformAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, runner.RunCount);
        Assert.NotNull(privateDirectory);
        Assert.False(Directory.Exists(privateDirectory));
    }

    /// <summary>A selected runtime identity change blocks the processor before its runner is called.</summary>
    [Fact]
    public async Task ChangedUserRuntimeBlocksBeforeProcessStart()
    {
        using var workspace = TempWorkspace.Create();
        string executableSha256 = workspace.CreateToolExecutable();
        byte[] runtimeBytes = [0x4D, 0x5A, 0x14, 0x00];
        string runtimePath = Path.Combine(workspace.Root, "runtime", "vcruntime140.dll");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
        await File.WriteAllBytesAsync(runtimePath, runtimeBytes, TestContext.Current.CancellationToken);
        var selection = new ToolchainRuntimeSelection(
            ToolchainRuntimeSource.User,
            runtimePath,
            Convert.ToHexStringLower(SHA256.HashData(runtimeBytes)));
        var deployment = new ExternalRuntimeDeployment(
            new(8, ToolchainRuntimeConfigurationStatus.Current, selection, selection, []),
            new LocalFileStore(),
            Path.Combine(workspace.Root, "deployments"));
        await File.WriteAllBytesAsync(runtimePath, [0xFF], TestContext.Current.CancellationToken);
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("runner should not run"));
        ExternalCombinerProcessor processor = workspace.CreateProcessor(executableSha256, runner, runtimeDeployment: deployment);

        ExternalProcessorResult result = await processor.TransformAsync(Request(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("toolchain-runtime.identity.changed", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    private static ExternalProcessorRequest Request(IReadOnlyList<ByteRange>? allowedWrites = null)
    {
        return new ExternalProcessorRequest(
            "run-synthetic",
            "processor-v1",
            "legacy-combiner-1.10",
            new byte[] { 0, 0, 0, 0 },
            allowedWrites ?? [new ByteRange(1, 1)]);
    }

    private sealed class FakeProcessRunner : IExternalProcessRunner
    {
        private readonly Func<ExternalProcessStartInfo, ExternalProcessResult> _run;

        internal FakeProcessRunner(Func<ExternalProcessStartInfo, ExternalProcessResult> run)
        {
            _run = run;
        }

        internal int RunCount { get; private set; }

        public ValueTask<ExternalProcessResult> RunAsync(
            ExternalProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return ValueTask.FromResult(_run(startInfo));
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        private const string ToolId = "legacy-combiner";
        private const string ToolVersion = "1.10";
        private const string ExecutableName = "combiner.exe";
        private readonly SharedTempWorkspace _workspace;

        private TempWorkspace(SharedTempWorkspace workspace)
        {
            _workspace = workspace;
            Root = workspace.Root;
            ToolRoot = Path.Combine(Root, "tools");
            StagingRoot = Path.Combine(Root, "staging");
            _ = Directory.CreateDirectory(ToolRoot);
            _ = Directory.CreateDirectory(StagingRoot);
        }

        internal string Root { get; }

        internal string ToolRoot { get; }

        internal string StagingRoot { get; }

        internal static TempWorkspace Create()
        {
            return new TempWorkspace(SharedTempWorkspace.Create("nfc-infra-tests"));
        }

        internal string CreateToolExecutable()
        {
            string toolDirectory = Path.Combine(ToolRoot, ToolId, ToolVersion);
            _ = Directory.CreateDirectory(toolDirectory);
            string executablePath = Path.Combine(toolDirectory, ExecutableName);
            File.WriteAllBytes(executablePath, [0x4D, 0x5A, 0x00, 0x01]);
            return Sha256(executablePath);
        }

        internal ExternalCombinerProcessor CreateProcessor(
            string executableSha256,
            IExternalProcessRunner runner,
            IReadOnlyList<string>? argumentTemplate = null,
            IEnumerable<ExternalCombinerInvocationProfile>? invocationProfiles = null,
            ExternalRuntimeDeployment? runtimeDeployment = null)
        {
            ExternalCombinerToolRegistry registry = new([Manifest(executableSha256, argumentTemplate)]);
            return new ExternalCombinerProcessor(
                registry,
                ToolRoot,
                StagingRoot,
                runner,
                invocationProfiles ?? [],
                runtimeDeployment);
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }

        private static ExternalCombinerToolManifest Manifest(
            string executableSha256,
            IReadOnlyList<string>? argumentTemplate = null)
        {
            return new ExternalCombinerToolManifest(
                "1.0",
                "legacy-combiner-1.10",
                ToolId,
                ToolVersion,
                "Legacy Combiner 1.10",
                "win-x64",
                ExecutableName,
                executableSha256,
                "legacy-combiner-inout-v1",
                "input-output-file",
                argumentTemplate ?? ["--input", "{staging.workBin}", "--output", "{staging.outputBin}"],
                "staging-directory",
                5,
                []);
        }

        private static string Sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
