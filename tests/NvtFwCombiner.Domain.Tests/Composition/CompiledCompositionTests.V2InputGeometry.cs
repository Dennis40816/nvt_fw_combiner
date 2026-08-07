using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
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
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
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

    /// <summary>Verifies source-view coverage retains its declared span, expected containers, warning code, and distinct compilation identity.</summary>
    [Fact]
    public void V2PlanArtifactBindsSourceViewGeometryAndWarningCode()
    {
        CompiledComposition baseline = CreateSourceViewComposition("DP_SIZE_WARNING");
        CompiledComposition changedWarning = CreateSourceViewComposition("DP_LENGTH_WARNING");
        CompiledComposition changedContainers = CreateSourceViewComposition(
            "DP_SIZE_WARNING",
            expectedInputLengths: [0x20, 0x40]);

        AddressSpace input = Assert.Single(baseline.Plan.AddressSpaces, space => space.AddressSpaceId == "input");
        Assert.Equal(8, input.Length);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal([16L], input.ExpectedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
        Assert.Equal("DP_SIZE_WARNING", input.UnexpectedInputLengthIssueCode);
        Assert.NotEqual(baseline.CompilationFingerprint, changedWarning.CompilationFingerprint);
        Assert.NotEqual(baseline.CompilationFingerprint, changedContainers.CompilationFingerprint);
    }

    /// <summary>Verifies declared-prefix authority binds its exact half-open snapshot, issue codes, and fingerprint.</summary>
    [Fact]
    public void V2PlanArtifactBindsDeclaredPrefixGeometryAndDiagnostics()
    {
        CompiledComposition baseline = CreateDeclaredPrefixComposition();
        CompiledComposition changedRequiredEnd = CreateDeclaredPrefixComposition(requiredEndExclusive: 9);
        CompiledComposition changedExpectedOuterLength = CreateDeclaredPrefixComposition(expectedOuterLengths: [8, 16]);
        CompiledComposition changedShortCode = CreateDeclaredPrefixComposition(shortInputIssueCode: "INPUT_TOO_SHORT");
        CompiledComposition changedOuterCode = CreateDeclaredPrefixComposition(
            unexpectedOuterLengthIssueCode: "INPUT_OUTER_SIZE");

        AddressSpace input = Assert.Single(baseline.Plan.AddressSpaces, space => space.AddressSpaceId == "input");
        CompiledDeclaredPrefixWithWarningInputLengthRequirement requirement = Assert.IsType<
            CompiledDeclaredPrefixWithWarningInputLengthRequirement>(
                Assert.Single(baseline.V2Details.InputContract.Slots).LengthRequirement);
        Assert.Equal(new ByteRange(0, 8), new ByteRange(0, input.Length));
        Assert.Null(input.InputPaddingByte);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal([8L], input.ExpectedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
        Assert.Equal("INPUT_OUTER_LENGTH", input.UnexpectedInputLengthIssueCode);
        Assert.Equal(8, requirement.RequiredEndExclusive);
        Assert.Equal([8L], requirement.ExpectedOuterLengths);
        Assert.Equal("INPUT_SHORT", requirement.ShortInputIssueCode);
        Assert.Equal("INPUT_OUTER_LENGTH", requirement.UnexpectedOuterLengthIssueCode);
        Assert.All(
            [changedRequiredEnd, changedExpectedOuterLength, changedShortCode, changedOuterCode],
            variant => Assert.NotEqual(baseline.CompilationFingerprint, variant.CompilationFingerprint));
    }

    /// <summary>Verifies declared-prefix values reject invalid half-open bounds and ordering.</summary>
    [Fact]
    public void DeclaredPrefixRequirementRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
            8,
            [7],
            "INPUT_SHORT",
            "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentException>(() => new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
            8,
            [16, 8],
            "INPUT_SHORT",
            "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
            (long)int.MaxValue + 1,
            [(long)int.MaxValue + 1],
            "INPUT_SHORT",
            "INPUT_OUTER_LENGTH"));
    }

    /// <summary>Source-view diagnostics are immutable, paired, ordered, and optional.</summary>
    [Fact]
    public void SourceViewCoverageRequirementValidatesOptionalOuterLengths()
    {
        long[] outerLengths = [8, 16];
        var requirement = new CompiledSourceViewCoverageInputLengthRequirement(
            outerLengths,
            "INPUT_OUTER_LENGTH");
        outerLengths[0] = 4;

        Assert.Equal([8L, 16L], requirement.ExpectedOuterLengths);
        Assert.Equal("INPUT_OUTER_LENGTH", requirement.UnexpectedOuterLengthIssueCode);
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledSourceViewCoverageInputLengthRequirement([8], unexpectedOuterLengthIssueCode: null));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledSourceViewCoverageInputLengthRequirement(
                expectedOuterLengths: null,
                "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledSourceViewCoverageInputLengthRequirement([], "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledSourceViewCoverageInputLengthRequirement([0], "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledSourceViewCoverageInputLengthRequirement([16, 8], "INPUT_OUTER_LENGTH"));
    }

    /// <summary>Verifies a V2 Replace artifact can use an exact reference clone and one declared padded DP source.</summary>
    [Fact]
    public void V2ReplaceArtifactBindsReferenceCloneAndExactDpPadding()
    {
        CompiledComposition composition = CreateDpReplaceComposition();

        Assert.Equal(CompositionKind.Replace, composition.CompositionKind);
        Assert.Equal(CompiledIcNumberPolicy.SingleSelector, composition.IcNumberPolicy);
        Assert.Equal(ImageInitializationKind.Reference, composition.Plan.OutputInitialization.Kind);
        Assert.Equal("reference-source", composition.Plan.OutputInitialization.ReferenceSpaceId);
        AddressSpace reference = Assert.Single(
            composition.Plan.AddressSpaces,
            static space => space.AddressSpaceId == "reference-source");
        AddressSpace dp = Assert.Single(
            composition.Plan.AddressSpaces,
            static space => space.AddressSpaceId == "dp-source");
        Assert.Empty(reference.AllowedInputLengths);
        Assert.Null(reference.InputPaddingByte);
        Assert.Empty(dp.AllowedInputLengths);
        Assert.Equal((byte)0, dp.InputPaddingByte);
        Assert.Equal(CompositionOperationKind.ReplaceRange, Assert.Single(composition.Plan.OrderedOperations).Kind);
    }

    private static CompiledComposition CreateTpComposition(
        long sourceLength,
        long outputCapacity,
        IReadOnlyList<ByteRange> sourceRanges)
    {
        return CreateV2(
            inputContract: new CompiledInputContract(
                [CompiledInputSlotTestFactory.Create(
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
            plan: CreateTpPlan(sourceLength, outputCapacity));
    }

    private static CompiledComposition CreateSourceViewComposition(
        string warningCode,
        IReadOnlyList<long>? expectedInputLengths = null)
    {
        const long sourceLength = 8;
        const long outputCapacity = 16;
        IReadOnlyList<long> expected = expectedInputLengths ?? [outputCapacity];
        return CreateV2(
            inputContract: new CompiledInputContract(
                [CompiledInputSlotTestFactory.Create(
                    "dp-slot",
                    "dp",
                    CompiledInputArtifactClass.DpFirmware,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledSourceViewCoverageInputLengthRequirement(expected, warningCode),
                    new CompiledNoInputNormalization())],
                [new CompiledInputSpaceBinding("input", "dp-slot", CompiledInputInstancePolicy.Singleton)]),
            regionAccessContract: CreateTpRegionAccessContract(
                [new ByteRange(0, sourceLength)],
                new ByteRange(0, outputCapacity)),
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
                        inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                        expectedInputLengths: expected,
                        unexpectedInputLengthIssueCode: warningCode),
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

    private static CompiledComposition CreateDeclaredPrefixComposition(
        long requiredEndExclusive = 8,
        IReadOnlyList<long>? expectedOuterLengths = null,
        string shortInputIssueCode = "INPUT_SHORT",
        string unexpectedOuterLengthIssueCode = "INPUT_OUTER_LENGTH")
    {
        expectedOuterLengths ??= [requiredEndExclusive];
        long outputCapacity = Math.Max(requiredEndExclusive, 16);
        return CreateV2(
            inputContract: new CompiledInputContract(
                [CompiledInputSlotTestFactory.Create(
                    "source-slot",
                    "source",
                    CompiledInputArtifactClass.Auxiliary,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                        requiredEndExclusive,
                        expectedOuterLengths,
                        shortInputIssueCode,
                        unexpectedOuterLengthIssueCode),
                    new CompiledNoInputNormalization())],
                [new CompiledInputSpaceBinding("input", "source-slot", CompiledInputInstancePolicy.Singleton)]),
            regionAccessContract: CreateTpRegionAccessContract(
                [new ByteRange(0, 1)],
                new ByteRange(0, outputCapacity)),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                outputCapacity),
            plan: new CompositionPlan(
                ImageInitialization.Blank("output-image", outputCapacity, 0),
                [
                    new AddressSpace(
                        "input",
                        requiredEndExclusive,
                        AddressSpaceMutability.Immutable,
                        inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                        expectedInputLengths: expectedOuterLengths,
                        unexpectedInputLengthIssueCode: unexpectedOuterLengthIssueCode),
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
                "copy one synthetic declared-prefix byte")]));
    }

    private static CompiledComposition CreateDpReplaceComposition()
    {
        const long capacity = 16;
        var contract = new CompiledInputContract(
            [
                CompiledInputSlotTestFactory.Create(
                    "reference-slot",
                    "reference",
                    CompiledInputArtifactClass.ReferenceImage,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(capacity),
                    new CompiledNoInputNormalization()),
                CompiledInputSlotTestFactory.Create(
                    "dp-slot",
                    "dp",
                    CompiledInputArtifactClass.DpFirmware,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(capacity),
                    new CompiledPadShorterInputNormalization(0, "synthetic-dp-padding")),
            ],
            [
                new CompiledInputSpaceBinding(
                    "reference-source",
                    "reference-slot",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "dp-source",
                    "dp-slot",
                    CompiledInputInstancePolicy.Singleton),
            ]);
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-source", capacity),
            [
                new AddressSpace("reference-source", capacity, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "dp-source",
                    capacity,
                    AddressSpaceMutability.Immutable,
                    inputPaddingByte: 0),
                new AddressSpace("output-image", capacity, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.ReplaceRange(
                "replace-dp-prefix",
                10,
                "dp-source",
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(0, 4),
                OverlapPolicy.Reject,
                "replace synthetic DP bytes")]);
        CompiledPhysicalRegionConstraint[] chain =
        [
            new CompiledPhysicalRegionConstraint(
                "root",
                FirmwareWriteConstraint.Forbidden,
                alignment: 1),
        ];
        var regionAccess = new CompiledRegionAccessContract(
            [],
            [
                new CompiledResolvedPhysicalView(
                    "reference-view",
                    "reference-source",
                    new ByteRange(0, capacity),
                    chain),
                new CompiledResolvedPhysicalView(
                    "dp-view",
                    "dp-source",
                    new ByteRange(0, 4),
                    chain),
                new CompiledResolvedPhysicalView(
                    "output-view",
                    "output-image",
                    new ByteRange(0, capacity),
                    chain),
            ]);
        return CreateV2(
            inputContract: contract,
            regionAccessContract: regionAccess,
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                capacity,
                "dp-replace"),
            plan: plan,
            compositionKind: CompositionKind.Replace,
            modeId: "dp-replace",
            experienceId: "dp-replace",
            icNumberPolicy: CompiledIcNumberPolicy.SingleSelector);
    }

    private static CompositionPlan CreateTpPlan(
        long sourceLength,
        long outputCapacity)
    {
        return new CompositionPlan(
            ImageInitialization.Blank("output-image", outputCapacity, 0),
            [
                new AddressSpace(
                    "input",
                    sourceLength,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange),
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
