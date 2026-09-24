namespace NvtFwCombiner.Domain.Composition;

/// <summary>Transfers one unchanged local validation to its selected bank's input/output view.</summary>
public sealed record CompiledBankScopedValidation : CompiledValidationRequirement
{
    internal CompiledBankScopedValidation(CompiledReferenceBank bank, CompiledValidationRequirement local)
        : base($"{bank.BankId}/{local.RuleId}", local.Stage, local.Severity, local.IssueCode)
    {
        DomainInvariant.Reject(local is not (CompiledUniformInputRangeValidation or CompiledFirmwareConfigBackupVersionValidation or
            CompiledFirmwareConfigBackupPlacementAuthorityValidation or CompiledFirmwareConfigBackupExpectedAddressValidation),
            "Unknown bank-local validation cannot be forwarded.");
        Bank = bank;
        Local = local;
    }

    /// <summary>Canonical bank and immutable local Reference binding.</summary>
    public CompiledReferenceBank Bank { get; }
    /// <summary>Unmodified parent validation, in local bank coordinates.</summary>
    public CompiledValidationRequirement Local { get; }
}
