using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Collapsible firmware slot group for repeated region families.</summary>
internal sealed partial class FirmwareSlotGroupViewModel : ObservableObject
{
    private ShellTextResources _text;
    /// <summary>Creates a firmware slot group.</summary>
    public FirmwareSlotGroupViewModel(
        IEnumerable<FirmwareSlotViewModel> slots,
        bool isExpanded,
        ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(slots);

        Slots = [.. slots];
        IsExpanded = isExpanded;
        _text = text ?? throw new ArgumentNullException(nameof(text));
        foreach (FirmwareSlotViewModel slot in Slots)
        {
            slot.PropertyChanged += SlotPropertyChanged;
        }
    }

    /// <summary>Group label shown in the expander header.</summary>
    public string Title => _text.GetReplaceRegionGroupTitle(Slots[0].RegionGroup);

    public string Summary => _text.FormatReplaceSlotGroupSummary(Slots[0].RegionGroup, Slots.Count);

    public ObservableCollection<FirmwareSlotViewModel> Slots { get; private set; }

    /// <summary>Publishes whole current slot projections; never copies an evolving list of slot fields.</summary>
    internal void UpdateSlots(IEnumerable<FirmwareSlotViewModel> slots)
    {
        ObservableCollection<FirmwareSlotViewModel> next = [.. slots];
        if (Slots.SequenceEqual(next)) { return; }
        DisconnectSlots();
        Slots = next;
        foreach (FirmwareSlotViewModel slot in Slots)
        {
            slot.PropertyChanged += SlotPropertyChanged;
        }
        // Every derived binding must observe the new publication, including future group properties.
        OnPropertyChanged(string.Empty);
    }

    internal void DisconnectSlots()
    {
        foreach (FirmwareSlotViewModel slot in Slots)
        {
            slot.PropertyChanged -= SlotPropertyChanged;
        }
    }

    public int SelectedCount => Slots.Count(slot => slot.HasFile);

    /// <summary>Compact selected/total count shown in collapsed headers.</summary>
    public string CountLabel => $"{SelectedCount}/{Slots.Count}";

    public string SelectionSummary => _text.FormatAreaSelectionSummary(
        SelectedCount,
        Slots.Count);

    internal void ApplyText(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (ReferenceEquals(_text, text)) { return; }
        _text = text;
        OnPropertyChanged(string.Empty);
    }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private void SlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FirmwareSlotViewModel.FilePath) or nameof(FirmwareSlotViewModel.HasFile)))
        {
            return;
        }

        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(SelectionSummary));
    }
}
