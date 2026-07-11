using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildProcessorTests
{
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

    /// <summary>Retains successful command evidence when a later command fails before process launch.</summary>
    [Fact]
    public async Task TransformPreservesExecutedCommandsWhenLaterStagingFails()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = [0x10, 0x20, 0x30, 0x40];
        var firstCommand = new LegacyCombinerPostbuildCommand(
            "first",
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
        var invalidSecondCommand = new LegacyCombinerPostbuildCommand(
            "second",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "outside-input",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Outside.bin",
                    0,
                    new ByteRange(firmware.Length, 1)),
            ]);
        var profile = new LegacyCombinerPostbuildProfile(
            "nfc.test.later-staging-failure-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [firstCommand, invalidSecondCommand],
            [firstCommand, invalidSecondCommand],
            "test command evidence preservation");
        FakeProcessRunner runner = new(_ => new ExternalProcessResult(0, false, string.Empty, string.Empty));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-later-staging-failure",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("legacy-combiner.staging.range-outside-input", issue.Code);
        Assert.Equal(1, runner.RunCount);
        ExternalProcessInvocation executed = Assert.Single(result.ExecutedCommands);
        Assert.Equal("NT51927BASED_GEN_CRC_MODE", executed.Arguments[0]);
    }
}
