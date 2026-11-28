// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Independent confirmation surface. The host supplies one model per opening; no workflow is invoked here.</summary>
public sealed partial class RecalculationConfirmModal : UserControl
{
    internal static readonly StyledProperty<RecalculationConfirmModel?> ModelProperty =
        AvaloniaProperty.Register<RecalculationConfirmModal, RecalculationConfirmModel?>(nameof(Model));

    /// <summary>Initializes shared styles, focus containment and command-based dismissal.</summary>
    public RecalculationConfirmModal()
    {
        InitializeComponent();
        ModalInitialFocus.Register(this, InitialFocusTarget);
        DetachedFromVisualTree += (_, _) => Model?.CancelCommand.Execute(null);
        PropertyChanged += (_, change) =>
        {
            if (change.Property == IsVisibleProperty && !IsVisible) { Model?.CancelCommand.Execute(null); }
            else if (change.Property == ModelProperty) { Release(change.GetOldValue<RecalculationConfirmModel?>()); }
        };
    }

    internal RecalculationConfirmModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    // A model that leaves while pending still gets its one outcome, and its successor gets its own initial focus.
    // A model set back before the posted action runs counts as never having left.
    // Posted: the visibility binding moves to the successor after this handler, so cancelling now would hide it too.
    // A host callback may clear Model only while Model is still that callback's own model.
    private void Release(RecalculationConfirmModel? previous)
    {
        if (previous is not { IsPending: true }) { return; }
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(previous, Model)) { previous.CancelCommand.Execute(null); }
            FocusPendingModel();
        }, DispatcherPriority.Input);
    }

    private void FocusPendingModel()
    {
        if (IsEffectivelyVisible && Model is { IsPending: true }) { _ = InitialFocusTarget().Focus(NavigationMethod.Tab); }
    }

    private Control InitialFocusTarget()
    {
        return Model is not { CanRecalculate: true } model ? NoRuleCancelButton
            : model.PreselectedChoice == RecalculationConfirmChoice.Recalculate ? RecalculateButton : SkipButton;
    }

    private void OnModalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) { return; }
        Model?.CancelCommand.Execute(null);
        e.Handled = true;
    }
}
