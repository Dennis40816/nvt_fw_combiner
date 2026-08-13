using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Collapsible firmware slot group for repeated region families.</summary>
internal sealed partial class FirmwareSlotGroupViewModel : ObservableObject
{
    private readonly ShellTextResources _text;
    /// <summary>Creates a firmware slot group.</summary>
    public FirmwareSlotGroupViewModel(
        string title,
        string summary,
        IEnumerable<FirmwareSlotViewModel> slots,
        bool isExpanded,
        ShellTextResources text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(slots);

        Title = title;
        Summary = summary;
        Slots = [.. slots];
        IsExpanded = isExpanded;
        _text = text ?? throw new ArgumentNullException(nameof(text));
        foreach (FirmwareSlotViewModel slot in Slots)
        {
            slot.PropertyChanged += SlotPropertyChanged;
        }
    }

    /// <summary>Group label shown in the expander header.</summary>
    public string Title { get; }

    public string Summary { get; }

    public ObservableCollection<FirmwareSlotViewModel> Slots { get; }

    public int SlotCount => Slots.Count;

    public int SelectedCount => Slots.Count(slot => slot.HasFile);

    /// <summary>Compact selected/total count shown in collapsed headers.</summary>
    public string CountLabel => $"{SelectedCount}/{SlotCount}";

    public string SelectionSummary => _text.FormatAreaSelectionSummary(
        SelectedCount,
        SlotCount);

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
