using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>AB slot facts retain typed values without concatenating bank metadata.</summary>
public sealed class AbDpMetadataTests
{
    /// <summary>A DP role does not surface TP identity facts from the same flash input.</summary>
    [Fact]
    public void DpSlotDoesNotLeakTouchIdentityFromFlashInspection()
    {
        var slot = new FirmwareSlotViewModel(CompositionAddressSpaceIds.DpAbInput,
            "DP AB", "DP input", FirmwareSlotKind.Dp);
        var inspection = new FirmwareInspectionSnapshot(null,
            new(0x22000, "2.0.0", 0x81, 0, true, 0, 1, 0x570A, null, default),
            null, null, null, null)
        {
            AbMergeFacts = new(CompositionAddressSpaceIds.DpAbInput,
                [new(CompiledInputVersionKind.DpA, 6, 0, 4095), new(CompiledInputVersionKind.DpB, 9, 1, 607)]),
            StandardEventBufferFormatVersion = 0x80,
            AbCommonEventBufferFormatVersion = 0xA3,
        };

        FirmwareInspectionProjection.ApplyFirmwareFacts(slot, inspection, ShellTextResources.For(ShellLanguage.English));

        Assert.Equal(["DP1 Version", "DP1 Jira Index", "DP2 Version", "DP2 Jira Index"],
            slot.FirmwareFacts.Select(static fact => fact.Label));
        Assert.False(slot.HasAdditionalFirmwareFacts);
    }

    /// <summary>Each bank exposes independent version and optional tracker in reading order.</summary>
    [Fact]
    public void KnownBanksExposeFourExactIndependentFacts()
    {
        FirmwareSlotViewModel slot = CreateSlot(ShellLanguage.English,
            new(CompiledInputVersionKind.DpA, 6, 0, 4095),
            new(CompiledInputVersionKind.DpB, 9, 1, 607));

        Assert.Equal(["DP1 Version", "DP1 Jira Index", "DP2 Version", "DP2 Jira Index"],
            slot.FirmwareFacts.Select(static fact => fact.Label));
        Assert.Equal(["D06-00", "AUTO_PRJ-4095", "D09-01", "AUTO_PRJ-607"],
            slot.FirmwareFacts.Select(static fact => fact.Value));
        Assert.Equal(4, slot.PrimaryFirmwareFacts.Count);
        Assert.False(slot.HasAdditionalFirmwareFacts);
        Assert.All(slot.FirmwareFacts, static fact =>
        {
            Assert.DoesNotContain(" · ", fact.Value, StringComparison.Ordinal);
            Assert.Equal($"{fact.Label}: {fact.Value}", fact.StateAutomationText);
        });
    }

    /// <summary>Absent or zero Jira never creates a placeholder or displaces bank order.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    [InlineData(null, 607)]
    [InlineData(4095, 0)]
    public void MissingTrackersAreOmitted(int? first, int? second)
    {
        FirmwareSlotViewModel slot = CreateSlot(ShellLanguage.English,
            new(CompiledInputVersionKind.DpA, 6, 0, (ushort?)first),
            new(CompiledInputVersionKind.DpB, 9, 1, (ushort?)second));

        string[] labels = ["DP1 Version", .. first > 0 ? new[] { "DP1 Jira Index" } : [],
            "DP2 Version", .. second > 0 ? new[] { "DP2 Jira Index" } : []];
        Assert.Equal(labels, slot.FirmwareFacts.Select(static fact => fact.Label));
        Assert.DoesNotContain(slot.FirmwareFacts, static fact => fact.Value == "AUTO_PRJ-0");
    }

    /// <summary>Unreadable banks retain localized help and do not invent Jira values.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownBankKeepsLocalizedVersionAndKnownBank(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        ShellTextResources text = ShellTextResources.For(language);
        FirmwareSlotViewModel slot = CreateSlot(language,
            new(CompiledInputVersionKind.DpA, null, null),
            new(CompiledInputVersionKind.DpB, 9, 1, 607));

        Assert.Equal(["DP1 Version", "DP2 Version", "DP2 Jira Index"],
            slot.FirmwareFacts.Select(static fact => fact.Label));
        FirmwareSlotFactViewModel unknown = slot.FirmwareFacts[0];
        Assert.True(unknown.IsUnknown);
        Assert.Equal(text.FirmwareSlotUnknownValueLabel, unknown.Value);
        Assert.Equal(text.FirmwareSlotUnknownFactDetail, unknown.StateDetail);
        Assert.Contains(text.FirmwareSlotUnknownFactDetail, unknown.StateAutomationText, StringComparison.Ordinal);
    }

    /// <summary>Both bank labels identify versions while preserving their decoded values.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TouchVersionLabelsExplicitlyNameVersions(bool chinese)
    {
        FirmwareSlotViewModel slot = CreateSlot(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English,
            new(CompiledInputVersionKind.TpA, 0x81, 0),
            new(CompiledInputVersionKind.TpB, 0x82, 3));
        Assert.Equal(["TPA Version", "TPB Version"], slot.FirmwareFacts.Select(static fact => fact.Label));
        Assert.Equal(["T81-00", "T82-03"], slot.FirmwareFacts.Select(static fact => fact.Value));
    }

    /// <summary>A read-only common TP byte fills an absent AB format fact without changing admitted format display.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbTouchSlotUsesCommonEventBufferOnlyWhenAdmittedFormatIsAbsent(bool admittedFormat)
    {
        var slot = new FirmwareSlotViewModel(CompositionAddressSpaceIds.TpAInput,
            "TP A", "TP A input", FirmwareSlotKind.Tp);
        var inspection = new FirmwareInspectionSnapshot(null,
            new(0x22000, "2.0.0", 0x81, 0, true, 0, 1, 0x570A, null, default),
            null, null, null, null)
        {
            AbMergeFacts = new(CompositionAddressSpaceIds.TpAInput,
                [new(CompiledInputVersionKind.TpA, 0x81, 0)])
            {
                EventBufferFormat = admittedFormat
                    ? new(0x97, "desay", "Desay", 1, new string('a', 64), "primary",
                        new(CompositionAddressSpaceIds.TpAInput, new ByteRange(0x22200, 0x100)),
                        new FirmwareArtifactPayload(CompositionAddressSpaceIds.TpAInput, new byte[0x37000]).Identity)
                    : null,
            },
            AbCommonEventBufferFormatVersion = 0xA3,
        };
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);

        FirmwareInspectionProjection.ApplyFirmwareFacts(slot, inspection, text);

        FirmwareSlotFactViewModel fact = Assert.Single(slot.FirmwareFacts,
            candidate => candidate.Label == text.EventBufferVersionLabel);
        Assert.Equal(admittedFormat ? "Auto Desay (0x97)" : "Auto STLA v1 (0xA3)", fact.Value);
        Assert.Contains(fact, slot.PrimaryFirmwareFacts);
    }

    /// <summary>AB retains its accepted bank version and shows shared identity facts only once.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public void TouchBankVersionIsNotDuplicatedByBackupFacts(bool hasFormat, bool chinese, bool invalidVersion = false)
    {
        var slot = new FirmwareSlotViewModel(CompositionAddressSpaceIds.TpAInput,
            "TP A", "TP A input", FirmwareSlotKind.Tp);
        var inspection = new FirmwareInspectionSnapshot(null,
            new(0x22000, "2.0.0", 0x81, 0x7E, !invalidVersion, 0, 1, 0x570A, null, default),
            null, null, null, null)
        {
            AbMergeFacts = new(CompositionAddressSpaceIds.TpAInput,
                [new(CompiledInputVersionKind.TpA, invalidVersion ? null : 0x81, invalidVersion ? null : 0)])
            {
                EventBufferFormat = hasFormat
                    ? new(0x97, "desay", "Desay", 1, new string('a', 64), "primary",
                        new(CompositionAddressSpaceIds.TpAInput, new ByteRange(0x22200, 0x100)),
                        new FirmwareArtifactPayload(CompositionAddressSpaceIds.TpAInput, new byte[0x37000]).Identity)
                    : null,
            },
        };
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        FirmwareInspectionProjection.ApplyFirmwareFacts(slot, inspection, text);
        Assert.Equal(["TPA Version", "PID", "Common FW Version"],
            slot.FirmwareFacts.Take(3).Select(static fact => fact.Label));
        Assert.Equal([invalidVersion ? text.FirmwareSlotUnknownValueLabel : "T81-00", "0x570A", "2.0.0"],
            slot.FirmwareFacts.Take(3).Select(static fact => fact.Value));
        if (hasFormat)
        {
            FirmwareSlotFactViewModel format = Assert.Single(slot.PrimaryFirmwareFacts,
                fact => fact.Label == text.EventBufferVersionLabel);
            Assert.Equal("Auto Desay (0x97)", format.Value);
        }
        _ = Assert.Single(slot.AdditionalFirmwareFacts, static fact => fact.Label == "IC Count");
        _ = Assert.Single(slot.AdditionalFirmwareFacts);
        Assert.Equal(hasFormat ? 4 : 3, slot.PrimaryFirmwareFacts.Count);
    }

    internal static FirmwareSlotViewModel CreateSlot(
        ShellLanguage language, params CompiledInputVersionObservation[] versions)
    {
        var slot = new FirmwareSlotViewModel(CompositionAddressSpaceIds.DpAbInput,
            "DP_AB BIN", "Complete two-bank DP container.", FirmwareSlotKind.Dp)
        {
            FilePath = @"C:\firmware\NT51929_initial code_TM149_2344x880_D06_20260610.bin",
        };
        ShellTextResources text = ShellTextResources.For(language);
        slot.ApplyExperienceText(text);
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        FirmwareInspectionProjection.ApplyFirmwareFacts(slot,
            new FirmwareInspectionSnapshot(null, null, null, null, null, null)
            { AbMergeFacts = new(CompositionAddressSpaceIds.DpAbInput, versions) }, text);
        return slot;
    }
}
