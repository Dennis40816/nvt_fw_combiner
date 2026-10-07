using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Launch-local appearance overrides through the shell's existing preference and propagation owner.</summary>
internal sealed class LaunchAppearanceSession
{
    private readonly MainWindowViewModel _viewModel;
    private readonly CapturePersistenceScope _persistence;
    // UI thread: retains each overridden field's original value once, including across later runtime commands.
    private AppearanceValues? _savedValues;

    internal LaunchAppearanceSession(MainWindowViewModel viewModel, CapturePersistenceScope persistence,
        Func<bool> canApply)
    {
        _viewModel = viewModel;
        _persistence = persistence;
        ApplyCommand = new RelayCommand<AppearanceValues>(Apply, values => values is not null && canApply());
        persistence.PreferenceExporter = ExportPreferences;
    }

    internal IRelayCommand<AppearanceValues> ApplyCommand { get; }

    private void Apply(AppearanceValues? values)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(values);
        AppearanceValues saved = _savedValues ?? new(null, null, null);
        _savedValues = new(
            saved.Theme ?? (values.Theme is not null ? _viewModel.SelectedTheme : null),
            saved.Language ?? (values.Language is not null ? _viewModel.SelectedLanguage : null),
            saved.ReducedMotion ?? (values.ReducedMotion.HasValue ? _viewModel.IsReducedMotionEnabled : null));
        _persistence.WithoutLocalStateWrites(() =>
        {
            if (values.Theme is not null) { _viewModel.SelectedTheme = values.Theme; }
            if (values.Language is not null) { _viewModel.SelectedLanguage = values.Language; }
            if (values.ReducedMotion.HasValue) { _viewModel.IsReducedMotionEnabled = values.ReducedMotion.Value; }
        });
    }

    private ShellPreferenceSnapshot ExportPreferences(MainWindowViewModel viewModel)
    {
        Dispatcher.UIThread.VerifyAccess();
        ShellPreferenceSnapshot current = viewModel.ExportShellPreferences();
        return current with
        {
            Theme = _savedValues?.Theme ?? current.Theme,
            Language = _savedValues?.Language ?? current.Language,
            IsReducedMotionEnabled = _savedValues?.ReducedMotion ?? current.IsReducedMotionEnabled,
        };
    }
}
