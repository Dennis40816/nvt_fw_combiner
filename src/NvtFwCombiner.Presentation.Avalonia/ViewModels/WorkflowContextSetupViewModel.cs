using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Cancelable device-context draft used before a Home workflow entry is opened.</summary>
internal sealed partial class WorkflowContextSetupViewModel : ObservableObject
{
    private CapabilitySelectorPublication? _selectorPublication;
    private string _selectedIc = string.Empty;

    public IReadOnlyList<string> IcChoices { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsNumberVisible { get; set; }

    public string SelectedIc
    {
        get => _selectedIc;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                OnPropertyChanged(nameof(SelectedIc));
                return;
            }

            if (!SetProperty(ref _selectedIc, value))
            {
                return;
            }

            RefreshNumberChoices(SelectedNumber);
        }
    }

    [ObservableProperty]
    public partial IReadOnlyList<IcNumberChoiceViewModel> NumberChoices { get; set; } = [];

    /// <summary>Gets or sets the planner token selected by the grouped choice.</summary>
    [ObservableProperty]
    public partial string SelectedNumber { get; set; } = IcNumberSelectionTokens.SingleChip;

    public IcNumberChoiceViewModel? SelectedNumberChoice
    {
        get => NumberChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(SelectedNumber, value.Token, StringComparison.Ordinal))
            {
                SelectedNumber = value.Token;
            }
        }
    }

    public void Configure(
        CapabilitySelectorPublication publication,
        string icId,
        string number,
        bool showNumber,
        IReadOnlyList<string>? icChoices = null)
    {
        ArgumentNullException.ThrowIfNull(publication);
        _selectorPublication = publication;
        IReadOnlyList<string> nextChoices = icChoices ?? publication.IcIds;
        if (nextChoices.Count == 0)
        {
            throw new ArgumentException("Workflow context requires at least one IC choice.", nameof(icChoices));
        }

        IcChoices = nextChoices;
        OnPropertyChanged(nameof(IcChoices));
        IsNumberVisible = showNumber;
        string selectedIc = IcChoices.Contains(icId, StringComparer.Ordinal)
            ? icId
            : IcChoices[0];
        _ = SetProperty(ref _selectedIc, selectedIc, nameof(SelectedIc));
        RefreshNumberChoices(number);
    }

    internal void Clear()
    {
        _selectorPublication = null;
        IcChoices = [];
        OnPropertyChanged(nameof(IcChoices));
        _ = SetProperty(ref _selectedIc, string.Empty, nameof(SelectedIc));
        NumberChoices = [];
        SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    partial void OnSelectedNumberChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    private void RefreshNumberChoices(string preferredToken)
    {
        NumberChoices = _selectorPublication is null || string.IsNullOrWhiteSpace(SelectedIc)
            ? []
            : UiCompositionRunner.GetNumberSelectionChoices(_selectorPublication, SelectedIc);
        SelectedNumber = NumberChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, preferredToken, StringComparison.Ordinal))?.Token ??
            (NumberChoices.Count > 0 ? NumberChoices[0].Token : IcNumberSelectionTokens.SingleChip);
        OnPropertyChanged(nameof(SelectedNumberChoice));
    }
}
