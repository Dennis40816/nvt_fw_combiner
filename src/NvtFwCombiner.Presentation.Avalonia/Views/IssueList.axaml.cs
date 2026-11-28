// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Standalone issue list. All instance state is UI thread only.</summary>
public sealed partial class IssueList : UserControl
{
    internal static readonly StyledProperty<IssueListModel?> ModelProperty =
        AvaloniaProperty.Register<IssueList, IssueListModel?>(nameof(Model));

    /// <summary>Defines the externally bindable selected issue key.</summary>
    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<IssueList, string?>(nameof(SelectedKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the existing shell resource bundle.</summary>
    public static readonly StyledProperty<object?> LabelsProperty =
        AvaloniaProperty.Register<IssueList, object?>(nameof(Labels));

    private readonly RelayCommand<string?> _select;
    private readonly RelayCommand _clear;
    private readonly ListParts? _parts;

    /// <summary>Creates an unwired list without workflow or service subscriptions.</summary>
    public IssueList()
    {
        _select = new RelayCommand<string?>(key => SetCurrentValue(SelectedKeyProperty,
            string.Equals(SelectedKey, key, StringComparison.Ordinal) ? null : key));
        _clear = new RelayCommand(() => SetCurrentValue(SelectedKeyProperty, null));
        InitializeComponent();
        _parts = new ListParts(this.FindControl<Border>("IssueListCard")!,
            this.FindControl<TextBlock>("IssueListTitle")!,
            this.FindControl<StackPanel>("IssueListErrors")!,
            this.FindControl<StackPanel>("IssueListWarnings")!,
            this.FindControl<StackPanel>("IssueListRows")!);
        Rebuild();
    }

    internal IssueListModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Gets or sets selection; null or an unknown key shows no details.</summary>
    public string? SelectedKey
    {
        get => GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    /// <summary>Gets or sets the shell localization resource bundle.</summary>
    public object? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    private IssueListTextResources Text => new((Labels as ShellTextResources)?.Language ?? ShellLanguage.English);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (_parts is null)
        {
            return;
        }
        if (change.Property == ModelProperty || change.Property == LabelsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedKeyProperty)
        {
            RefreshSelection();
            RevealSelection();
        }
    }

    private sealed record ListParts(Border Card, TextBlock Title, StackPanel Errors, StackPanel Warnings, StackPanel Rows);
    private sealed record RowContent(IssueListItem Item, Border Details);
}
