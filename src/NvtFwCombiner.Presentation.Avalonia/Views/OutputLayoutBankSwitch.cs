// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using static NvtFwCombiner.Presentation.Avalonia.Views.OutputLayoutPartVisuals;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>A bank viewport selector; the host alone owns availability, labels and write scope.</summary>
public sealed class OutputLayoutBankSwitch : UserControl
{
    /// <summary>Defines the published permission to view individual banks.</summary>
    public static readonly StyledProperty<bool> CanSwitchBanksProperty =
        AvaloniaProperty.Register<OutputLayoutBankSwitch, bool>(nameof(CanSwitchBanks));
    /// <summary>Defines the viewed bank, independently of any selected write banks.</summary>
    public static readonly StyledProperty<bool> IsViewingBankBProperty =
        AvaloniaProperty.Register<OutputLayoutBankSwitch, bool>(nameof(IsViewingBankB), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Defines the published A-bank label including its state.</summary>
    public static readonly StyledProperty<string> BankALabelProperty =
        AvaloniaProperty.Register<OutputLayoutBankSwitch, string>(nameof(BankALabel), string.Empty);
    /// <summary>Defines the published B-bank label including its state.</summary>
    public static readonly StyledProperty<string> BankBLabelProperty =
        AvaloniaProperty.Register<OutputLayoutBankSwitch, string>(nameof(BankBLabel), string.Empty);
    /// <summary>Defines the shell or component resource bundle.</summary>
    public static readonly StyledProperty<object?> LabelsProperty =
        AvaloniaProperty.Register<OutputLayoutBankSwitch, object?>(nameof(Labels));

    // UI thread only. Radio checked states are projections of IsViewingBankB.
    private readonly RelayCommand<bool> _select;
    private readonly Parts? _parts;

    /// <summary>Creates two accessible bank choices and the current bank subtitle.</summary>
    public OutputLayoutBankSwitch()
    {
        _select = new RelayCommand<bool>(bankB =>
        {
            if (CanSwitchBanks && IsEffectivelyEnabled) { SetCurrentValue(IsViewingBankBProperty, bankB); }
        }, _ => CanSwitchBanks && IsEffectivelyEnabled);
        var a = new RadioButton { Content = "A", Command = _select, CommandParameter = false, Classes = { "layoutPartBank" } };
        var b = new RadioButton { Content = "B", Command = _select, CommandParameter = true, Classes = { "layoutPartBank" } };
        var choices = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Children = { a, b } };
        TextBlock subtitle = Text(string.Empty, "layoutPartBody");
        var track = new Border { Child = choices, ClipToBounds = true, Classes = { "layoutPartBankTrack" } };
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { track, subtitle } };
        Resource(root, Grid.ColumnSpacingProperty, "NfcSpace12");
        Grid.SetColumn(subtitle, 1);
        _parts = new(root, a, b, subtitle);
        Content = root;
        AddHandler(KeyDownEvent, OnBankKeyDown, RoutingStrategies.Tunnel);
        Refresh();
    }

    /// <inheritdoc cref="CanSwitchBanksProperty" />
    public bool CanSwitchBanks { get => GetValue(CanSwitchBanksProperty); set => SetValue(CanSwitchBanksProperty, value); }
    /// <inheritdoc cref="IsViewingBankBProperty" />
    public bool IsViewingBankB { get => GetValue(IsViewingBankBProperty); set => SetValue(IsViewingBankBProperty, value); }
    /// <inheritdoc cref="BankALabelProperty" />
    public string BankALabel { get => GetValue(BankALabelProperty); set => SetValue(BankALabelProperty, value); }
    /// <inheritdoc cref="BankBLabelProperty" />
    public string BankBLabel { get => GetValue(BankBLabelProperty); set => SetValue(BankBLabelProperty, value); }
    /// <inheritdoc cref="LabelsProperty" />
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (_parts is not null && (change.Property == CanSwitchBanksProperty || change.Property == IsViewingBankBProperty ||
            change.Property == BankALabelProperty || change.Property == BankBLabelProperty || change.Property == LabelsProperty ||
            change.Property == IsEffectivelyEnabledProperty)) { Refresh(); }
    }

    private void Refresh()
    {
        if (_parts is not { } parts) { return; }
        parts.Root.IsVisible = CanSwitchBanks;
        parts.A.SetCurrentValue(ToggleButton.IsCheckedProperty, !IsViewingBankB);
        parts.B.SetCurrentValue(ToggleButton.IsCheckedProperty, IsViewingBankB);
        parts.Subtitle.Text = IsViewingBankB ? BankBLabel : BankALabel;
        Identify(this, "OutputLayout.BankView", OutputLayoutPartsTextResources.From(Labels).BankView);
        Identify(parts.A, "OutputLayout.Bank.A", BankALabel);
        Identify(parts.B, "OutputLayout.Bank.B", BankBLabel);
        _select.NotifyCanExecuteChanged();
    }

    private void OnBankKeyDown(object? sender, KeyEventArgs e)
    {
        if (_parts is not { } parts || !CanSwitchBanks || !IsEffectivelyEnabled) { return; }
        bool? bankB = e.Key is Key.Left or Key.Up or Key.Home ? false
            : e.Key is Key.Right or Key.Down or Key.End ? true
            : null;
        if (bankB is { } next)
        {
            _select.Execute(next);
            _ = (next ? parts.B : parts.A).Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    private sealed record Parts(Grid Root, RadioButton A, RadioButton B, TextBlock Subtitle);
}
