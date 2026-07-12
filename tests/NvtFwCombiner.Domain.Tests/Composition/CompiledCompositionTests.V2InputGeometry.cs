using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies TP-maximum artifacts fail closed when their immutable source space has no resolved source view.</summary>
    [Fact]
    public void V2PlanArtifactRejectsTpInputWithoutResolvedSourceView()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateTpComposition(
            sourceLength: 4,
            outputCapacity: 4,
            sourceRanges: []));
    }

    /// <summary>Verifies TP-maximum source capacity cannot exceed the exact maximum end-exclusive source-view span.</summary>
    [Fact]
    public void V2PlanArtifactRejectsTpInputSpaceLargerThanResolvedSourceSpan()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateTpComposition(
            sourceLength: 8,
            outputCapacity: 8,
            sourceRanges: [new ByteRange(0, 4)]));
    }

    /// <summary>Verifies TP-maximum source policy rejects a sole allowed artifact length that differs from its resolved span.</summary>
    [Fact]
    public void V2PlanArtifactRejectsTpInputAllowedLengthDifferentFromResolvedSpan()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateTpComposition(
            sourceLength: 8,
            outputCapacity: 8,
            sourceRanges: [new ByteRange(0, 8)],
            allowedInputLengths: [4]));
    }

    /// <summary>Verifies discontiguous TP views bind their greatest end-exclusive coordinate and that geometry changes identity.</summary>
    [Fact]
    public void V2PlanArtifactDerivesTpInputSpanFromMaximumResolvedViewEnd()
    {
        CompiledComposition shortSpan = CreateTpComposition(
            sourceLength: 4,
            outputCapacity: 32,
            sourceRanges: [new ByteRange(0, 4)]);
        CompiledComposition discontiguousSpan = CreateTpComposition(
            sourceLength: 28,
            outputCapacity: 32,
            sourceRanges: [new ByteRange(0, 4), new ByteRange(20, 8)]);

        AddressSpace input = Assert.Single(discontiguousSpan.Plan.AddressSpaces, space => space.AddressSpaceId == "input");
        Assert.Equal(28, input.Length);
        Assert.Equal([28L], input.AllowedInputLengths);
        Assert.NotEqual(shortSpan.CompilationFingerprint, discontiguousSpan.CompilationFingerprint);
    }

    /// <summary>Verifies the fixed TP upper boundary remains valid when the resolved source view ends exactly at 256 KiB.</summary>
    [Fact]
    public void V2PlanArtifactAcceptsTpInputAtExact256KiBBoundary()
    {
        CompiledComposition composition = CreateTpComposition(
            sourceLength: CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
            outputCapacity: CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
            sourceRanges: [new ByteRange(0, CompiledTpMaximum256KInputLengthRequirement.MaximumBytes)]);

        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "input");
        Assert.Equal(CompiledTpMaximum256KInputLengthRequirement.MaximumBytes, input.Length);
    }

    /// <summary>Verifies Domain independently rejects one byte beyond the TP policy limit even when the source view agrees.</summary>
    [Fact]
    public void V2PlanArtifactRejectsTpInputAbove256KiBBoundary()
    {
        long oversize = CompiledTpMaximum256KInputLengthRequirement.MaximumBytes + 1;

        _ = Assert.Throws<ArgumentException>(() => CreateTpComposition(
            sourceLength: oversize,
            outputCapacity: oversize,
            sourceRanges: [new ByteRange(0, oversize)]));
    }

    /// <summary>Verifies Normal DP extraction retains a declared span, map-capacity expectation, warning code, and distinct compilation identity.</summary>
    [Fact]
    public void V2PlanArtifactBindsNormalDpExtractionGeometryAndWarningCode()
    {
        CompiledComposition baseline = CreateNormalDpComposition("DP_SIZE_WARNING");
        CompiledComposition changedWarning = CreateNormalDpComposition("DP_LENGTH_WARNING");

        AddressSpace input = Assert.Single(baseline.Plan.AddressSpaces, space => space.AddressSpaceId == "input");
        Assert.Equal(8, input.Length);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal([16L], input.ExpectedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
        Assert.Equal("DP_SIZE_WARNING", input.UnexpectedInputLengthIssueCode);
        Assert.NotEqual(baseline.CompilationFingerprint, changedWarning.CompilationFingerprint);
    }

    /// <summary>Verifies V2 artifacts reject Normal DP contracts that can bypass extraction or lack a declared source span.</summary>
    [Fact]
    public void V2PlanArtifactRejectsInvalidNormalDpExtractionGeometry()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateNormalDpComposition(
            "DP_SIZE_WARNING",
            inputOversizePolicy: InputOversizePolicy.Reject,
            includeWarningCodeInAddressSpace: false));
        _ = Assert.Throws<ArgumentException>(() => CreateNormalDpComposition(
            "DP_SIZE_WARNING",
            sourceRanges: []));
    }

    private static CompiledComposition CreateTpComposition(
        long sourceLength,
        long outputCapacity,
        IReadOnlyList<ByteRange> sourceRanges,
        IReadOnlyList<long>? allowedInputLengths = null)
    {
        return CreateV2(
            inputContract: new CompiledInputContract(
                [new CompiledInputSlotRequirement(
                    "tp-slot",
                    "tp",
                    CompiledInputArtifactClass.TpFirmware,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledTpMaximum256KInputLengthRequirement(),
                    new CompiledNoInputNormalization())],
                [new CompiledInputSpaceBinding("input", "tp-slot", CompiledInputInstancePolicy.Singleton)]),
            regionAccessContract: CreateTpRegionAccessContract(sourceRanges, new ByteRange(0, outputCapacity)),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                outputCapacity),
            plan: CreateTpPlan(sourceLength, outputCapacity, allowedInputLengths));
    }

    private static CompiledComposition CreateNormalDpComposition(
        string warningCode,
        IReadOnlyList<ByteRange>? sourceRanges = null,
        InputOversizePolicy inputOversizePolicy = InputOversizePolicy.ExtractDeclaredRange,
        bool includeWarningCodeInAddressSpace = true)
    {
        const long sourceLength = 8;
        const long outputCapacity = 16;
        sourceRanges ??= [new ByteRange(0, sourceLength)];
        return CreateV2(
            inputContract: new CompiledInputContract(
                [new CompiledInputSlotRequirement(
                    "dp-slot",
                    "dp",
                    CompiledInputArtifactClass.DpFirmware,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledNormalDpExtractWithWarningInputLengthRequirement(warningCode),
                    new CompiledNoInputNormalization())],
                [new CompiledInputSpaceBinding("input", "dp-slot", CompiledInputInstancePolicy.Singleton)]),
            regionAccessContract: CreateTpRegionAccessContract(sourceRanges, new ByteRange(0, outputCapacity)),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                outputCapacity),
            plan: new CompositionPlan(
                ImageInitialization.Blank("output-image", outputCapacity, 0),
                [
                    new AddressSpace(
                        "input",
                        sourceLength,
                        AddressSpaceMutability.Immutable,
                        inputOversizePolicy: inputOversizePolicy,
                        expectedInputLengths: [outputCapacity],
                        unexpectedInputLengthIssueCode: includeWarningCodeInAddressSpace ? warningCode : null),
                    new AddressSpace("output-image", outputCapacity, AddressSpaceMutability.Mutable),
                ],
                [CompositionOperation.CopyRange(
                    "copy-input",
                    10,
                    "input",
                    new ByteRange(0, 1),
                    "output-image",
                    new ByteRange(0, 1),
                    OverlapPolicy.Reject,
                    "copy one synthetic DP byte")]));
    }

    private static CompositionPlan CreateTpPlan(
        long sourceLength,
        long outputCapacity,
        IReadOnlyList<long>? allowedInputLengths)
    {
        return new CompositionPlan(
            ImageInitialization.Blank("output-image", outputCapacity, 0),
            [
                new AddressSpace(
                    "input",
                    sourceLength,
                    AddressSpaceMutability.Immutable,
                    allowedInputLengths: allowedInputLengths ?? [sourceLength]),
                new AddressSpace("output-image", outputCapacity, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 1),
                "output-image",
                new ByteRange(0, 1),
                OverlapPolicy.Reject,
                "copy one synthetic TP byte")]);
    }

    private static CompiledRegionAccessContract CreateTpRegionAccessContract(
        IReadOnlyList<ByteRange> sourceRanges,
        ByteRange outputRange)
    {
        CompiledPhysicalRegionConstraint[] chain =
        [
            new CompiledPhysicalRegionConstraint(
                "root",
                FirmwareWriteConstraint.Forbidden,
                alignment: 1),
        ];
        CompiledResolvedPhysicalView[] views =
        [
            .. sourceRanges.Select((range, index) => new CompiledResolvedPhysicalView(
                $"tp-source-{index}",
                "input",
                range,
                chain)),
            new CompiledResolvedPhysicalView("output", "output-image", outputRange, chain),
        ];
        return new CompiledRegionAccessContract([], views);
    }
}
