using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class HexEditorViewportControl
{
    private const int HistoryFeedbackFrameCount = 18;

    private readonly HashSet<string> _historyFeedbackAddresses = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _historyFeedbackTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(35),
    };
    private IBrush? _historyFeedbackBrush;
    private int _historyFeedbackFrame;

    private void InitializeHistoryFeedback()
    {
        _historyFeedbackTimer.Tick += OnHistoryFeedbackTick;
    }

    private void DrawHistoryFeedback(
        DrawingContext context,
        HexEditorByteCellViewModel cell,
        Rect rect,
        bool isReference)
    {
        if (!isReference &&
            _historyFeedbackBrush is not null &&
            _historyFeedbackAddresses.Contains(cell.Address))
        {
            DrawRoundedRectangle(context, _historyFeedbackBrush, null, rect, 3);
        }
    }

    private void StartHistoryFeedback()
    {
        _historyFeedbackAddresses.Clear();
        if (_workspace is null)
        {
            StopHistoryFeedback();
            return;
        }

        _historyFeedbackAddresses.UnionWith(_workspace.HistoryFeedbackAddresses);
        if (_historyFeedbackAddresses.Count == 0)
        {
            StopHistoryFeedback();
            return;
        }

        _historyFeedbackFrame = 0;
        UpdateHistoryFeedbackBrush();
        _historyFeedbackTimer.Stop();
        _historyFeedbackTimer.Start();
        InvalidateVisual();
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
        _historyFeedbackAddresses.Clear();
        _historyFeedbackBrush = null;
        _historyFeedbackFrame = 0;
    }

    private static byte Interpolate(byte start, byte end, double progress)
    {
        return (byte)Math.Round(start + ((end - start) * progress));
    }
}
