namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One session-local report history entry that can reopen the existing report projection.</summary>
public sealed class ReportHistoryEntryViewModel
{
    /// <summary>Creates a report history entry.</summary>
    public ReportHistoryEntryViewModel(int sequence, ReportReviewViewModel report, string reportJson)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(reportJson);

        Sequence = sequence;
        Report = report;
        ReportJson = reportJson;
        SequenceLabel = $"#{sequence}";
        Title = report.Title;
        Status = report.Status;
        Context = CreateContext(report);
        Output = string.IsNullOrWhiteSpace(report.OutputFileName)
            ? "No output"
            : $"{report.OutputFileName} / {report.OutputSize} bytes";
        OutputHash = report.OutputHashLabel;
        CommandSummary = report.HasPostbuildInvocations
            ? FormatCount(report.PostbuildInvocationCount, "command")
            : "No external command";
        IssueSummary = report.HasPrimaryIssue
            ? FormatCount(report.BlockingIssueCount, "issue")
            : report.HasWarnings ? FormatCount(report.WarningCount, "warning") : "No issue";
        ArtifactPath = report.OutputArtifactPath;
        EvidenceSummary = $"{FormatCount(report.InputCount, "input")} / {FormatCount(report.OperationCount, "step")} / {FormatCount(report.MutationCount, "mutation")}";
    }

    /// <summary>Monotonic session sequence.</summary>
    public int Sequence { get; }

    /// <summary>Compact sequence label.</summary>
    public string SequenceLabel { get; }

    /// <summary>Report title.</summary>
    public string Title { get; }

    /// <summary>Run status.</summary>
    public string Status { get; }

    /// <summary>Composition context label.</summary>
    public string Context { get; }

    /// <summary>Output artifact summary.</summary>
    public string Output { get; }

    /// <summary>Short output hash label.</summary>
    public string OutputHash { get; }

    /// <summary>External processor command summary.</summary>
    public string CommandSummary { get; }

    /// <summary>Issue summary.</summary>
    public string IssueSummary { get; }

    /// <summary>Session-local artifact path.</summary>
    public string ArtifactPath { get; }

    /// <summary>Counts of report evidence sections.</summary>
    public string EvidenceSummary { get; }

    /// <summary>Report projection to reopen.</summary>
    public ReportReviewViewModel Report { get; }

    /// <summary>Original report JSON for Save report.</summary>
    public string ReportJson { get; }

    /// <summary>Exports this entry as a persistable local history snapshot.</summary>
    public ReportHistorySnapshot ToSnapshot()
    {
        return new ReportHistorySnapshot(
            Report.SourceName,
            ReportJson,
            Report.OutputArtifactPath,
            new ReportHistoryMetadataSnapshot(
                Title,
                Status,
                Context,
                Output,
                OutputHash,
                CommandSummary,
                IssueSummary,
                EvidenceSummary,
                Report.RunId,
                Report.StartedAtUtc,
                Report.IcId,
                Report.ModeId,
                Report.ExperienceId,
                Report.CompositionKind));
    }

    private static string CreateContext(ReportReviewViewModel report)
    {
        string workflow = string.IsNullOrWhiteSpace(report.CompositionKind)
            ? "Report"
            : report.CompositionKind;
        string experience = string.IsNullOrWhiteSpace(report.ExperienceId)
            ? report.SourceName
            : report.ExperienceId;
        string ic = string.IsNullOrWhiteSpace(report.IcId) ? "unknown IC" : report.IcId;
        return $"{workflow} / {experience} / {ic}";
    }

    private static string FormatCount(int count, string noun)
    {
        return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
    }
}
