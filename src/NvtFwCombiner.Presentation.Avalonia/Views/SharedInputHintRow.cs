// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using static NvtFwCombiner.Presentation.Avalonia.Views.OutputLayoutPartVisuals;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>A wrapping row for shared-input text, excluding the legacy generic hover instruction.</summary>
public sealed class SharedInputHintRow : UserControl
{
    /// <summary>Defines the localized hint. Empty or generic hover instructions hide the inner row.</summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<SharedInputHintRow, string?>(nameof(Hint));
    /// <summary>Defines the shell or component resource bundle.</summary>
    public static readonly StyledProperty<object?> LabelsProperty =
        AvaloniaProperty.Register<SharedInputHintRow, object?>(nameof(Labels));

    // UI thread only; the text element projects Hint and holds no workflow state.
    private readonly TextBlock _text;

    /// <summary>Creates a passive row without a pointer popup or keyboard stop.</summary>
    public SharedInputHintRow()
    {
        _text = Text(string.Empty);
        Content = _text;
        Refresh();
    }

    /// <inheritdoc cref="HintProperty" />
    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
    /// <inheritdoc cref="LabelsProperty" />
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (_text is not null && (change.Property == HintProperty || change.Property == LabelsProperty)) { Refresh(); }
    }

    private void Refresh()
    {
        _text.Text = Hint;
        _text.IsVisible = !string.IsNullOrWhiteSpace(Hint) &&
            !StringComparer.Ordinal.Equals(Hint, ShellTextResources.For(ShellLanguage.English).MemoryFocusHint) &&
            !StringComparer.Ordinal.Equals(Hint, ShellTextResources.For(ShellLanguage.ChineseTraditional).MemoryFocusHint);
        Identify(this, "OutputLayout.SharedInputHint", _text.IsVisible
            ? $"{OutputLayoutPartsTextResources.From(Labels).SharedInput}: {Hint}" : string.Empty);
    }
}
