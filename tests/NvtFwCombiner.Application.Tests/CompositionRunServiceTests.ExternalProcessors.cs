using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    private static CompositionRunRequest CreateExternalProcessorRequest()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-crc",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(1, 1)]),
                    OverlapPolicy.Reject,
                    "run synthetic external processor"),
            ]);
        return new CompositionRunRequest(
            "run-external",
            CreateCompiledComposition(
                plan,
                new LegacyCompiledCompositionIdentity(
                    "external-profile",
                    "1.0.0",
                    "NT-SYNTHETIC",
                    "external",
                    "standard-merge",
                    CompositionKind.Merge),
                "external.bin"),
            [],
            "external.bin");
    }

    private static CompositionRunRequest CreateFirmwareConfigBackupValidationRequest()
    {
        const int outputLength = 0x1100;
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", outputLength, 0),
            [new AddressSpace("output-image", outputLength, AddressSpaceMutability.Mutable)],
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    10,
                    "output-image",
                    new ByteRange(0, outputLength),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, outputLength)],
                        [new ByteRange(0, outputLength)]),
                    OverlapPolicy.ReplaceExisting,
                    "run synthetic postbuild before final output validation"),
            ]);
        CompiledValidationRequirement validation = CompiledValidationRequirements.FirmwareConfigBackupVersion(
            "verify-nvt-fwconfig-backup-version",
            "replace.ctrlram.fw-version-output-invalid",
            "replace.ctrlram.fw-version-output-mismatch",
            0x27,
            0x04);
        return new CompositionRunRequest(
            "run-fwconfig-final-output-validation",
            CreateCompiledComposition(
                plan,
                new LegacyCompiledCompositionIdentity(
                    "fwconfig-final-output-profile",
                    "1.0.0",
                    "NT-SYNTHETIC",
                    "external",
                    "ctrlram-replace",
                    CompositionKind.Replace),
                "fwconfig-final-output.bin",
                CompiledIcNumberPolicy.SingleSelector,
                [validation]),
            [],
            "fwconfig-final-output.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static CompositionRunRequest CreateStagedSourceExternalProcessorRequest(
        ByteRange? firmwareRange = null,
        string stagedSourceSpaceId = "ctrlram-input",
        IEnumerable<ExternalProcessorWriteRangeSection>? writeRangeSections = null)
    {
        ByteRange stagedFirmwareRange = firmwareRange ?? new ByteRange(1, 2);
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new(stagedSourceSpaceId, 2, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(0, 4)],
                        [
                            new ExternalProcessorStagedSourceBinding(
                                stagedSourceSpaceId,
                                new ByteRange(0, 2),
                                stagedFirmwareRange),
                        ],
                        allowedWriteRangeSections: writeRangeSections),
                    OverlapPolicy.ReplaceExisting,
                    "run synthetic postbuild"),
            ]);
        return new CompositionRunRequest(
            "run-staged-source",
            CreateCompiledComposition(
                plan,
                new LegacyCompiledCompositionIdentity(
                    "external-staged-source-profile",
                    "1.0.0",
                    "NT-SYNTHETIC",
                    "external",
                    "ctrlram-replace",
                    CompositionKind.Replace),
                "external-staged-source.bin",
                CompiledIcNumberPolicy.SingleSelector),
            [
                new InputArtifactBinding("reference-base", "reference-base", "reference-artifact"),
                new InputArtifactBinding(stagedSourceSpaceId, stagedSourceSpaceId, "ctrlram-artifact"),
            ],
            "external-staged-source.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static CompositionRunRequest CreateMappedExternalProcessorRequest()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("ctrlram-input", 2, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [
                CompositionOperation.ReplaceRange(
                    "replace-ctrlram",
                    10,
                    "ctrlram-input",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(1, 2),
                    OverlapPolicy.Reject,
                    "replace CtrlRAM payload"),
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    20,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(0, 4)]),
                    OverlapPolicy.ReplaceExisting,
                    "refresh header and integrity"),
            ]);
        return new CompositionRunRequest(
            "run-mapped-processor",
            CreateCompiledComposition(
                plan,
                new LegacyCompiledCompositionIdentity(
                    "mapped-processor-profile",
                    "1.0.0",
                    "NT-SYNTHETIC",
                    "external",
                    "ctrlram-replace",
                    CompositionKind.Replace),
                "mapped-processor.bin",
                CompiledIcNumberPolicy.SingleSelector),
            [
                new InputArtifactBinding("reference-base", "reference-base", "reference-artifact"),
                new InputArtifactBinding("ctrlram-input", "ctrlram-input", "ctrlram-artifact"),
            ],
            "mapped-processor.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private sealed class FakeExternalProcessor : IExternalProcessor
    {
        private readonly Func<ExternalProcessorRequest, ExternalProcessorResult> _transform;

        internal FakeExternalProcessor(Func<ExternalProcessorRequest, ExternalProcessorResult> transform)
        {
            _transform = transform;
        }

        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(_transform(request));
        }
    }
}
