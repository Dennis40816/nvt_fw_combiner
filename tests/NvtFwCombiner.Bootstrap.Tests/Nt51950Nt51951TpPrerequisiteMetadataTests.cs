using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Issue #176 evidence for TP FirmwareConfig-selected NT51950/NT51951 DPCMI
/// locations without duplicated metadata definitions.
/// </summary>
public sealed class Nt51950Nt51951TpPrerequisiteMetadataTests
{
    private const int Capacity = 0x40000;
    private const int FirmwareConfigStart = 0x36000;
    private const int FirmwareConfigMarkerStart = 0x36FFC;
    private const int Nt51950SingleDpcmiStart = 0x3B016;
    private const int SharedDpcmiStart = 0x05016;
    private const string FirmwareConfigDefinitionId =
        "firmware-config-general-parameters";

    /// <summary>
    /// Consumer bindings retain distinct locators while reusing the exact
    /// canonical #174 and #175 definition instances.
    /// </summary>
    [Fact]
    public void ConsumerBindingsReuseCanonicalDefinitionsButRetainMemberMaps()
    {
        MetadataPlanDefinition nt51950 = CreateStandardMergePlan("NT51950");
        MetadataPlanDefinition nt51951 = CreateStandardMergePlan("NT51951");
        MetadataPlanDefinition nt51927 = CreateStandardMergePlan("NT51927", inputLength: null);
        MetadataPlanDefinition nt51929 = CreateDpReplacePlan("NT51929");

        FirmwareMetadataStructure fwConfigProvider =
            StructureByDefinition(
                nt51927,
                FirmwareConfigDefinitionId);
        FirmwareMetadataStructure dpcmiProvider =
            Assert.Single(nt51929.Entries).StructureDefinition;
        FirmwareMetadataStructure fwConfig950 =
            StructureByDefinition(
                nt51950,
                FirmwareConfigDefinitionId);
        FirmwareMetadataStructure fwConfig951 =
            StructureByDefinition(
                nt51951,
                FirmwareConfigDefinitionId);
        FirmwareMetadataStructure dpcmi950 =
            StructureByDefinition(nt51950, DpcmiMetadataContract.StructureId);
        FirmwareMetadataStructure dpcmi951 =
            StructureByDefinition(nt51951, DpcmiMetadataContract.StructureId);

        Assert.Same(fwConfigProvider.Definition, fwConfig950.Definition);
        Assert.Same(fwConfigProvider.Definition, fwConfig951.Definition);
        Assert.Same(dpcmiProvider.Definition, dpcmi950.Definition);
        Assert.Same(dpcmiProvider.Definition, dpcmi951.Definition);
        Assert.NotSame(dpcmi950, dpcmi951);
        Assert.Equal(
            "nt51950-standard-merge-256k",
            nt51950.Entries[0].ResolvedMap.ImageMap.MapId);
        Assert.Equal(
            "nt51951-standard-merge-256k",
            nt51951.Entries[0].ResolvedMap.ImageMap.MapId);
        Assert.Equal(
            [0x40000L, 0x80000L, 0x100000L],
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51950"]
                .GetMapCapacities(out IReadOnlyList<CompositionIssue> issues));
        Assert.Empty(issues);
    }

    /// <summary>
    /// DP remains selected when TP is absent and the dependent DPCMI child
    /// reports the typed action that Presentation formats as Load TP first.
    /// </summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void MissingTpRetainsDpAndPublishesTypedPendingInput(string icId)
    {
        ResolvedMetadataPlan plan = Resolve(CreateStandardMergePlan(icId));
        var dp = new FirmwareArtifactPayload("dp-input", CreateDp());

        MetadataInspectionSnapshot snapshot =
            FirmwareMetadataInspector.Inspect(plan, [dp]);

        MetadataInspectionResult dpcmi = ResultByDefinition(
            snapshot,
            DpcmiMetadataContract.StructureId);
        Assert.Equal(MetadataInspectionState.WaitingForArtifact, dpcmi.State);
        Assert.Equal(ResolvedChildReadiness.PendingInput, dpcmi.Readiness);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.MissingArtifact,
            dpcmi.Resolution?.Failure);
        Assert.Equal(
            "firmware-config-standard-merge",
            dpcmi.Resolution?.Prerequisite?.StructureId);
        ResolvedPrerequisiteAction action =
            Assert.IsType<ResolvedPrerequisiteAction>(dpcmi.NextAction);
        Assert.Equal(
            ResolvedPrerequisiteActionKind.LoadArtifactFirst,
            action.Kind);
        Assert.Equal("tp-input", action.ArtifactBindingId);
        Assert.Equal("tp-input", action.SlotId);
        Assert.Equal(dp.Identity, Assert.Single(snapshot.ArtifactIdentities));
        InputSelectionMemberReadiness slotReadiness =
            InputSelectionReadinessResolver.ProjectMetadataDependency(
                dpcmi,
                isSelected: true);
        Assert.Equal("dp-input", slotReadiness.SlotId);
        Assert.False(slotReadiness.CanSelect);
        Assert.Equal(ResolvedChildReadiness.PendingInput, slotReadiness.Readiness);
        Assert.Equal(
            new InputSelectionNextAction(
                InputSelectionNextActionKind.LoadArtifactFirst,
                "tp-input",
                "tp-input"),
            slotReadiness.NextAction);
    }

    /// <summary>NT51950 count 1 and cascade count 2 select their evidenced anchors.</summary>
    [Theory]
    [InlineData(1, Nt51950SingleDpcmiStart, 0x12, 0x0A)]
    [InlineData(2, SharedDpcmiStart, 0x56, 0x0B)]
    public void Nt51950CurrentTpCountSelectsDpcmi(
        byte icCount,
        int expectedStart,
        byte expectedMajor,
        byte expectedMinor)
    {
        AssertDpcmiResolution(
            "NT51950",
            icCount,
            expectedStart,
            expectedMajor,
            expectedMinor);
    }

    /// <summary>NT51951 count 1 and cascade count 2 retain the shared 0x5000 anchor.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Nt51951CurrentTpCountRetainsSharedDpcmiAnchor(byte icCount)
    {
        AssertDpcmiResolution(
            "NT51951",
            icCount,
            SharedDpcmiStart,
            expectedMajor: 0x56,
            expectedMinor: 0x0B);
    }

    /// <summary>Unsupported observed counts are blocked rather than guessed.</summary>
    [Theory]
    [InlineData("NT51950", false)]
    [InlineData("NT51951", false)]
    [InlineData("NT51950", true)]
    [InlineData("NT51951", true)]
    public void UnsupportedTpCountBlocksDpcmiWithoutFallback(
        string icId,
        bool dpReplace)
    {
        ResolvedMetadataPlan plan = Resolve(
            dpReplace
                ? CreateDpReplacePlan(icId)
                : CreateStandardMergePlan(icId));
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            plan,
            [
                new FirmwareArtifactPayload(
                    dpReplace
                        ? CompositionAddressSpaceIds.DpReplacement
                        : "dp-input",
                    CreateDp()),
                new FirmwareArtifactPayload(
                    dpReplace
                        ? CompositionAddressSpaceIds.ReferenceBase
                        : "tp-input",
                    CreateTp(icCount: 0)),
            ]);

        MetadataInspectionResult dpcmi = ResultByDefinition(
            snapshot,
            DpcmiMetadataContract.StructureId);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, dpcmi.State);
        Assert.Equal(ResolvedChildReadiness.Blocked, dpcmi.Readiness);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure
                .PrerequisiteValueUnsupported,
            dpcmi.Resolution?.Failure);
        Assert.Null(dpcmi.NextAction);
    }

    /// <summary>A present but malformed TP blocks the prerequisite instead of requesting or guessing it.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void MalformedTpBlocksDpcmiWithoutLoadAction(string icId)
    {
        ResolvedMetadataPlan plan = Resolve(CreateStandardMergePlan(icId));
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            plan,
            [
                new FirmwareArtifactPayload("dp-input", CreateDp()),
                new FirmwareArtifactPayload("tp-input", new byte[0x100]),
            ]);

        MetadataInspectionResult dpcmi = ResultByDefinition(
            snapshot,
            DpcmiMetadataContract.StructureId);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, dpcmi.State);
        Assert.Equal(ResolvedChildReadiness.Blocked, dpcmi.Readiness);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.PrerequisiteRejected,
            dpcmi.Resolution?.Failure);
        Assert.Equal(
            "firmware-config-standard-merge",
            dpcmi.Resolution?.Prerequisite?.StructureId);
        Assert.Null(dpcmi.NextAction);
        InputSelectionMemberReadiness slotReadiness =
            InputSelectionReadinessResolver.ProjectMetadataDependency(
                dpcmi,
                isSelected: true);
        Assert.Equal(ResolvedChildReadiness.Blocked, slotReadiness.Readiness);
        Assert.False(slotReadiness.CanSelect);
        Assert.Null(slotReadiness.NextAction);
    }

    /// <summary>The built-in trust seam rejects every non-exact provider identity.</summary>
    [Theory]
    [InlineData("family")]
    [InlineData("version")]
    [InlineData("hash")]
    [InlineData("structure")]
    public void BuiltInDefinitionResolverRejectsNonExactReference(
        string mismatch)
    {
        var exact = new FirmwareMetadataStructureDefinitionReferenceDocument(
                "nt51929-nt51932",
                "1.3.0",
                "6cd257c38e4c9ecb4e44c14d12027e44a6d484b8176112dceccb7328d153b617",
                DpcmiMetadataContract.StructureId);
        FirmwareMetadataStructureDefinitionReferenceDocument changed =
            mismatch switch
            {
                "family" => new FirmwareMetadataStructureDefinitionReferenceDocument(
                    "unknown-family",
                    exact.FamilyVersion,
                    exact.FamilyContentHash,
                    exact.StructureId),
                "version" => new FirmwareMetadataStructureDefinitionReferenceDocument(
                    exact.FamilyId,
                    "1.2.1",
                    exact.FamilyContentHash,
                    exact.StructureId),
                "hash" => new FirmwareMetadataStructureDefinitionReferenceDocument(
                    exact.FamilyId,
                    exact.FamilyVersion,
                    "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                    exact.StructureId),
                "structure" =>
                    new FirmwareMetadataStructureDefinitionReferenceDocument(
                        exact.FamilyId,
                        exact.FamilyVersion,
                        exact.FamilyContentHash,
                        "unknown-structure"),
                _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
            };

        Assert.True(
            BuiltInCanonicalMetadataDefinitionResolver.Instance.TryResolve(
                exact,
                out FirmwareMetadataStructureDefinition? definition));
        Assert.NotNull(definition);
        Assert.False(
            BuiltInCanonicalMetadataDefinitionResolver.Instance.TryResolve(
                changed,
                out FirmwareMetadataStructureDefinition? rejected));
        Assert.Null(rejected);
    }

    /// <summary>
    /// A TP change invalidates the prior artifact/revision snapshot even when
    /// the capability publication token remains unchanged.
    /// </summary>
    [Fact]
    public void TpChangeRejectsStaleInspectionAndCurrentRevisionReResolves()
    {
        ResolvedMetadataPlan plan = Resolve(CreateStandardMergePlan("NT51950"));
        var dp = new FirmwareArtifactPayload("dp-input", CreateDp());
        var tpSingle = new FirmwareArtifactPayload("tp-input", CreateTp(icCount: 1));
        var tpCascade = new FirmwareArtifactPayload("tp-input", CreateTp(icCount: 2));
        MetadataInspectionSnapshot single = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(plan, 7, [dp, tpSingle]));
        MetadataInspectionSnapshot cascade = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(plan, 8, [dp, tpCascade]));

        Assert.True(MetadataInspectionPublicationGate.IsCurrent(
            single,
            plan,
            7,
            [dp, tpSingle]));
        Assert.False(MetadataInspectionPublicationGate.IsCurrent(
            single,
            plan,
            7,
            [dp, tpCascade]));
        Assert.False(MetadataInspectionPublicationGate.IsCurrent(
            single,
            plan,
            8,
            [dp, tpCascade]));
        Assert.True(MetadataInspectionPublicationGate.IsCurrent(
            cascade,
            plan,
            8,
            [dp, tpCascade]));
        Assert.Equal(
            new ByteRange(Nt51950SingleDpcmiStart, 3),
            DpcmiRange(single));
        Assert.Equal(
            new ByteRange(SharedDpcmiStart, 3),
            DpcmiRange(cascade));
    }

    private static void AssertDpcmiResolution(
        string icId,
        byte icCount,
        int expectedStart,
        byte expectedMajor,
        byte expectedMinor)
    {
        ResolvedMetadataPlan plan = Resolve(CreateStandardMergePlan(icId));
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            plan,
            [
                new FirmwareArtifactPayload("dp-input", CreateDp()),
                new FirmwareArtifactPayload("tp-input", CreateTp(icCount)),
            ]);
        MetadataInspectionSnapshot reverse = FirmwareMetadataInspector.Inspect(
            plan,
            [
                new FirmwareArtifactPayload("tp-input", CreateTp(icCount)),
                new FirmwareArtifactPayload("dp-input", CreateDp()),
            ]);
        MetadataInspectionResult dpcmi = ResultByDefinition(
            snapshot,
            DpcmiMetadataContract.StructureId);

        Assert.Equal(MetadataInspectionState.Value, dpcmi.State);
        Assert.Equal(ResolvedChildReadiness.Ready, dpcmi.Readiness);
        Assert.Null(dpcmi.NextAction);
        Assert.True(InputSelectionReadinessResolver
            .ProjectMetadataDependency(dpcmi, isSelected: true)
            .CanSelect);
        Assert.Equal(
            new ByteRange(expectedStart, 3),
            dpcmi.Resolution?.Resolved?.LocatorOutcome.ResolvedRange.Range);
        Assert.Equal(
            dpcmi.Resolution?.Resolved?.LocatorOutcome.ResolvedRange.Range,
            DpcmiRange(reverse));
        Assert.True(DpcmiMetadataProjector.TryProject(snapshot, out DpcmiMetadataFacts facts));
        Assert.Equal(expectedMajor, facts.MajorVersion);
        Assert.Equal(expectedMinor, facts.MinorVersion);
    }

    private static ByteRange DpcmiRange(MetadataInspectionSnapshot snapshot)
    {
        return ResultByDefinition(snapshot, DpcmiMetadataContract.StructureId)
            .Resolution!.Resolved!.LocatorOutcome.ResolvedRange.Range;
    }

    private static MetadataInspectionResult ResultByDefinition(
        MetadataInspectionSnapshot snapshot,
        string definitionId)
    {
        return Assert.Single(snapshot.Results, result =>
            StringComparer.Ordinal.Equals(
                result.PlanEntry.Definition.StructureDefinition.Definition.DefinitionId,
                definitionId));
    }

    private static FirmwareMetadataStructure StructureByDefinition(
        MetadataPlanDefinition plan,
        string definitionId)
    {
        return Assert.Single(plan.Entries, entry =>
            StringComparer.Ordinal.Equals(
                entry.StructureDefinition.Definition.DefinitionId,
                definitionId)).StructureDefinition;
    }

    private static ResolvedMetadataPlan Resolve(MetadataPlanDefinition plan)
    {
        return plan.Resolve(new ResolutionToken("nt51950-nt51951-prerequisite-test"));
    }

    private static MetadataPlanDefinition CreateStandardMergePlan(
        string icId,
        long? inputLength = Capacity)
    {
        return CreatePlan(
            BuiltInV2RegistrationRegistry.StandardMergeByIc[icId],
            inputLength);
    }

    private static MetadataPlanDefinition CreateDpReplacePlan(string icId)
    {
        return CreatePlan(
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[icId],
            Capacity);
    }

    private static MetadataPlanDefinition CreatePlan(
        BuiltInV2Registration registration,
        long? inputLength)
    {
        registration.TryCompile(
            inputLength,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        CompiledComposition compiled =
            Assert.IsType<CompiledComposition>(composition);
        return registration.CreateMetadataPlan(compiled);
    }

    private static byte[] CreateDp()
    {
        byte[] dp = new byte[Capacity];
        dp[Nt51950SingleDpcmiStart] = 0x34;
        dp[Nt51950SingleDpcmiStart + 1] = 0x12;
        dp[Nt51950SingleDpcmiStart + 2] = 0xA5;
        dp[SharedDpcmiStart] = 0x78;
        dp[SharedDpcmiStart + 1] = 0x56;
        dp[SharedDpcmiStart + 2] = 0xB9;
        return dp;
    }

    private static byte[] CreateTp(byte icCount)
    {
        byte[] tp = new byte[0x37000];
        tp[FirmwareConfigStart] = 0x42;
        tp[FirmwareConfigStart + 1] = 0xBD;
        tp[FirmwareConfigStart + 23] = icCount;
        tp[FirmwareConfigMarkerStart] = 0x00;
        tp[FirmwareConfigMarkerStart + 1] = 0x4E;
        tp[FirmwareConfigMarkerStart + 2] = 0x56;
        tp[FirmwareConfigMarkerStart + 3] = 0x54;
        return tp;
    }
}
