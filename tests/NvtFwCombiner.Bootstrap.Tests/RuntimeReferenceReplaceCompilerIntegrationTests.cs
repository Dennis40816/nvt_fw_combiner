using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <inheritdoc/>
public sealed class RuntimeReferenceReplaceCompilerIntegrationTests
{
    private const int ReferenceCapacity = 0x3C000;
    private const int FirmwareConfigBackupStart = 0x3B000;
    private const int VnStart = 0x315D0;

    /// <summary>Verifies a compiler-lowered CtrlRAM candidate reaches Application Preview through a synthetic identity processor without mutating caller inputs.</summary>
    [Fact]
    public async Task CompilerLoweredRuntimeReferenceCandidateRunsThroughSharedApplicationEngine()
    {
        byte[] reference = new byte[ReferenceCapacity];
        reference[FirmwareConfigBackupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = 1;
        reference[FirmwareConfigBackupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 4;
        reference[FirmwareConfigBackupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = 1;
        ReadOnlySpan<byte> nvtMarker = [0x00, 0x4E, 0x56, 0x54];
        nvtMarker.CopyTo(reference.AsSpan(0x3BFFC));
        reference[VnStart + 2] = 0x5A;
        byte[] source = [0xAA, 0xBB, 0xCC, 0xDD];
        byte[] originalReference = [.. reference];
        byte[] originalSource = [.. source];

        V2CompositionPlanCompileResult result = BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].CompileRuntimeReferenceReplace(
            "nt51926-ctrlram-replace-fw141-runtime-cascade",
            "0.4.0",
            "NT51926",
            ExperienceIds.CtrlRamReplace,
            new TopologySelection(2, "cascade", TopologySelectionSource.Requested, "ic-number"),
            [new FirmwareArtifactPayload("reference-base", reference)],
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2ExplicitMappingInputBinding("reference-base", "reference-base", ReferenceCapacity),
                    new V2ExplicitMappingInputBinding("source-a", "ctrlram-source", source.Length),
                ],
                [new ExplicitMapping(
                    "replace-source",
                    10,
                    ExplicitMappingOperationKind.ReplaceRange,
                    "source-a",
                    new ByteRange(2, 2),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(VnStart, 2),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "Synthetic runtime CtrlRAM Replace mapping")]));

        Assert.True(result.IsCompiled, string.Join(Environment.NewLine, result.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        Assert.Equal(CompiledProfilePromotionStage.Supported, composition.V2Details.Provenance.Promotion.Stage);
        var writer = new RecordingOutputWriter();
        var processor = new PassThroughProcessor();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["base-artifact"] = reference,
                ["source-artifact"] = source,
            }),
            new FakeClock([
                new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 15, 0, 0, 1, TimeSpan.Zero),
            ]),
            writer,
            processor);
        var request = new CompositionRunRequest(
            "compiler-lowered-runtime-reference",
            composition,
            [
                new InputArtifactBinding(
                    "reference-base",
                    "reference-base",
                    "base-artifact",
                    "base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "source-a",
                    "source-a",
                    "source-artifact",
                    "source.bin",
                    CompiledInputArtifactClass.CtrlRamReplacement),
            ],
            "runtime-ctrlram-replace.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        CompositionRunResult preview = await service.PreviewAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        byte[] expected = [.. originalReference];
        expected[VnStart] = 0xCC;
        expected[VnStart + 1] = 0xDD;
        Assert.Equal(expected, preview.OutputBytes.ToArray());
        Assert.Equal(originalReference, reference);
        Assert.Equal(originalSource, source);
        Assert.Equal(composition.CompilationFingerprint, preview.Report.CompilationFingerprint);
        Assert.Equal(
            ["reference-base", "source-a"],
            preview.Report.Inputs.Select(static input => input.ArtifactId).Order(StringComparer.Ordinal));
        Assert.False(writer.WasCalled);
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(
            [
                new ByteRange(0x18, 4),
                new ByteRange(0x1C, 4),
                new ByteRange(0x3C, 4),
                new ByteRange(0x4C, 4),
                new ByteRange(0x5C, 4),
                new ByteRange(0xFC, 4),
                new ByteRange(VnStart, 2),
                new ByteRange(0x32F50, 0x100),
                new ByteRange(0x3B000, 0x800),
            ],
            processor.AllowedWriteRanges);
    }

    private sealed class PassThroughProcessor : IExternalProcessor
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<ByteRange> AllowedWriteRanges { get; private set; } = [];

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            CallCount++;
            AllowedWriteRanges = request.AllowedWriteRanges;
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, [], []));
        }
    }

    private sealed class RecordingOutputWriter : ICompositionOutputWriter
    {
        public bool WasCalled { get; private set; }

        public ValueTask<CompositionOutputCommitReceipt> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return ValueTask.FromResult(CompositionOutputCommitReceipt.CreateLoose(
                $"committed:{fileName}", fileName, outputBytes.Span));
        }
    }
}
