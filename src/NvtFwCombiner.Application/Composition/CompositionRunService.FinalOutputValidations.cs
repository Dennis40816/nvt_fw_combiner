using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static List<FinalOutputValidationEvaluation> EvaluateFinalOutput(
        CompiledComposition compiledComposition,
        IReadOnlyDictionary<string, byte[]> inputBytes,
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
                CompiledFirmwareConfigBackupPlacementAuthorityValidation authority =>
                    ValidateFirmwareConfigBackupPlacementAuthority(
                        inputBytes,
                        outputBytes.Span,
                        authority),
                CompiledFirmwareConfigBackupExpectedAddressValidation expected =>
                    ValidateFirmwareConfigBackupExpectedAddress(outputBytes.Span, expected),
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

    private static CompositionIssue? ValidateFirmwareConfigBackupPlacementAuthority(
        IReadOnlyDictionary<string, byte[]> inputBytes,
        ReadOnlySpan<byte> outputBytes,
        CompiledFirmwareConfigBackupPlacementAuthorityValidation requirement)
    {
        if (!FirmwareConfigMetadataReader.TryReadBackup(
                outputBytes,
                out FirmwareConfigMetadata backupMetadata,
                out int markerCount))
        {
            return new CompositionIssue(
                requirement.IssueCode,
                $"Expected exactly one NVT marker (00 4E 56 54), but found {markerCount}.",
                requirement.RuleId,
                CompositionIssueSeverity.Error);
        }

        var backupEnvelope = new ByteRange(
            backupMetadata.StructureStart,
            requirement.BackupLength);
        if (!requirement.AuthorityRange.Contains(backupEnvelope))
        {
            return new CompositionIssue(
                requirement.IssueCode,
                $"Final output FWConfig Backup at 0x{backupMetadata.StructureStart:X} is outside compiled postbuild authority [0x{requirement.AuthorityRange.Start:X},0x{requirement.AuthorityRange.EndExclusive:X}).",
                requirement.RuleId,
                CompositionIssueSeverity.Error);
        }

        if (!inputBytes.TryGetValue(
                requirement.ReferenceAddressSpaceId,
                out byte[]? referenceBytes) ||
            requirement.AuthorityRange.EndExclusive > referenceBytes.LongLength ||
            requirement.AuthorityRange.EndExclusive > outputBytes.Length)
        {
            return new CompositionIssue(
                requirement.IssueCode,
                "Masked DiffDLM preservation audit cannot read the complete immutable Reference authority.",
                requirement.RuleId,
                CompositionIssueSeverity.Error);
        }

        if (!FirmwareConfigMetadataReader.TryReadBackup(
                referenceBytes,
                out FirmwareConfigMetadata referenceBackupMetadata,
                out int referenceMarkerCount))
        {
            return new CompositionIssue(
                requirement.IssueCode,
                $"Expected exactly one NVT marker (00 4E 56 54), but found {referenceMarkerCount} in the immutable Reference.",
                requirement.RuleId,
                CompositionIssueSeverity.Error);
        }

        var referenceBackupEnvelope = new ByteRange(
            referenceBackupMetadata.StructureStart,
            requirement.BackupLength);
        ByteRange[] permittedBackupMutations =
        [
            backupEnvelope,
            referenceBackupEnvelope,
        ];
        for (long offset = requirement.AuthorityRange.Start;
             offset < requirement.AuthorityRange.EndExclusive;
             offset++)
        {
            if (permittedBackupMutations.Any(range => range.Contains(offset)))
            {
                continue;
            }

            int index = checked((int)offset);
            if (referenceBytes[index] != outputBytes[index])
            {
                return new CompositionIssue(
                    requirement.InactiveMutationIssueCode,
                    $"Postbuild changed inactive masked DiffDLM byte 0x{offset:X} outside the permitted FWConfig Backup envelopes.",
                    requirement.RuleId,
                    CompositionIssueSeverity.Error);
            }
        }

        return null;
    }

    private static CompositionIssue? ValidateFirmwareConfigBackupExpectedAddress(
        ReadOnlySpan<byte> outputBytes,
        CompiledFirmwareConfigBackupExpectedAddressValidation requirement)
    {
        return !FirmwareConfigMetadataReader.TryReadBackup(
                   outputBytes,
                   out FirmwareConfigMetadata backupMetadata) ||
               backupMetadata.StructureStart == requirement.ExpectedStart
            ? null
            : new CompositionIssue(
                requirement.IssueCode,
                $"Postbuild placed FWConfig Backup at 0x{backupMetadata.StructureStart:X}; IC Count predicts 0x{requirement.ExpectedStart:X}. The actual address remains inside compiled authority.",
                requirement.RuleId,
                CompositionIssueSeverity.Warning);
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
