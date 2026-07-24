using Avalonia.Controls;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms a committed Build and offers direct navigation to its output BIN.</summary>
public sealed partial class BuildCompletedModal : UserControl
{
    /// <summary>Initializes the successful Build confirmation.</summary>
    public BuildCompletedModal()
    {
        InitializeComponent();
    }

}
