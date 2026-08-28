using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Behavior evidence for fixed-workflow file-read resource admission.</summary>
public sealed class CompiledInputArtifactResourceCeilingTests
{
    private const string InputSpaceId = "resource-input";

    /// <summary>The default is the owner-approved decimal 100 MB inclusive ceiling.</summary>
    [Fact]
    public void UncompiledInspectionUsesTheDecimalHundredMegabyteHardCeiling()
    {
        Assert.Equal(
            100_000_000,
            CompiledInputArtifactInspectionService.MaximumContentReadBytes);
    }

    /// <summary>Exact containers narrow below the hard bound but cannot expand it.</summary>
    [Theory]
    [InlineData(99_999_999, 99_999_999)]
    [InlineData(100_000_000, 100_000_000)]
    [InlineData(100_000_001, 100_000_000)]
    public void ExactCompleteContainerNarrowsAtTheHardCeiling(
        long exactBytes,
        long expectedMaximum)
    {
        CompiledComposition composition = Composition(
            sourceLength: exactBytes,
            inputLengthRequirement: new CompiledExactBytesInputLengthRequirement(exactBytes));

        long maximum = CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
            composition,
            InputSpaceId);

        Assert.Equal(expectedMaximum, maximum);
    }

    /// <summary>Padding permits short input only and does not admit an oversized file.</summary>
    [Fact]
    public void PaddedExactInputStillUsesItsExactLengthAsTheMaximum()
    {
        CompiledComposition composition = Composition(sourceLength: 64, inputPaddingByte: 0xFF);

        long maximum = CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
            composition,
            InputSpaceId);

        Assert.Equal(64, maximum);
    }

    /// <summary>Explicit compiled maxima are reused as narrower resource limits.</summary>
    [Fact]
    public void ExplicitBoundedAndSourceViewMaximaNarrowTheHardCeiling()
    {
        CompiledComposition bounded = Composition(
            inputLengthRequirement: new CompiledBoundedInputLengthRequirement(1, 75_000_000));
        CompiledComposition sourceView = Composition(
            inputLengthRequirement: new CompiledSourceViewCoverageInputLengthRequirement(
                maximumBytes: 200_000));

        Assert.Equal(
            75_000_000,
            CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
                bounded,
                InputSpaceId));
        Assert.Equal(
            200_000,
            CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
                sourceView,
                InputSpaceId));
    }

    /// <summary>Prefix coverage and diagnostic outer lengths do not invent a maximum.</summary>
    [Fact]
    public void DeclaredPrefixAndExpectedOuterLengthsDoNotBecomeResourceMaxima()
    {
        CompiledComposition composition = Composition(
            inputLengthRequirement: new CompiledSourceViewCoverageInputLengthRequirement(
                expectedOuterLengths: [16, 32],
                unexpectedOuterLengthIssueCode: "TEST_OUTER_LENGTH",
                requiredEndExclusive: 16,
                shortInputIssueCode: "TEST_SHORT_INPUT"));

        long maximum = CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
            composition,
            InputSpaceId);

        Assert.Equal(100_000_000, maximum);
    }

    /// <summary>Only the existing CtrlRAM truncation contract may accept a larger container.</summary>
    [Fact]
    public void EvidencedCtrlRamTruncationRetainsTheHardCeilingForTrailingBytes()
    {
        CompiledComposition composition = Composition(
            sourceLength: 16,
            inputOversizePolicy: InputOversizePolicy.TruncateWithWarning,
            experienceId: ExperienceIds.CtrlRamReplace,
            compositionKind: CompositionKind.Replace);

        long maximum = CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
            composition,
            InputSpaceId);

        Assert.Equal(100_000_000, maximum);
    }

    /// <summary>An unbound address space cannot select a read policy.</summary>
    [Fact]
    public void UnknownAddressSpaceFailsClosed()
    {
        CompiledComposition composition = Composition(sourceLength: 16);

        _ = Assert.Throws<ArgumentException>(() =>
            CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
                composition,
                "unknown-input"));
    }

    private static CompiledComposition Composition(
        long sourceLength = 16,
        byte? inputPaddingByte = null,
        InputOversizePolicy inputOversizePolicy = InputOversizePolicy.Reject,
        string experienceId = ExperienceIds.StandardMerge,
        CompositionKind compositionKind = CompositionKind.Merge,
        CompiledInputLengthRequirement? inputLengthRequirement = null)
    {
        AddressSpace[] addressSpaces = compositionKind == CompositionKind.Replace
            ?
            [
                new AddressSpace("reference-input", sourceLength, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    InputSpaceId,
                    sourceLength,
                    AddressSpaceMutability.Immutable,
                    inputPaddingByte,
                    inputOversizePolicy),
                new AddressSpace("output-image", sourceLength, AddressSpaceMutability.Mutable),
            ]
            :
            [
                new AddressSpace(
                    InputSpaceId,
                    sourceLength,
                    AddressSpaceMutability.Immutable,
                    inputPaddingByte,
                    inputOversizePolicy),
                new AddressSpace("output-image", 16, AddressSpaceMutability.Mutable),
            ];
        var plan = new CompositionPlan(
            compositionKind == CompositionKind.Replace
                ? ImageInitialization.Reference("output-image", "reference-input", sourceLength)
                : ImageInitialization.Blank("output-image", 16, 0),
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-source",
                    100,
                    InputSpaceId,
                    new ByteRange(0, 1),
                    "output-image",
                    new ByteRange(0, 1),
                    OverlapPolicy.Reject,
                    "Copy one synthetic source byte."),
            ]);
        return CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                "resource-ceiling-test",
                "1.0.0",
                "NT-TEST",
                "standard",
                experienceId,
                compositionKind),
            "resource-ceiling.bin",
            inputLengthRequirement: inputLengthRequirement);
    }
}
