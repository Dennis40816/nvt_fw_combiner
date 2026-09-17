using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildProcessorTests
{
    /// <summary>Rejecting a pre-existing directory must not delete another run's evidence.</summary>
    [Fact]
    public async Task RejectedExistingStagingPreservesSentinelWithoutLaunchingTool()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Must not launch."));
        LegacyCombinerPostbuildProfile profile = CreateCrcOnlyProfile("nfc.test.owned-staging-v1", "test_fw.bin");
        var selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]);
        ExternalProcessorRequest request = new(
            "run-existing", profile.ProcessorId, profile.ToolBindingId, CreateFirmwareImage(), [],
            selection, protocolPlan: CompileProtocolPlan(profile, selection));
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
        FakeProcessRunner runner = new(_ =>
        {
            if (outcome == 2)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }

            return new ExternalProcessResult(outcome, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateCrcOnlyProfile("nfc.test.cleanup-v1", "test_fw.bin");
        var selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]);
        ExternalProcessorRequest request = new(
            "run-cleanup", profile.ProcessorId, profile.ToolBindingId, CreateFirmwareImage(), [],
            selection, protocolPlan: CompileProtocolPlan(profile, selection));
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner);
        if (outcome == 2)
        {
            _ = await Assert.ThrowsAsync<OperationCanceledException>(
                () => processor.TransformAsync(request, cancellation.Token).AsTask());
        }
        else
        {
            ExternalProcessorResult result = await processor.TransformAsync(request, cancellation.Token);
            Assert.Equal(outcome == 0, result.Succeeded);
        }

        Assert.Equal(1, runner.RunCount);
        Assert.Empty(Directory.GetFileSystemEntries(workspace.StagingRoot));
    }

    /// <summary>Rejects execution when the compiled invocation did not select the adapter protocol plan.</summary>
    [Fact]
    public async Task TransformRejectsMissingCompiledProtocolPlanBeforeLaunch()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProfile profile = CreateCrcOnlyProfile("nfc.test.missing-plan-v1", "test_fw.bin");
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner);
        ExternalProcessorRequest request = new(
            "run-missing-plan",
            profile.ProcessorId,
            profile.ToolBindingId,
            CreateFirmwareImage(),
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("legacy-combiner.compiled-plan.missing", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Rejects a profile-approved tool binding that is absent from the executable registry.</summary>
    [Fact]
    public async Task TransformRejectsUnknownRegisteredToolBindingBeforeLaunch()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProfile profile = CreateCrcOnlyProfile(
            "nfc.test.unknown-tool-v1",
            "test_fw.bin",
            "missing-tool-binding");
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-unknown-tool",
            profile.ProcessorId,
            profile.ToolBindingId,
            CreateFirmwareImage(),
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.binding.unknown", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Rejects a Legacy Combiner executable whose bytes do not match its registered SHA-256.</summary>
    [Fact]
    public async Task TransformRejectsExecutableShaMismatchBeforeLaunch()
    {
        using var workspace = TempWorkspace.Create();
        _ = workspace.CreateToolExecutable();
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProfile profile = CreateCrcOnlyProfile("nfc.test.sha-mismatch-v1", "test_fw.bin");
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(new string('0', 64), runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-sha-mismatch",
            profile.ProcessorId,
            profile.ToolBindingId,
            CreateFirmwareImage(),
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.executable-sha.mismatch", Assert.Single(result.Issues).Code);
        Assert.Equal(0, runner.RunCount);
    }

    /// <summary>Rejects multiple work-image projections into the same staged file offset when bytes differ.</summary>
    [Fact]
    public async Task TransformRejectsConflictingStagedFileProjection()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = [0x10, 0x11, 0x20, 0x21];
        FakeProcessRunner runner = new(_ => throw new InvalidOperationException("Process should not run."));
        LegacyCombinerPostbuildProfile profile = CreateProjectionConflictProfile();
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-projection-conflict",
            "nfc.test.projection-conflict-v1",
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

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
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staging.unexpected-file", issue.Code);
        Assert.Equal(1, runner.RunCount);
    }

    /// <summary>Rejects an unexpected entry before a later command can execute.</summary>
    [Fact]
    public async Task TransformRejectsUnexpectedStagingFileBeforeNextCommand()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = [0x10, 0x20, 0x30, 0x40, 0x99, 0x60];
        FakeProcessRunner runner = new(startInfo =>
        {
            File.WriteAllText(Path.Combine(startInfo.WorkingDirectory, "output", "unexpected.log"), "unexpected");
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateCopyThenRestoreProfile();
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-unexpected-file-before-next-command",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staging.unexpected-file", issue.Code);
        Assert.Equal(1, runner.RunCount);
        _ = Assert.Single(result.ExecutedCommands);
    }

    /// <summary>Rejects postbuild runs that leave undeclared directories in the staging tree.</summary>
    [Fact]
    public async Task TransformRejectsUnexpectedStagingDirectory()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        FakeProcessRunner runner = new(startInfo =>
        {
            _ = Directory.CreateDirectory(Path.Combine(startInfo.WorkingDirectory, "output", "unexpected"));
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProfile profile = CreateCrcOnlyProfile("nfc.test.unexpected-directory-v1", "test_fw.bin");
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-unexpected-directory",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staging.unexpected-directory", issue.Code);
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
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                LegacyCombinerPostbuildCatalog.Nt51950.ProcessorId,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.write-range.violation", issue.Code);
    }

    /// <summary>Rejects shortened output from command families without approved normalization semantics.</summary>
    [Fact]
    public async Task TransformRejectsShortenedNonMergeCommandOutput()
    {
        using var workspace = TempWorkspace.Create();
        string sha256 = workspace.CreateToolExecutable();
        byte[] firmware = CreateFirmwareImage();
        var command = new LegacyCombinerPostbuildCommand(
            "normal-mode-shortened",
            LegacyCombinerCommandFamily.NormalMode,
            "CRC_Enable",
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
            "nfc.test.non-merge-shortened-output-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "non_merge_shortened_fw.bin",
            [command],
            [command],
            "test non-merge shortened output rejection");
        FakeProcessRunner runner = new(startInfo =>
        {
            string firmwarePath = startInfo.Arguments.First(argument =>
                argument.EndsWith("non_merge_shortened_fw.bin", StringComparison.Ordinal));
            File.WriteAllBytes(firmwarePath, File.ReadAllBytes(firmwarePath)[..0x20]);
            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        });
        LegacyCombinerPostbuildProcessor processor = workspace.CreateProcessor(sha256, runner, [profile]);
        ExternalProcessorRequest request = new(
            "run-non-merge-shortened",
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            firmware,
            [],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-tool.output-length.changed", Assert.Single(result.Issues).Code);
        Assert.Equal(1, runner.RunCount);
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
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            protocolPlan: CompileProtocolPlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));

        ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("legacy-combiner.staging.range-outside-input", issue.Code);
        Assert.Equal(1, runner.RunCount);
        ExternalProcessInvocation executed = Assert.Single(result.ExecutedCommands);
        Assert.Equal("NT51927BASED_GEN_CRC_MODE", executed.Arguments[0]);
    }
}
