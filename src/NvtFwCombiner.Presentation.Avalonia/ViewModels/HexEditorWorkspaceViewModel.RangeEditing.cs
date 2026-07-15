using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.HexEditor;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Available raw-memory writes for the selected inclusive Hex Editor range.</summary>
public enum HexEditorRangeWriteMode
{
    /// <summary>Write the supplied byte sequence from Start without crossing End.</summary>
    Overwrite,

    /// <summary>Repeat one supplied byte across the selected range.</summary>
    Fill,
}

public sealed partial class HexEditorWorkspaceViewModel
{
    /// <summary>Selected range-write behavior in the inspector.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverwriteModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsFillModeSelected))]
    [NotifyPropertyChangedFor(nameof(CurrentWriteModeLabel))]
    [NotifyPropertyChangedFor(nameof(CurrentWriteModeTooltip))]
    [NotifyPropertyChangedFor(nameof(EditGuidance))]
    [NotifyPropertyChangedFor(nameof(EditNotice))]
    public partial HexEditorRangeWriteMode RangeWriteMode { get; set; } = HexEditorRangeWriteMode.Overwrite;

    /// <summary>Inline diagnostic for the Edit Region only; the document header remains informational.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditFeedback))]
    [NotifyPropertyChangedFor(nameof(EditNotice))]
    public partial string EditFeedback { get; set; } = string.Empty;

    /// <summary>True when Edit Region has a validation message that needs direct attention.</summary>
    public bool HasEditFeedback => !string.IsNullOrWhiteSpace(EditFeedback);

    /// <summary>True when the inspector writes an exact byte sequence.</summary>
    public bool IsOverwriteModeSelected => RangeWriteMode == HexEditorRangeWriteMode.Overwrite;

    /// <summary>Gets or sets whether the inspector repeats one byte across the range.</summary>
    public bool IsFillModeSelected
    {
        get => RangeWriteMode == HexEditorRangeWriteMode.Fill;
        set => RangeWriteMode = value
            ? HexEditorRangeWriteMode.Fill
            : HexEditorRangeWriteMode.Overwrite;
    }

    /// <summary>Localized compact label displayed beside the binary write-mode switch.</summary>
    public string CurrentWriteModeLabel => IsFillModeSelected
        ? Text.HexEditorFillModeLabel
        : Text.HexEditorOverwriteModeLabel;

    /// <summary>Localized explanation shown when hovering the write-mode switch.</summary>
    public string CurrentWriteModeTooltip => IsFillModeSelected
        ? Text.HexEditorFillModeTooltip
        : Text.HexEditorOverwriteModeTooltip;

    /// <summary>Short, always-visible rule for the active range-write mode.</summary>
    public string EditGuidance => IsFillModeSelected
        ? Text.HexEditorFillGuidance
        : Text.HexEditorOverwriteGuidance;

    /// <summary>One stable inspector notice: active-mode guidance normally, or the latest edit error.</summary>
    public string EditNotice => HasEditFeedback ? EditFeedback : EditGuidance;

    partial void OnRangeWriteModeChanged(HexEditorRangeWriteMode value)
    {
        ClearEditFeedback();
    }

    /// <summary>Selects exact sequence overwrite mode.</summary>
    public IRelayCommand SelectOverwriteModeCommand { get; }

    /// <summary>Selects one-byte fill mode.</summary>
    public IRelayCommand SelectFillModeCommand { get; }

    /// <summary>Applies the selected range-write mode to the in-memory work buffer.</summary>
    public IRelayCommand ApplyRangeEditCommand { get; }

    private void ApplyRangeEdit()
    {
        if (RangeWriteMode == HexEditorRangeWriteMode.Fill)
        {
            ApplyFillRange();
            return;
        }

        ApplyOverwriteRange();
    }

    private void ApplyRangeOperation(RawBinaryEditorOperationResult result, string selectedAddress)
    {
        if (!result.Succeeded)
        {
            EditFeedback = DescribeIssue(result.Issue!);
            return;
        }

        ClearEditFeedback();
        ApplySuccessfulOperation(result, selectedAddress);
    }

    private void ClearEditFeedback()
    {
        if (!string.IsNullOrEmpty(EditFeedback))
        {
            EditFeedback = string.Empty;
        }
    }
}
