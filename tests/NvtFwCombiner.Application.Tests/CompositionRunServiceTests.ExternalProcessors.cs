using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
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
            new CompositionRunProfile(
                "external-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "external",
                "standard-merge",
                CompositionKind.Merge),
            plan,
            [],
            "external.bin");
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
            new CompositionRunProfile(
                "external-staged-source-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "external",
                "ctrlram-replace",
                CompositionKind.Replace,
                IcNumberInputMode.SingleSelector),
            plan,
            [
                new InputArtifactBinding("reference-base", "reference-base", "reference-artifact"),
                new InputArtifactBinding(stagedSourceSpaceId, stagedSourceSpaceId, "ctrlram-artifact"),
            ],
            "external-staged-source.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static CompositionRunRequest CreateNt51926HeaderSemanticRequest()
    {
        const string ctrlRamSpaceId = "replace-ctrlram-normal";
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 0x100, AddressSpaceMutability.Immutable),
            new(ctrlRamSpaceId, 2, AddressSpaceMutability.Immutable),
            new("output-image", 0x100, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 0x100),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    10,
                    "output-image",
                    new ByteRange(0, 0x100),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 0x100)],
                        [new ByteRange(0, 0x100)],
                        [
                            new ExternalProcessorStagedSourceBinding(
                                ctrlRamSpaceId,
                                new ByteRange(0, 2),
                                new ByteRange(0x40, 2)),
                        ],
                        allowedWriteRangeSections:
                        [
                            new ExternalProcessorWriteRangeSection(
                                TpHeaderSectionIds.FlashHeaderCrc,
                                new ByteRange(0x1C, 4)),
                        ]),
                    OverlapPolicy.ReplaceExisting,
                    "run NT51926 postbuild"),
            ]);
        return new CompositionRunRequest(
            "run-nt51926-header-semantic",
            new CompositionRunProfile(
                "nt51926-header-semantic-profile",
                "1.0.0",
                "NT51926",
                "external",
                "ctrlram-replace",
                CompositionKind.Replace,
                IcNumberInputMode.SingleSelector),
            plan,
            [
                new InputArtifactBinding("reference-base", "reference-base", "reference-artifact"),
                new InputArtifactBinding(ctrlRamSpaceId, ctrlRamSpaceId, "ctrlram-artifact"),
            ],
            "nt51926-header-semantic.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static CompositionRunRequest CreateNt51927CopiedHeaderSemanticRequest()
    {
        const long outputLength = 0x1E3C0;
        var headerCopyRange = new ByteRange(0x1E230, 0x190);
        var headerSourceRange = new ByteRange(0x200, 0x190);
        AddressSpace[] addressSpaces =
        [
            new("reference-base", outputLength, AddressSpaceMutability.Immutable),
            new("output-image", outputLength, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", outputLength),
            addressSpaces,
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
                        [headerCopyRange],
                        allowedWriteRangeSections:
                        [
                            new ExternalProcessorWriteRangeSection(
                                TpHeaderSectionIds.HeaderCopyMaster,
                                headerCopyRange,
                                headerSourceRange),
                        ]),
                    OverlapPolicy.ReplaceExisting,
                    "run NT51927 postbuild"),
            ]);
        return new CompositionRunRequest(
            "run-nt51927-copied-header-semantic",
            new CompositionRunProfile(
                "nt51927-copied-header-semantic-profile",
                "1.0.0",
                "NT51927",
                "external",
                "ctrlram-replace",
                CompositionKind.Replace,
                IcNumberInputMode.NumericSelector),
            plan,
            [new InputArtifactBinding("reference-base", "reference-base", "reference-artifact")],
            "nt51927-copied-header-semantic.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));
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
