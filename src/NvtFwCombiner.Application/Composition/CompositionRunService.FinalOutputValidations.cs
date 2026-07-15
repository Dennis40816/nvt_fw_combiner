using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static List<FinalOutputValidationEvaluation> EvaluateFinalOutput(
        CompiledComposition compiledComposition,
        ReadOnlyMemory<byte> outputBytes)
    {
        var evaluations = new List<FinalOutputValidationEvaluation>();
        foreach (CompiledValidationRequirement requirement in compiledComposition.ValidationRequirements.Where(
                     static requirement => requirement.Stage == CompiledValidationStage.FinalOutput))
        {
            CompositionIssue? issue = requirement switch
            {
                CompiledFirmwareConfigBackupVersionValidation firmwareConfig =>
                    ValidateFirmwareConfigBackupVersion(outputBytes.Span, firmwareConfig),
                _ => new CompositionIssue(
                    requirement.IssueCode,
                    $"Final-output validation rule '{requirement.RuleId}' has no executable runtime evaluator.",
                    requirement.RuleId,
                    CompositionIssueSeverity.Error),
            };
            if (issue is not null)
            {
                evaluations.Add(new FinalOutputValidationEvaluation(
                    new ValidationRunSummary(
                        requirement.RuleId,
                        requirement.Stage,
                        ValidationRunStatus.Failed,
                        requirement.Severity,
                        issue.Code),
                    issue));
                continue;
            }

            evaluations.Add(new FinalOutputValidationEvaluation(
                new ValidationRunSummary(
                    requirement.RuleId,
                    requirement.Stage,
                    ValidationRunStatus.Passed,
                    requirement.Severity,
                    requirement.IssueCode),
                Issue: null));
        }

        return evaluations;
    }

    private static List<FinalOutputValidationEvaluation> CreateSkippedFinalOutputValidations(
        CompiledComposition compiledComposition)
    {
        return [
            .. compiledComposition.ValidationRequirements
                .Where(static requirement => requirement.Stage == CompiledValidationStage.FinalOutput)
                .Select(static requirement => new FinalOutputValidationEvaluation(
                    new ValidationRunSummary(
                        requirement.RuleId,
                        requirement.Stage,
                        ValidationRunStatus.Skipped,
                        requirement.Severity,
                        requirement.IssueCode),
                    Issue: null)),
        ];
    }

    private static CompositionIssue? ValidateFirmwareConfigBackupVersion(
        ReadOnlySpan<byte> outputBytes,
        CompiledFirmwareConfigBackupVersionValidation requirement)
    {
        string severity = ToIssueSeverity(requirement.Severity);
        return !FirmwareConfigMetadataReader.TryReadBackup(outputBytes, out FirmwareConfigMetadata backupMetadata) ||
            !backupMetadata.IsFirmwareVersionBarValid
            ? new CompositionIssue(
                requirement.InvalidIssueCode,
                "Final output has no valid TP FW version metadata in the canonical NVT FWConfig Backup.",
                requirement.RuleId,
                severity)
            : backupMetadata.FirmwareVersion != requirement.FirmwareVersion ||
              backupMetadata.FirmwareSubVersion != requirement.FirmwareSubVersion
                ? new CompositionIssue(
                    requirement.IssueCode,
                    "Final output NVT FWConfig Backup version does not match the compiled TP FW version edit.",
                    requirement.RuleId,
                    severity)
                : null;
    }

    private static string ToIssueSeverity(CompiledValidationSeverity severity)
    {
        return severity switch
        {
            CompiledValidationSeverity.Info => CompositionIssueSeverity.Info,
            CompiledValidationSeverity.Warning => CompositionIssueSeverity.Warning,
            CompiledValidationSeverity.Error => CompositionIssueSeverity.Error,
            _ => throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "Unknown compiled validation severity."),
        };
    }

    private sealed record FinalOutputValidationEvaluation(
        ValidationRunSummary Summary,
        CompositionIssue? Issue)
    {
        internal bool BlocksPublication =>
            Summary.Status == ValidationRunStatus.Failed &&
            Summary.Severity == CompiledValidationSeverity.Error;
    }
}
