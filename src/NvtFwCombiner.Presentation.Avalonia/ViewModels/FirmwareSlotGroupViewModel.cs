using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Collapsible firmware slot group for repeated region families.</summary>
public sealed partial class FirmwareSlotGroupViewModel : ObservableObject
{
    /// <summary>Creates a firmware slot group.</summary>
    public FirmwareSlotGroupViewModel(
        string title,
        string summary,
        IEnumerable<FirmwareSlotViewModel> slots,
        bool isExpanded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(slots);

        Title = title;
        Summary = summary;
        Slots = [.. slots];
        IsExpanded = isExpanded;
        foreach (FirmwareSlotViewModel slot in Slots)
        {
            slot.PropertyChanged += SlotPropertyChanged;
        }
    }

    /// <summary>Group label shown in the expander header.</summary>
    public string Title { get; }

    /// <summary>Plain-language group summary.</summary>
    public string Summary { get; }

    /// <summary>Slots inside this group.</summary>
    public ObservableCollection<FirmwareSlotViewModel> Slots { get; }

    /// <summary>Number of slots in this group.</summary>
    public int SlotCount => Slots.Count;

    /// <summary>Number of slots that currently have a selected file.</summary>
    public int SelectedCount => Slots.Count(slot => slot.HasFile);

    /// <summary>Compact selected/total count shown in collapsed headers.</summary>
    public string CountLabel => $"{SelectedCount}/{SlotCount}";

    /// <summary>Plain-language selection summary for this group.</summary>
    public string SelectionSummary => SelectedCount == 0
        ? $"{SlotCount} areas. None selected."
        : $"{SelectedCount} selected / {SlotCount} areas.";

    /// <summary>True when the group is expanded in the UI.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private void SlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FirmwareSlotViewModel.FilePath) or nameof(FirmwareSlotViewModel.HasFile)))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(SelectionSummary));
    }
}
