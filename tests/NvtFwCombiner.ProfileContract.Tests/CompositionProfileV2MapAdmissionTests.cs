using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;
using ResolvedFirmwareImageMap = NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests map-bound admission for normalized composition-profile-v2 definitions.</summary>
public sealed class CompositionProfileV2MapAdmissionTests
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>Verifies exact family identity, effective regions, and declared metadata admit a profile.</summary>
    [Fact]
    public void ValidateAdmitsExactMapIdentityEffectiveRegionAndResolvedMetadata()
    {
        CompositionProfileDefinition profile = Profile();
        ResolutionContext context = ResolveContext();

        ValidateStatic(profile, context);
        AdmissionResult result = Admit(profile, context);

        Assert.True(result.IsAdmitted);
        Assert.Empty(result.Issues);
        Assert.Empty(result.CapabilityAdmissions);
    }

    /// <summary>Verifies all required physical regions must be present in the selected map's effective region graph.</summary>
    [Fact]
    public void ValidateRejectsMissingRequiredRegion()
    {
        CompositionProfileDefinition profile = Profile(regionIds: ["dp-code", "missing-region"]);
        ResolutionContext context = ResolveContext();

        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            ValidateStatic(profile, context));

        Assert.Equal("profile-bundle.catalog.profile-required-region-missing", exception.Code);
        Assert.Contains("missing-region", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies metadata requirements must be declared by the selected canonical map.</summary>
    [Fact]
    public void ValidateRejectsMissingRequiredMetadataStructure()
    {
        CompositionProfileDefinition profile = Profile(structureIds: ["firmware-config", "missing-structure"]);
        ResolutionContext context = ResolveContext();

        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            ValidateStatic(profile, context));

        Assert.Equal("profile-bundle.catalog.profile-required-metadata-missing", exception.Code);
        Assert.Contains("missing-structure", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a required capability without one selected family binding blocks admission.</summary>
    [Fact]
    public void ValidateRejectsMissingRequiredCapabilities()
    {
        AdmissionResult result = Admit(Profile(capabilityIds: ["ab-code", "crc32"]));

        Assert.False(result.IsAdmitted);
        Assert.Equal(
            [
                "profile.v2.map.required-capability-missing",
                "profile.v2.map.required-capability-missing",
            ],
            result.Issues.Select(static issue => issue.Code));
        Assert.Contains("ab-code", result.Issues[0].Message, StringComparison.Ordinal);
        Assert.Contains("crc32", result.Issues[1].Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies declaration order cannot change run-dependent capability diagnostics.</summary>
    [Fact]
    public void ValidateOrdersAdmissionIssuesIndependentlyOfRequirementDeclarationOrder()
    {
        AdmissionResult first = Admit(
            Profile(
                capabilityIds: ["z-capability", "a-capability"]));
        AdmissionResult second = Admit(
            Profile(
                capabilityIds: ["a-capability", "z-capability"]));

        Assert.Equal(
            first.Issues.Select(static issue => issue.Code),
            second.Issues.Select(static issue => issue.Code));
        Assert.Equal(
            first.Issues.Select(static issue => issue.Message),
            second.Issues.Select(static issue => issue.Message));
        Assert.Equal(
            [
                "profile.v2.map.required-capability-missing",
                "profile.v2.map.required-capability-missing",
            ],
            first.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies admission reads the selected map's canonical values and not direct-source map identifiers.</summary>
    [Fact]
    public void ValidateAcceptsAliasedEffectiveCanonicalFacts()
    {
        ResolutionContext context = ResolveContext(useAliasedFacts: true);

        ValidateStatic(Profile(), context);
        AdmissionResult result = Admit(Profile(), context);

        Assert.True(result.IsAdmitted);
        Assert.Contains(context.ResolvedMap.FactProvenance, static provenance => provenance.AliasChain.Count != 0);
    }

    /// <summary>
    /// Exact span, field, series, and group references admit without creating
    /// operations, processor stages, or write authority.
    /// </summary>
    [Fact]
    public void ValidateAdmitsEveryExactTypedMetadataTargetWithoutExecutionAuthority()
    {
        CompositionProfileDefinition profile = Profile(
            metadataBindings:
            [
                TypedMetadataBinding(
                    new(FirmwareMetadataReferenceTargetKind.Span, "complete-header"),
                    new(FirmwareMetadataReferenceTargetKind.Field, "pid"),
                    new(FirmwareMetadataReferenceTargetKind.Series, "pid-series"),
                    new(FirmwareMetadataReferenceTargetKind.Group, "pid-group")),
            ]);

        ResolutionContext context = ResolveContext(useTypedMetadata: true);
        ValidateStatic(profile, context);
        AdmissionResult result = Admit(profile, context);

        Assert.True(result.IsAdmitted);
        Assert.Empty(result.Issues);
        _ = Assert.Single(profile.Operations);
        Assert.Empty(profile.ProcessorStages);
        Assert.Empty(profile.Validations);
        Assert.Equal(RegionAccessKind.ReadOnly, Assert.Single(profile.RegionAccessRules).Access);
    }

    /// <summary>Every typed target kind fails closed when its exact ID is absent.</summary>
    [Theory]
    [InlineData(FirmwareMetadataReferenceTargetKind.Span)]
    [InlineData(FirmwareMetadataReferenceTargetKind.Field)]
    [InlineData(FirmwareMetadataReferenceTargetKind.Series)]
    [InlineData(FirmwareMetadataReferenceTargetKind.Group)]
    public void ValidateRejectsUnknownTypedMetadataTarget(
        FirmwareMetadataReferenceTargetKind kind)
    {
        CompositionProfileDefinition profile = Profile(
            metadataBindings:
            [
                TypedMetadataBinding(
                    new FirmwareMetadataReferenceTarget(kind, "missing-target")),
            ]);

        ResolutionContext context = ResolveContext(useTypedMetadata: true);
        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            ValidateStatic(profile, context));

        Assert.Equal("profile-bundle.catalog.profile-metadata-target-missing", exception.Code);
        Assert.Contains("missing-target", exception.Message, StringComparison.Ordinal);
    }

    private static AdmissionResult Admit(CompositionProfileDefinition profile)
    {
        return Admit(profile, ResolveContext());
    }

    private static AdmissionResult Admit(
        CompositionProfileDefinition profile,
        ResolutionContext context)
    {
        IReadOnlyList<CompositionIssue> issues = context.Family.AdmitRequiredCapabilities(
            profile.MapBinding,
            context.ResolvedMap,
            out IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityAdmissions);
        return new AdmissionResult(issues, capabilityAdmissions);
    }

    private static void ValidateStatic(
        CompositionProfileDefinition profile,
        ResolutionContext context)
    {
        TrustedProfileBundleCatalogFactory.ValidateStaticMapContract(
            profile,
            context.Family,
            context.ResolvedMap.ImageMap,
            new TrustedProfileBundleCatalogEntryIdentity(
                "profile-entry",
                "profiles/profile.json",
                "composition-profile-v2",
                FamilyHash));
    }

    private static CompositionProfileDefinition Profile(
        string familyId = "synthetic-family",
        string familyVersion = "1.0.0",
        string familyContentHash = FamilyHash,
        IEnumerable<string>? mapIds = null,
        IEnumerable<string>? regionIds = null,
        IEnumerable<string>? structureIds = null,
        IEnumerable<string>? capabilityIds = null,
        IReadOnlyList<CompositionProfileMetadataBinding>? metadataBindings = null)
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        return CompositionProfileV2DefinitionTestData.Create(parts with
        {
            MapBinding = new CompositionProfileMapBinding(
                familyId,
                familyVersion,
                familyContentHash,
                mapIds ?? ["standard-map"],
                regionIds ?? ["dp-code"],
                structureIds ?? ["firmware-config"],
                capabilityIds ?? []),
            MetadataBindings = metadataBindings ?? parts.MetadataBindings,
            Validations = metadataBindings is null ? parts.Validations : [],
        });
    }

    private static ResolutionContext ResolveContext(
        bool useAliasedFacts = false,
        bool useTypedMetadata = false,
        string familyContentHash = FamilyHash)
    {
        FirmwareMetadataField pid = new(
            "pid",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
        FirmwareMetadataSet metadata = new(
            "metadata",
            [new FirmwareMetadataStructure(
                "firmware-config",
                "tp-firmware",
                1,
                new FirmwareAbsoluteRangeLocator(
                    new FirmwareAddressedRange("flash", new ByteRange(0, 1)),
                    "dp-code"),
                [pid],
                [],
                typedDefinition: useTypedMetadata
                    ? TypedMetadataDefinition()
                    : null)],
            ["metadata-evidence"]);
        FirmwareMapApplicability applicability = new(
            ["NT-SYNTHETIC"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            16);
        FirmwareRegionSet regions = new(
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
                new FirmwareRegion(
                    "dp-code",
                    "root",
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Code,
                    new ByteRange(0, 1),
                    FirmwareWriteConstraint.Forbidden),
                new FirmwareRegion(
                    "unmapped",
                    "root",
                    FirmwareRegionOwner.Unknown,
                    FirmwareRegionKind.Unmapped,
                    new ByteRange(1, 15),
                    FirmwareWriteConstraint.Forbidden),
            ],
            ["region-evidence"]);
        FirmwareImageMap map = useAliasedFacts
            ? AliasedMap(applicability, regions, metadata)
            : FirmwareImageMapTestFactory.CreateDirect(
                "standard-map",
                "flash",
                applicability,
                FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
                [regions],
                [metadata],
                ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            familyContentHash,
            [map],
            [metadata]);
        FirmwareMapResolutionResult result = definition.ResolveMap(new FirmwareMapResolutionInputs(
            "NT-SYNTHETIC",
            "standard",
            16,
            requestedTopology: null,
            [new FirmwareArtifactPayload("tp-firmware", [0x01])]));

        return new ResolutionContext(
            definition,
            Assert.IsType<ResolvedFirmwareImageMap>(result.ResolvedMap));
    }

    private static CompositionProfileMetadataBinding TypedMetadataBinding(
        params FirmwareMetadataReferenceTarget[] targets)
    {
        return new CompositionProfileMetadataBinding(
            "typed-header",
            "source",
            "firmware-config",
            targets,
            [
                CompositionProfileMetadataPurpose.Inspection,
                CompositionProfileMetadataPurpose.Copy,
                CompositionProfileMetadataPurpose.Relocation,
                CompositionProfileMetadataPurpose.Integrity,
                CompositionProfileMetadataPurpose.Processor,
            ],
            ["owner-table"]);
    }

    private static FirmwareTpFlashHeaderDefinition TypedMetadataDefinition()
    {
        return new FirmwareTpFlashHeaderDefinition(
            [new FirmwareMetadataNamedSpan("complete-header", new ByteRange(0, 1))],
            [
                new FirmwareTpFlashHeaderFieldSemantics(
                    "pid",
                    "complete-header",
                    TpFlashHeaderFieldSubject.Header,
                    TpFlashHeaderFieldRole.Option,
                    logicalIndex: 0),
            ],
            [
                new FirmwareMetadataFieldSeries(
                    "pid-series",
                    [new FirmwareMetadataFieldSeriesMember(0, "pid")],
                    [
                        new FirmwareMetadataFieldSeriesApplicability(
                            1,
                            [0]),
                    ]),
            ],
            [
                new FirmwareMetadataFieldGroup(
                    "pid-group",
                    ["pid"],
                    []),
            ]);
    }

    private static FirmwareImageMap AliasedMap(
        FirmwareMapApplicability applicability,
        FirmwareRegionSet regions,
        FirmwareMetadataSet metadata)
    {
        return new FirmwareImageMap(
            "standard-map",
            "flash",
            applicability,
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [AliasedBinding(regions, applicability)],
            [AliasedBinding(metadata, applicability)],
            ["map-evidence"]);
    }

    private static FirmwareMapFactBinding<TFact> AliasedBinding<TFact>(
        TFact value,
        FirmwareMapApplicability applicability)
        where TFact : class, IFirmwareMapFact
    {
        var target = new FirmwareMapFactKey(
            "NT-SYNTHETIC",
            "standard-map",
            value.FactKind,
            "effective-" + value.CanonicalFactId);
        var source = new FirmwareMapFactKey(
            "NT-SOURCE",
            "source-map",
            value.FactKind,
            value.CanonicalFactId);
        var factApplicability = FirmwareFactApplicability.FromMap(applicability);
        var hop = new FirmwareFactAliasHop(
            "alias-" + value.CanonicalFactId,
            target,
            source,
            factApplicability,
            "Synthetic canonical fact inheritance.",
            ["alias-evidence"]);
        var provenance = new FirmwareFactProvenance(target, value, [hop]);
        return new FirmwareMapFactBinding<TFact>(
            factApplicability,
            provenance);
    }

    private sealed record ResolutionContext(
        FirmwareFamilyResolutionDefinition Family,
        ResolvedFirmwareImageMap ResolvedMap);

    private sealed record AdmissionResult(
        IReadOnlyList<CompositionIssue> Issues,
        IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> CapabilityAdmissions)
    {
        internal bool IsAdmitted => Issues.Count == 0;
    }
}
