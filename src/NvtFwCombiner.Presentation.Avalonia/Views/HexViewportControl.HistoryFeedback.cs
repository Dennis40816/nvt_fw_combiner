using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class HexViewportControl
{
    private const int HistoryFeedbackFrameCount = 18;

    private readonly DispatcherTimer _historyFeedbackTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(35),
    };
    private IBrush? _historyFeedbackBrush;
    private int _historyFeedbackFrame;
    private int _historyFeedbackVersion = -1;

    private void InitializeHistoryFeedback()
    {
        _historyFeedbackTimer.Tick += OnHistoryFeedbackTick;
    }

    private void DrawHistoryFeedback(DrawingContext context, HexViewportCell cell, Rect rect, bool isReference)
    {
        IBrush? brush = IsReducedMotionEnabled ? SelectedRowBrush : _historyFeedbackBrush;
        if (!isReference && brush is not null && cell.HasHistoryFeedback)
        {
            DrawRoundedRectangle(context, brush, null, rect, 3);
        }
    }

    private void StartHistoryFeedback()
    {
        if (Snapshot is not { } snapshot || snapshot.DecorationVersion == _historyFeedbackVersion)
        {
            return;
        }

        _historyFeedbackVersion = snapshot.DecorationVersion;
        RefreshHistoryFeedbackMotion();
    }

    private void RefreshHistoryFeedbackMotion()
    {
        bool hasHistoryFeedback = Snapshot is { } snapshot &&
                                  snapshot.Rows.SelectMany(row => row.Cells).Any(cell => cell.HasHistoryFeedback);
        if (!hasHistoryFeedback)
        {
            StopHistoryFeedback();
            return;
        }

        _historyFeedbackTimer.Stop();
        _historyFeedbackFrame = 0;
        _historyFeedbackBrush = null;
        if (ShouldAnimateHistoryFeedback(IsReducedMotionEnabled, hasHistoryFeedback))
        {
            EnsureThemePalette();
            UpdateHistoryFeedbackBrush();
            _historyFeedbackTimer.Start();
        }

        InvalidateVisual();
    }

    internal static bool ShouldAnimateHistoryFeedback(bool isReducedMotionEnabled, bool hasHistoryFeedback)
    {
        return hasHistoryFeedback && !isReducedMotionEnabled;
    }

    private void OnHistoryFeedbackTick(object? sender, EventArgs e)
    {
        _historyFeedbackFrame++;
        if (_historyFeedbackFrame >= HistoryFeedbackFrameCount)
        {
            StopHistoryFeedback();
            InvalidateVisual();
            return;
        }

        UpdateHistoryFeedbackBrush();
        InvalidateVisual();
    }

    private void UpdateHistoryFeedbackBrush()
    {
        double progress = (double)_historyFeedbackFrame / HistoryFeedbackFrameCount;
        _historyFeedbackBrush = HistoryFeedbackAccentBrush is ISolidColorBrush accent
            ? new SolidColorBrush(accent.Color, 0.44 * (1 - progress))
            : throw new InvalidOperationException("Hex history feedback requires a solid theme accent brush.");
    }

    private void RefreshHistoryFeedbackBrush()
    {
        if (_historyFeedbackTimer.IsEnabled)
        {
            UpdateHistoryFeedbackBrush();
        }
    }

    private void StopHistoryFeedback()
    {
        _historyFeedbackTimer.Stop();
        _historyFeedbackBrush = null;
        _historyFeedbackFrame = 0;
    }

}
