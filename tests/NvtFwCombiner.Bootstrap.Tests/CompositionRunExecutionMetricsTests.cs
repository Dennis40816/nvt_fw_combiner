using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks deterministic automatic-Build counters before Replace orchestration optimization.</summary>
public sealed class CompositionRunExecutionMetricsTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Locks the current automatic-Build run and input-read counts for each DP container capacity.</summary>
    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x80000)]
    [InlineData(0x100000)]
    public async Task AutomaticReferenceReplaceBaselineCountsPreviewAndBuildForEachDpCapacity(int outputLength)
    {
        byte[] referenceBytes = CreateFilledBytes(outputLength, 0x11);
        byte[] replacementBytes = CreateFilledBytes(outputLength, 0x22);
        var reader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact"] = referenceBytes,
            ["replacement-artifact"] = replacementBytes,
        });
        var writer = new CountingOutputWriter();
        var clock = new CountingClock();
        var service = new CompositionRunService(
            reader,
            clock,
            writer);

        CompositionRunResult result = await CompositionRunExecutionSupport
            .PreviewOrBuildAsync(
                service,
                CreateDpReplaceRequest(outputLength),
                build: true,
                CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(replacementBytes, result.OutputBytes.ToArray());
        Assert.Equal(2, clock.RunCount);
        Assert.Equal(4, reader.CallCount);
        Assert.Equal(4, reader.SuccessfulReadCount);
        Assert.Equal(1, writer.CallCount);
    }

    /// <summary>Verifies standalone Preview records one run without committing output.</summary>
    [Fact]
    public async Task PreviewOnlyBaselineRecordsOneRunWithoutBuildOrCommit()
    {
        const int outputLength = 0x40000;
        var reader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact"] = CreateFilledBytes(outputLength, 0x11),
            ["replacement-artifact"] = CreateFilledBytes(outputLength, 0x22),
        });
        var writer = new CountingOutputWriter();
        var clock = new CountingClock();
        var service = new CompositionRunService(reader, clock, writer);

        CompositionRunResult result = await CompositionRunExecutionSupport
            .PreviewOrBuildAsync(
                service,
                CreateDpReplaceRequest(outputLength),
                build: false,
                CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(1, clock.RunCount);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(2, reader.SuccessfulReadCount);
        Assert.Equal(0, writer.CallCount);
    }

    /// <summary>Verifies a failed Preview records no Build or output commitment.</summary>
    [Fact]
    public async Task AutomaticBuildPreviewFailureRecordsNoBuildOrCommit()
    {
        const int outputLength = 0x40000;
        var reader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact"] = CreateFilledBytes(outputLength, 0x11),
        });
        var writer = new CountingOutputWriter();
        var clock = new CountingClock();
        var service = new CompositionRunService(reader, clock, writer);

        CompositionRunResult result = await CompositionRunExecutionSupport
            .PreviewOrBuildAsync(
                service,
                CreateDpReplaceRequest(outputLength),
                build: true,
                CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Equal(1, clock.RunCount);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(1, reader.SuccessfulReadCount);
        Assert.Equal(0, writer.CallCount);
    }

    /// <summary>Locks the current automatic-Build processor-session and command counts for CtrlRAM plans.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(13)]
    public async Task AutomaticCtrlRamBaselineCountsDuplicateProcessorSessionsAndCommands(int commandCount)
    {
        const int outputLength = 64;
        const int ctrlRamLength = 8;
        byte[] referenceBytes = CreateFilledBytes(outputLength, 0x11);
        byte[] replacementBytes = CreateFilledBytes(ctrlRamLength, 0x22);
        var reader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact"] = referenceBytes,
            ["ctrlram-artifact"] = replacementBytes,
        });
        var writer = new CountingOutputWriter();
        var processor = new CountingExternalProcessor(commandCount);
        var clock = new CountingClock();
        var service = new CompositionRunService(
            reader,
            clock,
            writer,
            processor);

        CompositionRunResult result = await CompositionRunExecutionSupport
            .PreviewOrBuildAsync(
                service,
                CreateCtrlRamReplaceRequest(outputLength, ctrlRamLength),
                build: true,
                CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(replacementBytes, result.OutputBytes[..ctrlRamLength].ToArray());
        Assert.Equal(2, clock.RunCount);
        Assert.Equal(4, reader.CallCount);
        Assert.Equal(4, reader.SuccessfulReadCount);
        Assert.Equal(2, processor.CallCount);
        Assert.Equal(checked(commandCount * 2), processor.InvocationCount);
        Assert.Equal(commandCount, Assert.Single(result.Report.Operations).ExecutedCommands.Count);
        Assert.Equal(1, writer.CallCount);
    }

    private static CompositionRunRequest CreateDpReplaceRequest(int outputLength)
    {
        var outputRange = new ByteRange(0, outputLength);
        var profile = new CompositionProfileDefinition(
            "synthetic-performance-dp-replace",
            "1.0.0",
            "NT-SYNTHETIC",
            IcWorkflowIds.DpReplace,
            CompositionKind.Replace,
            IcWorkflowIds.DpReplace,
            "synthetic-performance-dp-replace.bin",
            ImageInitialization.Reference(
                CompositionAddressSpaceIds.OutputImage,
                CompositionAddressSpaceIds.ReferenceBase,
                outputLength),
            [
                new AddressSpace(
                    CompositionAddressSpaceIds.ReferenceBase,
                    outputLength,
                    AddressSpaceMutability.Immutable),
                new AddressSpace(
                    CompositionAddressSpaceIds.DpReplacement,
                    outputLength,
                    AddressSpaceMutability.Immutable),
                new AddressSpace(
                    CompositionAddressSpaceIds.OutputImage,
                    outputLength,
                    AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp",
                    100,
                    CompositionAddressSpaceIds.DpReplacement,
                    outputRange,
                    CompositionAddressSpaceIds.OutputImage,
                    outputRange,
                    OverlapPolicy.Reject,
                    "Replace the complete synthetic DP performance range."),
            ],
            [
                new ProfileRegion(
                    "dp",
                    CompositionAddressSpaceIds.OutputImage,
                    outputRange,
                    RegionAtomicity.Whole,
                    RegionWritePolicy.WholeOnly,
                    classificationTags: ["dp"]),
            ],
            [new RegionAccessRule("dp", RegionAccessKind.Whole, "Synthetic DP performance baseline.")],
            IcNumberInputMode.SingleSelector);
        return CreateRequest(
            profile,
            [
                new InputArtifactBinding(
                    CompositionAddressSpaceIds.ReferenceBase,
                    "reference-base",
                    "reference-artifact"),
                new InputArtifactBinding(
                    CompositionAddressSpaceIds.DpReplacement,
                    "dp-replacement",
                    "replacement-artifact"),
            ],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static CompositionRunRequest CreateCtrlRamReplaceRequest(int outputLength, int ctrlRamLength)
    {
        var outputRange = new ByteRange(0, outputLength);
        var ctrlRamRange = new ByteRange(0, ctrlRamLength);
        var profile = new CompositionProfileDefinition(
            "synthetic-performance-ctrlram-replace",
            "1.0.0",
            "NT-SYNTHETIC",
            IcWorkflowIds.CtrlRamReplace,
            CompositionKind.Replace,
            IcWorkflowIds.CtrlRamReplace,
            "synthetic-performance-ctrlram-replace.bin",
            ImageInitialization.Reference(
                CompositionAddressSpaceIds.OutputImage,
                CompositionAddressSpaceIds.ReferenceBase,
                outputLength),
            [
                new AddressSpace(
                    CompositionAddressSpaceIds.ReferenceBase,
                    outputLength,
                    AddressSpaceMutability.Immutable),
                new AddressSpace(
                    CompositionAddressSpaceIds.CtrlRamReplacement,
                    ctrlRamLength,
                    AddressSpaceMutability.Immutable),
                new AddressSpace(
                    CompositionAddressSpaceIds.OutputImage,
                    outputLength,
                    AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    100,
                    CompositionAddressSpaceIds.OutputImage,
                    outputRange,
                    new ExternalProcessorInvocation(
                        "synthetic-postbuild",
                        "synthetic-tool",
                        [outputRange],
                        [ctrlRamRange],
                        [
                            new ExternalProcessorStagedSourceBinding(
                                CompositionAddressSpaceIds.CtrlRamReplacement,
                                ctrlRamRange,
                                ctrlRamRange),
                        ]),
                    OverlapPolicy.Reject,
                    "Run the synthetic CtrlRAM performance postbuild."),
            ],
            [
                new ProfileRegion(
                    "ctrlram",
                    CompositionAddressSpaceIds.OutputImage,
                    ctrlRamRange,
                    RegionAtomicity.Whole,
                    RegionWritePolicy.WholeOnly,
                    processorDependencyIds: ["synthetic-postbuild"],
                    classificationTags: ["tp-ctrlram"]),
            ],
            [new RegionAccessRule("ctrlram", RegionAccessKind.Whole, "Synthetic CtrlRAM performance baseline.")],
            IcNumberInputMode.SingleSelector);
        return CreateRequest(
            profile,
            [
                new InputArtifactBinding(
                    CompositionAddressSpaceIds.ReferenceBase,
                    "reference-base",
                    "reference-artifact"),
                new InputArtifactBinding(
                    CompositionAddressSpaceIds.CtrlRamReplacement,
                    "ctrlram-replacement",
                    "ctrlram-artifact"),
            ],
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static CompositionRunRequest CreateRequest(
        CompositionProfileDefinition profile,
        IReadOnlyList<InputArtifactBinding> bindings,
        IcNumberSelection selection)
    {
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(
            compile.IsSuccess,
            string.Join(Environment.NewLine, compile.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        return new CompositionRunRequest(
            "run-performance-baseline",
            compile.CompiledComposition!,
            bindings,
            profile.DefaultOutputFileName,
            icNumberSelection: selection);
    }

    private static byte[] CreateFilledBytes(int length, byte value)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    private sealed class CountingArtifactReader : IArtifactReader
    {
        private readonly IReadOnlyDictionary<string, byte[]> _artifacts;

        internal CountingArtifactReader(IReadOnlyDictionary<string, byte[]> artifacts)
        {
            _artifacts = artifacts;
        }

        internal int CallCount { get; private set; }

        internal int SuccessfulReadCount { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            string artifactId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            byte[] bytes = _artifacts[artifactId];
            SuccessfulReadCount++;
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(bytes);
        }
    }

    private sealed class CountingClock : ISystemClock
    {
        private int _callCount;

        internal int RunCount
        {
            get
            {
                Assert.Equal(0, _callCount % 2);
                return _callCount / 2;
            }
        }

        public DateTimeOffset UtcNow => StartedAtUtc.AddSeconds(_callCount++);
    }

    private sealed class CountingOutputWriter : ICompositionOutputWriter
    {
        internal int CallCount { get; private set; }

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult($"committed:{fileName}");
        }
    }

    private sealed class CountingExternalProcessor : IExternalProcessor
    {
        private readonly int _commandCount;

        internal CountingExternalProcessor(int commandCount)
        {
            _commandCount = commandCount;
        }

        internal int CallCount { get; private set; }

        internal int InvocationCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            byte[] output = request.InputBytes.ToArray();
            ExternalProcessorStagedSource source = Assert.Single(request.StagedSources);
            source.Bytes.Span.CopyTo(output.AsSpan(
                checked((int)source.FirmwareRange.Start),
                checked((int)source.FirmwareRange.Length)));
            ExternalProcessInvocation[] commands = [
                .. Enumerable.Range(0, _commandCount).Select(index =>
                    new ExternalProcessInvocation(
                        "C:\\tools\\Combiner.exe",
                        $"C:\\staging\\performance-{CallCount}",
                        ["CRC_Enable", $"command-{index + 1}"])),
            ];
            InvocationCount = checked(InvocationCount + commands.Length);
            return ValueTask.FromResult(ExternalProcessorResult.Success(
                output,
                [source.FirmwareRange],
                commands));
        }
    }
}
