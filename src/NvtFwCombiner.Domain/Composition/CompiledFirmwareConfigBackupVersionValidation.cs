namespace NvtFwCombiner.Domain.Composition;

/// <summary>Factory methods for closed runtime validation requirements admitted by a compiler.</summary>
internal static partial class CompiledValidationRequirements
{
    /// <summary>Requires the final canonical FWConfig Backup to contain the expected TP FW version.</summary>
    internal static CompiledFirmwareConfigBackupVersionValidation FirmwareConfigBackupVersion(
        string ruleId,
        string invalidIssueCode,
        string mismatchIssueCode,
        byte firmwareVersion,
        byte firmwareSubVersion)
    {
        return new CompiledFirmwareConfigBackupVersionValidation(
            ruleId,
            invalidIssueCode,
            mismatchIssueCode,
            firmwareVersion,
            firmwareSubVersion);
    }
}

/// <summary>
/// Requires the final output's unique NVT-located FWConfig Backup to retain a valid FW/bar pair and
/// the user-confirmed TP FW version values.
/// </summary>
public sealed record CompiledFirmwareConfigBackupVersionValidation : CompiledValidationRequirement
{
    internal CompiledFirmwareConfigBackupVersionValidation(
        string ruleId,
        string invalidIssueCode,
        string mismatchIssueCode,
        byte firmwareVersion,
        byte firmwareSubVersion)
        : base(
            ruleId,
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            mismatchIssueCode)
    {
        InvalidIssueCode = RequiredValue.NotBlank(invalidIssueCode);
        FirmwareVersion = firmwareVersion;
        FirmwareSubVersion = firmwareSubVersion;
    }

    /// <summary>Issue code emitted when the canonical Backup is missing or its FW/bar pair is invalid.</summary>
    public string InvalidIssueCode { get; }

    /// <summary>Expected final TP FW version.</summary>
    public byte FirmwareVersion { get; }

    /// <summary>Expected final TP FW sub-version.</summary>
    public byte FirmwareSubVersion { get; }
}
