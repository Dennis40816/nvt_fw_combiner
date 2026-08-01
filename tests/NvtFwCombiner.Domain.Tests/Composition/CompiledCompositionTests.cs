using System.Reflection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests atomic compiled-composition identity and fingerprint invariants.</summary>
public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies a legacy artifact carries only the identity and authority the legacy compiler can prove.</summary>
    [Fact]
    public void LegacyArtifactCapturesAtomicIdentityAndAuthority()
    {
        CompiledComposition composition = CreateMerge();
        Assert.Equal(
            "2504b2afade952c5e2f9ade9323396454f7d65484654704d0c4c268816c997d5",
            composition.CompilationFingerprint);

        Assert.Equal("profile-a", composition.ProfileId);
        Assert.Equal("1.0.0", composition.ProfileVersion);
        Assert.Equal("NT-SYNTHETIC", composition.IcId);
        Assert.Equal("standard", composition.ModeId);
        Assert.Equal("standard-merge", composition.ExperienceId);
        Assert.Equal(CompositionKind.Merge, composition.CompositionKind);
        Assert.Equal("output.bin", composition.DefaultOutputFileName);
        Assert.Equal(CompiledIcNumberPolicy.NotApplicable, composition.IcNumberPolicy);
        Assert.Equal(CompiledCompositionEligibility.LegacyRuntimeExecutable, composition.Eligibility);
        Assert.Null(composition.IntegrityFingerprint);
        LegacyProfileCompilationAuthority authority = Assert.IsType<LegacyProfileCompilationAuthority>(
            composition.Authority);
        Assert.Equal("0.2", authority.ModelVersion);
        Assert.Equal(64, composition.CompilationFingerprint.Length);
        Assert.All(
            composition.CompilationFingerprint,
            character => Assert.True(character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        Assert.Empty(typeof(CompiledComposition).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(CompiledComposition).GetMethod(
            "CreateLegacy",
            BindingFlags.Static | BindingFlags.Public));
    }

    /// <summary>Verifies legacy artifacts require both a byte plan and compiler-owned identity.</summary>
    [Fact]
    public void LegacyArtifactRequiresPlanAndIdentity()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable)],
            []);
        LegacyCompiledCompositionIdentity identity = CreateMergeIdentity();

        _ = Assert.Throws<ArgumentNullException>(() => CompiledComposition.CreateLegacy(
            null!,
            identity,
            "output.bin",
            CompiledIcNumberPolicy.NotApplicable));
        _ = Assert.Throws<ArgumentNullException>(() => CompiledComposition.CreateLegacy(
            plan,
            null!,
            "output.bin",
            CompiledIcNumberPolicy.NotApplicable));
    }

    /// <summary>Verifies every legacy identity field is mandatory at the Domain minting boundary.</summary>
    [Theory]
    [InlineData("", "1.0.0", "NT-SYNTHETIC", "standard", "standard-merge")]
    [InlineData("profile-a", "", "NT-SYNTHETIC", "standard", "standard-merge")]
    [InlineData("profile-a", "1.0.0", "", "standard", "standard-merge")]
    [InlineData("profile-a", "1.0.0", "NT-SYNTHETIC", "", "standard-merge")]
    [InlineData("profile-a", "1.0.0", "NT-SYNTHETIC", "standard", "")]
    public void LegacyIdentityRequiresEveryField(
        string profileId,
        string profileVersion,
        string icId,
        string modeId,
        string experienceId)
    {
        _ = Assert.Throws<ArgumentException>(() => new LegacyCompiledCompositionIdentity(
            profileId,
            profileVersion,
            icId,
            modeId,
            experienceId,
            CompositionKind.Merge));
    }

    /// <summary>Verifies an unknown composition kind cannot enter a compiled identity.</summary>
    [Fact]
    public void LegacyIdentityRejectsUnknownCompositionKind()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyCompiledCompositionIdentity(
            "invalid-kind",
            "1.0.0",
            "NT-SYNTHETIC",
            "invalid",
            "standard-merge",
            (CompositionKind)int.MaxValue));
    }

    /// <summary>Verifies IC-number policy remains closed and consistent with Merge or Replace intent.</summary>
    [Fact]
    public void IcNumberPolicyMustMatchCompositionKind()
    {
        CompositionPlan mergePlan = CreateMerge().Plan;
        CompositionPlan replacePlan = CreateReplace(CompiledIcNumberPolicy.SingleSelector).Plan;

        _ = Assert.Throws<ArgumentException>(() => CompiledComposition.CreateLegacy(
            mergePlan,
            CreateMergeIdentity(),
            "output.bin",
            CompiledIcNumberPolicy.SingleSelector));
        _ = Assert.Throws<ArgumentException>(() => CompiledComposition.CreateLegacy(
            replacePlan,
            CreateReplaceIdentity(),
            "output.bin",
            CompiledIcNumberPolicy.NotApplicable));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CompiledComposition.CreateLegacy(
            replacePlan,
            CreateReplaceIdentity(),
            "output.bin",
            (CompiledIcNumberPolicy)int.MaxValue));

    }

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

        Assert.Contains("plain filename", exception.Message, StringComparison.Ordinal);
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

    /// <summary>Verifies legacy final-output validation requirements are immutable compiled policy and fingerprinted.</summary>
    [Fact]
    public void FingerprintBindsLegacyFinalOutputValidationRequirement()
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

        CompiledValidationRequirement outsideInput =
            CompiledValidationRequirements.RejectUniformInputRanges(
                "outside-input",
                CompiledValidationSeverity.Error,
                "input.uniform",
                "input",
                [new ByteRange(3, 2)]);
        _ = Assert.Throws<ArgumentException>(() =>
            CreateMerge(validationRequirements: [outsideInput]));
    }

    /// <summary>Verifies initializer, output selection, input policy, and operation semantics affect the fingerprint.</summary>
    [Fact]
    public void FingerprintBindsPlanSemantics()
    {
        CompiledComposition baseline = CreateMerge();
        CompiledComposition[] variants =
        [
            CreateMerge(initializationFillByte: 0xFF),
            CreateMerge(inputPaddingByte: 0xFF),
            CreateMerge(inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            CreateMerge(allowedInputLengths: [2, 4]),
            CreateMerge(expectedInputLengths: [4, 8]),
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
        byte? inputPaddingByte = null,
        InputOversizePolicy inputOversizePolicy = InputOversizePolicy.Reject,
        IReadOnlyList<long>? allowedInputLengths = null,
        IReadOnlyList<long>? expectedInputLengths = null,
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
            AddressSpaceMutability.Immutable,
            inputPaddingByte,
            inputOversizePolicy,
            allowedInputLengths,
            expectedInputLengths);
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
        var identity = new LegacyCompiledCompositionIdentity(
            profileId,
            profileVersion,
            icId,
            modeId,
            experienceId,
            CompositionKind.Merge);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, initializationFillByte),
            spaces,
            operations);
        return CompiledComposition.CreateLegacy(
            plan,
            identity,
            defaultOutputFileName,
            CompiledIcNumberPolicy.NotApplicable,
            validationRequirements);
    }

    private static CompiledComposition CreateReplace(CompiledIcNumberPolicy policy)
    {
        LegacyCompiledCompositionIdentity identity = CreateReplaceIdentity();
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference", 4),
            [
                new AddressSpace("reference", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            []);
        return CompiledComposition.CreateLegacy(plan, identity, "replace.bin", policy);
    }

    private static CompiledComposition CreateMultiOutput(string outputSpaceId)
    {
        LegacyCompiledCompositionIdentity identity = CreateMergeIdentity();
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 4, 0),
                ImageInitialization.Blank("scratch", 4, 0),
            ],
            outputSpaceId,
            [
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
                new AddressSpace("scratch", 4, AddressSpaceMutability.Mutable),
            ],
            []);
        return CompiledComposition.CreateLegacy(
            plan,
            identity,
            "output.bin",
            CompiledIcNumberPolicy.NotApplicable);
    }

    private static CompiledComposition CreatePatchComposition(
        long targetStart = 0,
        byte patchByte = 0x11,
        OverlapPolicy overlapPolicy = OverlapPolicy.Reject)
    {
        var identity = new LegacyCompiledCompositionIdentity(
            "patch-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "patch",
            "general-merge",
            CompositionKind.Merge);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 2, 0),
            [new AddressSpace("output-image", 2, AddressSpaceMutability.Mutable)],
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
        return CompiledComposition.CreateLegacy(
            plan,
            identity,
            "patch.bin",
            CompiledIcNumberPolicy.NotApplicable);
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
        var identity = new LegacyCompiledCompositionIdentity(
            "processor-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "processor",
            "standard-merge",
            CompositionKind.Merge);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [
                new AddressSpace("source", 3, AddressSpaceMutability.Immutable),
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
        return CompiledComposition.CreateLegacy(
            plan,
            identity,
            "processor.bin",
            CompiledIcNumberPolicy.NotApplicable);
    }

    private static LegacyCompiledCompositionIdentity CreateMergeIdentity()
    {
        return new LegacyCompiledCompositionIdentity(
            "profile-a",
            "1.0.0",
            "NT-SYNTHETIC",
            "standard",
            "standard-merge",
            CompositionKind.Merge);
    }

    private static LegacyCompiledCompositionIdentity CreateReplaceIdentity()
    {
        return new LegacyCompiledCompositionIdentity(
            "replace-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "replace",
            "general-replace",
            CompositionKind.Replace);
    }
}
