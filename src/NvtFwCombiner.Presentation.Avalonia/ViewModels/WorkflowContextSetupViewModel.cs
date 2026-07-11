using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Cancelable device-context draft used before a Home workflow entry is opened.</summary>
public sealed partial class WorkflowContextSetupViewModel : ObservableObject
{
    /// <summary>Gets available IC identifiers.</summary>
    public IReadOnlyList<string> IcChoices { get; } = UiCompositionRunner.GetSupportedIcIds();

    /// <summary>Gets whether this workflow requires an IC-count choice.</summary>
    [ObservableProperty]
    public partial bool IsNumberVisible { get; set; }

    /// <summary>Gets or sets the draft IC identifier.</summary>
    [ObservableProperty]
    public partial string SelectedIc { get; set; } = UiCompositionRunner.GetDefaultIcId();

    /// <summary>Gets selectable grouped IC-count choices for the draft IC.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<IcNumberChoiceViewModel> NumberChoices { get; set; } =
        UiCompositionRunner.GetNumberSelectionChoices(UiCompositionRunner.GetDefaultIcId());

    /// <summary>Gets or sets the planner token selected by the grouped choice.</summary>
    [ObservableProperty]
    public partial string SelectedNumber { get; set; } = WorkbenchIcNumberTokens.SingleChip;

    /// <summary>Gets or sets the selected grouped IC-count choice.</summary>
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

    /// <summary>Resets the independent draft from the active shell context.</summary>
    public void Configure(string icId, string number, bool showNumber)
    {
        IsNumberVisible = showNumber;
        SelectedIc = icId;
        RefreshNumberChoices(number);
    }

    partial void OnSelectedIcChanged(string value)
    {
        RefreshNumberChoices(SelectedNumber);
    }

    partial void OnSelectedNumberChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    private void RefreshNumberChoices(string preferredToken)
    {
        NumberChoices = UiCompositionRunner.GetNumberSelectionChoices(SelectedIc);
        SelectedNumber = NumberChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, preferredToken, StringComparison.Ordinal))?.Token ??
            (NumberChoices.Count > 0 ? NumberChoices[0].Token : WorkbenchIcNumberTokens.SingleChip);
        OnPropertyChanged(nameof(SelectedNumberChoice));
    }
}
