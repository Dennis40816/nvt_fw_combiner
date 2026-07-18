namespace NvtFwCombiner.PerformanceProbe;

internal sealed record PerformanceProbeReport(
    string SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    SourceEvidence Source,
    EnvironmentEvidence Environment,
    ProbeSettings Settings,
    IReadOnlyList<ReportCaseEvidence> ReportCases,
    UiBuildEvidence UiBuild,
    IReadOnlyList<string> Notes);

internal sealed record SourceEvidence(
    string Branch,
    string Commit,
    bool? IsDirty,
    string LegacyCombinerManifestSha256);

internal sealed record EnvironmentEvidence(
    string OperatingSystem,
    string Framework,
    string ProcessArchitecture,
    string Processor,
    int LogicalProcessorCount,
    bool IsServerGc,
    long PeakWorkingSetBytes);

internal sealed record ProbeSettings(
    int WarmupCount,
    int IterationCount,
    string ColdDefinition,
    int DispatcherHeartbeatIntervalMilliseconds);

internal sealed record ReportCaseEvidence(
    string Name,
    int DifferenceCount,
    int SectionCount,
    int JsonByteCount,
    string JsonSha256,
    int InitialSummaryRows,
    int InitialGroupHeaders,
    int InitialVisibleDetailRows,
    int FirstExpandedGroupRows,
    OperationEvidence SummaryReady,
    OperationEvidence FirstDetailReady);

internal sealed record UiBuildEvidence(
    string Case,
    IReadOnlyDictionary<string, string> InputSha256,
    string ExpectedOutputSha256,
    UiBuildSample Cold,
    NumericDistribution WarmTotalMilliseconds,
    NumericDistribution WarmClickToActiveMilliseconds,
    NumericDistribution WarmMaximumHeartbeatGapMilliseconds,
    NumericDistribution WarmWorkingSetDeltaBytes,
    int MinimumWarmHeartbeatCount,
    bool AllProgressNotificationsUsedDispatcherThread,
    bool AllHeartbeatsUsedDispatcherThread);

internal sealed record OperationEvidence(
    OperationSample Cold,
    NumericDistribution WarmElapsedMilliseconds,
    NumericDistribution WarmAllocatedBytes,
    NumericDistribution WarmWorkingSetDeltaBytes);

internal sealed record OperationSample(
    double ElapsedMilliseconds,
    long AllocatedBytes,
    long WorkingSetDeltaBytes);

internal sealed record UiBuildSample(
    double TotalMilliseconds,
    double ClickToActiveMilliseconds,
    double MaximumHeartbeatGapMilliseconds,
    int HeartbeatCount,
    long WorkingSetDeltaBytes,
    bool ProgressNotificationUsedDispatcherThread,
    bool HeartbeatsUsedDispatcherThread);

internal sealed record NumericDistribution(
    double Minimum,
    double Median,
    double P95,
    double Maximum,
    double Mean)
{
    internal static NumericDistribution Create(IEnumerable<double> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        double[] values = [.. source.Order()];
        return values.Length > 0
            ? new NumericDistribution(
                Round(values[0]),
                Round(Percentile(values, 0.50)),
                Round(Percentile(values, 0.95)),
                Round(values[^1]),
                Round(values.Average()))
            : throw new ArgumentException(
                "A performance distribution requires at least one sample.",
                nameof(source));
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        int index = Math.Max(0, checked((int)Math.Ceiling(sortedValues.Length * percentile) - 1));
        return sortedValues[index];
    }

    private static double Round(double value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }
}
