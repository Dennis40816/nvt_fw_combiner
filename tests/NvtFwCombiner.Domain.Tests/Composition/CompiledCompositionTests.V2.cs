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

        Assert.Equal("profile-v2", composition.V2Details.ProfileId);
        Assert.Equal("2.0.0", composition.V2Details.ProfileVersion);
        Assert.Equal("NT-SYNTHETIC", composition.V2Details.Provenance.Context.MemberId);
        Assert.Equal("standard", composition.V2Details.Provenance.Context.ModeId);
        Assert.Equal("standard-merge", composition.V2Details.ExperienceId);
        Assert.Equal(CompositionKind.Merge, composition.V2Details.CompositionKind);
        Assert.Equal("{original-name}_merged.bin", composition.V2Details.OutputNamingRequirement.FileNameTemplate);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        Assert.Null(composition.V2Details.IcNumberInputMode);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        V2CompilationProvenance provenance = details.Provenance;
        Assert.Equal("bundle-v2", provenance.Bundle.BundleId);
        Assert.Equal("profile-entry", provenance.ProfileEntry.EntryId);
        Assert.Equal("map", provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.Compilable, provenance.Promotion.Stage);
        Assert.Equal(["profile-evidence"], provenance.ProfileEvidenceRefs);
        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        Assert.Equal(["original-name"], output.TokenRequirements.Select(static requirement => requirement.TokenId));
        Assert.True(composition.CompilationFingerprint.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        Assert.Equal(
            "c944019eeaa7592c0801b22951d1353400c8a51b4dc8685f10e5a4a721bb1644",
            composition.CompilationFingerprint);
        Assert.Null(typeof(CompiledComposition).GetMethod("CreateV2", BindingFlags.Static | BindingFlags.Public));
    }

    /// <summary>Compiled artifacts reject IC-number modes outside the canonical closed vocabulary.</summary>
    [Fact]
    public void V2DetailsRejectUnknownIcNumberInputMode()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateV2(icNumberInputMode: (IcNumberInputMode)99));
    }

    /// <summary>Verifies compiler-established runtime eligibility retains its supported output policy.</summary>
    [Fact]
    public void V2RuntimeArtifactRequiresSupportedUnblockedTokenFreePromotion()
    {
        CompiledComposition runtime = CreateV2(
            promotion: new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            outputTemplate: "runtime.bin",
            outputInvalidCharacterPolicy: CompiledOutputInvalidCharacterPolicy.Reject,
            requiredOutputTokenIds: [],
            runtimeExecutable: true);

        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, runtime.Eligibility);
        Assert.Equal("runtime.bin", runtime.V2Details.OutputNamingRequirement.FileNameTemplate);
        CompiledComposition overridable = CreateV2(
            promotion: new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            outputTemplate: "runtime.bin",
            outputInvalidCharacterPolicy: CompiledOutputInvalidCharacterPolicy.Reject,
            requiredOutputTokenIds: [],
            allowOutputOverride: true,
            runtimeExecutable: true);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, overridable.Eligibility);
    }

    /// <summary>Canonical normal FlashCode and TP-firmware renderers are closed executable contracts.</summary>
    [Fact]
    public void V2RuntimeArtifactAdmitsCanonicalNormalOutputRenderers()
    {
        CompiledProfilePromotion supported =
            new(CompiledProfilePromotionStage.Supported, []);
        CompiledComposition flashCode = CreateV2(
            promotion: supported,
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                memberId: "NT51929"),
            outputNaming: NormalFlashCodeOutput(),
            runtimeExecutable: true);
        CompiledComposition tpFirmware = CreateV2(
            promotion: supported,
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                memberId: "NT51950"),
            outputNaming: TpFirmwareOutput(),
            runtimeExecutable: true);

        Assert.Equal(
            CompiledOutputNameRendererKind.NormalFlashCodeV1,
            flashCode.V2Details.OutputNamingRequirement.RendererKind);
        Assert.Equal(
            CompiledOutputNameRendererKind.TpFirmwareV1,
            tpFirmware.V2Details.OutputNamingRequirement.RendererKind);
        Assert.Equal(
            CompiledCompositionEligibility.V2RuntimeExecutable,
            flashCode.Eligibility);
        Assert.Equal(
            CompiledCompositionEligibility.V2RuntimeExecutable,
            tpFirmware.Eligibility);
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
        Assert.Equal(["original-name"], repeated.TokenRequirements.Select(static requirement => requirement.TokenId));
    }

    /// <summary>
    /// Normal FlashCode and TP-firmware names are closed compiled contracts whose
    /// metadata placeholders are explicit rather than Bootstrap-owned defaults.
    /// </summary>
    [Fact]
    public void NormalOutputNamingContractsOwnRendererAndMissingMetadataPolicy()
    {
        CompiledOutputNamingRequirement flashCode = NormalFlashCodeOutput();
        CompiledOutputNamingRequirement tpFirmware = TpFirmwareOutput();

        Assert.Equal(
            CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
            flashCode.RuleId);
        Assert.Equal(CompiledOutputArtifactType.FlashCode, flashCode.OutputArtifactType);
        Assert.Equal(
            CompiledOutputNameRendererKind.NormalFlashCodeV1,
            flashCode.RendererKind);
        Assert.Equal(
            CompiledOutputNamingRequirement.TpFirmwareV1RuleId,
            tpFirmware.RuleId);
        Assert.Equal(CompiledOutputArtifactType.TpFirmware, tpFirmware.OutputArtifactType);
        Assert.Equal(
            CompiledOutputNameRendererKind.TpFirmwareV1,
            tpFirmware.RendererKind);
        Assert.Equal(
            ("dp-inspection", CompiledOutputTokenSourceKind.DpcmiVersion, "xxxx",
                CompiledOutputTokenMissingPolicy.UsePlaceholder),
            MissingPolicy(flashCode, "dp-version"));
        Assert.Equal(
            ("tp-inspection", CompiledOutputTokenSourceKind.FirmwareConfigTpVersion, "xxxx",
                CompiledOutputTokenMissingPolicy.UsePlaceholder),
            MissingPolicy(flashCode, "tp-version"));
        Assert.Equal(
            (null, CompiledOutputTokenSourceKind.CompiledIc, null,
                CompiledOutputTokenMissingPolicy.Block),
            MissingPolicy(flashCode, "ic"));
        Assert.Equal(
            (null, CompiledOutputTokenSourceKind.RunDateUtc, null,
                CompiledOutputTokenMissingPolicy.Block),
            MissingPolicy(flashCode, "date"));
        Assert.Equal(
            ("tp-inspection", CompiledOutputTokenSourceKind.FirmwareConfigTpVersion, "xxxx",
                CompiledOutputTokenMissingPolicy.UsePlaceholder),
            MissingPolicy(tpFirmware, "tp-version"));
        Assert.DoesNotContain(
            tpFirmware.TokenRequirements,
            static requirement => requirement.TokenId == "dp-version");

        static (string? MetadataBindingId, CompiledOutputTokenSourceKind SourceKind,
            string? Placeholder, CompiledOutputTokenMissingPolicy Policy)
            MissingPolicy(
                CompiledOutputNamingRequirement output,
                string tokenId)
        {
            CompiledOutputTokenRequirement requirement =
                Assert.Single(output.TokenRequirements, candidate =>
                    candidate.TokenId == tokenId);
            return (
                requirement.MetadataBindingId,
                requirement.SourceKind,
                requirement.Placeholder,
                requirement.MissingPolicy);
        }
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

    /// <summary>Verifies compiled V2 region access and resolved physical-view provenance bind approval identity.</summary>
    [Fact]
    public void V2FingerprintBindsRegionAccessAndPhysicalViewProvenance()
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        FirmwareRegion root = resolvedMap.ImageMap.Regions.Single(static region => region.RegionId == "root");
        CompiledComposition readOnly = CreateV2(regionAccessContract: CreateRegionAccessContract(
            RegionAccessKind.ReadOnly,
            "Source bytes are inspectable only.",
            root), resolvedMap: resolvedMap);
        CompiledComposition hidden = CreateV2(regionAccessContract: CreateRegionAccessContract(
            RegionAccessKind.Hidden,
            "Source bytes are inspectable only.",
            root), resolvedMap: resolvedMap);
        CompiledComposition changedReason = CreateV2(regionAccessContract: CreateRegionAccessContract(
            RegionAccessKind.ReadOnly,
            "Source bytes require a different review rule.",
            root), resolvedMap: resolvedMap);

        CompiledRegionAccessRequirement access = Assert.Single(readOnly.V2Details.RegionAccessContract.Requirements);
        Assert.Equal("root", access.RegionId);
        Assert.Equal(RegionAccessKind.ReadOnly, access.Access);
        Assert.Equal(["root"], access.GoverningRegionChain.Select(static region => region.RegionId));
        Assert.Equal(["input", "output"], readOnly.V2Details.RegionAccessContract.ResolvedViews.Select(static view => view.ViewId));
        Assert.NotEqual(readOnly.CompilationFingerprint, hidden.CompilationFingerprint);
        Assert.NotEqual(readOnly.CompilationFingerprint, changedReason.CompilationFingerprint);
    }

    /// <summary>Verifies a V2 artifact cannot retain region chains or parts that disagree with its selected canonical map.</summary>
    [Fact]
    public void V2RegionAccessContractRejectsMapMismatches()
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        FirmwareRegion canonicalRoot = resolvedMap.ImageMap.Regions.Single(static region => region.RegionId == "root");
        var wrongConstraint = new FirmwareRegion(
            "root",
            parentRegionId: null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Image,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.WholeRegion);
        _ = Assert.Throws<ArgumentException>(() => CreateV2(regionAccessContract: CreateRegionAccessContract(
            RegionAccessKind.Whole,
            "Incorrect physical constraint.",
            wrongConstraint), resolvedMap: resolvedMap));
        _ = Assert.Throws<ArgumentException>(() => CreateV2(regionAccessContract: CreateRegionAccessContract(
            RegionAccessKind.Parts,
            "Unknown child.",
            canonicalRoot,
            allowedSubregionIds: ["missing-child"]), resolvedMap: resolvedMap));
    }

    /// <summary>Verifies resolved-view provenance cannot omit either the canonical root or deepest containing region.</summary>
    [Fact]
    public void V2RegionAccessContractRejectsTruncatedPhysicalViewChains()
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap nestedMap = CreateResolvedNestedMap();
        FirmwareRegion root = nestedMap.ImageMap.Regions.Single(static region => region.RegionId == "root");
        FirmwareRegion left = nestedMap.ImageMap.Regions.Single(static region => region.RegionId == "left");
        CompiledRegionAccessContract leafOnly = new(
            [],
            [new CompiledResolvedPhysicalView("input", "input", new ByteRange(0, 2), [left])]);
        CompiledRegionAccessContract rootOnly = new(
            [],
            [new CompiledResolvedPhysicalView("input", "input", new ByteRange(0, 2), [root])]);

        _ = Assert.Throws<ArgumentException>(() => CreateV2(resolvedMap: nestedMap, regionAccessContract: leafOnly));
        _ = Assert.Throws<ArgumentException>(() => CreateV2(resolvedMap: nestedMap, regionAccessContract: rootOnly));
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
                new FirmwareMetadataBytes([0xA0]), new FirmwareMetadataBytes([0xF0]))]),
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
            new FirmwareMetadataBytes([0xA0])));
        string masked = CalculateFingerprint(new CompiledViewByteAssertionValidation(
            "view", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error, "VIEW", "output-view",
            new FirmwareMetadataBytes([0xA0]), new FirmwareMetadataBytes([0xF0])));
        string changedMask = CalculateFingerprint(new CompiledViewByteAssertionValidation(
            "view", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error, "VIEW", "output-view",
            new FirmwareMetadataBytes([0xA0]), new FirmwareMetadataBytes([0x0F])));
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
        IEnumerable<CompiledValidationRequirement>? validationRequirements = null,
        IEnumerable<FirmwareMapFactBinding<FirmwareCapabilityFact>>? requiredCapabilities = null,
        CompiledInputContract? inputContract = null,
        CompiledRegionAccessContract? regionAccessContract = null,
        CompiledOutputNamingRequirement? outputNaming = null,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap? resolvedMap = null,
        CompositionPlan? plan = null,
        bool runtimeExecutable = false,
        string modeId = "standard",
        string experienceId = "standard-merge",
        CompositionKind compositionKind = CompositionKind.Merge,
        IcNumberInputMode? icNumberInputMode = null,
        string profileId = "profile-v2",
        string profileVersion = "2.0.0")
    {
        resolvedMap ??= CreateResolvedMap(familyContentHash, modeId: modeId);
        var bundle = new ProfileBundleIdentity(
            bundleId,
            bundleVersion,
            bundleContentHash,
            trustAnchorBindingId);
        var entry = new ProfileBundleEntryIdentity(profileEntryId, profileEntryHash);
        var provenance = new V2CompilationProvenance(
            bundle,
            entry,
            new ResolvedMapV2CompilationContext(resolvedMap),
            promotion ?? new CompiledProfilePromotion(CompiledProfilePromotionStage.Compilable, []),
            profileEvidenceRefs ?? ["profile-evidence"],
            validationRequirements ?? [],
            requiredCapabilities ?? []);
        CompiledOutputNamingRequirement output = outputNaming ?? new CompiledOutputNamingRequirement(
            outputTemplate,
            allowOutputOverride,
            outputInvalidCharacterPolicy,
            requiredOutputTokenIds ?? ["original-name"]);
        var details = new V2CompiledCompositionDetails(
            profileId,
            profileVersion,
            experienceId,
            compositionKind,
            provenance,
            inputContract ?? CreateInputContract(),
            regionAccessContract ?? new CompiledRegionAccessContract([], []),
            output,
            icNumberInputMode);
        plan ??= new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [
                new AddressSpace(
                    "input",
                    4,
                    AddressSpaceMutability.Immutable,
                    allowedInputLengths: [4]),
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
        return runtimeExecutable
            ? CompiledComposition.CreateV2RuntimeExecutable(plan, details)
            : CompiledComposition.CreateV2(plan, details);
    }

    private static CompiledInputContract CreateInputContract()
    {
        return new CompiledInputContract(
            [CompiledInputSlotTestFactory.Create(
                "input-slot",
                "input",
                CompiledInputArtifactClass.ReferenceImage,
                required: true,
                CompiledInputSlotCardinality.ExactlyOne,
                [".bin"],
                new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
                new CompiledNoInputNormalization())],
            [new CompiledInputSpaceBinding(
                "input",
                "input-slot",
                CompiledInputInstancePolicy.Singleton)]);
    }

    private static CompiledRegionAccessContract CreateRegionAccessContract(
        RegionAccessKind access,
        string reason,
        FirmwareRegion region,
        IReadOnlyList<string>? allowedSubregionIds = null)
    {
        FirmwareRegion[] chain = [region];
        return new CompiledRegionAccessContract(
            [new CompiledRegionAccessRequirement(
                "root",
                access,
                reason,
                allowedSubregionIds ?? [],
                chain)],
            [
                new CompiledResolvedPhysicalView("input", "input", new ByteRange(0, 4), chain),
                new CompiledResolvedPhysicalView("output", "output-image", new ByteRange(0, 4), chain),
            ]);
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap CreateResolvedMap(
        string familyContentHash,
        long capacity = 4,
        string modeId = "standard",
        string memberId = "NT-SYNTHETIC")
    {
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                [memberId],
                [modeId],
                TopologyRequirement.NoTopologyConstraint(),
                capacity),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, capacity),
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
            memberId,
            modeId,
            capacity,
            requestedTopology: null,
            []));

        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(result.ResolvedMap);
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap CreateResolvedNestedMap()
    {
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
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
                [
                    new FirmwareRegion(
                        "root",
                        parentRegionId: null,
                        FirmwareRegionOwner.System,
                        FirmwareRegionKind.Image,
                        new ByteRange(0, 4),
                        FirmwareWriteConstraint.DeclaredSubregions),
                    new FirmwareRegion(
                        "left",
                        "root",
                        FirmwareRegionOwner.System,
                        FirmwareRegionKind.Data,
                        new ByteRange(0, 2),
                        FirmwareWriteConstraint.WholeRegion),
                    new FirmwareRegion(
                        "right",
                        "root",
                        FirmwareRegionOwner.System,
                        FirmwareRegionKind.Data,
                        new ByteRange(2, 2),
                        FirmwareWriteConstraint.WholeRegion),
                ],
                ["map-evidence"])],
            [],
            ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
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
