namespace NvtFwCombiner.Bootstrap.Tests;

internal sealed class RecordingAuthoringInspectionProgress : IProgress<AuthoringInspectionProgress>
{
    internal List<AuthoringInspectionProgress> Updates { get; } = [];

    public void Report(AuthoringInspectionProgress progress)
    {
        Updates.Add(progress);
    }
}
