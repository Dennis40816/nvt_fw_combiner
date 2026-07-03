using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests staged CtrlRAM postbuild execution through approved legacy Combiner.exe profiles.</summary>
public sealed class LegacyCombinerPostbuildProcessorTests
{
    /// <summary>Verifies a normal-mode profile stages BIN files and accepts declared output changes.</summary>
    [Fact]
    public async Task TransformStagesBinFilesAndAcceptsDeclaredChanges()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        bool inspectedNormalCtrlRam = false;
        bool mutated = false;
        FakeProcessRunner runner = new(startInfo =>
        {
            Assert.Equal("CRC_Enable", startInfo.Arguments[0]);
            Assert.EndsWith("nt51926_fw.bin", startInfo.Arguments[1], StringComparison.Ordinal);

            if (startInfo.Arguments.Any(argument =>
                argument.EndsWith(Path.Combine("BIN", "Normal_Ctrlram.bin"), StringComparison.Ordinal)))
            {
                string normalCtrlRam = Path.Combine(startInfo.WorkingDirectory, "BIN", "Normal_Ctrlram.bin");
                Assert.True(File.Exists(normalCtrlRam));
                byte[] stagedNormal = File.ReadAllBytes(normalCtrlRam);
                Assert.Equal(firmware[0x22800], stagedNormal[0]);
                Assert.Equal(firmware[0x22800 + 127], stagedNormal[127]);
                inspectedNormalCtrlRam = true;
            }

            if (!mutated)
            {
                byte[] output = File.ReadAllBytes(startInfo.Arguments[1]);
                output[0x32A70] ^= 0x5A;
                File.WriteAllBytes(startInfo.Arguments[1], output);
                mutated = true;
            }

            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner);
        ExternalProcessorRequest request = new(
            "run-nt51926",
            LegacyCombinerPostbuildCatalog.Nt51926.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [new ByteRange(0x32A70, 1)],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(inspectedNormalCtrlRam);
        Assert.Equal(2, runner.RunCount);
        Assert.Equal((byte)(firmware[0x32A70] ^ 0x5A), result.OutputBytes.Span[0x32A70]);
        Assert.Equal(new ByteRange(0x32A70, 1), Assert.Single(result.ChangedRanges));
    }

    /// <summary>Verifies NT51923 cascade stages split DiffDLM.bin source offsets from the work image.</summary>
    [Fact]
    public async Task TransformStagesSplitDiffDlmFileForNt51923Cascade()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        bool inspected = false;
        FakeProcessRunner runner = new(startInfo =>
        {
            if (!inspected)
            {
                string diffDlm = Path.Combine(startInfo.WorkingDirectory, "BIN", "DiffDLM.bin");
                byte[] stagedDiff = File.ReadAllBytes(diffDlm);

                Assert.Equal(0x2000, stagedDiff.Length);
                Assert.Equal(firmware[0x28800], stagedDiff[0]);
                Assert.Equal(firmware[0x28800 + 0x0BFF], stagedDiff[0x0BFF]);
                Assert.Equal(firmware[0x29400], stagedDiff[0x1400]);
                Assert.Equal(firmware[0x29400 + 0x0BFF], stagedDiff[0x1FFF]);
                inspected = true;
            }

            string firmwarePath = startInfo.Arguments.First(argument =>
                argument.EndsWith("nt51923_fw.bin", StringComparison.Ordinal));
            byte[] output = File.ReadAllBytes(firmwarePath);
            output[0x30310] = unchecked((byte)(output[0x30310] + 1));
            File.WriteAllBytes(firmwarePath, output);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner);
        ExternalProcessorRequest request = new(
            "run-nt51923-cascade",
            LegacyCombinerPostbuildCatalog.Nt51923.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [new ByteRange(0x30310, 1)],
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(inspected);
        Assert.Equal(2, runner.RunCount);
    }

    /// <summary>Verifies NT51927 two-chip mode runs the MERGE_MODE and CRC-only postbuild sequence.</summary>
    [Fact]
    public async Task TransformRunsNt51927TwoChipMergeAndCrcSequence()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        MakeNt51927TwoChipSharedStagedBlocksConsistent(firmware);
        List<string> modes = [];
        FakeProcessRunner runner = new(startInfo =>
        {
            modes.Add(startInfo.Arguments[0]);
            if (modes.Count == 1)
            {
                Assert.Equal("MERGE_MODE", startInfo.Arguments[0]);
                Assert.EndsWith("nt51927_fw.bin", startInfo.Arguments[1], StringComparison.Ordinal);
                Assert.Contains(
                    startInfo.Arguments,
                    argument => argument.EndsWith(Path.Combine("BIN", "Normal_Ctrlram.bin"), StringComparison.Ordinal));
            }

            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner);
        ExternalProcessorRequest request = new(
            "run-nt51927-2chip",
            LegacyCombinerPostbuildCatalog.Nt51927.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(10, runner.RunCount);
        Assert.Equal(8, modes.Count(mode => mode == "MERGE_MODE"));
        Assert.Equal(2, modes.Count(mode => mode == "NT51927BASED_GEN_CRC_MODE"));
    }

    /// <summary>Verifies command-shortened Combiner output is normalized back to full firmware length.</summary>
    [Fact]
    public async Task TransformNormalizesCommandShortenedFirmwareWhenCoverageIsComplete()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        FakeProcessRunner runner = new(startInfo =>
        {
            string firmwarePath = startInfo.Arguments.First(argument =>
                argument.EndsWith("shortened_fw.bin", StringComparison.Ordinal));
            byte[] shortened = File.ReadAllBytes(firmwarePath)[..0x20];
            shortened[0x12] ^= 0x33;
            File.WriteAllBytes(firmwarePath, shortened);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateShortenedOutputProfile();
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-shortened",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [new ByteRange(0x12, 1)],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal(firmware.Length, result.OutputBytes.Length);
        Assert.Equal((byte)(firmware[0x12] ^ 0x33), result.OutputBytes.Span[0x12]);
        Assert.Equal(firmware[0x20], result.OutputBytes.Span[0x20]);
        Assert.Equal(new ByteRange(0x12, 1), Assert.Single(result.ChangedRanges));
    }

    /// <summary>Rejects multiple work-image projections into the same staged file offset when bytes differ.</summary>
    [Fact]
    public async Task TransformRejectsConflictingStagedFileProjection()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = [0x10, 0x11, 0x20, 0x21];
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [CreateProjectionConflictProfile()]);
        ExternalProcessorRequest request = new(
            "run-projection-conflict",
            "nfc.test.projection-conflict-v1",
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("legacy-combiner.staging.projection-conflict", issue.Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Rejects postbuild runs that leave files outside the manifest-declared staging outputs.</summary>
    [Fact]
    public async Task TransformRejectsUnexpectedStagingFile()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        FakeProcessRunner runner = new(startInfo =>
        {
            File.WriteAllText(Path.Combine(startInfo.WorkingDirectory, "output", "unexpected.log"), "unexpected");
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateCrcOnlyProfile("nfc.test.unexpected-file-v1", "test_fw.bin");
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-unexpected-file",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staging.unexpected-file", issue.Code);
        Assert.Equal(1, runner.RunCount);
    }

    /// <summary>Rejects unexpected files after each command before a later command can reset staging.</summary>
    [Fact]
    public async Task TransformRejectsUnexpectedStagingFileBeforeNextCommandReset()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        FakeProcessRunner runner = new(startInfo =>
        {
            File.WriteAllText(Path.Combine(startInfo.WorkingDirectory, "BIN", "unexpected.tmp"), "unexpected");
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateTwoCommandCrcOnlyProfile();
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-unexpected-before-reset",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staging.unexpected-file", issue.Code);
        Assert.Equal(1, runner.RunCount);
    }

    /// <summary>Verifies mutations outside declared postbuild authority fail closed.</summary>
    [Fact]
    public async Task TransformRejectsOutOfRangePostbuildMutation()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        bool mutated = false;
        FakeProcessRunner runner = new(startInfo =>
        {
            string firmwarePath = startInfo.Arguments.First(argument =>
                argument.EndsWith("nt51950_fw.bin", StringComparison.Ordinal));
            if (!mutated)
            {
                byte[] output = File.ReadAllBytes(firmwarePath);
                output[0x10] ^= 0x40;
                File.WriteAllBytes(firmwarePath, output);
                mutated = true;
            }

            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner);
        ExternalProcessorRequest request = new(
            "run-nt51950-out-of-range",
            LegacyCombinerPostbuildCatalog.Nt51950.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [new ByteRange(0x2D30C, 1)],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.write-range.violation", issue.Code);
    }

    private static byte[] CreateFirmwareImage()
    {
        byte[] bytes = new byte[0x40000];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return bytes;
    }

    private static void MakeNt51927TwoChipSharedStagedBlocksConsistent(byte[] firmware)
    {
        Array.Copy(firmware, 0x16800, firmware, 0x1F800, 16);
        Array.Copy(firmware, 0x1CBD0, firmware, 0x25BD0, 5728);
    }

    private static LegacyCombinerPostbuildProfile CreateProjectionConflictProfile()
    {
        var command = new LegacyCombinerPostbuildCommand(
            "projection-conflict",
            LegacyCombinerCommandFamily.NormalMode,
            "CRC_Enable",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "first",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Same.bin",
                    0,
                    new ByteRange(0, 2)),
                new LegacyCombinerBlockArgument(
                    "second",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Same.bin",
                    0,
                    new ByteRange(2, 2)),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.projection-conflict-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [command],
            [command],
            "test projection conflict profile");
    }

    private static LegacyCombinerPostbuildProfile CreateCrcOnlyProfile(string processorId, string firmwareFileName)
    {
        var command = new LegacyCombinerPostbuildCommand(
            "crc-only",
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
        return new LegacyCombinerPostbuildProfile(
            processorId,
            "NTTEST",
            "legacy-combiner-1.13.0",
            firmwareFileName,
            [command],
            [command],
            "test crc-only profile");
    }

    private static LegacyCombinerPostbuildProfile CreateTwoCommandCrcOnlyProfile()
    {
        LegacyCombinerPostbuildCommand first = new(
            "crc-only-first",
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
        LegacyCombinerPostbuildCommand second = new(
            "crc-only-second",
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.unexpected-before-reset-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [first, second],
            [first, second],
            "test multi-command unexpected file profile");
    }

    private static LegacyCombinerPostbuildProfile CreateShortenedOutputProfile()
    {
        var command = new LegacyCombinerPostbuildCommand(
            "shortened-output",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "first-block",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Short.bin",
                    0,
                    new ByteRange(0, 0x20)),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.shortened-output-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "shortened_fw.bin",
            [command],
            [command],
            "test shortened output profile");
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
        private const string ToolVersion = "1.13.0";
        private const string ExecutableName = "Combiner.exe";
        private static int s_workspaceId;

        private TempWorkspace(string root)
        {
            Root = root;
            ToolRoot = Path.Combine(root, "tools");
            StagingRoot = Path.Combine(root, "staging");
            _ = Directory.CreateDirectory(ToolRoot);
            _ = Directory.CreateDirectory(StagingRoot);
        }

        internal string Root { get; }

        internal string ToolRoot { get; }

        internal string StagingRoot { get; }

        internal static TempWorkspace Create()
        {
            int workspaceId = Interlocked.Increment(ref s_workspaceId);
            string root = Path.Combine(
                Path.GetTempPath(),
                "nfc-legacy-postbuild-tests",
                FormattableString.Invariant($"{workspaceId:D4}"));
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            return new TempWorkspace(root);
        }

        internal string CreateToolExecutable()
        {
            string toolDirectory = Path.Combine(ToolRoot, ToolId, ToolVersion);
            _ = Directory.CreateDirectory(toolDirectory);
            string executablePath = Path.Combine(toolDirectory, ExecutableName);
            File.WriteAllBytes(executablePath, [0x4D, 0x5A, 0x13, 0x00]);
            return Sha256(executablePath);
        }

        internal LegacyCombinerPostbuildProcessor CreateProcessor(
            string executableSha256,
            IExternalProcessRunner runner,
            IEnumerable<LegacyCombinerPostbuildProfile>? profiles = null)
        {
            var registry = new ExternalCombinerToolRegistry([Manifest(executableSha256)]);
            return new LegacyCombinerPostbuildProcessor(
                registry,
                profiles ?? LegacyCombinerPostbuildCatalog.All,
                ToolRoot,
                StagingRoot,
                runner);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static ExternalCombinerToolManifest Manifest(string executableSha256)
        {
            return new ExternalCombinerToolManifest(
                "1.0",
                "legacy-combiner-1.13.0",
                ToolId,
                ToolVersion,
                "Legacy Combiner 1.13.0",
                "win-x64",
                ExecutableName,
                executableSha256,
                "legacy-combiner-postbuild-v1",
                "in-place",
                ["{staging.runDir}"],
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
