using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class HexViewportControl
{
    private static readonly string[] ThemeBrushKeys =
    [
        "NfcTextBrush",
        "NfcTextSecondaryBrush",
        "NfcAccentStrongBrush",
        "NfcCautionTextBrush",
        "NfcReferenceInputTextBrush",
        "NfcControllerInputTextBrush",
        "NfcSuccessTextBrush",
        "NfcInfoSurfaceStrongBrush",
        "NfcAccentSurfaceSubtleBrush",
        "NfcCautionSurfaceBrush",
        "NfcWarningSurfaceBrush",
        "NfcControllerInputSurfaceBrush",
        "NfcReferenceInputSurfaceBrush",
        "NfcControllerInputTextBrush",
        "NfcReferenceInputSurfaceBrush",
        "NfcSuccessSurfaceBrush",
        "NfcWarningAccentStrongBrush",
        "NfcAccentBrush",
        "NfcCautionBorderSoftBrush",
        "NfcHexEditorAccentBrush",
        "NfcInfoTextBrush",
        "NfcSuccessAccentBrush",
        "NfcReferenceInputTextBrush",
    ];

    private IBrush[] _themeBrushes = [];
    private ThemeVariant? _resolvedTheme;
    private IPen? SelectedPenValue { get; set; }
    private IPen? ChangedPenValue { get; set; }
    private IPen? StructuralPenValue { get; set; }
    private IPen? HoverPenValue { get; set; }
    private IPen? SearchMatchPenValue { get; set; }
    private IPen? ReferenceChangedPenValue { get; set; }

    private IBrush NormalTextBrush => ThemeBrush(HexViewportBrushRole.NormalText);
    private IBrush SecondaryTextBrush => ThemeBrush(HexViewportBrushRole.SecondaryText);
    private IBrush SelectedTextBrush => ThemeBrush(HexViewportBrushRole.SelectedText);
    private IBrush ChangedTextBrush => ThemeBrush(HexViewportBrushRole.ChangedText);
    private IBrush StructuralTextBrush => ThemeBrush(HexViewportBrushRole.StructuralText);
    private IBrush ReferenceTextBrush => ThemeBrush(HexViewportBrushRole.ReferenceText);
    private IBrush SearchMatchTextBrush => ThemeBrush(HexViewportBrushRole.SearchMatchText);
    private IBrush SelectedBrush => ThemeBrush(HexViewportBrushRole.SelectedSurface);
    private IBrush SelectedRowBrush => ThemeBrush(HexViewportBrushRole.SelectedRowSurface);
    private IBrush ChangedBrush => ThemeBrush(HexViewportBrushRole.ChangedSurface);
    private IBrush ChangedRowBrush => ThemeBrush(HexViewportBrushRole.ChangedRowSurface);
    private IBrush ReferenceRowBrush => ThemeBrush(HexViewportBrushRole.ReferenceRowSurface);
    private IBrush ReferenceChangedBrush => ThemeBrush(HexViewportBrushRole.ReferenceChangedSurface);
    private IBrush ReferenceMarkerBrush => ThemeBrush(HexViewportBrushRole.ReferenceMarker);
    private IBrush StructuralLabelBrush => ThemeBrush(HexViewportBrushRole.StructuralLabelSurface);
    private IBrush SearchMatchBrush => ThemeBrush(HexViewportBrushRole.SearchMatchSurface);
    private IBrush ChangedMarkerBrush => ThemeBrush(HexViewportBrushRole.ChangedMarker);
    private IPen SelectedPen => SelectedPenValue ?? throw ThemePaletteNotResolved();
    private IPen ChangedPen => ChangedPenValue ?? throw ThemePaletteNotResolved();
    private IPen StructuralPen => StructuralPenValue ?? throw ThemePaletteNotResolved();
    private IPen HoverPen => HoverPenValue ?? throw ThemePaletteNotResolved();
    private IPen SearchMatchPen => SearchMatchPenValue ?? throw ThemePaletteNotResolved();
    private IPen ReferenceChangedPen => ReferenceChangedPenValue ?? throw ThemePaletteNotResolved();

    private void InitializeThemePalette()
    {
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs eventArgs)
    {
        _resolvedTheme = null;
        InvalidateVisual();
    }

    private void EnsureThemePalette()
    {
        ThemeVariant theme = ActualThemeVariant;
        if (_themeBrushes.Length == (int)HexViewportBrushRole.Count &&
            Equals(_resolvedTheme, theme))
        {
            return;
        }

        if (ThemeBrushKeys.Length != (int)HexViewportBrushRole.Count)
        {
            throw new InvalidOperationException("Hex viewport theme role and resource counts do not match.");
        }

        var brushes = new IBrush[ThemeBrushKeys.Length];
        for (int index = 0; index < ThemeBrushKeys.Length; index++)
        {
            string key = ThemeBrushKeys[index];
            if (!this.TryFindResource(key, theme, out object? resource) || resource is not IBrush brush)
            {
                throw new InvalidOperationException($"Hex viewport theme resource '{key}' is unavailable for {theme}.");
            }

            brushes[index] = brush;
        }

        _themeBrushes = brushes;
        SelectedPenValue = new Pen(ThemeBrush(HexViewportBrushRole.SelectedBorder), 1);
        ChangedPenValue = new Pen(ThemeBrush(HexViewportBrushRole.ChangedBorder), 1);
        StructuralPenValue = new Pen(ThemeBrush(HexViewportBrushRole.StructuralBorder), 1.5);
        HoverPenValue = new Pen(ThemeBrush(HexViewportBrushRole.HoverBorder), 1.5);
        SearchMatchPenValue = new Pen(ThemeBrush(HexViewportBrushRole.SearchMatchBorder), 1);
        ReferenceChangedPenValue = new Pen(ThemeBrush(HexViewportBrushRole.ReferenceChangedBorder), 1);
        RebuildThemeTextCaches();
        _resolvedTheme = theme;
    }

    private void RebuildThemeTextCaches()
    {
        _normalHex = CreateHexTextCache(NormalTextBrush, NormalTypeface);
        _selectedHex = CreateHexTextCache(SelectedTextBrush, StrongTypeface);
        _changedHex = CreateHexTextCache(ChangedTextBrush, StrongTypeface);
        _structuralHex = CreateHexTextCache(StructuralTextBrush, NormalTypeface);
        _referenceHex = CreateHexTextCache(ReferenceTextBrush, NormalTypeface);
        _referenceChangedHex = CreateHexTextCache(ReferenceTextBrush, StrongTypeface);
        _referenceStructuralHex = CreateHexTextCache(StructuralTextBrush, NormalTypeface);
        _searchMatchHex = CreateHexTextCache(SearchMatchTextBrush, StrongTypeface);
        _normalAscii = CreateAsciiTextCache(SecondaryTextBrush, NormalTypeface);
        _changedAscii = CreateAsciiTextCache(ChangedTextBrush, StrongTypeface);
        _referenceAscii = CreateAsciiTextCache(ReferenceTextBrush, NormalTypeface);
        _referenceChangedAscii = CreateAsciiTextCache(ReferenceTextBrush, StrongTypeface);
        _structuralAscii = CreateAsciiTextCache(StructuralTextBrush, NormalTypeface);
        _searchMatchAscii = CreateAsciiTextCache(SearchMatchTextBrush, StrongTypeface);
    }

    private IBrush ThemeBrush(HexViewportBrushRole role)
    {
        int index = (int)role;
        return index >= 0 && index < _themeBrushes.Length
            ? _themeBrushes[index]
            : throw ThemePaletteNotResolved();
    }

    private static InvalidOperationException ThemePaletteNotResolved()
    {
        return new InvalidOperationException("Hex viewport theme palette has not been resolved.");
    }

    private enum HexViewportBrushRole
    {
        NormalText,
        SecondaryText,
        SelectedText,
        ChangedText,
        StructuralText,
        ReferenceText,
        SearchMatchText,
        SelectedSurface,
        SelectedRowSurface,
        ChangedSurface,
        ChangedRowSurface,
        ReferenceRowSurface,
        ReferenceChangedSurface,
        ReferenceMarker,
        StructuralLabelSurface,
        SearchMatchSurface,
        ChangedMarker,
        SelectedBorder,
        ChangedBorder,
        StructuralBorder,
        HoverBorder,
        SearchMatchBorder,
        ReferenceChangedBorder,
        Count,
    }
}
