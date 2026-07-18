using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Compares deterministic automatic-Build counters and parity before and after orchestration optimization.</summary>
public sealed class CompositionRunExecutionMetricsTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies automatic DP Build preserves the approved two-run result with one run and one input pass.</summary>
    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x80000)]
    [InlineData(0x100000)]
    public async Task AutomaticReferenceReplaceMatchesBaselineWithOneRunForEachDpCapacity(int outputLength)
    {
        byte[] referenceBytes = CreateFilledBytes(outputLength, 0x11);
        byte[] replacementBytes = CreateFilledBytes(outputLength, 0x22);
        var baselineReader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact.bin"] = referenceBytes,
            ["replacement-artifact.bin"] = replacementBytes,
        });
        var baselineWriter = new CountingOutputWriter();
        var baselineClock = new CountingClock();
        var baselineService = new CompositionRunService(
            baselineReader,
            baselineClock,
            baselineWriter);
        CompositionRunRequest request = CreateDpReplaceRequest(outputLength);

        CompositionRunResult baseline = await PreviewThenBuildAsync(baselineService, request);

        var reader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact.bin"] = referenceBytes,
            ["replacement-artifact.bin"] = replacementBytes,
        });
        var writer = new CountingOutputWriter();
        var clock = new CountingClock();
        var service = new CompositionRunService(
            reader,
            clock,
            writer);

        CompositionRunResult result = await service
            .PreviewOrBuildAsync(
                request,
                build: true,
                CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(
            CreateExpectedDpReplaceOutput(request.CompiledComposition.Plan, referenceBytes, replacementBytes),
            result.OutputBytes.ToArray());
        AssertRunParity(baseline, result);
        Assert.Equal(2, baselineClock.RunCount);
        Assert.Equal(4, baselineReader.CallCount);
        Assert.Equal(4, baselineReader.SuccessfulReadCount);
        Assert.Equal(1, baselineWriter.CallCount);
        Assert.Equal(1, clock.RunCount);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(2, reader.SuccessfulReadCount);
        Assert.Equal(1, writer.CallCount);
    }

    /// <summary>Verifies standalone Preview records one run without committing output.</summary>
    [Fact]
    public async Task PreviewOnlyBaselineRecordsOneRunWithoutBuildOrCommit()
    {
        const int outputLength = 0x40000;
        var reader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact.bin"] = CreateFilledBytes(outputLength, 0x11),
            ["replacement-artifact.bin"] = CreateFilledBytes(outputLength, 0x22),
        });
        var writer = new CountingOutputWriter();
        var clock = new CountingClock();
        var service = new CompositionRunService(reader, clock, writer);

        CompositionRunResult result = await service
            .PreviewOrBuildAsync(
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
            ["reference-artifact.bin"] = CreateFilledBytes(outputLength, 0x11),
        });
        var writer = new CountingOutputWriter();
        var clock = new CountingClock();
        var service = new CompositionRunService(reader, clock, writer);

        CompositionRunResult result = await service
            .PreviewOrBuildAsync(
                CreateDpReplaceRequest(outputLength),
                build: true,
                CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Equal(1, clock.RunCount);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(1, reader.SuccessfulReadCount);
        Assert.Equal(0, writer.CallCount);
    }

    /// <summary>Verifies automatic CtrlRAM Build preserves report/output parity with one processor session.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(13)]
    public async Task AutomaticCtrlRamMatchesBaselineWithOneProcessorSession(int commandCount)
    {
        const int outputLength = 64;
        const int ctrlRamLength = 8;
        byte[] referenceBytes = CreateFilledBytes(outputLength, 0x11);
        byte[] replacementBytes = CreateFilledBytes(ctrlRamLength, 0x22);
        var baselineReader = new CountingArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact"] = referenceBytes,
            ["ctrlram-artifact"] = replacementBytes,
        });
        var baselineWriter = new CountingOutputWriter();
        var baselineProcessor = new CountingExternalProcessor(commandCount);
        var baselineClock = new CountingClock();
        var baselineService = new CompositionRunService(
            baselineReader,
            baselineClock,
            baselineWriter,
            baselineProcessor);
        CompositionRunRequest request = CreateCtrlRamReplaceRequest(outputLength, ctrlRamLength);

        CompositionRunResult baseline = await PreviewThenBuildAsync(baselineService, request);

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

        CompositionRunResult result = await service
            .PreviewOrBuildAsync(
                request,
                build: true,
                CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(replacementBytes, result.OutputBytes[..ctrlRamLength].ToArray());
        AssertRunParity(baseline, result);
        Assert.Equal(2, baselineClock.RunCount);
        Assert.Equal(4, baselineReader.CallCount);
        Assert.Equal(4, baselineReader.SuccessfulReadCount);
        Assert.Equal(2, baselineProcessor.CallCount);
        Assert.Equal(checked(commandCount * 2), baselineProcessor.InvocationCount);
        Assert.Equal(1, baselineWriter.CallCount);
        Assert.Equal(1, clock.RunCount);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(2, reader.SuccessfulReadCount);
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(commandCount, processor.InvocationCount);
        Assert.Equal(commandCount, Assert.Single(result.Report.Operations).ExecutedCommands.Count);
        Assert.Equal(1, writer.CallCount);
    }

    private static async ValueTask<CompositionRunResult> PreviewThenBuildAsync(
        CompositionRunService service,
        CompositionRunRequest request)
    {
        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);
        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        return await service.BuildAsync(
            request.WithApprovedPreviewToken(Assert.IsType<string>(preview.PreviewToken)),
            CancellationToken.None);
    }

    private static void AssertRunParity(CompositionRunResult baseline, CompositionRunResult optimized)
    {
        Assert.Equal(baseline.Status, optimized.Status);
        Assert.Equal(baseline.OutputBytes.ToArray(), optimized.OutputBytes.ToArray());
        Assert.Equal(baseline.Report.Output.Size, optimized.Report.Output.Size);
        Assert.Equal(baseline.Report.Output.Sha256, optimized.Report.Output.Sha256);
        Assert.Equal(
            baseline.Report.Mutations.Select(static mutation => (
                mutation.OperationId,
                mutation.Kind,
                mutation.TargetSpaceId,
                mutation.TargetRange,
                mutation.ChangedByteCount,
                mutation.BeforeSha256,
                mutation.AfterSha256,
                mutation.Reason)),
            optimized.Report.Mutations.Select(static mutation => (
                mutation.OperationId,
                mutation.Kind,
                mutation.TargetSpaceId,
                mutation.TargetRange,
                mutation.ChangedByteCount,
                mutation.BeforeSha256,
                mutation.AfterSha256,
                mutation.Reason)));
        Assert.Equal(
            baseline.Report.Operations.SelectMany(static operation =>
                operation.ExecutedCommands.SelectMany(static command => command.Arguments)),
            optimized.Report.Operations.SelectMany(static operation =>
                operation.ExecutedCommands.SelectMany(static command => command.Arguments)));
        Assert.Equal(
            baseline.Report.Validations.Select(static validation => (
                validation.RuleId,
                validation.Stage,
                validation.Status,
                validation.Severity,
                validation.IssueCode)),
            optimized.Report.Validations.Select(static validation => (
                validation.RuleId,
                validation.Stage,
                validation.Status,
                validation.Severity,
                validation.IssueCode)));
        Assert.Equal(
            baseline.Report.Issues.Select(static issue => (issue.Code, issue.Message, issue.OperationId)),
            optimized.Report.Issues.Select(static issue => (issue.Code, issue.Message, issue.OperationId)));
    }

    private static CompositionRunRequest CreateDpReplaceRequest(int outputLength)
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51950",
            outputLength,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(registered);
        Assert.Empty(issues);
        CompiledComposition compiledComposition = Assert.IsType<CompiledComposition>(composition);
        return CreateRequest(
            compiledComposition,
            [
                CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    CompositionAddressSpaceIds.ReferenceBase,
                    "reference-artifact.bin"),
                CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    CompositionAddressSpaceIds.DpReplacement,
                    "replacement-artifact.bin"),
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
            [new RegionAccessRule("ctrlram", RegionAccessKind.Whole)],
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
        var plan = new CompositionPlan(
            profile.Initialization,
            profile.AddressSpaces,
            profile.Operations);
        CompiledComposition composition = CompiledComposition.CreateLegacy(
            plan,
            new LegacyCompiledCompositionIdentity(
                profile.ProfileId,
                profile.ProfileVersion,
                profile.IcId,
                profile.ModeId,
                profile.ExperienceId,
                profile.CompositionKind),
            profile.DefaultOutputFileName,
            CompiledIcNumberPolicy.SingleSelector,
            profile.ValidationRequirements);
        return CreateRequest(composition, bindings, selection);
    }

    private static CompositionRunRequest CreateRequest(
        CompiledComposition composition,
        IReadOnlyList<InputArtifactBinding> bindings,
        IcNumberSelection selection)
    {
        return new CompositionRunRequest(
            "run-performance-baseline",
            composition,
            bindings,
            composition.DefaultOutputFileName,
            icNumberSelection: selection);
    }

    private static byte[] CreateExpectedDpReplaceOutput(
        CompositionPlan plan,
        byte[] referenceBytes,
        byte[] replacementBytes)
    {
        byte[] expected = [.. replacementBytes];
        foreach (CompositionOperation operation in plan.OrderedOperations.Where(static operation =>
                     string.Equals(
                         operation.SourceSpaceId,
                         CompositionAddressSpaceIds.ReferenceBase,
                         StringComparison.Ordinal)))
        {
            ByteRange sourceRange = operation.SourceRange ?? throw new InvalidOperationException(
                $"Reference restore operation '{operation.OperationId}' has no source range.");
            ByteRange targetRange = operation.TargetRange;
            referenceBytes.AsSpan(
                    checked((int)sourceRange.Start),
                    checked((int)sourceRange.Length))
                .CopyTo(expected.AsSpan(
                    checked((int)targetRange.Start),
                    checked((int)targetRange.Length)));
        }

        return expected;
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
                        "synthetic-combiner",
                        $"synthetic-staging-session-{CallCount}",
                        ["synthetic-mode", $"command-{index + 1}"])),
            ];
            InvocationCount = checked(InvocationCount + commands.Length);
            return ValueTask.FromResult(ExternalProcessorResult.Success(
                output,
                [source.FirmwareRange],
                commands));
        }
    }
}
