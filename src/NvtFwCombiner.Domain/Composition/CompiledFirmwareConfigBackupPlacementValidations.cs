namespace NvtFwCombiner.Domain.Composition;

internal static partial class CompiledValidationRequirements
{
    /// <summary>Requires one canonical NVT Backup wholly inside compiled postbuild authority.</summary>
    internal static CompiledFirmwareConfigBackupPlacementAuthorityValidation
        FirmwareConfigBackupPlacementAuthority(
            string ruleId,
            string issueCode,
            string inactiveMutationIssueCode,
            string referenceAddressSpaceId,
            ByteRange authorityRange,
            long backupLength)
    {
        return new CompiledFirmwareConfigBackupPlacementAuthorityValidation(
            ruleId,
            issueCode,
            inactiveMutationIssueCode,
            referenceAddressSpaceId,
            authorityRange,
            backupLength);
    }

    /// <summary>Warns when the canonical NVT Backup is valid but not at its count-derived expected address.</summary>
    internal static CompiledFirmwareConfigBackupExpectedAddressValidation
        FirmwareConfigBackupExpectedAddress(
            string ruleId,
            string issueCode,
            long expectedStart)
    {
        return new CompiledFirmwareConfigBackupExpectedAddressValidation(
            ruleId,
            issueCode,
            expectedStart);
    }
}

/// <summary>Final-output failure contract for missing, ambiguous, or out-of-authority FWConfig Backup.</summary>
public sealed record CompiledFirmwareConfigBackupPlacementAuthorityValidation :
    CompiledValidationRequirement
{
    internal CompiledFirmwareConfigBackupPlacementAuthorityValidation(
        string ruleId,
        string issueCode,
        string inactiveMutationIssueCode,
        string referenceAddressSpaceId,
        ByteRange authorityRange,
        long backupLength)
        : base(
            ruleId,
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            issueCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backupLength);
        InactiveMutationIssueCode = RequiredValue.NotBlank(inactiveMutationIssueCode);
        ReferenceAddressSpaceId = RequiredValue.NotBlank(referenceAddressSpaceId);
        AuthorityRange = authorityRange;
        BackupLength = backupLength;
    }

    /// <summary>Issue code used when postbuild changes inactive bytes outside the original and actual Backup envelopes.</summary>
    public string InactiveMutationIssueCode { get; }

    /// <summary>Immutable reference address space used by the final preservation audit.</summary>
    public string ReferenceAddressSpaceId { get; }

    /// <summary>Count-resolved bounded authority after active DiffDLM records.</summary>
    public ByteRange AuthorityRange { get; }

    /// <summary>Complete FWConfig Backup envelope length.</summary>
    public long BackupLength { get; }
}

/// <summary>Final-output warning contract for an authorized but unexpected FWConfig Backup address.</summary>
public sealed record CompiledFirmwareConfigBackupExpectedAddressValidation :
    CompiledValidationRequirement
{
    internal CompiledFirmwareConfigBackupExpectedAddressValidation(
        string ruleId,
        string issueCode,
        long expectedStart)
        : base(
            ruleId,
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Warning,
            issueCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedStart);
        ExpectedStart = expectedStart;
    }

    /// <summary>Count-derived aligned FWConfig Backup start.</summary>
    public long ExpectedStart { get; }
}
