using System.Security.Cryptography;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using SharedTempWorkspace = NvtFwCombiner.TestSupport.TempWorkspace;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests staged external combiner execution and host-side diff policy.</summary>
public sealed class ExternalCombinerProcessorTests
{
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

        internal ExternalCombinerProcessor CreateProcessor(string executableSha256, IExternalProcessRunner runner)
        {
            ExternalCombinerToolRegistry registry = new([Manifest(executableSha256)]);
            return new ExternalCombinerProcessor(registry, ToolRoot, StagingRoot, runner);
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }

        private static ExternalCombinerToolManifest Manifest(string executableSha256)
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
                ["--input", "{staging.workBin}", "--output", "{staging.outputBin}"],
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
