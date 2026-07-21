using Avalonia.Controls;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms switching IC Number when readable FWConfig contradicts the selected plan.</summary>
public sealed partial class FirmwareNumberMismatchModal : UserControl
{
    /// <summary>Initializes the Number mismatch confirmation surface.</summary>
    public FirmwareNumberMismatchModal()
    {
        InitializeComponent();
    }
}
