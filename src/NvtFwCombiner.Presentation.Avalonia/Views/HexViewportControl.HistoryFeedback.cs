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
        if (!isReference && _historyFeedbackBrush is not null && cell.HasHistoryFeedback)
        {
            DrawRoundedRectangle(context, _historyFeedbackBrush, null, rect, 3);
        }
    }

    private void StartHistoryFeedback()
    {
        if (Snapshot is not { } snapshot || snapshot.DecorationVersion == _historyFeedbackVersion)
        {
            return;
        }

        _historyFeedbackVersion = snapshot.DecorationVersion;
        if (!snapshot.Rows.SelectMany(row => row.Cells).Any(cell => cell.HasHistoryFeedback))
        {
            StopHistoryFeedback();
            return;
        }

        _historyFeedbackFrame = 0;
        UpdateHistoryFeedbackBrush();
        _historyFeedbackTimer.Stop();
        _historyFeedbackTimer.Start();
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
        byte alpha = (byte)Math.Round(112 * (1 - progress));
        byte red = Interpolate(96, 45, progress);
        byte green = Interpolate(165, 212, progress);
        byte blue = Interpolate(250, 191, progress);
        _historyFeedbackBrush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
    }

    private void StopHistoryFeedback()
    {
        _historyFeedbackTimer.Stop();
        _historyFeedbackBrush = null;
        _historyFeedbackFrame = 0;
    }

    private static byte Interpolate(byte start, byte end, double progress)
    {
        return (byte)Math.Round(start + ((end - start) * progress));
    }
}
