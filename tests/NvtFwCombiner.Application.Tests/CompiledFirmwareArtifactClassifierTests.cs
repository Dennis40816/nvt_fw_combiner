using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Canonical Application classification evidence for one resolved DP/TP composition.</summary>
public sealed class CompiledFirmwareArtifactClassifierTests
{
    /// <summary>An exact container with plausible DP and TP source ranges is FlashCode.</summary>
    [Fact]
    public void ExactContainerWithDeclaredDpAndTpContentIsFlashCode()
    {
        byte[] candidate = Candidate(dpProgrammed: true, tpProgrammed: true, length: 16);

        CompiledFirmwareArtifactClassification result =
            CompiledFirmwareArtifactClassifier.Classify(Composition(), candidate);

        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, result.Kind);
        Assert.All(result.Signals, static signal =>
            Assert.Equal(CompiledFirmwareArtifactSignalStatus.Satisfied, signal.Status));
    }

    /// <summary>A TP source with a repeated-byte DP range is identified without claiming FlashCode.</summary>
    [Fact]
    public void PlausibleTpWithUniformDpIsTpFirmware()
    {
        CompiledFirmwareArtifactClassification result =
            CompiledFirmwareArtifactClassifier.Classify(
                Composition(),
                Candidate(dpProgrammed: false, tpProgrammed: true, length: 16));

        Assert.Equal(CompiledFirmwareArtifactKind.TpFirmware, result.Kind);
        Assert.Equal(
            CompiledFirmwareArtifactSignalStatus.NotSatisfied,
            Signal(result, CompiledFirmwareArtifactSignalKind.DpContentPlausibility).Status);
        Assert.Equal(
            CompiledFirmwareArtifactSignalStatus.Satisfied,
            Signal(result, CompiledFirmwareArtifactSignalKind.TpContentPlausibility).Status);
    }

    /// <summary>A full-length Initial Code candidate with no plausible TP range remains Unknown.</summary>
    [Fact]
    public void FullLengthInitialCodeWithUniformTpIsUnknown()
    {
        CompiledFirmwareArtifactClassification result =
            CompiledFirmwareArtifactClassifier.Classify(
                Composition(),
                Candidate(dpProgrammed: true, tpProgrammed: false, length: 16));

        Assert.Equal(CompiledFirmwareArtifactKind.Unknown, result.Kind);
        Assert.Equal(
            new ByteRange(4, 4),
            Signal(result, CompiledFirmwareArtifactSignalKind.TpContentPlausibility).FailedRange);
    }

    /// <summary>Valid section content cannot replace exact complete-container capacity authority.</summary>
    [Fact]
    public void ExtraTailDoesNotBecomeFlashCode()
    {
        CompiledFirmwareArtifactClassification result =
            CompiledFirmwareArtifactClassifier.Classify(
                Composition(),
                Candidate(dpProgrammed: true, tpProgrammed: true, length: 17));

        Assert.Equal(CompiledFirmwareArtifactKind.Unknown, result.Kind);
        Assert.Equal(
            CompiledFirmwareArtifactSignalStatus.NotSatisfied,
            Signal(result, CompiledFirmwareArtifactSignalKind.DeclaredContainerCapacity).Status);
    }

    /// <summary>Missing profile plausibility authority yields Unknown instead of optimistic inference.</summary>
    [Fact]
    public void MissingTpPlausibilityDeclarationFailsClosedAsUnknown()
    {
        CompiledFirmwareArtifactClassification result =
            CompiledFirmwareArtifactClassifier.Classify(
                Composition(includeTpValidation: false),
                Candidate(dpProgrammed: true, tpProgrammed: true, length: 16));

        Assert.Equal(CompiledFirmwareArtifactKind.Unknown, result.Kind);
        Assert.Equal(
            CompiledFirmwareArtifactSignalStatus.NotDeclared,
            Signal(result, CompiledFirmwareArtifactSignalKind.TpContentPlausibility).Status);
    }

    /// <summary>A candidate that cannot cover either declared source range remains Unknown with exact failed ranges.</summary>
    [Fact]
    public void ShortCandidateReportsMissingSectionCoverage()
    {
        CompiledFirmwareArtifactClassification result =
            CompiledFirmwareArtifactClassifier.Classify(Composition(), [0x10, 0x11]);

        Assert.Equal(CompiledFirmwareArtifactKind.Unknown, result.Kind);
        Assert.Equal(
            new ByteRange(2, 2),
            Signal(result, CompiledFirmwareArtifactSignalKind.DpSourceCoverage).FailedRange);
        Assert.Equal(
            new ByteRange(2, 6),
            Signal(result, CompiledFirmwareArtifactSignalKind.TpSourceCoverage).FailedRange);
    }

    /// <summary>Optional outer-container diagnostics remain warnings and do not change the accepted source view.</summary>
    [Fact]
    public void SourceViewOuterLengthDiagnosticDoesNotBlockAcceptedSnapshot()
    {
        CompiledInputArtifactInspectionResult result = CompiledInputArtifactInspectionService.Inspect(
            Composition(dpExpectedOuterLengths: [16], dpUnexpectedOuterLengthIssueCode: "DP_OUTER_LENGTH"),
            "dp-input",
            new byte[8]);

        Assert.Equal(CompiledInputArtifactInspectionSeverity.Warning, result.Severity);
        Assert.False(result.BlocksBuild);
        Assert.Equal("DP_OUTER_LENGTH", result.IssueCode);
        Assert.Equal(new ByteRange(0, 4), result.AcceptedSnapshotRange);
        Assert.Equal(new ByteRange(4, 4), result.IgnoredTrailingRange);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength, result.NextAction);
    }

    /// <summary>Public classification and inspection APIs fail closed when required compiled authority is absent.</summary>
    [Fact]
    public void CanonicalInspectionRejectsMissingOrIncompleteAuthority()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            CompiledFirmwareArtifactClassifier.Classify(null!, []));
        CompiledComposition composition = Composition();
        _ = Assert.Throws<ArgumentException>(() =>
            CompiledInputArtifactInspectionService.Inspect(
                composition.V2Details.InputContract,
                "dp-input",
                new byte[4]));
        _ = Assert.Throws<ArgumentException>(() =>
            CompiledInputArtifactInspectionService.Inspect(
                composition,
                "missing-input",
                new byte[4]));
    }

    /// <summary>Classification evidence must contain exactly one signal of every closed kind.</summary>
    [Fact]
    public void ClassificationResultRejectsInvalidOrDuplicateSignals()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompiledFirmwareArtifactClassification(
                (CompiledFirmwareArtifactKind)int.MaxValue,
                []));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledFirmwareArtifactClassification(
                CompiledFirmwareArtifactKind.Unknown,
                []));
        CompiledFirmwareArtifactSignal[] signals =
        [
            .. Enum.GetValues<CompiledFirmwareArtifactSignalKind>().Select(kind =>
                new CompiledFirmwareArtifactSignal(
                    kind,
                    CompiledFirmwareArtifactSignalStatus.NotDeclared,
                    AddressSpaceId: null,
                    RequiredEndExclusive: 0,
                    FailedRange: null)),
        ];
        signals[^1] = signals[0];

        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledFirmwareArtifactClassification(
                CompiledFirmwareArtifactKind.Unknown,
                signals));
    }

    private static CompiledFirmwareArtifactSignal Signal(
        CompiledFirmwareArtifactClassification result,
        CompiledFirmwareArtifactSignalKind kind)
    {
        return Assert.Single(result.Signals, signal => signal.Kind == kind);
    }

    private static byte[] Candidate(bool dpProgrammed, bool tpProgrammed, int length)
    {
        byte[] candidate = new byte[length];
        if (dpProgrammed)
        {
            candidate[0] = 0x10;
            candidate[1] = 0x11;
        }

        if (tpProgrammed)
        {
            candidate[4] = 0x20;
            candidate[5] = 0x21;
        }

        return candidate;
    }

    private static CompiledComposition Composition(
        bool includeTpValidation = true,
        IReadOnlyList<long>? dpExpectedOuterLengths = null,
        string? dpUnexpectedOuterLengthIssueCode = null)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = ResolvedMap();
        CompiledValidationRequirement[] validations =
        [
            CompiledValidationRequirements.RejectUniformInputRanges(
                "dp-content-plausibility",
                CompiledValidationSeverity.Warning,
                "DP_UNIFORM_CONTENT_WARNING",
                "dp-input",
                [new ByteRange(0, 4)]),
            .. includeTpValidation
                ? new CompiledValidationRequirement[]
                {
                    CompiledValidationRequirements.RejectUniformInputRanges(
                        "tp-content-plausibility",
                        CompiledValidationSeverity.Warning,
                        "TP_UNIFORM_CONTENT_WARNING",
                        "tp-input",
                        [new ByteRange(4, 4)]),
                }
                : [],
        ];
        var inputContract = new CompiledInputContract(
            [
                CompiledInputSlotTestFactory.Create(
                    "dp-slot",
                    "dp",
                    CompiledInputArtifactClass.DpFirmware,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledSourceViewCoverageInputLengthRequirement(
                        dpExpectedOuterLengths,
                        dpUnexpectedOuterLengthIssueCode),
                    new CompiledNoInputNormalization()),
                CompiledInputSlotTestFactory.Create(
                    "tp-slot",
                    "tp",
                    CompiledInputArtifactClass.TpFirmware,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledSourceViewCoverageInputLengthRequirement(),
                    new CompiledNoInputNormalization()),
            ],
            [
                new CompiledInputSpaceBinding(
                    "dp-input",
                    "dp-slot",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "tp-input",
                    "tp-slot",
                    CompiledInputInstancePolicy.Singleton),
            ]);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "bundle-v2",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "profile-entry",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            resolvedMap,
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            ["profile-evidence"],
            validations,
            []);
        var details = new V2CompiledCompositionDetails(
            "classification-profile",
            "1.0.0",
            ExperienceIds.StandardMerge,
            CompositionKind.Merge,
            provenance,
            inputContract,
            new CompiledRegionAccessContract([], []),
            new CompiledOutputNamingRequirement(
                "classified.bin",
                allowOverride: true,
                CompiledOutputInvalidCharacterPolicy.Reject,
                []));
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 16, 0),
            [
                new AddressSpace(
                    "dp-input",
                    4,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                    expectedInputLengths: dpExpectedOuterLengths,
                    unexpectedInputLengthIssueCode: dpUnexpectedOuterLengthIssueCode),
                new AddressSpace(
                    "tp-input",
                    8,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange),
                new AddressSpace("output-image", 16, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-dp",
                    100,
                    "dp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "copy declared DP source"),
                CompositionOperation.CopyRange(
                    "copy-tp",
                    200,
                    "tp-input",
                    new ByteRange(4, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    "copy declared TP source"),
            ]);

        return CompiledComposition.CreateV2RuntimeExecutable(plan, details);
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap()
    {
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "classification-map",
            "flash",
            new FirmwareMapApplicability(
                ["NT-SYNTHETIC"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                16),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [
                new FirmwareRegionSet(
                    "physical",
                    "flash",
                    [
                        new FirmwareRegion(
                            "root",
                            parentRegionId: null,
                            FirmwareRegionOwner.System,
                            FirmwareRegionKind.Image,
                            new ByteRange(0, 16),
                            FirmwareWriteConstraint.Forbidden),
                    ],
                    ["map-evidence"]),
            ],
            [],
            ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "classification-family",
            "1.0.0",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            [map],
            []);
        FirmwareMapResolutionResult result = definition.ResolveMap(
            new FirmwareMapResolutionInputs(
                "NT-SYNTHETIC",
                "standard",
                16,
                null,
                []));

        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(
            result.ResolvedMap);
    }
}
