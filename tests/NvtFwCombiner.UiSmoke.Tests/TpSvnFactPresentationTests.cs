using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>TP SVN card facts follow owner decisions 34, 36 and 40 without restyling other facts.</summary>
public sealed class TpSvnFactPresentationTests
{
    private static readonly FirmwareConfigMetadataSnapshot Metadata =
        new(0x22000, "2.0.0", 0x80, 0x7F, true, 0, 1, 0x4E01, null, default);

    /// <summary>Golden-derived stamps decode into the owner tooltip lines and the single owner anomaly.</summary>
    [Theory]
    [InlineData(new byte[] { 0xC0, 0x19, 0x49, 0x14 }, "C0 19 49 14", "LOCAL_BUILD, DIFF_EXIST", "194914", null)]
    [InlineData(new byte[] { 0x60, 0x18, 0x31, 0x51 }, "60 18 31 51", "DIFF_EXIST, NO_SVN_RECORD", "183151", null)]
    [InlineData(new byte[] { 0xC3, 0x80, 0x13, 0x30 }, "C3 80 13 30", "LOCAL_BUILD, DIFF_EXIST", "801330",
        "Undefined flag bits 0x03. InsertPID.py may be faulty.")]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, "00 00 00 00", "none", "000000",
        "No SVN stamp. InsertPID.py may not have run.")]
    [InlineData(new byte[] { 0xC2, 0x1A, 0x2B, 0x3C }, "C2 1A 2B 3C", "LOCAL_BUILD, DIFF_EXIST", "not BCD",
        "Undefined flag bits 0x02. InsertPID.py may be faulty.")]
    public void StandardTpCardShowsRawBytesAndAnAlwaysPresentNote(
        byte[] raw, string value, string flags, string revision, string? warning)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var inspection = new FirmwareInspectionSnapshot(null, Metadata, null, null, null, null)
        {
            StandardEventBufferFormatVersion = 0x82,
            StandardTpSvn = Stamp(raw),
        };

        IReadOnlyList<FirmwareSlotFactViewModel> facts = UiCompositionRunner.GetFirmwareSlotFacts(inspection, text: text);

        Assert.Equal(["TP Version", "PID", "Common FW Version", "IC Count", text.EventBufferVersionLabel, "TP SVN"],
            facts.Select(static fact => fact.Label));
        FirmwareSlotFactViewModel svn = facts[^1];
        Assert.Equal(value, svn.Value);
        Assert.True(svn.IsPrimary);
        Assert.Equal(FirmwareSlotFactState.Ordinary, svn.State);
        Assert.False(svn.HasStateIcon);
        FirmwareSlotFactNote note = Assert.IsType<FirmwareSlotFactNote>(svn.Note);
        FirmwareSlotFactNoteRow[] expectedRows =
            [new("Flags:", flags), new("Revision:", revision, revision != "not BCD")];
        FirmwareSlotFactNoteWarning[] expectedWarnings = warning is null ? [] : [new(warning)];
        Assert.Equal(expectedRows, note.Rows);
        Assert.Equal(expectedWarnings, note.Warnings);
        Assert.Equal(warning is null ? FirmwareSlotFactNoteKind.Info : FirmwareSlotFactNoteKind.Warning, note.Kind);
        Assert.Equal(warning is null ? FirmwareSlotFactViewModel.InfoIconPathData : FirmwareSlotFactViewModel.WarningIconPathData,
            svn.NoteIconPathData);
        Assert.Contains($"Flags: {flags}.", svn.StateAutomationText, StringComparison.Ordinal);
        Assert.Contains($"Revision: {revision}.", svn.StateAutomationText, StringComparison.Ordinal);
    }

    /// <summary>Only the two warnings carry owner zh-TW text; the tooltip terms stay verbatim.</summary>
    [Fact]
    public void ChineseWarningsUseOwnerTextAndKeepTooltipTerms()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        FirmwareSlotFactNote undefined = UiCompositionRunner.CreateTpSvnFact(Stamp([0xC3, 0x80, 0x13, 0x30]), text).Note!;
        FirmwareSlotFactNote unstamped = UiCompositionRunner.CreateTpSvnFact(Stamp([0, 0, 0, 0]), text).Note!;

        Assert.Equal("旗標位元組含未定義的位元 0x03。InsertPID.py 疑似有問題。", Assert.Single(undefined.Warnings).Text);
        Assert.Equal("沒有 SVN stamp。InsertPID.py 可能沒有執行。", Assert.Single(unstamped.Warnings).Text);
        Assert.Equal(["Flags:", "Revision:"], undefined.Rows.Select(static row => row.Label));
        Assert.Equal("none", unstamped.Rows[0].Value);
    }

    /// <summary>
    /// Review F-1: a parsed stamp is shown whenever the observation exists, even when FWConfig is missing or its
    /// version bar is invalid; the other facts keep their existing FWConfig conditions and readiness is unchanged.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TpSvnIsShownIndependentlyOfFwConfig(bool isBase, bool invalidVersionBar)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        FirmwareConfigMetadataSnapshot? metadata = invalidVersionBar
            ? Metadata with { FirmwareVersionBar = 0x00, IsFirmwareVersionBarValid = false }
            : null;
        var inspection = new FirmwareInspectionSnapshot(null, metadata, null, null, null, null)
        {
            StandardTpSvn = Stamp([0x00, 0x00, 0x00, 0x00]),
        };
        var slot = new FirmwareSlotViewModel(isBase ? "replace-base" : "tp", isBase ? "Base" : "TP BIN", "input",
            isBase ? FirmwareSlotKind.Base : FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\input.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");

        FirmwareInspectionProjection.ApplyFirmwareFacts(slot, inspection, text);

        string[] expected = isBase && invalidVersionBar
            ? ["TP Version", "PID", "Common FW Version", "IC Count", "TP SVN"]
            : ["TP SVN"];
        Assert.Equal(expected, slot.FirmwareFacts.Select(static fact => fact.Label));
        FirmwareSlotFactViewModel svn = Assert.Single(slot.PrimaryFirmwareFacts, static fact => fact.Label == "TP SVN");
        Assert.Equal(FirmwareSlotFactNoteKind.Warning, svn.Note!.Kind);
        Assert.Equal(FirmwareSlotFactState.Ordinary, svn.State);
        Assert.Equal(FirmwareInputInspectionSeverity.Valid, slot.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Verified, slot.SemanticState);
    }

    /// <summary>An AB Reference shows each bank code's stamp after Event Buffer Version (A)/(B).</summary>
    [Fact]
    public void AbReferenceShowsBothBankStampsAfterEventBuffer()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        CtrlRamBaseBankInspection[] banks =
        [
            new("a-bank", new ByteRange(0, 0x40000), Metadata, new(CompiledInputVersionKind.TpA, 0x80, 0),
                null, 0x84, [], Stamp([0xC0, 0x19, 0x49, 0x14])),
            new("b-bank", new ByteRange(0x40000, 0x40000), Metadata, new(CompiledInputVersionKind.TpB, 0x80, 0),
                null, 0x84, [], Stamp([0x00, 0x00, 0x00, 0x00])),
        ];
        var inspection = new FirmwareInspectionSnapshot(null, null, null, null, null, null)
        {
            CtrlRamBaseInspection = new(CtrlRamBaseKind.AbFlash, new AbCtrlRamDraftState(AbCtrlRamBankSelection.A),
                banks, [], new ResolutionToken("test-current-publication"), FileStamp.FromBytes([])),
        };

        IReadOnlyList<FirmwareSlotFactViewModel> facts = UiCompositionRunner.GetFirmwareSlotFacts(inspection, true, text);

        Assert.Equal(
            [$"{text.EventBufferVersionLabel} (A)", $"{text.EventBufferVersionLabel} (B)", "TP SVN (A)", "TP SVN (B)"],
            facts.Where(static fact => fact.IsPrimary).TakeLast(4).Select(static fact => fact.Label));
        Assert.Equal(FirmwareSlotFactNoteKind.Info, facts.Single(static fact => fact.Label == "TP SVN (A)").Note!.Kind);
        Assert.Equal(FirmwareSlotFactNoteKind.Warning, facts.Single(static fact => fact.Label == "TP SVN (B)").Note!.Kind);
        Assert.DoesNotContain(facts, static fact => fact.Label == "TP SVN");
    }

    /// <summary>AB Merge TP A/TP B inputs are separate single-TP cards, each with its own TP SVN.</summary>
    [Fact]
    public void AbMergeTpCardShowsItsOwnStampAfterEventBuffer()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var slot = new FirmwareSlotViewModel("tp-b-input", "TP B BIN", "TP B input", FirmwareSlotKind.Tp);
        var inspection = new FirmwareInspectionSnapshot(null, Metadata, null, null, null, null)
        {
            AbMergeFacts = new AbMergeInputFacts(CompositionAddressSpaceIds.TpBInput,
                [new(CompiledInputVersionKind.TpB, 0x80, 0)])
            {
                TpSvn = Stamp([0xC0, 0x19, 0x49, 0x14]),
            },
            AbCommonEventBufferFormatVersion = 0x84,
        };

        IReadOnlyList<FirmwareSlotFactViewModel> facts = FirmwareInspectionProjection.GetFirmwareFacts(slot, inspection, text);

        Assert.Equal(["TPB Version", "PID", "Common FW Version", "IC Count", text.EventBufferVersionLabel, "TP SVN"],
            facts.Select(static fact => fact.Label));
    }

    /// <summary>A value note never changes a non-ordinary fact such as the TP Version warning.</summary>
    [Fact]
    public void ValueNoteIsLimitedToOrdinaryFacts()
    {
        var note = new FirmwareSlotFactNote([new("Flags:", "none")], []);

        _ = Assert.Throws<ArgumentException>(() => new FirmwareSlotFactViewModel(
            "TP Version", "T80-00", FirmwareSlotFactState.Warning, "Warning", "Invalid version bar")
        {
            Note = note,
        });
    }

    private static TpSvnObservation Stamp(byte[] raw)
    {
        return new TpSvnObservation("tp-svn", new FirmwareAddressedRange("flash", new ByteRange(0xA024, 4)), raw);
    }
}
