using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class ReportHistoryTestStore
{
    private static readonly ILocalFileStore Files = CompositionHostServices.Create().LocalFiles;

    internal static IReadOnlyList<ReportHistorySnapshot> Load(string path)
    {
        return ReportHistoryFileStore.LoadAsync(Files, path, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    internal static void Save(string path, IEnumerable<ReportHistorySnapshot> snapshots)
    {
        ReportHistoryFileStore.SaveAsync(Files, path, snapshots, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    internal static Task SaveAsync(
        string path,
        IEnumerable<ReportHistorySnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        return ReportHistoryFileStore.SaveAsync(Files, path, snapshots, cancellationToken);
    }
}
