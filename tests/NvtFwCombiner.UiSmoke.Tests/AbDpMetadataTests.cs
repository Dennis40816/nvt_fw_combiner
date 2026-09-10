using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>AB slot facts retain typed values without concatenating bank metadata.</summary>
public sealed class AbDpMetadataTests
{
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

    /// <summary>Splitting DP facts does not relabel or reformat touch versions.</summary>
    [Fact]
    public void TouchVersionFactsRemainUnchanged()
    {
        FirmwareSlotViewModel slot = CreateSlot(ShellLanguage.English,
            new(CompiledInputVersionKind.TpA, 0x81, 0),
            new(CompiledInputVersionKind.TpB, 0x82, 3));
        Assert.Equal(["TPA", "TPB"], slot.FirmwareFacts.Select(static fact => fact.Label));
        Assert.Equal(["T81-00", "T82-03"], slot.FirmwareFacts.Select(static fact => fact.Value));
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
        FirmwareInspectionProjection.ApplyAbInputFacts(slot,
            new FirmwareInspectionSnapshot(null, null, null, null, null, null)
            { AbMergeFacts = new(CompositionAddressSpaceIds.DpAbInput, versions) }, text);
        return slot;
    }
}
