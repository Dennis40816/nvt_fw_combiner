namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>View model for the production-backed firmware desktop.</summary>
internal sealed partial class MainWindowViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
}

internal enum ShellPage
{
    Home,

    Settings,

    Merge,

    Replace,

    HexEditor,
}
