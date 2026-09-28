using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// TP-SVN-MODEL-1113-01 acceptance T2-T4 through the production inspection path. Expected bytes are read directly
/// from the Golden files at the owner offsets (TP start + 0x24; B bank start + TP start + 0x24), never through the
/// profiles; synthetic cases are labelled as such and never count as Golden evidence.
/// </summary>
public sealed class TpSvnInputInspectionTests
{
    private const string GoldenRoot = "testdata/golden/canonical";

    /// <summary>T2: every Standard Merge Golden TP input publishes the stamp at TP start + 0x24.</summary>
    [Theory]
    [InlineData("NT51923", "NT51923/standard-merge/gen-flash/topology-unscoped/nt51923-gen-flash/inputs/nt51923", 0x24, "C3801330")]
    [InlineData("NT51926", "NT51926/standard-merge/gen-flash/topology-unscoped/nt51926-gen-flash/inputs/nt51926", 0x24, "C0178357")]
    [InlineData("NT51917", "NT51927/standard-merge/gen-flash/topology-unscoped/nt51927-gen-flash/inputs/nt51927", 0x24, "C0179679")]
    [InlineData("NT51927", "NT51927/standard-merge/gen-flash/topology-unscoped/nt51927-gen-flash/inputs/nt51927", 0x24, "C0179679")]
    [InlineData("NT51928", "NT51928/standard-merge/gen-flash/topology-unscoped/nt51928-gen-flash/inputs/nt51928", 0x24, "C0159453")]
    [InlineData("NT51919", "NT51929/standard-merge/gen-flash/topology-unscoped/nt51929-gen-flash/inputs/nt51929", 0x7024, "C0174181")]
    [InlineData("NT51929", "NT51929/standard-merge/gen-flash/topology-unscoped/nt51929-gen-flash/inputs/nt51929", 0x7024, "C0174181")]
    [InlineData("NT51932", "NT51932/standard-merge/gen-flash/topology-unscoped/nt51932-gen-flash/inputs/nt51932", 0x7024, "00000000")]
    [InlineData("NT51950", "NT51950/standard-merge/dp-256k/topology-unscoped/51950-dp-256k/inputs/nt51950", 0xA024, "C0192152")]
    [InlineData("NT51951", "NT51951/standard-merge/dp-512k/topology-unscoped/51951-dp-512k/inputs/nt51951", 0xA024, "C0197654")]
    public void StandardTpInputPublishesItsStampAtTpStart(string icId, string inputPrefix, long offset, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        byte[] tp = ReadGolden(inputPrefix + "-tp-input.bin");
        byte[] dp = ReadGolden(inputPrefix + "-dp-input.bin");

        TpSvnObservation stamp = Assert.IsType<TpSvnObservation>(InspectStandard(icId, tp, dp).StandardTpSvn);

        Assert.Equal(new FirmwareAddressedRange("flash", new ByteRange(offset, 4)), stamp.Range);
        Assert.Equal(Convert.ToHexString(tp, (int)offset, 4), Hex(stamp));
        Assert.Equal(expected, Hex(stamp));
        Assert.Equal(expected == "00000000" ? TpSvnAnomaly.NotStamped
            : expected.StartsWith("C3", StringComparison.Ordinal) ? TpSvnAnomaly.UndefinedFlagBits : TpSvnAnomaly.None,
            stamp.Anomaly);
    }

    /// <summary>
    /// T2 counterexample (decision 36): nt51950_fw_T80.bin reads 00 00 00 00 at file offset 0x24, but its stamp is at
    /// TP start + 0x24 (0xA024); the inspection never reads file offset 0x24.
    /// </summary>
    [Fact]
    public void Nt51950StampIsReadAtTpStartNotAtFileOffset0x24()
    {
        byte[] tp = ReadGolden("NT51950/ab-merge/boe-d82t80/topology-unscoped/nt51950-ab-boe-d82t80/inputs/nt51950_fw_T80.bin");
        byte[] dp = ReadGolden("NT51950/standard-merge/dp-256k/topology-unscoped/51950-dp-256k/inputs/nt51950-dp-input.bin");
        Assert.Equal("00000000", Convert.ToHexString(tp, 0x24, 4));

        TpSvnObservation stamp = Assert.IsType<TpSvnObservation>(InspectStandard("NT51950", tp, dp).StandardTpSvn);

        Assert.Equal(new ByteRange(0xA024, 4), stamp.Range.Range);
        Assert.Equal("C0194914", Hex(stamp));
        Assert.Equal("194914", stamp.RevisionDigits);
        Assert.Equal(TpSvnAnomaly.None, stamp.Anomaly);
    }

    /// <summary>
    /// T4: the stamp value never changes the inspection issues or readiness of the same input (anomalies are
    /// information only; no Build blocker).
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, TpSvnAnomaly.NotStamped)]
    [InlineData(new byte[] { 0xDF, 0xAB, 0xCD, 0xEF }, TpSvnAnomaly.UndefinedFlagBits)]
    public void StampValueNeverChangesInspectionIssuesOrReadiness(byte[] stamp, TpSvnAnomaly anomaly)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        const string Prefix = "NT51927/standard-merge/gen-flash/topology-unscoped/nt51927-gen-flash/inputs/nt51927";
        byte[] original = ReadGolden(Prefix + "-tp-input.bin");
        byte[] dp = ReadGolden(Prefix + "-dp-input.bin");
        byte[] changed = (byte[])original.Clone();
        stamp.CopyTo(changed, 0x24);

        FirmwareInspectionSnapshot before = InspectStandard("NT51927", original, dp);
        FirmwareInspectionSnapshot after = InspectStandard("NT51927", changed, dp);

        Assert.Equal(anomaly, Assert.IsType<TpSvnObservation>(after.StandardTpSvn).Anomaly);
        Assert.Equal(before.InputSlotStatus?.BlocksBuild, after.InputSlotStatus?.BlocksBuild);
        Assert.Equal(before.InputSlotStatus?.InspectionLifecycle, after.InputSlotStatus?.InspectionLifecycle);
        Assert.Equal(before.InputSlotStatus?.InspectionIssueCode, after.InputSlotStatus?.InspectionIssueCode);
        Assert.Equal(before.FirmwareConfig?.IsFirmwareVersionBarValid, after.FirmwareConfig?.IsFirmwareVersionBarValid);
    }

    /// <summary>T2: CtrlRAM Standard Bases publish their own main stamp (exact Standard plan or TP-only consensus).</summary>
    [Theory]
    [InlineData("NT51927", "NT51927/ctrlram-replace/fw1.4.0/cascade-3/nt51927-3chip-self-20260705/inputs/nt51927-3ic-tm-tl177xfks03-gm-d08t9b-20260703.bin", "C28019C2", TpSvnAnomaly.UndefinedFlagBits, null)]
    [InlineData("NT51927", "NT51927/ctrlram-replace/fw1.3.2/cascade-2/nt51927-2chip-self-20260705/inputs/nt51927-2ic-csot1560-d09t0d-jira0251-20260617.bin", "C0195992", TpSvnAnomaly.None, "195992")]
    [InlineData("NT51923", "NT51923/ctrlram-replace/fw1.4.1/single/nt51923-fw141-single-auto-prj-662-20260717/inputs/NT51923_TPFW_T81_20260430.bin", "C0193916", TpSvnAnomaly.None, "193916")]
    public void CtrlRamStandardBasePublishesItsMainStamp(
        string icId, string basePath, string expected, TpSvnAnomaly anomaly, string? revision)
    {
        byte[] image = ReadGolden(basePath);

        FirmwareInspectionSnapshot inspection = InspectBase(icId, image);

        CtrlRamBaseInspection baseInspection = Assert.IsType<CtrlRamBaseInspection>(inspection.CtrlRamBaseInspection);
        Assert.True(baseInspection.Kind is CtrlRamBaseKind.StandardTp or CtrlRamBaseKind.StandardFlash, baseInspection.Kind.ToString());
        TpSvnObservation stamp = Assert.IsType<TpSvnObservation>(inspection.StandardTpSvn);
        Assert.Equal(new ByteRange(0x24, 4), stamp.Range.Range);
        Assert.Equal(Convert.ToHexString(image, 0x24, 4), Hex(stamp));
        Assert.Equal(expected, Hex(stamp));
        Assert.Equal(anomaly, stamp.Anomaly);
        Assert.Equal(revision, stamp.RevisionDigits);
    }

    /// <summary>
    /// Decisions 43 and 45: the Header-copy SVN is modeled but never shown; a copy that differs from the main stamp
    /// changes neither the shown stamp nor the Base classification, issues or facts.
    /// </summary>
    [Fact]
    public void CtrlRamBaseShowsOnlyTheMainStampWhenTheHeaderCopyDiffers()
    {
        byte[] original = ReadGolden("NT51927/ctrlram-replace/fw1.4.1/single/nt51927-fw141-single-auto-prj-529-20260717/expected/NT51927_Flashcode_TM_TL113UFKS01_GM_D04T01_20260612.bin");
        byte[] changed = (byte[])original.Clone();
        Assert.Equal(Convert.ToHexString(original, 0x24, 4), Convert.ToHexString(original, 0x32DE4, 4));
        changed.AsSpan(0x32DE4, 4).Clear();

        FirmwareInspectionSnapshot before = InspectBase("NT51927", original);
        FirmwareInspectionSnapshot after = InspectBase("NT51927", changed);

        TpSvnObservation stamp = Assert.IsType<TpSvnObservation>(after.StandardTpSvn);
        Assert.Equal(new ByteRange(0x24, 4), stamp.Range.Range);
        Assert.Equal("60183151", Hex(stamp));
        Assert.Equal(TpSvnAnomaly.None, stamp.Anomaly);
        CtrlRamBaseInspection first = Assert.IsType<CtrlRamBaseInspection>(before.CtrlRamBaseInspection);
        CtrlRamBaseInspection second = Assert.IsType<CtrlRamBaseInspection>(after.CtrlRamBaseInspection);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.Issues.Select(static issue => issue.Code), second.Issues.Select(static issue => issue.Code));
        Assert.Equal(first.StandardEventBufferFormatVersion, second.StandardEventBufferFormatVersion);
        Assert.Equal(before.FirmwareConfig, after.FirmwareConfig);
    }

    /// <summary>
    /// T3: AB Merge TP A/TP B inputs each publish their own stamp at TP start + 0x24 and are never compared; a
    /// synthetic B input with a different (all-zero) stamp keeps A unchanged.
    /// </summary>
    [Theory]
    [InlineData("NT51929", "NT51929/ab-merge/t05-d06/topology-unscoped/nt51929-ab-t05-d06/inputs/nt51929_TPFW_T05_20260611.bin", 0x7024, "C0195417")]
    [InlineData("NT51950", "NT51950/ab-merge/boe-d82t80/topology-unscoped/nt51950-ab-boe-d82t80/inputs/nt51950_fw_T80.bin", 0xA024, "C0194914")]
    public async Task AbMergeTpInputsPublishTheirOwnStamp(string icId, string tpPath, int offset, string expected)
    {
        byte[] a = ReadGolden(tpPath);
        byte[] b = (byte[])a.Clone();
        b.AsSpan(offset, 4).Clear();

        AbMergeInspectionBatch batch = await ((AbMergeAuthoringExperience)BootstrapTestHost.Services.AbMergeAuthoring)
            .InspectInputSlotsAsync(icId,
                [
                    new("a", "a.bin", AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpAInput, AbMergeTopologyToken: Token(icId)),
                    new("b", "b.bin", AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpBInput, AbMergeTopologyToken: Token(icId)),
                ],
                path => path == "a.bin" ? a : b, TestContext.Current.CancellationToken);

        TpSvnObservation stampA = Assert.IsType<TpSvnObservation>(batch.Facts["a"].TpSvn);
        TpSvnObservation stampB = Assert.IsType<TpSvnObservation>(batch.Facts["b"].TpSvn);
        Assert.Equal(new ByteRange(offset, 4), stampA.Range.Range);
        Assert.Equal(new ByteRange(offset, 4), stampB.Range.Range);
        Assert.Equal(expected, Hex(stampA));
        Assert.Equal("00000000", Hex(stampB));
        Assert.Equal(TpSvnAnomaly.NotStamped, stampB.Anomaly);
        Assert.Equal(batch.Statuses["a"].BlocksBuild, batch.Statuses["b"].BlocksBuild);
    }

    /// <summary>
    /// T3 (decision 44): an AB Reference Base shows each bank's own stamp at B bank start + TP start + 0x24, read
    /// bank-locally through the existing AbFlash classification. The 1 MiB NT51951 Common and NT51950 Common cascade
    /// images (512 KiB banks, 0x8A024) are synthetic: no Golden exists for them.
    /// </summary>
    [Theory]
    [InlineData("NT51929", "NT51929/ab-merge/t05-d06/topology-unscoped/nt51929-ab-t05-d06/expected/NT51929ZT_Flashcode_TM_TL150UQAS01-00_Stellantis_V28_D06T05_20260611_AB.bin", 0x40000, 0x7024)]
    [InlineData("NT51950", "NT51950/ab-merge/boe-d82t80/topology-unscoped/nt51950-ab-boe-d82t80/expected/NT51950_FlashCode_BOE1720_BOEVX_2560x1660_A_D82T80_B_D82T80_20260616_BOE.bin", 0x40000, 0xA024)]
    [InlineData("NT51950", "NT51950/ab-merge/osd-d03t02/single/nt51950-ab-osd-d03t02-20260924/expected/NT51950TT_Flashcode_BOE_NIO_Hiway_A_D03T02_B_D03T02_NioHiway_20260924.bin", 0x40000, 0xA024)]
    [InlineData("NT51951", null, 0x80000, 0xA024)]
    [InlineData("NT51950", null, 0x80000, 0xA024)]
    public void AbReferenceBasePublishesEachBankStampBankLocally(string icId, string? referencePath, int bankStart, int localOffset)
    {
        byte[] image = referencePath is not null ? ReadGolden(referencePath)
            : icId == "NT51951" ? CreateSyntheticNt51951CommonReference() : CreateSyntheticNt51950CommonCascadeReference();

        FirmwareInspectionSnapshot inspection = InspectBase(icId, image);

        CtrlRamBaseInspection baseInspection = Assert.IsType<CtrlRamBaseInspection>(inspection.CtrlRamBaseInspection);
        Assert.Equal(CtrlRamBaseKind.AbFlash, baseInspection.Kind);
        Assert.Null(inspection.StandardTpSvn);
        CtrlRamBaseBankInspection bankA = Assert.Single(baseInspection.Banks, static bank => bank.BankId == "a-bank");
        CtrlRamBaseBankInspection bankB = Assert.Single(baseInspection.Banks, static bank => bank.BankId == "b-bank");
        Assert.Equal(bankStart, bankB.Range.Start);
        TpSvnObservation stampA = Assert.IsType<TpSvnObservation>(bankA.TpSvn);
        TpSvnObservation stampB = Assert.IsType<TpSvnObservation>(bankB.TpSvn);
        Assert.Equal(new ByteRange(localOffset, 4), stampA.Range.Range);
        Assert.Equal(new ByteRange(localOffset, 4), stampB.Range.Range);
        Assert.Equal(Convert.ToHexString(image, localOffset, 4), Hex(stampA));
        Assert.Equal(Convert.ToHexString(image, bankStart + localOffset, 4), Hex(stampB));
        if (referencePath is null)
        {
            Assert.NotEqual(Hex(stampA), Hex(stampB));
        }
    }

    /// <summary>
    /// T3 (decision 83): the published NT51950 cascade and NT51951 routes build a Desay-format AB image on the Common
    /// 1 MiB map, so each TP input's own stamp lands at 0x0A024 (A) and 0x8A024 (B); the retired closed-Desay
    /// position 0x4A024 does not hold the B stamp. Synthetic TP inputs: no Golden exists for these routes.
    /// </summary>
    [Theory]
    [InlineData("NT51950", "cascade", 2, "nt51950-ab-merge-1024k")]
    [InlineData("NT51951", null, 1, "nt51951-ab-merge-1024k")]
    public async Task PublishedDesayAbMergePlacesTheBCodeStampAtTheCommonMapPosition(
        string icId, string? topology, byte chipCount, string mapId)
    {
        using TempWorkspace workspace = TempWorkspace.Create("tp-svn-published-desay");
        var environment = new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools"));
        Assert.True((await ((IExternalProcessorEnvironmentLoader)environment)
            .LoadToCompletionAsync(null, TestContext.Current.CancellationToken)).Succeeded);
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, localStateDirectory: IsolatedLocalState.CreateDirectory(),
            configurationPath: workspace.PathFor("format.json"));
        string a = workspace.Write("a.bin", CreateAbTpInput(0x97, chipCount, [0xC0, 0x20, 0x26, 0x01]));
        string b = workspace.Write("b.bin", CreateAbTpInput(0xA6, chipCount, [0x80, 0x20, 0x26, 0x02]));

        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), icId, topology,
            [new("tp-a-input", a, File.ReadAllBytes(a)), new("tp-b-input", b, File.ReadAllBytes(b))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        Assert.Equal(mapId,
            prepared.Snapshot!.ExactCapability!.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        CapabilityActionReadinessSnapshot? readiness = await host.AbMergeAuthoring.GetActionReadinessAsync(
            prepared.Snapshot, TestContext.Current.CancellationToken);
        string output = workspace.PathFor("output.bin");
        CompositionRunResult run = await host.CompositionExecution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(prepared.Snapshot,
                new Dictionary<string, string> { ["tp-a-input"] = a, ["tp-b-input"] = b },
                build: true, outputPath: output, actionReadiness: readiness),
            new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
        Assert.True(run.Succeeded, CompositionRunReportJson.Serialize(run));
        Assert.Equal("desay", run.Report.AbMergeFormat!.FormatId);

        byte[] image = File.ReadAllBytes(output);
        Assert.Equal(0x100000, image.Length);
        Assert.Equal("C0202601", Convert.ToHexString(image, 0xA024, 4));
        Assert.Equal("80202602", Convert.ToHexString(image, 0x8A024, 4));
        Assert.NotEqual("80202602", Convert.ToHexString(image, 0x4A024, 4));
    }

    /// <summary>T4: a Reference no layout recognizes shows no stamp and no bank; no position is guessed.</summary>
    [Theory]
    [InlineData("NT51929", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    public void UnrecognizedReferenceShowsNoStamp(string icId, int length)
    {
        FirmwareInspectionSnapshot inspection = InspectBase(icId, new byte[length]);

        CtrlRamBaseInspection baseInspection = Assert.IsType<CtrlRamBaseInspection>(inspection.CtrlRamBaseInspection);
        Assert.Equal(CtrlRamBaseKind.Unknown, baseInspection.Kind);
        Assert.Null(inspection.StandardTpSvn);
        Assert.Null(baseInspection.StandardTpSvn);
        Assert.All(baseInspection.Banks, static bank => Assert.Null(bank.TpSvn));
    }

    private static FirmwareInspectionSnapshot InspectStandard(string icId, byte[] tp, byte[] dp)
    {
        return BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical, icId,
                [
                    new("dp", "dp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
                    new("tp", "tp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput),
                ],
                path => path == "tp.bin" ? tp : dp)
            .Single(static result => result.InspectionId == "tp").Inspection;
    }

    private static FirmwareInspectionSnapshot InspectBase(string icId, byte[] image)
    {
        return Assert.Single(BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical, icId,
            [new("base", "base.bin", CtrlRamRequest: new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip),
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
            _ => image)).Inspection;
    }

    private static string? Token(string icId)
    {
        return icId == "NT51950" ? IcNumberSelectionTokens.SingleChip : null;
    }

    private static string Hex(TpSvnObservation stamp)
    {
        return Convert.ToHexString(stamp.RawBytes.Span);
    }

    private static byte[] ReadGolden(string relativePath)
    {
        return File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot($"{GoldenRoot}/{relativePath}"));
    }

    /// <summary>
    /// A synthetic standalone TP input of the NT51950/51 AB family (the AbMergeFormatRuntimeTests shape): primary
    /// FWConfig with the given Event Buffer Format byte, Backup with the IC count and NVT marker, and a TP SVN stamp at
    /// TP start + 0x24 (0xA024).
    /// </summary>
    private static byte[] CreateAbTpInput(byte format, byte chipCount, byte[] stamp)
    {
        byte[] tp = new byte[0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = format;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x3600C] = 0x84;
        tp[0x36017] = chipCount;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        stamp.CopyTo(tp, 0xA024);
        return tp;
    }

    private static byte[] CreateSyntheticNt51950CommonCascadeReference()
    {
        byte[] bank = ReadGolden("NT51950/standard-merge/dp-256k/topology-unscoped/51950-dp-256k/expected/nt51950-expected-output.bin");
        byte[] image = new byte[0x100000];
        image.AsSpan().Fill(0xFF);
        bank.CopyTo(image, 0);
        bank.CopyTo(image, 0x80000);
        image[0x8A024] = 0x80;
        image[0x8A025] = 0x20;
        image[0x8A026] = 0x26;
        image[0x8A027] = 0x09;
        return image;
    }

    private static byte[] CreateSyntheticNt51951CommonReference()
    {
        byte[] bank = ReadGolden(
            "NT51951/ctrlram-replace/fw2.0.0/single/nt51951-fw200-single-auto-prj-695-20260718/expected/nt51951-expected-output.bin");
        byte[] image = new byte[0x100000];
        bank.CopyTo(image, 0);
        bank.CopyTo(image, 0x80000);
        image[0x8A024] = 0x60;
        image[0x8A025] = 0x21;
        image[0x8A026] = 0x00;
        image[0x8A027] = 0x07;
        return image;
    }
}
