using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests atomic compiled-composition identity and fingerprint invariants.</summary>
public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies every supported Replace IC-number policy is represented without null or unknown states.</summary>
    [Theory]
    [InlineData(CompiledIcNumberPolicy.SingleSelector)]
    [InlineData(CompiledIcNumberPolicy.CascadeSelector)]
    [InlineData(CompiledIcNumberPolicy.NumericSelector)]
    public void ReplaceAcceptsExplicitIcNumberPolicies(CompiledIcNumberPolicy policy)
    {
        CompiledComposition composition = CreateReplace(policy);

        Assert.Equal(policy, composition.IcNumberPolicy);
    }

    /// <summary>Verifies compiled default output policy cannot contain path or control syntax.</summary>
    [Theory]
    [InlineData("../output.bin")]
    [InlineData("folder/output.bin")]
    [InlineData("folder\\output.bin")]
    [InlineData("C:output.bin")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("bad\nname.bin")]
    public void DefaultOutputFileNameMustBePlain(string defaultOutputFileName)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateMerge(
            defaultOutputFileName: defaultOutputFileName));

        Assert.Contains("path or control", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies declaration order cannot perturb the canonical compilation fingerprint.</summary>
    [Fact]
    public void FingerprintCanonicalizesSetAndOperationDeclarations()
    {
        CompiledComposition first = CreateMerge(reverseDeclarations: false);
        CompiledComposition second = CreateMerge(reverseDeclarations: true);

        Assert.Equal(first.CompilationFingerprint, second.CompilationFingerprint);
    }

    /// <summary>Verifies profile identity, output policy, and IC policy are part of compilation identity.</summary>
    [Fact]
    public void FingerprintBindsProfileAndRunPolicy()
    {
        CompiledComposition baseline = CreateMerge();
        CompiledComposition[] variants =
        [
            CreateMerge(profileId: "profile-b"),
            CreateMerge(profileVersion: "1.0.1"),
            CreateMerge(icId: "NT-OTHER"),
            CreateMerge(modeId: "alternate"),
            CreateMerge(experienceId: "general-merge"),
            CreateMerge(defaultOutputFileName: "alternate.bin"),
            CreateReplace(CompiledIcNumberPolicy.SingleSelector),
        ];

        Assert.All(
            variants,
            variant => Assert.NotEqual(baseline.CompilationFingerprint, variant.CompilationFingerprint));
        Assert.NotEqual(
            CreateReplace(CompiledIcNumberPolicy.SingleSelector).CompilationFingerprint,
            CreateReplace(CompiledIcNumberPolicy.CascadeSelector).CompilationFingerprint);
    }

    /// <summary>Verifies final-output validation requirements are immutable compiled policy and fingerprinted.</summary>
    [Fact]
    public void FingerprintBindsFinalOutputValidationRequirement()
    {
        CompiledValidationRequirement baselineRequirement =
            CompiledValidationRequirements.FirmwareConfigBackupVersion(
                "verify-nvt-fwconfig-backup-version",
                "replace.ctrlram.fw-version-output-invalid",
                "replace.ctrlram.fw-version-output-mismatch",
                0x27,
                0x04);
        CompiledValidationRequirement changedRequirement =
            CompiledValidationRequirements.FirmwareConfigBackupVersion(
                "verify-nvt-fwconfig-backup-version",
                "replace.ctrlram.fw-version-output-invalid",
                "replace.ctrlram.fw-version-output-mismatch",
                0x28,
                0x04);
        CompiledComposition baseline = CreateMerge(validationRequirements: [baselineRequirement]);
        CompiledComposition changed = CreateMerge(validationRequirements: [changedRequirement]);

        CompiledFirmwareConfigBackupVersionValidation requirement = Assert.IsType<
            CompiledFirmwareConfigBackupVersionValidation>(Assert.Single(baseline.ValidationRequirements));
        Assert.Equal(CompiledValidationStage.FinalOutput, requirement.Stage);
        Assert.Equal(CompiledValidationSeverity.Error, requirement.Severity);
        Assert.Equal(0x27, requirement.FirmwareVersion);
        Assert.Equal(0x04, requirement.FirmwareSubVersion);
        Assert.NotEqual(baseline.CompilationFingerprint, changed.CompilationFingerprint);
    }

    /// <summary>Verifies dynamic FWConfig Backup authority and expected placement remain complete compiled identity.</summary>
    [Fact]
    public void FingerprintBindsFirmwareConfigBackupPlacementRequirements()
    {
        CompiledValidationRequirement baselineAuthority =
            CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority(
                "verify-nvt-fwconfig-backup-authority",
                "replace.ctrlram.fwconfig-backup-invalid",
                "replace.ctrlram.dynamic-diffdlm-inactive-mutation",
                "reference-image",
                new ByteRange(4, 8),
                4);
        CompiledValidationRequirement[] authorityVariants =
        [
            CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority(
                "verify-nvt-fwconfig-backup-authority",
                "replace.ctrlram.fwconfig-backup-invalid",
                "replace.ctrlram.dynamic-diffdlm-other-mutation",
                "reference-image",
                new ByteRange(4, 8),
                4),
            CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority(
                "verify-nvt-fwconfig-backup-authority",
                "replace.ctrlram.fwconfig-backup-invalid",
                "replace.ctrlram.dynamic-diffdlm-inactive-mutation",
                "other-reference",
                new ByteRange(4, 8),
                4),
            CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority(
                "verify-nvt-fwconfig-backup-authority",
                "replace.ctrlram.fwconfig-backup-invalid",
                "replace.ctrlram.dynamic-diffdlm-inactive-mutation",
                "reference-image",
                new ByteRange(5, 8),
                4),
            CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority(
                "verify-nvt-fwconfig-backup-authority",
                "replace.ctrlram.fwconfig-backup-invalid",
                "replace.ctrlram.dynamic-diffdlm-inactive-mutation",
                "reference-image",
                new ByteRange(4, 8),
                5),
        ];
        string baselineAuthorityFingerprint = CreateMerge(
            validationRequirements: [baselineAuthority]).CompilationFingerprint;
        Assert.All(
            authorityVariants,
            variant => Assert.NotEqual(
                baselineAuthorityFingerprint,
                CreateMerge(validationRequirements: [variant]).CompilationFingerprint));

        CompiledValidationRequirement baselineExpected =
            CompiledValidationRequirements.FirmwareConfigBackupExpectedAddress(
                "verify-nvt-fwconfig-backup-expected-address",
                "replace.ctrlram.fwconfig-backup-unexpected",
                8);
        CompiledValidationRequirement changedExpected =
            CompiledValidationRequirements.FirmwareConfigBackupExpectedAddress(
                "verify-nvt-fwconfig-backup-expected-address",
                "replace.ctrlram.fwconfig-backup-unexpected",
                9);
        Assert.NotEqual(
            CreateMerge(validationRequirements: [baselineExpected]).CompilationFingerprint,
            CreateMerge(validationRequirements: [changedExpected]).CompilationFingerprint);

        CompiledValidationRequirement baselineUniform =
            CompiledValidationRequirements.RejectUniformInputRanges(
                "reject-uniform-input",
                CompiledValidationSeverity.Error,
                "input.uniform",
                "input",
                [new ByteRange(0, 2)]);
        CompiledValidationRequirement[] uniformVariants =
        [
            CompiledValidationRequirements.RejectUniformInputRanges(
                "reject-uniform-input",
                CompiledValidationSeverity.Warning,
                "input.uniform",
                "input",
                [new ByteRange(0, 2)]),
            CompiledValidationRequirements.RejectUniformInputRanges(
                "reject-uniform-input",
                CompiledValidationSeverity.Error,
                "input.uniform",
                "input",
                [new ByteRange(1, 2)]),
        ];
        string baselineUniformFingerprint = CreateMerge(
            validationRequirements: [baselineUniform]).CompilationFingerprint;
        Assert.All(
            uniformVariants,
            variant => Assert.NotEqual(
                baselineUniformFingerprint,
                CreateMerge(validationRequirements: [variant]).CompilationFingerprint));

    }

    /// <summary>Verifies initializer, output selection, input policy, and operation semantics affect the fingerprint.</summary>
    [Fact]
    public void FingerprintBindsPlanSemantics()
    {
        CompiledComposition baseline = CreateMerge();
        CompiledComposition[] variants =
        [
            CreateMerge(initializationFillByte: 0xFF),
            CreateMerge(copyOperationId: "copy-other"),
            CreateMerge(copySourceStart: 1),
            CreateMerge(copyOverlapPolicy: OverlapPolicy.ReplaceExisting),
            CreateMerge(copyReason: "different reason"),
            CreateMerge(copyProvenance: OperationProvenance.RuntimeGeneralMapping("mapping-a")),
            CreateMerge(copySequence: 30, fillSequence: 10),
            CreateMultiOutput("scratch"),
        ];

        Assert.All(
            variants,
            variant => Assert.NotEqual(baseline.CompilationFingerprint, variant.CompilationFingerprint));
        Assert.NotEqual(
            CreateMultiOutput("output-image").CompilationFingerprint,
            CreateMultiOutput("scratch").CompilationFingerprint);
        Assert.NotEqual(
            CreatePatchComposition().CompilationFingerprint,
            CreatePatchComposition(targetStart: 1).CompilationFingerprint);
        Assert.NotEqual(
            CreatePatchComposition().CompilationFingerprint,
            CreatePatchComposition(patchByte: 0x22).CompilationFingerprint);
        Assert.NotEqual(
            CreatePatchComposition().CompilationFingerprint,
            CreatePatchComposition(overlapPolicy: OverlapPolicy.ReplaceExisting).CompilationFingerprint);
    }

    /// <summary>Verifies external processor authority and staged-source provenance affect compilation identity.</summary>
    [Fact]
    public void FingerprintBindsExternalProcessorDeclaration()
    {
        CompiledComposition baseline = CreateProcessorComposition();
        CompiledComposition[] variants =
        [
            CreateProcessorComposition(toolBindingId: "tool-b"),
            CreateProcessorComposition(processorId: "processor-b"),
            CreateProcessorComposition(readLength: 2),
            CreateProcessorComposition(writeStart: 1),
            CreateProcessorComposition(writeSectionId: "header-b"),
            CreateProcessorComposition(writeSectionSourceStart: 1),
            CreateProcessorComposition(stagedSourceStart: 1),
            CreateProcessorComposition(stagedFirmwareStart: 1),
            CreateProcessorComposition(stagedArtifactId: "artifact-b"),
            CreateProcessorComposition(
                stagedArtifactSourceSpaceId: "output-image"),
            CreateProcessorComposition(stagedArtifactSourceStart: 1),
            CreateProcessorComposition(outputAssertion: new ExternalProcessorOutputAssertion(new ByteRange(2, 1), [0xA2])),
            CreateProcessorComposition(outputAssertion: new ExternalProcessorOutputAssertion(new ByteRange(1, 1), [0xA1])),
        ];

        Assert.All(
            variants,
            variant =>
            {
                Assert.NotEqual(
                    baseline.CompilationFingerprint,
                    variant.CompilationFingerprint);
                Assert.NotEqual(
                    baseline.IntegrityFingerprint,
                    variant.IntegrityFingerprint);
            });
    }

    private static CompiledComposition CreateMerge(
        string profileId = "profile-a",
        string profileVersion = "1.0.0",
        string icId = "NT-SYNTHETIC",
        string modeId = "standard",
        string experienceId = "standard-merge",
        string defaultOutputFileName = "output.bin",
        byte initializationFillByte = 0,
        string copyOperationId = "copy-input",
        long copySourceStart = 0,
        OverlapPolicy copyOverlapPolicy = OverlapPolicy.Reject,
        string copyReason = "copy immutable input",
        OperationProvenance? copyProvenance = null,
        int copySequence = 10,
        int fillSequence = 20,
        bool reverseDeclarations = false,
        IReadOnlyList<CompiledValidationRequirement>? validationRequirements = null)
    {
        var input = new AddressSpace(
            "input",
            4,
            AddressSpaceMutability.Immutable);
        var output = new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable);
        var copy = CompositionOperation.CopyRange(
            copyOperationId,
            copySequence,
            "input",
            new ByteRange(copySourceStart, 2),
            "output-image",
            new ByteRange(0, 2),
            copyOverlapPolicy,
            copyReason,
            copyProvenance);
        var fill = CompositionOperation.FillRange(
            "fill-tail",
            fillSequence,
            "output-image",
            new ByteRange(2, 2),
            0xAA,
            OverlapPolicy.Reject,
            "fill output tail");
        AddressSpace[] spaces = reverseDeclarations ? [output, input] : [input, output];
        CompositionOperation[] operations = reverseDeclarations ? [fill, copy] : [copy, fill];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, initializationFillByte),
            spaces,
            operations);
        return CreateV2(
            outputTemplate: defaultOutputFileName,
            requiredOutputTokenIds: [],
            validationRequirements: validationRequirements,
            inputContract: CreateExactMapInputContract("input", CompiledInputArtifactClass.ReferenceImage),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                modeId: modeId,
                memberId: icId),
            plan: plan,
            modeId: modeId,
            experienceId: experienceId,
            profileId: profileId,
            profileVersion: profileVersion);
    }

    private static CompiledComposition CreateReplace(CompiledIcNumberPolicy policy)
    {
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference", 4),
            [
                new AddressSpace("reference", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            []);
        return CreateV2(
            outputTemplate: "replace.bin",
            requiredOutputTokenIds: [],
            inputContract: CreateExactMapInputContract(
                "reference",
                CompiledInputArtifactClass.ReferenceImage),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                modeId: "replace"),
            plan: plan,
            modeId: "replace",
            experienceId: "general-replace",
            compositionKind: CompositionKind.Replace,
            icNumberPolicy: policy,
            profileId: "replace-profile",
            profileVersion: "1.0.0");
    }

    private static CompiledComposition CreateMultiOutput(string outputSpaceId)
    {
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 4, 0),
                ImageInitialization.Blank("scratch", 4, 0),
            ],
            outputSpaceId,
            [
                new AddressSpace("input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
                new AddressSpace("scratch", 4, AddressSpaceMutability.Mutable),
            ],
            []);
        return CreateV2(
            outputTemplate: "output.bin",
            requiredOutputTokenIds: [],
            inputContract: CreateExactMapInputContract(
                "input",
                CompiledInputArtifactClass.ReferenceImage),
            plan: plan,
            profileId: "profile-a",
            profileVersion: "1.0.0");
    }

    private static CompiledComposition CreatePatchComposition(
        long targetStart = 0,
        byte patchByte = 0x11,
        OverlapPolicy overlapPolicy = OverlapPolicy.Reject)
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 2, 0),
            [
                new AddressSpace("input", 2, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 2, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.PatchScalar(
                    "patch-byte",
                    10,
                    "output-image",
                    new ByteRange(targetStart, 1),
                    [patchByte],
                    overlapPolicy,
                    "patch one byte"),
            ]);
        return CreateV2(
            outputTemplate: "patch.bin",
            requiredOutputTokenIds: [],
            inputContract: CreateExactMapInputContract(
                "input",
                CompiledInputArtifactClass.ReferenceImage,
                2),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                capacity: 2,
                modeId: "patch"),
            plan: plan,
            modeId: "patch",
            experienceId: "general-merge",
            profileId: "patch-profile",
            profileVersion: "1.0.0");
    }

    private static CompiledComposition CreateProcessorComposition(
        string processorId = "processor-a",
        string toolBindingId = "tool-a",
        long readLength = 4,
        long writeStart = 0,
        string writeSectionId = "header-a",
        long writeSectionSourceStart = 0,
        long stagedSourceStart = 0,
        long stagedFirmwareStart = 0,
        string stagedArtifactId = "artifact-a",
        string stagedArtifactSourceSpaceId = "source",
        long stagedArtifactSourceStart = 0,
        ExternalProcessorOutputAssertion? outputAssertion = null)
    {
        var invocation = new ExternalProcessorInvocation(
            processorId,
            toolBindingId,
            [new ByteRange(0, readLength)],
            [new ByteRange(writeStart, 4 - writeStart)],
            [
                new ExternalProcessorStagedSourceBinding(
                    "source",
                    new ByteRange(stagedSourceStart, 2),
                    new ByteRange(stagedFirmwareStart, 2)),
            ],
            [
                new ExternalProcessorWriteRangeSection(
                    writeSectionId,
                    new ByteRange(writeStart, 2),
                    new ByteRange(writeSectionSourceStart, 2)),
            ],
            [
                new ExternalProcessorStagedArtifactBinding(
                    stagedArtifactId,
                    stagedArtifactSourceSpaceId,
                    new ByteRange(stagedArtifactSourceStart, 1)),
            ],
            outputAssertions: outputAssertion is null ? [] : [outputAssertion]);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [
                new AddressSpace("source", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.RunExternalProcessor(
                    "run-processor",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    invocation,
                    OverlapPolicy.Reject,
                    "run processor"),
            ]);
        return CreateV2(
            outputTemplate: "processor.bin",
            requiredOutputTokenIds: [],
            inputContract: CreateExactMapInputContract(
                "source",
                CompiledInputArtifactClass.ReferenceImage),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                modeId: "processor"),
            plan: plan,
            modeId: "processor",
            profileId: "processor-profile",
            profileVersion: "1.0.0");
    }

    private static CompiledInputContract CreateExactMapInputContract(
        string addressSpaceId,
        CompiledInputArtifactClass artifactClass,
        long capacity = 4)
    {
        string slotId = $"{addressSpaceId}-slot";
        return new CompiledInputContract(
            [CompiledInputSlotTestFactory.Create(
                slotId,
                addressSpaceId,
                artifactClass,
                required: true,
                CompiledInputSlotCardinality.ExactlyOne,
                [".bin"],
                new CompiledExactResolvedMapCapacityInputLengthRequirement(capacity),
                new CompiledNoInputNormalization())],
            [new CompiledInputSpaceBinding(
                addressSpaceId,
                slotId,
                CompiledInputInstancePolicy.Singleton)]);
    }
}
