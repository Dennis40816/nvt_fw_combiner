using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests staged CtrlRAM postbuild execution through approved legacy Combiner.exe profiles.</summary>
public sealed partial class LegacyCombinerPostbuildProcessorTests
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
            string mapPath = Path.Combine(startInfo.WorkingDirectory, "output", "map.txt");
            Assert.True(File.Exists(mapPath));
            Assert.Empty(File.ReadAllBytes(mapPath));

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

    /// <summary>Verifies replacement source bytes are staged for Combiner pasteback without pre-writing the firmware file.</summary>
    [Fact]
    public async Task TransformUsesStagedSourceOverridesWithoutPrePaste()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = [0x10, 0x20, 0x30, 0x40];
        bool inspected = false;
        FakeProcessRunner runner = new(startInfo =>
        {
            string firmwarePath = startInfo.Arguments[1];
            Assert.Equal(firmware, File.ReadAllBytes(firmwarePath));

            string replacementPath = Path.Combine(startInfo.WorkingDirectory, "BIN", "Replacement.bin");
            Assert.Equal([0xAA, 0xBB], File.ReadAllBytes(replacementPath));

            byte[] output = File.ReadAllBytes(firmwarePath);
            output[1] = 0xAA;
            output[2] = 0xBB;
            File.WriteAllBytes(firmwarePath, output);
            inspected = true;
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateStagedReplacementProfile();
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-staged-source",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [new ByteRange(1, 2)],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            [new ExternalProcessorStagedSource(new ByteRange(1, 2), new byte[] { 0xAA, 0xBB })]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.True(inspected);
        Assert.Equal([0x10, 0xAA, 0xBB, 0x40], result.OutputBytes.ToArray());
        Assert.Equal(new ByteRange(1, 2), Assert.Single(result.ChangedRanges));
    }

    /// <summary>Verifies source-offset-zero blocks preserve a shorter immutable BIN for Legacy Combiner EOF semantics.</summary>
    [Fact]
    public async Task TransformPreservesShortExactStagedFile()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        LegacyCombinerPostbuildProfile profile = CreateExactStagedFileProfile(sourceOffset: 0);
        FakeProcessRunner runner = new(startInfo =>
        {
            Assert.Equal(
                [0xA1, 0xA2],
                File.ReadAllBytes(Path.Combine(startInfo.WorkingDirectory, "BIN", "Replacement.bin")));
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-short-exact-staged-file",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            new byte[8],
            [],
            stagedArtifacts: [new ExternalProcessorStagedArtifact("replacement", new byte[] { 0xA1, 0xA2 })]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal(1, runner.RunCount);
    }

    /// <summary>Verifies nonzero source-offset blocks fail closed when the declared range exceeds a short BIN.</summary>
    [Fact]
    public async Task TransformRejectsShortExactStagedFileAtNonzeroSourceOffset()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        LegacyCombinerPostbuildProfile profile = CreateExactStagedFileProfile(sourceOffset: 1);
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-short-offset-staged-file",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            new byte[8],
            [],
            stagedArtifacts: [new ExternalProcessorStagedArtifact("replacement", new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 })]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.staged-file.range-outside-artifact", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies Combiner BIN blocks can consume engine-created immutable artifacts without pre-pasting them into output.</summary>
    [Fact]
    public async Task TransformStagesImmutableArtifactsWithoutPrePaste()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = [0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80];
        LegacyCombinerPostbuildProfile profile = CreateArtifactSourceProfile();
        FakeProcessRunner runner = new(startInfo =>
        {
            string firmwarePath = startInfo.Arguments[2];
            Assert.Equal(firmware, File.ReadAllBytes(firmwarePath));
            Assert.Equal([0xA1, 0xA2, 0xA3, 0xA4], File.ReadAllBytes(
                Path.Combine(startInfo.WorkingDirectory, "BIN", "ABank.bin")));
            Assert.Equal([0xB1, 0xB2, 0xB3, 0xB4], File.ReadAllBytes(
                Path.Combine(startInfo.WorkingDirectory, "BIN", "BBank.bin")));

            byte[] output = File.ReadAllBytes(firmwarePath);
            output[6] = 0xEE;
            File.WriteAllBytes(firmwarePath, output);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-staged-artifacts",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [new ByteRange(6, 1)],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            stagedArtifacts:
            [
                new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 }),
                new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2, 0xB3, 0xB4 }),
            ]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal((byte)0xEE, result.OutputBytes.Span[6]);
        Assert.Equal(new ByteRange(6, 1), Assert.Single(result.ChangedRanges));
    }

    /// <summary>Verifies a selected Combiner block cannot refer to an artifact absent from the engine request.</summary>
    [Fact]
    public async Task TransformRejectsMissingStagedArtifactBeforeProcessLaunch()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        LegacyCombinerPostbuildProfile profile = CreateArtifactSourceProfile();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-missing-staged-artifact",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            new byte[8],
            [],
            stagedArtifacts: [new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2, 0xB3, 0xB4 })]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.staged-artifact.unknown", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies every engine-created artifact must be consumed by the selected command plan.</summary>
    [Fact]
    public async Task TransformRejectsUnusedStagedArtifactBeforeProcessLaunch()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        LegacyCombinerPostbuildProfile profile = CreateArtifactSourceProfile();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-unused-staged-artifact",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            new byte[8],
            [],
            stagedArtifacts:
            [
                new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 }),
                new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2, 0xB3, 0xB4 }),
                new ExternalProcessorStagedArtifact("unused-bank", new byte[] { 0xC1, 0xC2, 0xC3, 0xC4 }),
            ]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.staged-artifact.unused", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies artifact source ranges are bounded by the immutable engine-created artifact.</summary>
    [Fact]
    public async Task TransformRejectsStagedArtifactRangeOutsideArtifactBeforeProcessLaunch()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        LegacyCombinerPostbuildProfile profile = CreateArtifactSourceProfile();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-staged-artifact-range",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            new byte[8],
            [],
            stagedArtifacts:
            [
                new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2, 0xA3 }),
                new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2, 0xB3, 0xB4 }),
            ]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.staged-artifact.range-outside-artifact", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Verifies Combiner cannot mutate an engine-created immutable artifact in the staging directory.</summary>
    [Fact]
    public async Task TransformRejectsModifiedStagedArtifact()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        LegacyCombinerPostbuildProfile profile = CreateArtifactSourceProfile();
        FakeProcessRunner runner = new(startInfo =>
        {
            string artifactPath = Path.Combine(startInfo.WorkingDirectory, "BIN", "ABank.bin");
            byte[] artifact = File.ReadAllBytes(artifactPath);
            artifact[0] ^= 0xFF;
            File.WriteAllBytes(artifactPath, artifact);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-modified-staged-artifact",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            new byte[8],
            [],
            stagedArtifacts:
            [
                new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 }),
                new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2, 0xB3, 0xB4 }),
            ]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.staged-artifact.modified", Assert.Single(result.Issues).Code);
        Assert.Equal(1, runner.RunCount);
    }

    /// <summary>Verifies an artifact consumed by a later command is validated across the whole selected command plan.</summary>
    [Fact]
    public async Task TransformAllowsArtifactsConsumedByLaterCommand()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        LegacyCombinerPostbuildProfile profile = CreateArtifactSourceThenCrcProfile();
        FakeProcessRunner runner = new(_ => new ExternalProcessResult(0, false, string.Empty, string.Empty));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-later-staged-artifact",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            new byte[8],
            [],
            stagedArtifacts:
            [
                new ExternalProcessorStagedArtifact("a-bank", new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 }),
                new ExternalProcessorStagedArtifact("b-bank", new byte[] { 0xB1, 0xB2, 0xB3, 0xB4 }),
            ]);

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal(2, runner.RunCount);
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
        Assert.Equal(10, result.ExecutedCommands.Count);
        Assert.Equal("MERGE_MODE", result.ExecutedCommands[0].Arguments[0]);
        Assert.EndsWith("nt51927_fw.bin", result.ExecutedCommands[0].Arguments[1], StringComparison.Ordinal);
        Assert.Equal(8, modes.Count(mode => mode == "MERGE_MODE"));
        Assert.Equal(2, modes.Count(mode => mode == "NT51927BASED_GEN_CRC_MODE"));
    }

    /// <summary>Verifies later commands stage BIN files from the initial staged-source image, not a mutated work file.</summary>
    [Fact]
    public async Task TransformStagesEachCommandFromInitialPostReplacementImage()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = [0x10, 0x20, 0x30, 0x40, 0x99, 0x60];
        int call = 0;
        FakeProcessRunner runner = new(startInfo =>
        {
            call++;
            string firmwarePath = startInfo.Arguments[1];
            byte[] output = File.ReadAllBytes(firmwarePath);
            if (call == 1)
            {
                output[4] = output[0];
                File.WriteAllBytes(firmwarePath, output);
                return new ExternalProcessResult(0, false, string.Empty, string.Empty);
            }

            string stagedReplacement = Path.Combine(startInfo.WorkingDirectory, "BIN", "Replacement.bin");
            byte[] staged = File.ReadAllBytes(stagedReplacement);
            Assert.Equal(0x99, Assert.Single(staged));
            output[4] = staged[0];
            File.WriteAllBytes(firmwarePath, output);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateCopyThenRestoreProfile();
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-copy-then-restore",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal(firmware, result.OutputBytes.ToArray());
        Assert.Empty(result.ChangedRanges);
        Assert.Equal(2, runner.RunCount);
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

    /// <summary>Locks two- and thirteen-command plans to one sequential processor pipeline.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(13)]
    public async Task TransformRunsCountedCommandsInOneSequentialPipeline(int commandCount)
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        LegacyCombinerPostbuildCommand[] commands = [
            .. Enumerable.Range(0, commandCount).Select(index => new LegacyCombinerPostbuildCommand(
                $"crc-{index}",
                LegacyCombinerCommandFamily.CrcOnlyMode,
                "NT51927BASED_GEN_CRC_MODE",
                "CRC32",
                [])),
        ];
        var profile = new LegacyCombinerPostbuildProfile(
            $"nfc.test.{commandCount}-command-pipeline-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "counted_pipeline_fw.bin",
            commands,
            commands,
            "test counted sequential pipeline");
        FakeProcessRunner runner = new(_ => new ExternalProcessResult(0, false, string.Empty, string.Empty));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            $"run-{commandCount}-command-pipeline",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal(commandCount, runner.RunCount);
        Assert.Equal(commandCount, result.ExecutedCommands.Count);
        Assert.Equal(firmware, result.OutputBytes.ToArray());
        Assert.Empty(result.ChangedRanges);
    }

    /// <summary>Verifies shortened output restores the exact tail produced by the preceding command.</summary>
    [Fact]
    public async Task TransformNormalizesShortenedFirmwareFromPrecedingPipelineState()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        var first = new LegacyCombinerPostbuildCommand(
            "update-tail",
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
        var second = new LegacyCombinerPostbuildCommand(
            "shorten-prefix",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "prefix",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Short.bin",
                    0,
                    new ByteRange(0, 0x20)),
            ]);
        var profile = new LegacyCombinerPostbuildProfile(
            "nfc.test.pipeline-shortened-output-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "pipeline_shortened_fw.bin",
            [first, second],
            [first, second],
            "test pipeline shortened output profile");
        int call = 0;
        FakeProcessRunner runner = new(startInfo =>
        {
            call++;
            string firmwarePath = startInfo.Arguments.First(argument =>
                argument.EndsWith("pipeline_shortened_fw.bin", StringComparison.Ordinal));
            byte[] output = File.ReadAllBytes(firmwarePath);
            if (call == 1)
            {
                output[0x30] ^= 0x55;
                File.WriteAllBytes(firmwarePath, output);
            }
            else
            {
                byte[] shortened = output[..0x20];
                shortened[0x12] ^= 0x33;
                File.WriteAllBytes(firmwarePath, shortened);
            }

            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-pipeline-shortened",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [new ByteRange(0x12, 1), new ByteRange(0x30, 1)],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal(2, runner.RunCount);
        Assert.Equal(firmware.Length, result.OutputBytes.Length);
        Assert.Equal((byte)(firmware[0x12] ^ 0x33), result.OutputBytes.Span[0x12]);
        Assert.Equal((byte)(firmware[0x30] ^ 0x55), result.OutputBytes.Span[0x30]);
        Assert.Equal([new ByteRange(0x12, 1), new ByteRange(0x30, 1)], result.ChangedRanges);
    }

}
