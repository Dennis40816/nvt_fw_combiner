using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>All-bank display facts are independent of the banks selected for execution.</summary>
public sealed class CtrlRamBaseFactsTests
{
    /// <summary>Standard TP and Base format the one typed raw byte without adding a DP fact.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandardEventBufferFactUsesExistingLocalizedInfoCard(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(
            chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var metadata = new FirmwareConfigMetadataSnapshot(0x1000, "2.5.9", 0x42, 0xBD,
            true, 7, 1, 0x0927, null, default);
        foreach ((byte raw, string expected) in new[]
        {
            ((byte)0xA3, "Auto STLA v1 (0xA3)"),
            ((byte)0x00, $"{text.FirmwareSlotUnknownValueLabel} (0x00)"),
            ((byte)0x80, "Common (0x80)"),
            ((byte)0x85, "Common (0x85)"),
        })
        {
            var inspection = new FirmwareInspectionSnapshot(null, metadata, null, null, null, null)
            {
                StandardEventBufferFormatVersion = raw,
            };
            foreach (bool isBase in new[] { false, true })
            {
                IReadOnlyList<FirmwareSlotFactViewModel> facts = UiCompositionRunner.GetFirmwareSlotFacts(
                    inspection, isBase, text);
                FirmwareSlotFactViewModel format = Assert.Single(facts,
                    fact => fact.Label == text.EventBufferVersionLabel);
                Assert.Equal(expected, format.Value);
                Assert.True(format.IsPrimary);
            }
        }

        var noTpMetadata = new FirmwareInspectionSnapshot(null, null, null, null, null, null)
        {
            StandardEventBufferFormatVersion = 0xA3,
        };
        Assert.DoesNotContain(UiCompositionRunner.GetFirmwareSlotFacts(noTpMetadata),
            fact => fact.Label == text.EventBufferVersionLabel);
    }

    /// <summary>Equal typed facts share one label; unequal facts retain their bank and DP stays in Details.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbFactsRetainDistinctBanksAndShowEachMissingEventBuffer(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var a = new FirmwareConfigMetadataSnapshot(0x23000, "2.0.0", 5, 250, true, 0, 1, 0x4703, null, default);
        FirmwareConfigMetadataSnapshot b = a with
        {
            FirmwareConfigBackupStart = 0x23020,
            FirmwareVersion = 6,
            FirmwareVersionBar = 249,
            ProjectId = 0x4704,
        };
        CtrlRamBaseBankInspection[] banks =
        [
            new("a-bank", new ByteRange(0, 0x40000), a,
                new(CompiledInputVersionKind.TpA, 5, 0), new(CompiledInputVersionKind.DpA, 6, 0), null, []),
            new("b-bank", new ByteRange(0x40000, 0x40000), b,
                new(CompiledInputVersionKind.TpB, 6, 0), new(CompiledInputVersionKind.DpB, 7, 0), null, []),
        ];
        var inspection = new FirmwareInspectionSnapshot(null, null, null, null, null, null)
        {
            CtrlRamBaseInspection = new(CtrlRamBaseKind.AbFlash, new AbCtrlRamDraftState(AbCtrlRamBankSelection.A),
                banks, [], new ResolutionToken("test-current-publication"), FileStamp.FromBytes([])),
        };
        IReadOnlyList<FirmwareSlotFactViewModel> facts = UiCompositionRunner.GetFirmwareSlotFacts(inspection, true, text);
        Assert.Equal("T05-00", Assert.Single(facts, static fact => fact.Label == "TPA Version").Value);
        Assert.Equal("T06-00", Assert.Single(facts, static fact => fact.Label == "TPB Version").Value);
        Assert.Equal("0x4703", Assert.Single(facts, static fact => fact.Label == "PID (A)").Value);
        Assert.Equal("0x4704", Assert.Single(facts, static fact => fact.Label == "PID (B)").Value);
        _ = Assert.Single(facts, static fact => fact.Label == "Common FW Version (A/B)");
        Assert.False(Assert.Single(facts, static fact => fact.Label == "IC Count (A/B)").IsPrimary);
        Assert.Equal(["IC Count (A/B)", "DPA Version", "DPB Version"],
            facts.Where(static fact => !fact.IsPrimary).Take(3).Select(static fact => fact.Label));
        Assert.Equal([$"{text.EventBufferVersionLabel} (A)", $"{text.EventBufferVersionLabel} (B)"],
            facts.Where(static fact => fact.IsPrimary).TakeLast(2).Select(static fact => fact.Label));
        Assert.False(Assert.Single(facts, static fact => fact.Label == "DPA Version").IsPrimary);
        Assert.False(Assert.Single(facts, static fact => fact.Label == "DPB Version").IsPrimary);
        foreach ((string bank, string range) in new[]
        {
            ("A", chinese ? "參考映像 [0x00000,0x40000)" : "Reference [0x00000,0x40000)"),
            ("B", chinese ? "參考映像 [0x40000,0x80000)" : "Reference [0x40000,0x80000)"),
        })
        {
            string rangeLabel = chinese
                ? $"{bank} Bank 範圍"
                : $"{bank} bank range";
            string backupLabel = chinese ? $"{bank} FWConfig 備份" : $"{bank} FWConfig Backup";
            FirmwareSlotFactViewModel rangeFact = Assert.Single(facts, fact => fact.Label == rangeLabel);
            Assert.DoesNotContain(facts, fact => fact.Label == backupLabel);
            Assert.Equal(range, rangeFact.Value);
            Assert.False(rangeFact.IsPrimary);
        }
        foreach (string bankId in new[] { "A", "B" })
        {
            FirmwareSlotFactViewModel format = Assert.Single(facts,
                fact => fact.Label == $"{text.EventBufferVersionLabel} ({bankId})");
            Assert.Equal(text.FirmwareFactNotProvidedLabel, format.Value);
            Assert.True(format.IsPrimary);
        }
        Assert.DoesNotContain(facts, static fact => fact.Label == "TP Version");
    }

    /// <summary>Raw bytes, names and missing values belong to their own bank.</summary>
    [Fact]
    public void AbEventBufferFactsRetainRawByteAndMissingBank()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        foreach ((byte? aValue, byte? bValue) in new (byte?, byte?)[] { (0xA3, 0x00), (null, 0x7F) })
        {
            var inspection = new FirmwareInspectionSnapshot(null, null, null, null, null, null)
            {
                CtrlRamBaseInspection = new(CtrlRamBaseKind.AbFlash, new AbCtrlRamDraftState(AbCtrlRamBankSelection.B),
                [
                    new("a-bank", new ByteRange(0, 0x40000), null, null, null, aValue, []),
                    new("b-bank", new ByteRange(0x40000, 0x40000), null, null, null, bValue, []),
                ], [], new ResolutionToken("test-current-publication"), FileStamp.FromBytes([])),
            };
            IReadOnlyList<FirmwareSlotFactViewModel> facts = UiCompositionRunner.GetFirmwareSlotFacts(inspection, true, text);
            string a = Assert.Single(facts, fact => fact.Label == $"{text.EventBufferVersionLabel} (A)").Value;
            string b = Assert.Single(facts, fact => fact.Label == $"{text.EventBufferVersionLabel} (B)").Value;
            foreach (string bank in new[] { "A", "B" })
            {
                Assert.DoesNotContain(facts,
                    fact => fact.Label == $"{bank} FWConfig Backup");
            }
            if (aValue is null)
            {
                Assert.Equal(text.FirmwareFactNotProvidedLabel, a);
                Assert.Equal($"{text.FirmwareSlotUnknownValueLabel} (0x7F)", b);
            }
            else
            {
                Assert.Equal("Auto STLA v1 (0xA3)", a);
                Assert.Equal($"{text.FirmwareSlotUnknownValueLabel} (0x00)", b);
                Assert.DoesNotContain(text.FirmwareFactNotProvidedLabel, b, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>The shared card displays only the typed detected kind and clears it with the file.</summary>
    [Theory]
    [InlineData(CtrlRamBaseKind.StandardTp, "Standard TP Code")]
    [InlineData(CtrlRamBaseKind.StandardFlash, "Standard FlashCode")]
    [InlineData(CtrlRamBaseKind.AbFlash, "AB FlashCode")]
    [InlineData(CtrlRamBaseKind.Unknown, "")]
    public void BaseTypeComesFromInspection(CtrlRamBaseKind kind, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var slot = new FirmwareSlotViewModel("base", "Base", "Select", FirmwareSlotKind.Base) { FilePath = "arbitrary.bin" };
        slot.SetCurrentInspectionProjection(new(null, null, null, null, null, null)
        {
            CtrlRamBaseInspection = new(kind, null, [], [], new ResolutionToken("test-current-publication"), FileStamp.FromBytes([])),
        });
        Assert.Equal(expected, slot.DetectedBaseTypeLabel);
        Assert.Equal(expected.Length != 0, slot.HasDetectedBaseType);
        slot.ClearCurrentInspectionProjection();
        Assert.False(slot.HasDetectedBaseType);
    }
}
