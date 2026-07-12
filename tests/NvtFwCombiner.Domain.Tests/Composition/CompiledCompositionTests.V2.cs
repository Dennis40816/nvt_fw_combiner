using System.Reflection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies a v2 plan artifact carries resolved physical provenance but remains non-executable.</summary>
    [Fact]
    public void V2PlanArtifactCapturesCompleteImmutableProvenance()
    {
        CompiledComposition composition = CreateV2();

        Assert.Equal("profile-v2", composition.ProfileId);
        Assert.Equal("2.0.0", composition.ProfileVersion);
        Assert.Equal("NT-SYNTHETIC", composition.IcId);
        Assert.Equal("standard", composition.ModeId);
        Assert.Equal("standard-merge", composition.ExperienceId);
        Assert.Equal(CompositionKind.Merge, composition.CompositionKind);
        Assert.Equal("{original-name}_merged.bin", composition.DefaultOutputFileName);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        Assert.Equal(CompiledIcNumberPolicy.NotApplicable, composition.IcNumberPolicy);
        ProfileBundleV2CompilationAuthority authority = Assert.IsType<ProfileBundleV2CompilationAuthority>(
            composition.Authority);
        Assert.Equal("1.0", authority.ModelVersion);

        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        V2CompilationProvenance provenance = details.Provenance;
        Assert.Equal("bundle-v2", provenance.Bundle.BundleId);
        Assert.Equal("profile-entry", provenance.ProfileEntry.EntryId);
        Assert.Equal("map", provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.Compilable, provenance.Promotion.Stage);
        Assert.Equal(["profile-evidence"], provenance.ProfileEvidenceRefs);
        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        Assert.Equal(["original-name"], output.RequiredTokenIds);
        Assert.True(composition.CompilationFingerprint.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        Assert.Equal(
            "37d6f2f2164dcbeea85eec23c259ad608a808c988c54fbc52701696bb01f2e84",
            composition.CompilationFingerprint);
        Assert.Null(typeof(CompiledComposition).GetMethod("CreateV2", BindingFlags.Static | BindingFlags.Public));
    }

    /// <summary>Verifies a v2 artifact cannot be minted before profile compilation evidence is sufficient.</summary>
    [Fact]
    public void V2PlanArtifactRejectsPromotionBelowCompilable()
    {
        CompiledProfilePromotion promotion = new(CompiledProfilePromotionStage.Authorable, []);

        _ = Assert.Throws<ArgumentException>(() => CreateV2(promotion: promotion));
    }

    /// <summary>Verifies profile-bundle and output requirement values cannot be forged with malformed or ambiguous identities.</summary>
    [Fact]
    public void V2IdentityAndOutputRequirementsRejectInvalidValues()
    {
        _ = Assert.Throws<ArgumentException>(() => new ProfileBundleIdentity(
            "bundle",
            "1.0.0",
            "not-a-hash",
            "release-binding"));
        _ = Assert.Throws<ArgumentException>(() => new ProfileBundleEntryIdentity("entry", new string('A', 64)));
        _ = Assert.Throws<ArgumentException>(() => new CompiledProfilePromotion(
            CompiledProfilePromotionStage.Supported,
            [new CompiledProfilePromotionBlocker(
                "release",
                CompiledProfilePromotionBlockerKind.Release,
                "Release evidence is pending.",
                [])]));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "folder/output.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "{original-name}.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "{original-name.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["original-name"]));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "output?.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "CON.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "LPT\u00B2.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "output. ",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "{UPPER}.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["UPPER"]));
        _ = Assert.Throws<ArgumentException>(() => new CompiledOutputNamingRequirement(
            "{-token}.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["-token"]));

        var repeated = new CompiledOutputNamingRequirement(
            "{original-name}-{original-name}.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["original-name"]);
        Assert.Equal(["original-name"], repeated.RequiredTokenIds);
    }

    /// <summary>Verifies v2 identity, resolved physical provenance, promotion, and unrendered naming all bind the fingerprint.</summary>
    [Fact]
    public void V2FingerprintBindsCompleteCompilerEvidence()
    {
        CompiledComposition baseline = CreateV2();
        CompiledComposition[] variants =
        [
            CreateV2(bundleId: "bundle-v3"),
            CreateV2(bundleVersion: "2.0.0"),
            CreateV2(bundleContentHash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            CreateV2(trustAnchorBindingId: "release-binding-two"),
            CreateV2(profileEntryId: "profile-entry-two"),
            CreateV2(profileEntryHash: "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"),
            CreateV2(familyContentHash: "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"),
            CreateV2(
                outputTemplate: "{source-name}_alternate.bin",
                requiredOutputTokenIds: ["source-name"]),
            CreateV2(allowOutputOverride: true),
            CreateV2(outputInvalidCharacterPolicy: CompiledOutputInvalidCharacterPolicy.Reject),
            CreateV2(profileEvidenceRefs: ["profile-evidence-two"]),
            CreateV2(promotion: new CompiledProfilePromotion(
                CompiledProfilePromotionStage.ExecutableCandidate,
                [new CompiledProfilePromotionBlocker(
                    "golden-pending",
                    CompiledProfilePromotionBlockerKind.Golden,
                    "Synthetic golden evidence is pending.",
                    ["golden-evidence"])])),
        ];

        Assert.All(variants, variant => Assert.NotEqual(baseline.CompilationFingerprint, variant.CompilationFingerprint));
    }

    /// <summary>Verifies canonical V2 list snapshots prevent declaration order from affecting identity.</summary>
    [Fact]
    public void V2FingerprintCanonicalizesEvidenceAndNamingTokenDeclarations()
    {
        CompiledComposition first = CreateV2(
            profileEvidenceRefs: ["evidence-b", "evidence-a"],
            outputTemplate: "{source-name}-{original-name}.bin",
            requiredOutputTokenIds: ["source-name", "original-name"]);
        CompiledComposition second = CreateV2(
            profileEvidenceRefs: ["evidence-a", "evidence-b"],
            outputTemplate: "{source-name}-{original-name}.bin",
            requiredOutputTokenIds: ["original-name", "source-name"]);

        Assert.Equal(first.CompilationFingerprint, second.CompilationFingerprint);
    }

    /// <summary>Verifies every closed validation requirement shape remains in the v2 compilation fingerprint.</summary>
    [Fact]
    public void V2FingerprintBindsValidationRequirements()
    {
        CompiledComposition baseline = CreateV2();
        CompiledValidationFieldReference field = new("firmware-config", "pid");
        CompiledComposition[] variants =
        [
            CreateV2(validationRequirements:
            [new CompiledMetadataValueValidation(
                "metadata-value", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "META", field,
                CompiledValidationMetadataComparison.OneOf,
                [new CompiledValidationIntegerLiteral(2), new CompiledValidationTextLiteral("A")])]),
            CreateV2(validationRequirements:
            [new CompiledPidSanityValidation(
                "pid", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "PID", field)]),
            CreateV2(validationRequirements:
            [new CompiledMetadataEqualityValidation(
                "equal", CompiledValidationStage.PreOperation, CompiledValidationSeverity.Warning, "EQUAL", field,
                new CompiledValidationFieldReference("legacy", "pid"))]),
            CreateV2(validationRequirements:
            [new CompiledRejectMetadataBytePatternValidation(
                "pattern", CompiledValidationStage.PreOperation, CompiledValidationSeverity.Error, "PATTERN", field,
                [CompiledValidationRejectedBytePattern.AllFF, CompiledValidationRejectedBytePattern.AllZero])]),
            CreateV2(validationRequirements:
            [new CompiledViewByteAssertionValidation(
                "view", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error, "VIEW", "output-view",
                new CompiledValidationBytes([0xA0]), new CompiledValidationBytes([0xF0]))]),
        ];

        Assert.All(variants, variant => Assert.NotEqual(baseline.CompilationFingerprint, variant.CompilationFingerprint));
    }

    /// <summary>Verifies validation payload fields and canonical declaration order remain compilation identity.</summary>
    [Fact]
    public void V2FingerprintBindsValidationPayloadFieldsAndCanonicalOrder()
    {
        CompiledValidationFieldReference field = new("firmware-config", "pid");
        CompiledValidationFieldReference alternateField = new("firmware-config", "pid-two");
        var metadata = new CompiledMetadataValueValidation(
            "metadata", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "META", field,
            CompiledValidationMetadataComparison.Equal, [new CompiledValidationIntegerLiteral(2)]);
        string baseline = CalculateFingerprint(metadata);
        CompiledValidationRequirement[] metadataVariants =
        [
            new CompiledMetadataValueValidation(
                "metadata", CompiledValidationStage.PreOperation, CompiledValidationSeverity.Error, "META", field,
                CompiledValidationMetadataComparison.Equal, [new CompiledValidationIntegerLiteral(2)]),
            new CompiledMetadataValueValidation(
                "metadata", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Warning, "META", field,
                CompiledValidationMetadataComparison.Equal, [new CompiledValidationIntegerLiteral(2)]),
            new CompiledMetadataValueValidation(
                "metadata", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "META_OTHER", field,
                CompiledValidationMetadataComparison.Equal, [new CompiledValidationIntegerLiteral(2)]),
            new CompiledMetadataValueValidation(
                "metadata", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "META", alternateField,
                CompiledValidationMetadataComparison.Equal, [new CompiledValidationIntegerLiteral(2)]),
            new CompiledMetadataValueValidation(
                "metadata", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "META", field,
                CompiledValidationMetadataComparison.NotEqual, [new CompiledValidationIntegerLiteral(2)]),
            new CompiledMetadataValueValidation(
                "metadata", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "META", field,
                CompiledValidationMetadataComparison.Equal, [new CompiledValidationIntegerLiteral(3)]),
        ];
        Assert.All(metadataVariants, variant => Assert.NotEqual(baseline, CalculateFingerprint(variant)));

        Assert.NotEqual(
            CalculateFingerprint(new CompiledMetadataEqualityValidation(
                "equal", CompiledValidationStage.PreOperation, CompiledValidationSeverity.Error, "EQUAL", field, alternateField)),
            CalculateFingerprint(new CompiledMetadataEqualityValidation(
                "equal", CompiledValidationStage.PreOperation, CompiledValidationSeverity.Error, "EQUAL", alternateField, field)));
        Assert.NotEqual(
            CalculateFingerprint(new CompiledRejectMetadataBytePatternValidation(
                "pattern", CompiledValidationStage.PreOperation, CompiledValidationSeverity.Error, "PATTERN", field,
                [CompiledValidationRejectedBytePattern.AllZero])),
            CalculateFingerprint(new CompiledRejectMetadataBytePatternValidation(
                "pattern", CompiledValidationStage.PreOperation, CompiledValidationSeverity.Error, "PATTERN", field,
                [CompiledValidationRejectedBytePattern.AllFF])));

        string withoutMask = CalculateFingerprint(new CompiledViewByteAssertionValidation(
            "view", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error, "VIEW", "output-view",
            new CompiledValidationBytes([0xA0])));
        string masked = CalculateFingerprint(new CompiledViewByteAssertionValidation(
            "view", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error, "VIEW", "output-view",
            new CompiledValidationBytes([0xA0]), new CompiledValidationBytes([0xF0])));
        string changedMask = CalculateFingerprint(new CompiledViewByteAssertionValidation(
            "view", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error, "VIEW", "output-view",
            new CompiledValidationBytes([0xA0]), new CompiledValidationBytes([0x0F])));
        Assert.NotEqual(withoutMask, masked);
        Assert.NotEqual(masked, changedMask);

        CompiledValidationRequirement first = new CompiledPidSanityValidation(
            "pid", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "PID", field);
        CompiledValidationRequirement second = new CompiledMetadataEqualityValidation(
            "equality", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error, "EQUAL", field, alternateField);
        Assert.Equal(
            CreateV2(validationRequirements: [first, second]).CompilationFingerprint,
            CreateV2(validationRequirements: [second, first]).CompilationFingerprint);

        static string CalculateFingerprint(CompiledValidationRequirement validation)
        {
            return CreateV2(validationRequirements: [validation]).CompilationFingerprint;
        }
    }

    private static CompiledComposition CreateV2(
        string bundleId = "bundle-v2",
        string bundleVersion = "1.0.0",
        string bundleContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        string trustAnchorBindingId = "release-binding",
        string profileEntryId = "profile-entry",
        string profileEntryHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        string familyContentHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
        CompiledProfilePromotion? promotion = null,
        string outputTemplate = "{original-name}_merged.bin",
        bool allowOutputOverride = false,
        CompiledOutputInvalidCharacterPolicy outputInvalidCharacterPolicy = CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
        IEnumerable<string>? requiredOutputTokenIds = null,
        IEnumerable<string>? profileEvidenceRefs = null,
        IEnumerable<CompiledValidationRequirement>? validationRequirements = null)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(familyContentHash);
        var bundle = new ProfileBundleIdentity(
            bundleId,
            bundleVersion,
            bundleContentHash,
            trustAnchorBindingId);
        var entry = new ProfileBundleEntryIdentity(profileEntryId, profileEntryHash);
        var provenance = new V2CompilationProvenance(
            bundle,
            entry,
            resolvedMap,
            promotion ?? new CompiledProfilePromotion(CompiledProfilePromotionStage.Compilable, []),
            profileEvidenceRefs ?? ["profile-evidence"],
            validationRequirements ?? []);
        var output = new CompiledOutputNamingRequirement(
            outputTemplate,
            allowOutputOverride,
            outputInvalidCharacterPolicy,
            requiredOutputTokenIds ?? ["original-name"]);
        var details = new V2CompiledCompositionDetails(provenance, output);
        var identity = new V2CompiledCompositionIdentity(
            "profile-v2",
            "2.0.0",
            "standard-merge",
            CompositionKind.Merge,
            details);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [
                new AddressSpace("input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(0, 4),
                OverlapPolicy.Reject,
                "copy synthetic immutable input")]);
        return CompiledComposition.CreateV2(
            plan,
            identity,
            CompiledIcNumberPolicy.NotApplicable);
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap CreateResolvedMap(
        string familyContentHash)
    {
        var map = FirmwareImageMap.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT-SYNTHETIC"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                4),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 4),
                    FirmwareWriteConstraint.Forbidden)],
                ["map-evidence"])],
            [],
            ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            familyContentHash,
            [map],
            []);
        FirmwareMapResolutionResult result = definition.ResolveMap(new FirmwareMapResolutionInputs(
            "NT-SYNTHETIC",
            "standard",
            4,
            requestedTopology: null,
            []));

        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(result.ResolvedMap);
    }
}
