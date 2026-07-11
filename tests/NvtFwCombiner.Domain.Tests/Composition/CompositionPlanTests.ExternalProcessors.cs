using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionPlanTests
{
    /// <summary>Verifies external staged source bytes must come from immutable input address spaces.</summary>
    [Fact]
    public void ExternalProcessorStagedSourceRejectsMutableSourceSpace()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 2, AddressSpaceMutability.Mutable),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
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
                        [new ByteRange(1, 2)],
                        [
                            new ExternalProcessorStagedSourceBinding(
                                "scratch",
                                new ByteRange(0, 2),
                                new ByteRange(1, 2)),
                        ]),
                    OverlapPolicy.ReplaceExisting,
                    "run postbuild"),
            ]));
    }

    /// <summary>Verifies CtrlRAM replace plans may combine truncation with external CRC/header processors.</summary>
    [Fact]
    public void ExternalProcessorAllowsInputTruncationForCtrlRamReplace()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("ctrlram-input", 2, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];

        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-crc",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "crc-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(3, 1)]),
                    OverlapPolicy.Reject,
                    "run crc"),
            ]);

        Assert.Contains("ctrlram-input", plan.RequiredInputAddressSpaceIds);
    }

    /// <summary>Verifies processor plans cannot receive fabricated padded bytes.</summary>
    [Fact]
    public void ExternalProcessorRejectsInputPadding()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("source", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-crc",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "crc-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(3, 1)]),
                    OverlapPolicy.Reject,
                    "run crc"),
            ]));
    }
}
