using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Application.Tests.Metadata;

/// <summary>Tests the common slot-specific metadata inspection seam without copying firmware definitions.</summary>
public sealed class FirmwareMetadataInspectorTests
{
    private const string FamilyHash =
        "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    /// <summary>DPCMI resolves through its logical CMD1/Page0 register range and derives DP Version plus Jira.</summary>
    [Fact]
    public void DpcmiInspectionProjectsDerivedFactsFromCanonicalRawFields()
    {
        ResolvedMetadataPlan plan = CreateDpcmiPlan();
        byte[] dp = new byte[0x80];
        dp[0x36] = 0x2E;
        dp[0x37] = 0x03;
        dp[0x38] = 0xA4;

        MetadataInspectionSnapshot inspected = FirmwareMetadataInspector.Inspect(
            plan,
            [new FirmwareArtifactPayload(CompositionAddressSpaceIds.DpReplacement, dp)]);

        MetadataInspectionResult value = Assert.Single(inspected.Results);
        Assert.Equal(MetadataInspectionState.Value, value.State);
        Assert.Equal(new ByteRange(0x36, 3), value.Resolution!.Resolved!.LocatorOutcome.ResolvedRange.Range);
        Assert.True(DpcmiMetadataProjector.TryProject(inspected, out DpcmiMetadataFacts facts));
        Assert.Equal((byte)0x03, facts.MajorVersion);
        Assert.Equal((byte)0x0A, facts.MinorVersion);
        Assert.Equal((ushort)0x42E, facts.JiraNumber);
        Assert.Equal("030A", facts.VersionToken);
        Assert.Equal("AUTO_PRJ-1070", facts.JiraBadge);
        Assert.Equal(plan.ResolutionToken, inspected.ResolutionToken);
    }

    /// <summary>A TP artifact never satisfies or executes a DP-slot DPCMI declaration.</summary>
    [Fact]
    public void DpcmiInspectionDoesNotUseTpOnlyArtifact()
    {
        ResolvedMetadataPlan plan = CreateDpcmiPlan();
        byte[] tp = new byte[0x80];
        tp[0x36] = 0x2E;
        tp[0x37] = 0x03;
        tp[0x38] = 0xA4;

        MetadataInspectionSnapshot inspected = FirmwareMetadataInspector.Inspect(
            plan,
            [new FirmwareArtifactPayload("tp-firmware", tp)]);

        MetadataInspectionResult waiting = Assert.Single(inspected.Results);
        Assert.Equal(MetadataInspectionState.WaitingForArtifact, waiting.State);
        Assert.Equal(
            CompositionAddressSpaceIds.DpReplacement,
            waiting.PlanEntry.Definition.StructureDefinition.ArtifactBindingId);
        Assert.False(DpcmiMetadataProjector.TryProject(inspected, out _));
    }

    /// <summary>A declared but truncated DP artifact remains distinct from an undeclared structure or missing artifact.</summary>
    [Fact]
    public void DpcmiInspectionReportsBlockedArtifactWhenDeclaredRangeIsUnavailable()
    {
        ResolvedMetadataPlan plan = CreateDpcmiPlan();

        MetadataInspectionSnapshot inspected = FirmwareMetadataInspector.Inspect(
            plan,
            [new FirmwareArtifactPayload(
                CompositionAddressSpaceIds.DpReplacement,
                new byte[0x20])]);

        MetadataInspectionResult blocked = Assert.Single(inspected.Results);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, blocked.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds,
            blocked.Resolution!.Failure);
        Assert.False(DpcmiMetadataProjector.TryProject(inspected, out _));
    }

    /// <summary>Reference-only plan contracts reject copied, empty, duplicate, or undeclared selectors.</summary>
    [Fact]
    public void MetadataPlanEntryRejectsInvalidReferenceSelectors()
    {
        DpcmiFixture fixture = CreateDpcmiFixture();
        DpcmiFixture otherFixture = CreateDpcmiFixture();
        DpcmiFixture otherStructureFixture = CreateDpcmiFixture(
            structureId: "other-structure");

        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(fixture, spaceId: "other-space"));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(
                fixture,
                familyDefinition: otherFixture.Family));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(
                fixture,
                structureDefinition: otherStructureFixture.Structure));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(
                fixture,
                metadataSetBinding: otherFixture.MetadataBinding));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(fixture, fieldIds: []));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(fixture, fieldIds: [" "]));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(
                fixture,
                fieldIds:
                [
                    DpcmiMetadataContract.JiraLowFieldId,
                    DpcmiMetadataContract.JiraLowFieldId,
                ]));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(fixture, fieldIds: ["undeclared"]));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(fixture, purposes: []));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(
                fixture,
                purposes: [MetadataReferencePurpose.Validation, MetadataReferencePurpose.Validation]));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateDpcmiEntry(
                fixture,
                purposes: [(MetadataReferencePurpose)int.MaxValue]));
    }

    /// <summary>One plan cannot contain null, duplicate, or cross-resolution bindings.</summary>
    [Fact]
    public void MetadataPlanDefinitionRejectsInvalidEntrySets()
    {
        DpcmiFixture firstFixture = CreateDpcmiFixture();
        MetadataPlanEntry first = CreateDpcmiEntry(firstFixture);
        DpcmiFixture secondFixture = CreateDpcmiFixture();
        MetadataPlanEntry second = CreateDpcmiEntry(
            secondFixture,
            bindingId: "other-dpcmi-inspection");
        ResolvedFirmwareImageMap secondResolution = Assert.IsType<ResolvedFirmwareImageMap>(
            firstFixture.Family.ResolveMap(new FirmwareMapResolutionInputs(
                "NT51929",
                "dp-replace",
                0x80,
                requestedTopology: null,
                artifacts:
                [
                    new FirmwareArtifactPayload(
                        CompositionAddressSpaceIds.DpReplacement,
                        new byte[0x80]),
                ])).ResolvedMap);
        MetadataPlanEntry sameFamilyOtherResolution = CreateDpcmiEntry(
            firstFixture with { ResolvedMap = secondResolution },
            bindingId: "same-family-other-resolution");

        _ = Assert.Throws<ArgumentException>(() =>
            new MetadataPlanDefinition([null!]));
        _ = Assert.Throws<ArgumentException>(() =>
            new MetadataPlanDefinition([first, first]));
        _ = Assert.Throws<ArgumentException>(() =>
            new MetadataPlanDefinition([first, second]));
        _ = Assert.Throws<ArgumentException>(() =>
            new MetadataPlanDefinition([first, sameFamilyOtherResolution]));
    }

    /// <summary>Empty declarations stay empty, while zero Jira is a valid value without a badge.</summary>
    [Fact]
    public void EmptyPlanAndZeroJiraRemainDistinctValidResults()
    {
        ResolvedMetadataPlan emptyPlan = MetadataPlanDefinition.Empty.Resolve(
            new ResolutionToken("empty-catalog:1"));
        MetadataInspectionSnapshot empty = FirmwareMetadataInspector.Inspect(
            emptyPlan,
            []);

        Assert.Empty(empty.Results);
        Assert.False(DpcmiMetadataProjector.TryProject(empty, out _));

        ResolvedMetadataPlan plan = CreateDpcmiPlan();
        MetadataInspectionSnapshot inspected = FirmwareMetadataInspector.Inspect(
            plan,
            [new FirmwareArtifactPayload(
                CompositionAddressSpaceIds.DpReplacement,
                new byte[0x80])]);

        Assert.True(DpcmiMetadataProjector.TryProject(inspected, out DpcmiMetadataFacts facts));
        Assert.Null(facts.JiraBadge);
        Assert.Equal("0000", facts.VersionToken);
    }

    /// <summary>Canonical assertion failures are invalid metadata, not missing or blocked artifacts.</summary>
    [Fact]
    public void DpcmiInspectionReportsInvalidDeclaredBytes()
    {
        ResolvedMetadataPlan plan = CreateDpcmiPlan(expectedFirstByte: 0xAA);

        MetadataInspectionSnapshot inspected = FirmwareMetadataInspector.Inspect(
            plan,
            [new FirmwareArtifactPayload(
                CompositionAddressSpaceIds.DpReplacement,
                new byte[0x80])]);

        MetadataInspectionResult invalid = Assert.Single(inspected.Results);
        Assert.Equal(MetadataInspectionState.Invalid, invalid.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.StructureDecodeFailed,
            invalid.Resolution!.Failure);
        Assert.False(DpcmiMetadataProjector.TryProject(inspected, out _));
    }

    /// <summary>A publication token change alone invalidates an otherwise identical inspection snapshot.</summary>
    [Fact]
    public void PublicationGateRejectsChangedResolutionToken()
    {
        DpcmiFixture fixture = CreateDpcmiFixture();
        var definition = new MetadataPlanDefinition(
        [
            CreateDpcmiEntry(fixture),
        ]);
        ResolvedMetadataPlan original = definition.Resolve(
            new ResolutionToken("catalog:1"));
        ResolvedMetadataPlan reloaded = definition.Resolve(
            new ResolutionToken("catalog:2"));
        var artifact = new FirmwareArtifactPayload(
            CompositionAddressSpaceIds.DpReplacement,
            new byte[0x80]);
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(original, 5, [artifact]));

        Assert.True(MetadataInspectionPublicationGate.IsCurrent(
            snapshot,
            original,
            5,
            [artifact]));
        Assert.False(MetadataInspectionPublicationGate.IsCurrent(
            snapshot,
            reloaded,
            5,
            [artifact]));
    }

    private static ResolvedMetadataPlan CreateDpcmiPlan(byte? expectedFirstByte = null)
    {
        DpcmiFixture fixture = CreateDpcmiFixture(expectedFirstByte);
        var definition = new MetadataPlanDefinition(
        [
            CreateDpcmiEntry(fixture),
        ]);
        return definition.Resolve(new ResolutionToken("test-catalog:1"));
    }

    private static MetadataPlanEntry CreateDpcmiEntry(
        DpcmiFixture fixture,
        string bindingId = "dpcmi-inspection",
        string? spaceId = null,
        IEnumerable<string>? fieldIds = null,
        IEnumerable<MetadataReferencePurpose>? purposes = null,
        FirmwareFamilyResolutionDefinition? familyDefinition = null,
        ResolvedFirmwareImageMap? resolvedMap = null,
        FirmwareMapFactBinding<FirmwareMetadataSet>? metadataSetBinding = null,
        FirmwareMetadataStructure? structureDefinition = null)
    {
        return new MetadataPlanEntry(
            bindingId,
            spaceId ?? CompositionAddressSpaceIds.DpReplacement,
            CompositionAddressSpaceIds.DpReplacement,
            familyDefinition ?? fixture.Family,
            resolvedMap ?? fixture.ResolvedMap,
            metadataSetBinding ?? fixture.MetadataBinding,
            structureDefinition ?? fixture.Structure,
            fieldIds ??
            [
                DpcmiMetadataContract.JiraLowFieldId,
                DpcmiMetadataContract.MajorVersionFieldId,
                DpcmiMetadataContract.MinorVersionFieldId,
                DpcmiMetadataContract.JiraHighFieldId,
            ],
            purposes ??
            [
                MetadataReferencePurpose.Validation,
                MetadataReferencePurpose.OutputNaming,
                MetadataReferencePurpose.Display,
                MetadataReferencePurpose.Version,
            ]);
    }

    private static DpcmiFixture CreateDpcmiFixture(
        byte? expectedFirstByte = null,
        string structureId = DpcmiMetadataContract.StructureId)
    {
        FirmwareMetadataByteAssertion[] assertions = expectedFirstByte is { } expected
            ? [FirmwareMetadataByteAssertion.Exact(0, [expected])]
            : [];
        FirmwareMetadataStructure dpcmi = new(
            structureId,
            CompositionAddressSpaceIds.DpReplacement,
            3,
            new FirmwareRegionRelativeLocator(
                "initial-code-cmd1-page0",
                DpcmiMetadataContract.FirstRegister,
                "initial-code-cmd1-page0"),
            [
                Unsigned(DpcmiMetadataContract.JiraLowFieldId, 0),
                Unsigned(DpcmiMetadataContract.MajorVersionFieldId, 1),
                Unsigned(
                    DpcmiMetadataContract.MinorVersionFieldId,
                    2,
                    new FirmwareMetadataBitSlice(4, 4)),
                Unsigned(
                    DpcmiMetadataContract.JiraHighFieldId,
                    2,
                    new FirmwareMetadataBitSlice(0, 4)),
            ],
            assertions);
        FirmwareMetadataSet metadataSet = new(
            "initial-code-metadata",
            [dpcmi],
            ["owner-dpcmi-evidence"]);
        FirmwareRegionSet regionSet = new(
            "physical",
            "flash",
            [
                new FirmwareRegion(
                    "flash-image",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 0x80),
                    FirmwareWriteConstraint.Forbidden),
                new FirmwareRegion(
                    "initial-code",
                    "flash-image",
                    FirmwareRegionOwner.Dp,
                    FirmwareRegionKind.Code,
                    new ByteRange(0, 0x60),
                    FirmwareWriteConstraint.WholeRegion),
                new FirmwareRegion(
                    "initial-code-prefix",
                    "initial-code",
                    FirmwareRegionOwner.Dp,
                    FirmwareRegionKind.Data,
                    new ByteRange(0, 0x20),
                    FirmwareWriteConstraint.Forbidden),
                new FirmwareRegion(
                    "initial-code-cmd1-page0",
                    "initial-code",
                    FirmwareRegionOwner.Dp,
                    FirmwareRegionKind.Command,
                    new ByteRange(0x20, 0x40),
                    FirmwareWriteConstraint.Forbidden),
                new FirmwareRegion(
                    "flash-tail",
                    "flash-image",
                    FirmwareRegionOwner.Reserved,
                    FirmwareRegionKind.Reserved,
                    new ByteRange(0x60, 0x20),
                    FirmwareWriteConstraint.Forbidden),
            ],
            ["owner-map-evidence"]);
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT51929"],
                ["dp-replace"],
                TopologyRequirement.NoTopologyConstraint(),
                0x80),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [regionSet],
            [metadataSet],
            ["owner-map-evidence"]);
        var family = new FirmwareFamilyResolutionDefinition(
            "nt51929",
            "1.0.0",
            FamilyHash,
            [map],
            [metadataSet]);
        byte[] resolutionBytes = new byte[0x80];
        if (expectedFirstByte is { } required)
        {
            resolutionBytes[0x36] = required;
        }

        ResolvedFirmwareImageMap resolvedMap = Assert.IsType<ResolvedFirmwareImageMap>(
            family.ResolveMap(new FirmwareMapResolutionInputs(
                "NT51929",
                "dp-replace",
                0x80,
                requestedTopology: null,
                artifacts:
                [
                    new FirmwareArtifactPayload(
                        CompositionAddressSpaceIds.DpReplacement,
                        resolutionBytes),
                ])).ResolvedMap);
        FirmwareMapFactBinding<FirmwareMetadataSet> metadataBinding =
            Assert.Single(map.MetadataSetBindings);
        return new DpcmiFixture(family, resolvedMap, metadataBinding, dpcmi);
    }

    private static FirmwareMetadataField Unsigned(
        string fieldId,
        long offset,
        FirmwareMetadataBitSlice? bitSlice = null)
    {
        return new FirmwareMetadataField(
            fieldId,
            offset,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            bitSlice);
    }

    private sealed record DpcmiFixture(
        FirmwareFamilyResolutionDefinition Family,
        ResolvedFirmwareImageMap ResolvedMap,
        FirmwareMapFactBinding<FirmwareMetadataSet> MetadataBinding,
        FirmwareMetadataStructure Structure);
}
