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

    /// <summary>Verifies named staging artifacts may read initialized work buffers but must stay in declared space bounds.</summary>
    [Fact]
    public void ExternalProcessorArtifactBindingAllowsMutableWorkBufferAndRejectsOutOfBoundsRange()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("a-work", 2, AddressSpaceMutability.Mutable),
        ];

        var validPlan = new CompositionPlan(
            [
                ImageInitialization.Blank("a-work", 2, 0),
                ImageInitialization.Blank("output-image", 4, 0),
            ],
            "output-image",
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "combine",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(0, 4)],
                        stagedArtifactBindings:
                        [new ExternalProcessorStagedArtifactBinding("a-bank", "a-work", new ByteRange(0, 2))]),
                    OverlapPolicy.Reject,
                    "combine work buffer"),
            ]);

        _ = Assert.Single(validPlan.OrderedOperations[0].ExternalProcessorInvocation!.StagedArtifactBindings);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompositionPlan(
            [
                ImageInitialization.Blank("a-work", 2, 0),
                ImageInitialization.Blank("output-image", 4, 0),
            ],
            "output-image",
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "combine-invalid",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(0, 4)],
                        stagedArtifactBindings:
                        [new ExternalProcessorStagedArtifactBinding("a-bank", "a-work", new ByteRange(1, 2))]),
                    OverlapPolicy.Reject,
                    "combine work buffer"),
            ]));
    }

    /// <summary>Verifies artifact snapshots cannot race a same-sequence write to their mutable source buffer.</summary>
    [Fact]
    public void ExternalProcessorArtifactBindingRejectsSameSequenceMutableWriteDependency()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("a-work", 2, AddressSpaceMutability.Mutable),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [
                ImageInitialization.Blank("a-work", 2, 0),
                ImageInitialization.Blank("output-image", 4, 0),
            ],
            "output-image",
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "prepare-a-bank",
                    10,
                    "a-work",
                    new ByteRange(0, 2),
                    0xA1,
                    OverlapPolicy.Reject,
                    "prepare A bank"),
                CompositionOperation.RunExternalProcessor(
                    "combine",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(0, 4)],
                        stagedArtifactBindings:
                        [new ExternalProcessorStagedArtifactBinding("a-bank", "a-work", new ByteRange(0, 2))]),
                    OverlapPolicy.Reject,
                    "combine work buffer"),
            ]));
    }

    /// <summary>Verifies malformed artifact-binding collections fail with a contract exception before sorting.</summary>
    [Fact]
    public void ExternalProcessorArtifactBindingRejectsNullEntry()
    {
        _ = Assert.Throws<ArgumentException>(() => new ExternalProcessorInvocation(
            "processor-v1",
            "tool-v1",
            [new ByteRange(0, 1)],
            [new ByteRange(0, 1)],
            stagedArtifactBindings: [null!]));
    }

    /// <summary>Verifies an imported processor postcondition cannot be mutated after invocation validation.</summary>
    [Fact]
    public void ExternalProcessorOutputAssertionsAreReadOnly()
    {
        var invocation = new ExternalProcessorInvocation(
            "processor-v1",
            "tool-v1",
            [new ByteRange(0, 4)],
            [new ByteRange(2, 1)],
            outputAssertions: [new ExternalProcessorOutputAssertion(new ByteRange(2, 1), [0x44])]);

        var assertions = (IList<ExternalProcessorOutputAssertion>)invocation.OutputAssertions;

        Assert.True(assertions.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => assertions[0] = new ExternalProcessorOutputAssertion(
            new ByteRange(2, 1),
            [0x43]));
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
