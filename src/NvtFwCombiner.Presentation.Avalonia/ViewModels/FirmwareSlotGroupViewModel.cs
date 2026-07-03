using System.Collections.ObjectModel;
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
    }

    /// <summary>Group label shown in the expander header.</summary>
    public string Title { get; }

    /// <summary>Plain-language group summary.</summary>
    public string Summary { get; }

    /// <summary>Slots inside this group.</summary>
    public ObservableCollection<FirmwareSlotViewModel> Slots { get; }

    /// <summary>Number of slots in this group.</summary>
    public int SlotCount => Slots.Count;

    /// <summary>True when the group is expanded in the UI.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}
