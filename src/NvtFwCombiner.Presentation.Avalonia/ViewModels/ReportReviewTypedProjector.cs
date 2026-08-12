using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Projects an in-process typed report without traversing its durable JSON representation.</summary>
internal static class ReportReviewTypedProjector
{
    internal static ReportReviewViewModel Project(
        CompositionRunReport report,
        bool suppressOutput,
        string sourceName,
        string? outputArtifactPath,
        CompositionRunInspectionSnapshot? inspectionSnapshot,
        ShellLanguage language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        OutputArtifactSummary? output = suppressOutput ? null : report.Output;
        IReadOnlyList<ReportLineViewModel> inputs = ReportReviewViewModel.ProjectInputs(
            report.Inputs,
            cancellationToken);
        IReadOnlyList<ReportLineViewModel> operations = ReportReviewViewModel.ProjectOperations(
            report.Operations,
            language,
            cancellationToken);
        IReadOnlyList<ReportLineViewModel> mutations = ReportReviewViewModel.ProjectMutations(
            report.Mutations,
            cancellationToken);
        ReportReviewViewModel.OutputDifferenceProjection outputDifferences =
            ReportReviewViewModel.ProjectOutputDifferences(
                report.OutputDifferences,
                inspectionSnapshot?.OutputSpaceId ?? "reported-output",
                output?.Size ?? 0,
                language,
                cancellationToken);
        IReadOnlyList<ReportLineViewModel> issues = ReportReviewViewModel.ProjectIssues(
            report.Issues,
            cancellationToken);
        string compositionKind = report.CompositionKind.ToString();
        string startedAt = report.StartedAtUtc.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
            CultureInfo.InvariantCulture);
        string status = ReportReviewViewModel.CreateStatus(issues, language);
        cancellationToken.ThrowIfCancellationRequested();

        return new ReportReviewViewModel(
            false,
            sourceName,
            reportJsonUtf8ByteCount: null,
            report.ProfileId,
            report.IcId,
            report.ModeId,
            report.ExperienceId,
            compositionKind,
            report.RunId,
            startedAt,
            $"{report.ProfileId} ({report.IcId})",
            $"{compositionKind} / {report.ExperienceId} / " +
                $"{ReportReviewViewModel.Shorten(report.RunId, 18)} / {startedAt}",
            status,
            ReportReviewViewModel.ProjectOutput(output),
            output?.FileName ?? string.Empty,
            output?.Size ?? 0,
            output?.Committed,
            output?.Sha256 ?? string.Empty,
            outputArtifactPath ?? string.Empty,
            inspectionSnapshot,
            inputs,
            operations,
            mutations,
            outputDifferences,
            issues,
            language);
    }
}
