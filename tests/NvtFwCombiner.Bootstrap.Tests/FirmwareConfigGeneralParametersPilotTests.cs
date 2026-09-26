using System.Buffers.Binary;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Headless evidence for the one canonical FirmwareConfig General Parameters
/// definition and its NT51927/NT51928 TP-metadata pilot.
/// </summary>
public sealed class FirmwareConfigGeneralParametersPilotTests
{
    private const int TpLength = 217088;
    private const int BackupStart = 0x1000;
    private const int MarkerStart = BackupStart + 0xFFC;

    /// <summary>Standard display explicitly selects the already-declared Event Buffer field.</summary>
    [Theory]
    [InlineData("NT51927")]
    [InlineData("NT51928")]
    public void StandardInspectionSelectsEventBufferField(string icId)
    {
        MetadataPlanEntry entry = CreatePlanEntry(icId);
        Assert.Contains(entry.TargetReferences, static target =>
            target.Kind == FirmwareMetadataReferenceTargetKind.Field &&
            target.TargetId == "event-buffer-format-version");
    }

    /// <summary>The selected canonical field is read from the same TP capture and locator.</summary>
    [Theory]
    [InlineData("NT51927")]
    [InlineData("NT51928")]
    public void StandardEventBufferProjectionRejectsMovedOrAmbiguousBackup(string icId)
    {
        ResolvedMetadataPlan plan = CreateResolvedPlan(icId);
        byte[] tp = CreateValidTp();
        tp[BackupStart + 0x0C] = 0xA3;
        Assert.Equal((byte)0xA3,
            FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(plan, tp, BackupStart)?.EventBufferFormatVersion);
        CanonicalEventBufferFieldObservation observed = Assert.IsType<CanonicalEventBufferFieldObservation>(
            FirmwareConfigGeneralParametersProjector.ReadObservation(plan, tp, BackupStart)?.EventBuffer);
        Assert.Equal(new ByteRange(BackupStart + 0x0C, 1), observed.FieldRange.Range);
        Assert.Equal("flash", observed.FieldRange.AddressSpaceId);
        tp[BackupStart + 0x0C] = 0;
        Assert.Equal((byte)0,
            FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(plan, tp, BackupStart)?.EventBufferFormatVersion);
        Assert.Null(FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(
            plan, tp, BackupStart + 1)?.EventBufferFormatVersion);
        WriteMarker(tp, 0x3000);
        Assert.Null(FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(
            plan, tp, BackupStart)?.EventBufferFormatVersion);
    }

    /// <summary>Display facts come from the common structure without per-profile field selection.</summary>
    [Fact]
    public void EventBufferProjectionDoesNotRequirePerProfileFieldTarget()
    {
        MetadataPlanEntry selected = CreatePlanEntry("NT51927");
        var withoutEvent = new MetadataPlanEntry(selected.BindingId, selected.SpaceId, selected.SlotId,
            selected.FamilyDefinition, selected.ResolvedMap, selected.MetadataSetBinding,
            selected.StructureDefinition, selected.TargetReferences.Where(static target =>
                target.TargetId != FirmwareConfigGeneralParametersContract.EventBufferFormatVersion),
            selected.Purposes, selected.EvidenceRefs);
        ResolvedMetadataPlan plan = new MetadataPlanDefinition([withoutEvent]).Resolve(
            new ResolutionToken("standard-event-without-target"));
        byte[] tp = CreateValidTp();
        tp[BackupStart + 0x0C] = 0xA3;

        Assert.Equal((byte)0xA3, FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(
            plan, tp, BackupStart)?.EventBufferFormatVersion);
    }

    /// <summary>Every Standard family supplies the same canonical TP display fact.</summary>
    [Theory]
    [InlineData("NT51917", 0)]
    [InlineData("NT51917", 0xA3)]
    [InlineData("NT51917", 0x7F)]
    [InlineData("NT51919", 0)]
    [InlineData("NT51919", 0xA3)]
    [InlineData("NT51919", 0x7F)]
    [InlineData("NT51923", 0)]
    [InlineData("NT51923", 0xA3)]
    [InlineData("NT51923", 0x7F)]
    [InlineData("NT51926", 0)]
    [InlineData("NT51926", 0xA3)]
    [InlineData("NT51926", 0x7F)]
    [InlineData("NT51927", 0)]
    [InlineData("NT51927", 0xA3)]
    [InlineData("NT51927", 0x7F)]
    [InlineData("NT51928", 0)]
    [InlineData("NT51928", 0xA3)]
    [InlineData("NT51928", 0x7F)]
    [InlineData("NT51929", 0)]
    [InlineData("NT51929", 0xA3)]
    [InlineData("NT51929", 0x7F)]
    [InlineData("NT51932", 0)]
    [InlineData("NT51932", 0xA3)]
    [InlineData("NT51932", 0x7F)]
    [InlineData("NT51950", 0)]
    [InlineData("NT51950", 0xA3)]
    [InlineData("NT51950", 0x7F)]
    [InlineData("NT51951", 0)]
    [InlineData("NT51951", 0xA3)]
    [InlineData("NT51951", 0x7F)]
    public void CommonEventBufferSupplyIsIndependentOfIcFieldLists(string icId, byte value)
    {
        // NT51950/NT51951 declare the Backup only at the NVT end flag [0x36FFC, 0x37000) (ADR 0076).
        int start = icId is "NT51950" or "NT51951" ? 0x36000 : 0x10000;
        byte[] dp = new byte[0x40000];
        dp[0] = 0x13;
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(icId, dp,
            ["dp-input", "tp-input"], out _, out ResolvedCapability? capability, out IReadOnlyList<CompositionIssue> issues),
            string.Join(" | ", issues.Select(static issue => issue.Message)));
        Assert.NotNull(capability);
        MetadataPlanEntry entry = capability.MetadataPlan.Entries.Single(static item =>
            item.Definition.StructureDefinition.StructureId == "firmware-config-general-parameters").Definition;
        ResolvedMetadataPlan plan = new MetadataPlanDefinition([entry]).Resolve(capability.ResolutionToken);
        byte[] original = CreateValidTp();
        byte[] image = new byte[entry.ResolvedMap.CapacityBytes];
        original.AsSpan(BackupStart, 0x1000).CopyTo(image.AsSpan(start));
        image[start + 0x0C] = value;
        var inputs = new FirmwareMapResolutionInputs(entry.MemberId, entry.ResolvedMap.ModeId,
            entry.ResolvedMap.CapacityBytes, requestedTopology: null,
            [new FirmwareArtifactPayload(entry.SpaceId, image)]);
        FirmwareMetadataStructureResolution resolution = entry.FamilyDefinition.ResolveMetadataStructure(entry.ImageMap.MapId,
            entry.StructureDefinition.StructureId, inputs);
        Assert.True(resolution.Resolved is not null, $"{icId}: {resolution.Failure}");
        Assert.Equal(value, FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(
            plan, image, start)?.EventBufferFormatVersion);
    }

    /// <summary>The reported NT51926 screenshot input publishes Event Buffer through the real inspection path.</summary>
    [Fact]
    public void Nt51926CanonicalTpSnapshotIncludesCommonEventBuffer()
    {
        string folder = Path.Combine(RepositoryPaths.FindRepositoryRoot(),
            "testdata/golden/canonical/NT51926/standard-merge/gen-flash/topology-unscoped/nt51926-gen-flash/inputs");
        byte[] tp = File.ReadAllBytes(Path.Combine(folder, "nt51926-tp-input.bin"));
        byte[] dp = File.ReadAllBytes(Path.Combine(folder, "nt51926-dp-input.bin"));
        IReadOnlyList<FirmwareInspectionSnapshotResult> results = BuiltInFirmwareInspection.InspectFirmwareBatch(
            BootstrapTestHost.Canonical, "NT51926",
            [new("tp", "tp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput),
                new("dp", "dp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput)],
            path => path == "tp.bin" ? tp : dp);
        Assert.Equal((byte)0x82, results.Single(static item => item.InspectionId == "tp")
            .Inspection.StandardEventBufferFormatVersion);
        Assert.Null(results.Single(static item => item.InspectionId == "dp")
            .Inspection.StandardEventBufferFormatVersion);
    }

    /// <summary>A real partial-family AB Base has display metadata without manufacturing Standard inputs.</summary>
    [Fact]
    public void Nt51950AbBaseSuppliesBothEventBufferValues()
    {
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            "ab-merge", "NT51950", "expected-output", "boe-d82t80"));
        FirmwareInspectionSnapshot inspection = Assert.Single(BuiltInFirmwareInspection.InspectFirmwareBatch(
            BootstrapTestHost.Canonical, "NT51950",
            [new("base", "base.bin", CtrlRamRequest: new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip),
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)], _ => reference)).Inspection;
        Assert.Equal(CtrlRamBaseKind.AbFlash, inspection.CtrlRamBaseInspection!.Kind);
        Assert.Equal(2, inspection.CtrlRamBaseInspection.Banks.Count);
        foreach (CtrlRamBaseBankInspection bank in inspection.CtrlRamBaseInspection.Banks)
        {
            int fieldAddress = checked((int)(bank.Range.Start + bank.FirmwareConfig!.FirmwareConfigBackupStart + 0x0C));
            Assert.Equal(reference[fieldAddress], bank.EventBufferFormatVersion);
            byte[] bankBytes = reference.AsSpan(checked((int)bank.Range.Start), checked((int)bank.Range.Length)).ToArray();
            ResolvedMetadataPlan plan = Assert.IsType<ResolvedMetadataPlan>(BootstrapTestHost.Canonical.Catalog
                .ResolveFullImageMetadataPlan("NT51950", bankBytes.Length).MetadataPlan);
            long start = bank.FirmwareConfig.FirmwareConfigBackupStart;
            Assert.Null(FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(plan, bankBytes, start + 1));
            Assert.Null(FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(plan, bankBytes.AsMemory(0, bankBytes.Length - 1), start));
            // A marker outside the layout-declared end flag is neither counted nor rejected (ADR 0076) ...
            WriteMarker(bankBytes, 0xB000);
            Assert.Equal(bank.EventBufferFormatVersion, FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(
                plan, bankBytes, start)?.EventBufferFormatVersion);
            // ... and it cannot replace a missing end flag.
            bankBytes.AsSpan(checked((int)start + 0xFFC), 4).Clear();
            Assert.Null(FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(plan, bankBytes, start));
        }
    }

    /// <summary>TP-only map consensus publishes only a source-identical canonical field.</summary>
    [Fact]
    public void StandardEventBufferConsensusRejectsMissingOrDisagreeingCandidate()
    {
        byte[] tp = CreateValidTp();
        tp[BackupStart + 0x0C] = 0xA3;
        CanonicalEventBufferFieldObservation observed = Assert.IsType<CanonicalEventBufferFieldObservation>(
            FirmwareConfigGeneralParametersProjector.ReadObservation(
                CreateResolvedPlan("NT51927"), tp, BackupStart)?.EventBuffer);
        Assert.Equal((byte)0xA3, FirmwareArtifactClassificationResolver.SelectCommonEventBufferFormat(
            [observed, observed]));
        Assert.Null(FirmwareArtifactClassificationResolver.SelectCommonEventBufferFormat(
            [observed, null]));
        Assert.Null(FirmwareArtifactClassificationResolver.SelectCommonEventBufferFormat(
            [observed, observed with { Value = 0xA4 }]));
        Assert.Null(FirmwareArtifactClassificationResolver.SelectCommonEventBufferFormat(
            [observed, observed with { FamilyContentHash = "different-family" }]));
        Assert.Null(FirmwareArtifactClassificationResolver.SelectCommonEventBufferFormat(
            [observed, observed with { FieldRange = new FirmwareAddressedRange("flash",
                new ByteRange(BackupStart + 0x0D, 1)) }]));
    }

    /// <summary>Standard TP and Base carry one typed observation; DP has no TP Event Buffer fact.</summary>
    [Theory]
    [InlineData("NT51927")]
    [InlineData("NT51928")]
    public void StandardTpAndBaseSnapshotsCarrySelectedEventBuffer(string icId)
    {
        byte[] tp = CreateValidTp();
        tp[BackupStart + 0x0C] = 0xA3;
        FirmwareInspectionSnapshotInput[] tpInput = icId == "NT51928"
            ? [new("dp", "dp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
                new("tp", "tp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput)]
            : [new("tp", "tp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput)];
        IReadOnlyList<FirmwareInspectionSnapshotResult> standardResults =
            BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical, icId,
                tpInput, path => path == "dp.bin" ? new byte[0x40000] : tp);
        FirmwareInspectionSnapshot standard = standardResults.Single(static result =>
            result.InspectionId == "tp").Inspection;
        Assert.Equal((byte)0xA3, standard.StandardEventBufferFormatVersion);

        FirmwareInspectionSnapshot reference = Assert.Single(
            BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical, icId,
                [new("base", "base.bin", CtrlRamRequest: new CtrlRamInspectionRequest(
                    IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => tp)).Inspection;
        CtrlRamBaseInspection baseInspection = Assert.IsType<CtrlRamBaseInspection>(
            reference.CtrlRamBaseInspection);
        Assert.Equal(CtrlRamBaseKind.StandardTp, baseInspection.Kind);
        // TP-only map consensus retains only a canonical field shared by every current candidate.
        Assert.Equal((byte)0xA3, reference.StandardEventBufferFormatVersion);
        Assert.Equal(reference.FileStamp, baseInspection.ReferenceStamp);

        byte[] fullReference = new byte[icId == "NT51927" ? 0x40000 : 0x80000];
        tp.CopyTo(fullReference, 0);
        FirmwareInspectionSnapshot exactReference = Assert.Single(
            BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical, icId,
                [new("base", "base.bin", CtrlRamRequest: new CtrlRamInspectionRequest(
                    IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => fullReference)).Inspection;
        Assert.Equal((byte)0xA3, exactReference.StandardEventBufferFormatVersion);
    }

    /// <summary>A captured Base fact cannot be carried into another file or catalog publication.</summary>
    [Fact]
    public void StandardBaseEventBufferRejectsChangedStampOrPublication()
    {
        var host = new IsolatedBootstrapTestHost();
        byte[] tp = CreateValidTp();
        tp[BackupStart + 0x0C] = 0xA3;
        FirmwareInspectionSnapshot captured = Assert.Single(
            BuiltInFirmwareInspection.InspectFirmwareBatch(host.Canonical, "NT51927",
                [new("base", "base.bin", CtrlRamRequest: new CtrlRamInspectionRequest(
                    IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => tp)).Inspection;
        CtrlRamBaseInspection baseInspection = Assert.IsType<CtrlRamBaseInspection>(
            captured.CtrlRamBaseInspection);
        Assert.Equal((byte)0xA3, captured.StandardEventBufferFormatVersion);

        byte[] changed = (byte[])tp.Clone();
        changed[BackupStart + 0x0C] = 0;
        FirmwareInspectionSnapshot changedFile = BuiltInFirmwareInspection.InspectFirmware(
            host.Canonical.FirmwareInspection, "NT51927", "base.bin", null,
            new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip), _ => changed,
            baseInspection: baseInspection);
        Assert.Null(changedFile.StandardEventBufferFormatVersion);

        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        FirmwareInspectionSnapshot changedPublication = BuiltInFirmwareInspection.InspectFirmware(
            host.Canonical.FirmwareInspection, "NT51927", "base.bin", null,
            new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip), _ => tp,
            baseInspection: baseInspection);
        Assert.Null(changedPublication.StandardEventBufferFormatVersion);
    }

    /// <summary>
    /// Both pilot routes bind the same source-backed metadata structure while
    /// retaining member-specific maps and NT51928's distinct LDC part.
    /// </summary>
    [Fact]
    public void Nt51927AndNt51928ReuseOneMetadataDefinitionButRetainDistinctMaps()
    {
        MetadataPlanEntry nt51927 = CreatePlanEntry("NT51927");
        MetadataPlanEntry nt51928 = CreatePlanEntry(
            "NT51928",
            inputLength: 0x80000,
            selectedInputSlotIds: [CompositionAddressSpaceIds.LdcInput]);

        Assert.Equal(
            "nt51917-nt51927-nt51928-canonical-container",
            nt51927.FamilyDefinition.FamilyId);
        Assert.Equal(
            nt51927.FamilyDefinition.FamilyId,
            nt51928.FamilyDefinition.FamilyId);
        Assert.Equal("1.4.0", nt51927.FamilyDefinition.FamilyVersion);
        Assert.Equal("1.5.1", nt51928.FamilyDefinition.FamilyVersion);
        Assert.Equal(
            "firmware-config-general-parameters",
            nt51927.StructureDefinition.StructureId);
        Assert.Equal(
            nt51927.StructureDefinition.StructureId,
            nt51928.StructureDefinition.StructureId);
        Assert.Equal(
            nt51927.StructureDefinition.Fields.Select(FieldIdentity),
            nt51928.StructureDefinition.Fields.Select(FieldIdentity));
        Assert.Contains(
            "common-fw-ap-fwconfig-general-parameters",
            nt51927.MetadataSetBinding.Value.EvidenceRefs);

        FirmwareMetadataStructure structure = nt51927.StructureDefinition;
        Assert.Equal(0x29, structure.LengthBytes);
        Assert.Equal(36, structure.Fields.Count);
        Assert.All(structure.Fields, static field =>
            Assert.False(string.IsNullOrWhiteSpace(field.SourceName)));
        Assert.Equal(
            structure.Fields.Count,
            structure.Fields.Select(static field => field.SourceName)
                .Distinct(StringComparer.Ordinal).Count());

        long nextOffset = 0;
        foreach (FirmwareMetadataField field in
                 structure.Fields.OrderBy(static field => field.Range.Start))
        {
            Assert.Equal(nextOffset, field.Range.Start);
            Assert.Equal(FirmwareMetadataEncoding.UnsignedInteger, field.Encoding);
            Assert.Equal(FirmwareMetadataByteOrder.LittleEndian, field.ByteOrder);
            nextOffset = field.Range.EndExclusive;
        }
        Assert.Equal(0x29, nextOffset);
        Assert.Equal(ExpectedFieldDefinitions, structure.Fields.Select(FieldIdentity));
        FirmwareMetadataFieldRelation relation = Assert.Single(structure.Relations);
        Assert.Equal("firmware-version-complement", relation.RelationId);
        Assert.Equal(
            FirmwareMetadataFieldRelationKind.BitwiseComplement,
            relation.Kind);
        Assert.Equal("tp-firmware-version", relation.SourceFieldId);
        Assert.Equal(
            "tp-firmware-version-complement",
            relation.RelatedFieldId);

        FirmwareImageMap map27 = nt51927.ResolvedMap.ImageMap;
        FirmwareImageMap map28 = nt51928.ResolvedMap.ImageMap;
        Assert.Equal("nt51927-standard-merge-256k", map27.MapId);
        Assert.Equal("nt51928-standard-merge-512k", map28.MapId);
        Assert.Equal(0x40000, map27.CapacityBytes);
        Assert.Equal(0x80000, map28.CapacityBytes);
        Assert.Equal(Region(map27, "tp-code").Range, Region(map28, "tp-code").Range);
        Assert.Equal(Region(map27, "dp-code").Range, Region(map28, "dp-code").Range);
        Assert.DoesNotContain(map27.Regions, static region => region.RegionId == "ldc-code");
        Assert.Equal(new ByteRange(0x40000, 0x22000), Region(map28, "ldc-code").Range);
    }

    /// <summary>
    /// The generic inspector decodes every physical field once, projects the
    /// accepted semantic subset, and remains byte-for-byte compatible with the
    /// legacy Backup reader for overlapping facts.
    /// </summary>
    [Theory]
    [InlineData("NT51927")]
    [InlineData("NT51928")]
    public void CanonicalInspectionProjectsFactsAndMatchesLegacyBackupReader(string icId)
    {
        byte[] tp = CreateValidTp();
        MetadataInspectionSnapshot snapshot = Inspect(icId, tp);

        MetadataInspectionResult result = Assert.Single(snapshot.Results);
        Assert.Equal(MetadataInspectionState.Value, result.State);
        FirmwareResolvedMetadataStructure resolved =
            Assert.IsType<FirmwareResolvedMetadataStructure>(result.Resolution?.Resolved);
        Assert.Equal(new ByteRange(BackupStart, 0x29), resolved.LocatorOutcome.ResolvedRange.Range);
        Assert.Equal(36, resolved.DecodedStructure.Facts.Count);
        Assert.True(
            FirmwareConfigGeneralParametersProjector.TryProject(
                snapshot,
                out FirmwareConfigGeneralParametersFacts facts));

        Assert.Equal(0x42, facts.TpFirmwareVersion);
        Assert.Equal(0xBD, facts.TpFirmwareVersionComplement);
        Assert.True(facts.IsTpFirmwareVersionComplementValid);
        Assert.Equal(18, facts.SensorCountX);
        Assert.Equal(32, facts.SensorCountY);
        Assert.Equal(1920, facts.DisplayResolutionX);
        Assert.Equal(1080, facts.DisplayResolutionY);
        Assert.Equal(10, facts.MaximumOperableFingers);
        Assert.Equal(0x10, facts.ReportIrqType);
        Assert.Equal(FirmwareConfigReportIrqMode.LevelLow, facts.ReportIrqMode);
        Assert.Equal(7, facts.TpFirmwareSubVersion);
        Assert.Equal(4096, facts.TpResolutionX);
        Assert.Equal(2560, facts.TpResolutionY);
        Assert.Equal(3, facts.ObservedIcCount);
        Assert.Equal(1, facts.OutermostIcMasterEnable);
        Assert.True(facts.IsOutermostIcMasterEnableValid);
        Assert.True(facts.UseOutermostIcAsMaster);
        Assert.Equal(1, facts.CascadeEnable);
        Assert.Equal("2.5.9", facts.CommonFirmwareVersion);
        Assert.Equal(0x0927, facts.Pid);

        Assert.True(
            FirmwareConfigMetadataReader.TryReadBackup(
                tp,
                out FirmwareConfigMetadata legacy));
        Assert.Equal(BackupStart, legacy.StructureStart);
        Assert.Equal(legacy.FirmwareVersion, facts.TpFirmwareVersion);
        Assert.Equal(
            legacy.FirmwareVersionBar,
            facts.TpFirmwareVersionComplement);
        Assert.Equal(
            legacy.IsFirmwareVersionBarValid,
            facts.IsTpFirmwareVersionComplementValid);
        Assert.Equal(legacy.FirmwareSubVersion, facts.TpFirmwareSubVersion);
        Assert.Equal(legacy.ChipNumber, facts.ObservedIcCount);
        Assert.Equal(
            legacy.CommonFwMajorVersion,
            facts.CommonFirmwareMajorVersion);
        Assert.Equal(
            legacy.CommonFwMinorVersion,
            facts.CommonFirmwareMinorVersion);
        Assert.Equal(
            legacy.CommonFwAdditionalVersion,
            facts.CommonFirmwareAdditionalVersion);
        Assert.Equal(legacy.ProjectId, facts.Pid);
    }

    /// <summary>A failed complement relation is reported without discarding otherwise valid facts.</summary>
    [Fact]
    public void ComplementMismatchRetainsDecodedFacts()
    {
        byte[] tp = CreateValidTp();
        tp[BackupStart + 1] = 0;

        MetadataInspectionSnapshot snapshot = Inspect("NT51927", tp);

        Assert.Equal(MetadataInspectionState.Value, Assert.Single(snapshot.Results).State);
        Assert.True(
            FirmwareConfigGeneralParametersProjector.TryProject(
                snapshot,
                out FirmwareConfigGeneralParametersFacts facts));
        Assert.False(facts.IsTpFirmwareVersionComplementValid);
        Assert.Equal(0x42, facts.TpFirmwareVersion);
        Assert.Equal(1920, facts.DisplayResolutionX);
    }

    /// <summary>Zero and multiple complete markers preserve their exact observed counts.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void MarkerCardinalityFailureReportsExactOwnerApprovedDiagnostic(
        int markerCount)
    {
        byte[] tp = CreateValidTp();
        if (markerCount == 0)
        {
            Array.Clear(tp, MarkerStart, 4);
        }
        else
        {
            WriteMarker(tp, 0x3000);
        }

        MetadataInspectionSnapshot snapshot = Inspect("NT51927", tp);

        MetadataInspectionResult blocked = Assert.Single(snapshot.Results);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, blocked.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch,
            blocked.Resolution?.Failure);
        Assert.Equal(markerCount, blocked.Resolution?.ObservedMarkerMatchCount);
        Assert.True(
            FirmwareConfigGeneralParametersProjector.TryCreateDiagnostic(
                snapshot,
                out FirmwareConfigInspectionDiagnostic diagnostic));
        Assert.Equal("firmware-config.marker-count-mismatch", diagnostic.Code);
        Assert.Equal(
            $"Expected exactly one NVT marker (00 4E 56 54), but found {markerCount}.",
            diagnostic.Message);
        Assert.False(FirmwareConfigGeneralParametersProjector.TryProject(snapshot, out _));
    }

    /// <summary>
    /// Missing and truncated TP inputs remain distinct prerequisite/range
    /// failures and never fall back to a speculative Primary address.
    /// </summary>
    [Fact]
    public void MissingAndTruncatedTpRemainDistinctFromMarkerCardinality()
    {
        ResolvedMetadataPlan plan = CreateResolvedPlan("NT51927");
        MetadataInspectionSnapshot missing = FirmwareMetadataInspector.Inspect(plan, []);
        MetadataInspectionSnapshot truncated = FirmwareMetadataInspector.Inspect(
            plan,
            [new FirmwareArtifactPayload("tp-input", new byte[0x2000])]);

        Assert.Equal(
            MetadataInspectionState.WaitingForArtifact,
            Assert.Single(missing.Results).State);
        MetadataInspectionResult blocked = Assert.Single(truncated.Results);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, blocked.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds,
            blocked.Resolution?.Failure);
        Assert.Null(blocked.Resolution?.ObservedMarkerMatchCount);
        Assert.False(
            FirmwareConfigGeneralParametersProjector.TryCreateDiagnostic(
                truncated,
                out _));
    }

    /// <summary>A unique marker whose T - 0xFFF result is invalid stays a typed range failure.</summary>
    [Fact]
    public void UniqueMarkerWithInvalidBackupRangeDoesNotBecomeCountFailure()
    {
        byte[] tp = new byte[TpLength];
        WriteMarker(tp, 0x100);

        MetadataInspectionSnapshot snapshot = Inspect("NT51927", tp);

        MetadataInspectionResult blocked = Assert.Single(snapshot.Results);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, blocked.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds,
            blocked.Resolution?.Failure);
        Assert.Null(blocked.Resolution?.ObservedMarkerMatchCount);
        Assert.False(
            FirmwareConfigGeneralParametersProjector.TryCreateDiagnostic(
                snapshot,
                out _));
    }

    private static MetadataInspectionSnapshot Inspect(string icId, byte[] tp)
    {
        return FirmwareMetadataInspector.Inspect(
            CreateResolvedPlan(icId),
            [new FirmwareArtifactPayload("tp-input", tp)]);
    }

    private static ResolvedMetadataPlan CreateResolvedPlan(string icId, long? inputLength = null)
    {
        MetadataPlanEntry entry = CreatePlanEntry(icId, inputLength);
        return new MetadataPlanDefinition([entry]).Resolve(
            new ResolutionToken($"fwconfig-pilot:{icId}"));
    }

    private static MetadataPlanEntry CreatePlanEntry(
        string icId,
        long? inputLength = null,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc[icId];
        CompiledComposition? composition;
        IReadOnlyList<CompositionIssue> issues;
        if (selectedInputSlotIds is null)
        {
            registration.TryCompile(inputLength, out composition, out issues);
        }
        else
        {
            registration.TryCompile(inputLength, selectedInputSlotIds, out composition, out issues);
        }
        Assert.Empty(issues);
        CompiledComposition compiled = Assert.IsType<CompiledComposition>(composition);
        return Assert.Single(
            registration.CreateMetadataPlan(compiled).Entries,
            static entry => StringComparer.Ordinal.Equals(
                entry.BindingId,
                "firmware-config-general-parameters-inspection"));
    }

    private static byte[] CreateValidTp()
    {
        byte[] tp = new byte[TpLength];
        Span<byte> config = tp.AsSpan(BackupStart, 0x29);
        config[0] = 0x42;
        config[1] = 0xBD;
        config[2] = 18;
        config[3] = 32;
        BinaryPrimitives.WriteUInt16LittleEndian(config[4..], 1920);
        BinaryPrimitives.WriteUInt16LittleEndian(config[6..], 1080);
        config[8] = 4;
        config[9] = 10;
        config[10] = 2;
        config[11] = 0x10;
        config[12] = 1;
        config[13] = 5;
        config[14] = 6;
        config[15] = 20;
        config[16] = 34;
        config[17] = 7;
        BinaryPrimitives.WriteUInt16LittleEndian(config[18..], 4096);
        BinaryPrimitives.WriteUInt16LittleEndian(config[20..], 2560);
        config[22] = 1;
        config[23] = 3;
        config[24] = 1;
        config[25] = 8;
        config[26] = 2;
        config[27] = 5;
        config[28] = 9;
        config[29] = 0x11;
        config[30] = 0x22;
        config[31] = 0x33;
        config[32] = 0x44;
        config[33] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(config[34..], 0x0927);
        config[36] = 4;
        config[37] = 5;
        config[38] = 6;
        config[39] = 0x12;
        config[40] = 0x34;
        WriteMarker(tp, MarkerStart);
        return tp;
    }

    private static void WriteMarker(Span<byte> bytes, int start)
    {
        bytes[start] = 0x00;
        bytes[start + 1] = (byte)'N';
        bytes[start + 2] = (byte)'V';
        bytes[start + 3] = (byte)'T';
    }

    private static FirmwareRegion Region(FirmwareImageMap map, string regionId)
    {
        return map.Regions.Single(region =>
            StringComparer.Ordinal.Equals(region.RegionId, regionId));
    }

    private static (string FieldId, string? SourceName, long Offset, int Width)
        FieldIdentity(FirmwareMetadataField field)
    {
        return (field.FieldId, field.SourceName, field.Range.Start, field.WidthBytes);
    }

    private static readonly (
        string FieldId,
        string? SourceName,
        long Offset,
        int Width)[] ExpectedFieldDefinitions =
    [
        ("tp-firmware-version", "u8FWVersion", 0x00, 1),
        ("tp-firmware-version-complement", "u8FWVersionBar", 0x01, 1),
        ("sensor-count-x", "u8AlgNumberX", 0x02, 1),
        ("sensor-count-y", "u8AlgNumberY", 0x03, 1),
        ("display-resolution-x", "u16LCMResolutionX", 0x04, 2),
        ("display-resolution-y", "u16LCMResolutionY", 0x06, 2),
        ("common-firmware-format-version", "u8CommonFwFormatVersion", 0x08, 1),
        ("maximum-operable-fingers", "u8ReportFingerNum", 0x09, 1),
        ("button-count", "u8ButtonNum", 0x0A, 1),
        ("report-irq-type", "u8IRQ_Type", 0x0B, 1),
        ("event-buffer-format-version", "u8EventBufferFormatVersion", 0x0C, 1),
        ("customized-function", "u8CustomizedFunction", 0x0D, 1),
        ("customer-tuning-version", "u8CustomerTuningVersion", 0x0E, 1),
        ("hardware-sensor-count-x", "u8HWNumberX", 0x0F, 1),
        ("hardware-sensor-count-y", "u8HWNumberY", 0x10, 1),
        ("tp-firmware-subversion", "u8FWSubVersion", 0x11, 1),
        ("tp-resolution-x", "u16TPResolutionX", 0x12, 2),
        ("tp-resolution-y", "u16TPResolutionY", 0x14, 2),
        ("report-by-command-buffer", "u8ReportByComBuf", 0x16, 1),
        ("observed-ic-count", "u8Chip_Num", 0x17, 1),
        ("outermost-ic-master-enable", "u8BC_EN", 0x18, 1),
        ("maximum-buffer-count", "u8MaxBufferNum", 0x19, 1),
        ("common-firmware-major-version", "u8CommonFwMajorVersion", 0x1A, 1),
        ("common-firmware-minor-version", "u8CommonFwMinorVersion", 0x1B, 1),
        ("common-firmware-additional-version", "u8CommonFwAdditionalVersion", 0x1C, 1),
        ("auto-build-svn-byte-1", "u8AutoBuildSvnVer1", 0x1D, 1),
        ("auto-build-svn-byte-2", "u8AutoBuildSvnVer2", 0x1E, 1),
        ("auto-build-svn-byte-3", "u8AutoBuildSvnVer3", 0x1F, 1),
        ("auto-build-svn-byte-4", "u8AutoBuildSvnVer4", 0x20, 1),
        ("cascade-enable", "u8CascadeEn", 0x21, 1),
        ("pid", "u16NovaTekProjectID", 0x22, 2),
        ("x-one-dimensional-count", "u8X1DNum", 0x24, 1),
        ("y-one-dimensional-count", "u8Y1DNum", 0x25, 1),
        ("afe-count", "u8AFE_Num", 0x26, 1),
        ("event-buffer-high-byte", "u8EventBufHbyte", 0x27, 1),
        ("event-buffer-middle-byte", "u8EventBufMbyte", 0x28, 1),
    ];
}
