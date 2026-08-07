using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests capability evidence admission after family normalization and unique map resolution.</summary>
public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Verifies direct confirmed-present capability evidence admits a profile without granting execution.</summary>
    [Fact]
    public void NormalizeDirectCapabilityEvidenceAdmitsExactEffectiveMap()
    {
        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(
            Document(includePredicate: false),
            FamilyHash);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = ResolveMap(definition);

        CapabilityAdmissionResult result = Admit(
            CapabilityProfile(["ab-code"]),
            definition,
            resolvedMap);

        CompiledCapabilityAdmission evidence = Assert.Single(result.CapabilityAdmissions);
        Assert.Equal("ab-code", evidence.RequiredCapabilityId);
        Assert.Equal("NT00001", evidence.Binding.EffectiveKey.MemberId);
        Assert.Equal("map", evidence.Binding.EffectiveKey.MapId);
        Assert.Empty(evidence.Binding.Provenance.AliasChain);
        Assert.Empty(result.Issues);
    }

    /// <summary>Verifies aliased capability evidence retains its effective target and source provenance through admission.</summary>
    [Fact]
    public void NormalizeAliasedCapabilityEvidenceAdmitsOnlyItsEffectiveMapMember()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument targetMap = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument document = source with
        {
            Members = [.. source.Members, new FirmwareFamilyMemberDocument("NT00002", "Source IC")],
            Capabilities =
            [
                new FirmwareCapabilityFactDocument(
                    "source-capability",
                    "ab-code",
                    "NT00002",
                    "source-map",
                    AliasApplicability(),
                    "confirmed-present",
                    "source capability evidence",
                    ["source-capability-evidence"]),
            ],
            ImageMaps =
            [
                targetMap,
                targetMap with
                {
                    MapId = "source-map",
                    Applicability = targetMap.Applicability with { MemberIds = ["NT00002"] },
                },
            ],
            FactAliases =
            [
                new FirmwareCapabilityAliasDocument(
                    "target-capability-to-source",
                    "NT00001",
                    "map",
                    "target-capability",
                    "NT00002",
                    "source-map",
                    "source-capability",
                    AliasApplicability(),
                    "target capability inherits source evidence",
                    ["target-capability-evidence"]),
            ],
        };
        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = ResolveMap(definition);

        CapabilityAdmissionResult result = Admit(
            CapabilityProfile(["ab-code"]),
            definition,
            resolvedMap);

        CompiledCapabilityAdmission evidence = Assert.Single(result.CapabilityAdmissions);
        Assert.Equal("NT00001", evidence.Binding.EffectiveKey.MemberId);
        Assert.Equal("map", evidence.Binding.EffectiveKey.MapId);
        Assert.Equal("NT00002", evidence.Binding.DirectSourceKey.MemberId);
        Assert.Equal("source-map", evidence.Binding.DirectSourceKey.MapId);
        Assert.Equal(
            ["target-capability-to-source"],
            evidence.Binding.Provenance.AliasChain.Select(static hop => hop.AliasId));
        Assert.Empty(result.Issues);
    }

    /// <summary>Verifies non-present capability evidence remains an admission blocker rather than a support decision.</summary>
    [Theory]
    [InlineData("confirmed-absent", "profile.v2.map.required-capability-absent")]
    [InlineData("unknown", "profile.v2.map.required-capability-unknown")]
    public void NormalizeNonPresentCapabilityEvidenceBlocksAdmission(string state, string expectedIssueCode)
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(
            source with
            {
                Capabilities = [Assert.Single(source.Capabilities) with { State = state }],
            },
            FamilyHash);

        CapabilityAdmissionResult result = Admit(
            CapabilityProfile(["ab-code"]),
            definition,
            ResolveMap(definition));

        Assert.False(result.IsAdmitted);
        Assert.Empty(result.CapabilityAdmissions);
        Assert.Equal([expectedIssueCode], result.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies a capability bound only to another effective member/map cannot satisfy the selected target map.</summary>
    [Fact]
    public void NormalizeSourceOnlyCapabilityEvidenceCannotSatisfyTheTargetMap()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument targetMap = Assert.Single(source.ImageMaps);
        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(
            source with
            {
                Members = [.. source.Members, new FirmwareFamilyMemberDocument("NT00002", "Source IC")],
                Capabilities =
                [
                    new FirmwareCapabilityFactDocument(
                        "source-capability",
                        "ab-code",
                        "NT00002",
                        "source-map",
                        AliasApplicability(),
                        "confirmed-present",
                        "source capability evidence",
                        ["source-capability-evidence"]),
                ],
                ImageMaps =
                [
                    targetMap,
                    targetMap with
                    {
                        MapId = "source-map",
                        Applicability = targetMap.Applicability with { MemberIds = ["NT00002"] },
                    },
                ],
            },
            FamilyHash);

        CapabilityAdmissionResult result = Admit(
            CapabilityProfile(["ab-code"]),
            definition,
            ResolveMap(definition));

        Assert.False(result.IsAdmitted);
        Assert.Equal(["profile.v2.map.required-capability-missing"], result.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies capability applicability that depends on unavailable Common FW category evidence fails closed.</summary>
    [Fact]
    public void NormalizeCapabilityEvidenceRequiresAvailableCommonFirmwareCategory()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareCapabilityFactDocument capability = Assert.Single(source.Capabilities);
        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(
            source with
            {
                Capabilities =
                [
                    capability with
                    {
                        Applicability = capability.Applicability with
                        {
                            CommonFirmwareCategoryIds = ["common"],
                        },
                    },
                ],
            },
            FamilyHash);

        CapabilityAdmissionResult result = Admit(
            CapabilityProfile(["ab-code"]),
            definition,
            ResolveMap(definition));

        Assert.False(result.IsAdmitted);
        Assert.Equal(
            ["profile.v2.map.required-capability-applicability-unavailable"],
            result.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies a failed admission cannot retain partial canonical capability evidence.</summary>
    [Fact]
    public void AdmissionFailureDiscardsPartialCapabilityEvidence()
    {
        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(
            Document(includePredicate: false),
            FamilyHash);
        CapabilityAdmissionResult result = Admit(
            CapabilityProfile(["ab-code", "missing-capability"]),
            definition,
            ResolveMap(definition));

        Assert.False(result.IsAdmitted);
        Assert.Empty(result.CapabilityAdmissions);
        Assert.Equal(
            ["profile.v2.map.required-capability-missing"],
            result.Issues.Select(static issue => issue.Code));
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolveMap(
        FirmwareFamilyResolutionDefinition definition)
    {
        FirmwareMapResolutionResult result = definition.ResolveMap(Inputs([0xAA, (byte)'A', 0x02, 0x00]));
        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(result.ResolvedMap);
    }

    private static CompositionProfileDefinition CapabilityProfile(
        IReadOnlyList<string> requiredCapabilityIds)
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        return CompositionProfileV2DefinitionTestData.Create(parts with
        {
            MapBinding = new CompositionProfileMapBinding(
                "synthetic-family",
                "1.0.0",
                FamilyHash,
                ["map"],
                ["config-region"],
                ["config"],
                requiredCapabilityIds),
            Views =
            [
                new CompositionProfileView(
                    "source-view",
                    "source",
                    new MapRegionViewSelector("config-region")),
                parts.Views[1],
            ],
            MetadataBindings =
            [
                new CompositionProfileMetadataBinding(
                    "fwconfig",
                    "source",
                    "config",
                    ["chip-number"],
                    [CompositionProfileMetadataPurpose.Validation]),
            ],
            RegionAccessRules =
            [new CompositionProfileRegionAccess(
                "config-region",
                RegionAccessKind.ReadOnly,
                "Synthetic source region is immutable.")],
            Validations = [],
        });
    }

    private static CapabilityAdmissionResult Admit(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition definition,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        IReadOnlyList<CompositionIssue> issues = definition.AdmitRequiredCapabilities(
            profile.MapBinding,
            resolvedMap,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions);
        return new CapabilityAdmissionResult(issues, capabilityAdmissions);
    }

    private sealed record CapabilityAdmissionResult(
        IReadOnlyList<CompositionIssue> Issues,
        IReadOnlyList<CompiledCapabilityAdmission> CapabilityAdmissions)
    {
        internal bool IsAdmitted => Issues.Count == 0;
    }
}
