using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>The input-details preference is local, persisted, and disabled by default.</summary>
    [Fact]
    public void InputDetailsPreferenceDefaultsOffAndRoundTrips()
    {
        Assert.False(ShellPreferenceSnapshot.Default.ExpandInputDetailsByDefault);

        using var workspace = TempWorkspace.Create("nvt-fw-combiner-input-details-preference");
        string path = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var expected = new ShellPreferenceSnapshot(
            "Dark",
            "English",
            IsReducedMotionEnabled: false,
            ExpandInputDetailsByDefault: true);

        SavePreferences(path, expected);

        Assert.Equal(expected, LoadPreferences(path));
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.LoadShellPreferences(expected);
        Assert.True(viewModel.ExpandInputDetailsByDefault);
        Assert.Equal(expected, viewModel.ExportShellPreferences());
    }

    /// <summary>The default applies to newly disclosed facts without overriding a manual choice on refresh.</summary>
    [Fact]
    public void InputDetailsDefaultAppliesOnlyWhenAdditionalFactsFirstAppear()
    {
        var slot = new FirmwareSlotViewModel(
            CompositionAddressSpaceIds.TpInput,
            "TP BIN",
            "Touch firmware.",
            FirmwareSlotKind.Tp);
        FirmwareSlotFactViewModel[] facts =
        [
            new("TP Version", "T9B-00"),
            new("PID", "0x570A"),
            new("Common FW Version", "1.4.0"),
            new("Event Buffer Version", "0x97 - Desay"),
            new("IC Count", "3"),
        ];

        slot.SetFirmwareFacts(facts, expandAdditionalByDefault: true);
        Assert.True(slot.IsAdditionalFirmwareFactsExpanded);

        slot.IsAdditionalFirmwareFactsExpanded = false;
        slot.SetFirmwareFacts(facts, expandAdditionalByDefault: true);
        Assert.False(slot.IsAdditionalFirmwareFactsExpanded);

        slot.SetFirmwareFacts([]);
        slot.SetFirmwareFacts(facts, expandAdditionalByDefault: false);
        Assert.False(slot.IsAdditionalFirmwareFactsExpanded);
    }
}
