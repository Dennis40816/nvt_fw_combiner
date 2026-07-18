using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    /// <summary>Loads a readable report model from run report JSON.</summary>
    public static ReportReviewViewModel FromJson(
        string json,
        string sourceName,
        string? outputArtifactPath = null,
        ShellLanguage language = ShellLanguage.English)
    {
        return FromJsonCore(json, sourceName, outputArtifactPath, language, CancellationToken.None);
    }

    internal static ReportReviewViewModel FromJsonCancellable(
        string json,
        string sourceName,
        string? outputArtifactPath,
        ShellLanguage language,
        CancellationToken cancellationToken)
    {
        return FromJsonCore(json, sourceName, outputArtifactPath, language, cancellationToken);
    }

    private static ReportReviewViewModel FromJsonCore(
        string json,
        string sourceName,
        string? outputArtifactPath,
        ShellLanguage language,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        cancellationToken.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string profileId = GetString(root, nameof(ProfileId));
        string icId = GetString(root, nameof(IcId));
        string modeId = GetString(root, nameof(ModeId));
        string experienceId = GetString(root, nameof(ExperienceId));
        string compositionKind = GetString(root, nameof(CompositionKind));
        string runId = GetString(root, nameof(RunId));
        string startedAt = GetString(root, nameof(StartedAtUtc));
        string outputFileName = GetOutputString(root, "FileName");
        long outputSize = GetOutputLong(root, "Size");
        bool? outputCommitted = GetOutputCommitted(root);
        string outputSha256 = GetOutputString(root, "Sha256");
        IReadOnlyList<ReportLineViewModel> inputs = ParseInputs(root, cancellationToken);
        IReadOnlyList<ReportLineViewModel> operations = ParseOperations(root, language, cancellationToken);
        IReadOnlyList<ReportLineViewModel> mutations = ParseMutations(root, cancellationToken);
        IReadOnlyList<ReportLineViewModel> outputDifferences = ParseOutputDifferences(root, language, cancellationToken);
        IReadOnlyList<ReportLineViewModel> issues = ParseIssues(root, cancellationToken);
        string status = CreateStatus(issues, language);
        cancellationToken.ThrowIfCancellationRequested();

        return new ReportReviewViewModel(
            false,
            sourceName,
            profileId,
            icId,
            modeId,
            experienceId,
            compositionKind,
            runId,
            startedAt,
            $"{profileId} ({icId})",
            $"{compositionKind} / {experienceId} / {Shorten(runId, 18)} / {startedAt}",
            status,
            ParseOutput(root),
            outputFileName,
            outputSize,
            outputCommitted,
            outputSha256,
            outputArtifactPath ?? string.Empty,
            inputs,
            operations,
            mutations,
            outputDifferences,
            issues,
            language);
    }

    /// <summary>Creates an error report when JSON parsing or loading fails.</summary>
    public static ReportReviewViewModel Error(
        string sourceName,
        string message,
        string issueTitle = "Parse error",
        string status = "Invalid JSON",
        ShellLanguage language = ShellLanguage.English)
    {
        return new ReportReviewViewModel(
            false,
            sourceName,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "Report could not be loaded",
            sourceName,
            status,
            string.Empty,
            string.Empty,
            0,
            null,
            string.Empty,
            string.Empty,
            [],
            [],
            [],
            [],
            [new ReportLineViewModel(issueTitle, message, "report-json")],
            language);
    }
}
