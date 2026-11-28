// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using static NvtFwCombiner.Presentation.Avalonia.Views.OutputLayoutPartVisuals;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>A wrapping hint row; the host supplies actual shared-input text or an empty value.</summary>
public sealed class SharedInputHintRow : UserControl
{
    /// <summary>Defines the already localized shared-input hint. Empty hides the row.</summary>
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
        SetCurrentValue(IsVisibleProperty, !string.IsNullOrWhiteSpace(Hint));
        Identify(this, "OutputLayout.SharedInputHint", $"{OutputLayoutPartsTextResources.From(Labels).SharedInput}: {Hint}");
    }
}
