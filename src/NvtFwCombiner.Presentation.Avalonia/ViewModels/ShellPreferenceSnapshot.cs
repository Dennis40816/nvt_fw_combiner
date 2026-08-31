namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Serializable shell preference values that do not affect firmware execution policy.</summary>
internal sealed record ShellPreferenceSnapshot(
    string Theme,
    string Language,
    bool IsReducedMotionEnabled = false)
{
    /// <summary>Gets the fail-closed default shell preferences.</summary>
    public static ShellPreferenceSnapshot Default { get; } = new("Light", "English", false);
}
