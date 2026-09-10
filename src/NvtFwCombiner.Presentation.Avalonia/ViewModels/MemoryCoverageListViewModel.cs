using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Display-only disclosure of already projected supporting rows; never alters rail geometry.</summary>
internal sealed partial class MemoryCoverageListViewModel : ObservableObject
{
    private const int CollapsedRowLimit = 3;
    private MemoryCoverageSegmentViewModel[] _rows = [];
    private ShellTextResources _text = ShellTextResources.For(ShellLanguage.English);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    public partial bool IsExpanded { get; set; }

    public bool HasMoreRows => _rows.Length > CollapsedRowLimit;

    public string ToggleLabel => IsExpanded
        ? _text.MemoryShowFewerRegionsLabel
        : _text.FormatMemoryShowAllRegions(_rows.Length);

    public IReadOnlyList<MemoryCoverageSegmentViewModel> VisibleRows => IsExpanded
        ? _rows
        : [.. _rows.OrderBy(static row => row.ContentRole == MemoryContentRole.Unmapped).Take(CollapsedRowLimit)];

    [RelayCommand]
    private void Toggle()
    {
        IsExpanded = !IsExpanded;
    }

    public void Update(IEnumerable<MemoryCoverageSegmentViewModel> rows, ShellTextResources text, bool resetExpansion = true)
    {
        _rows = [.. rows.Where(static row => row.IsPrimaryContent)
            .OrderBy(static row => row.RangeStart ?? long.MaxValue)];
        _text = text;
        if (resetExpansion || !HasMoreRows)
        {
            IsExpanded = false;
        }
        OnPropertyChanged(nameof(VisibleRows));
        OnPropertyChanged(nameof(HasMoreRows));
        OnPropertyChanged(nameof(ToggleLabel));
    }
}
