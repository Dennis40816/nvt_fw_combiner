using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    internal static bool IsPrivateBankReference(CompiledComposition compilation, string addressSpaceId)
    {
        return compilation.V2Details.Provenance.Context is RuntimeReferenceBankReplaceV2CompilationContext context &&
            context.Banks.Any(bank => bank.Reference.ArtifactId == addressSpaceId);
    }

    private static void AddBankReferenceInputs(CompiledComposition compilation, Dictionary<string, byte[]> inputs, List<CompositionIssue> issues)
    {
        VerifiedBankReferenceInputs.Add(compilation, inputs, issues);
    }

    private static Dictionary<string, byte[]> BankValidationInputs(IReadOnlyDictionary<string, byte[]> inputs, CompiledReferenceBank bank)
    {
        var local = inputs.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);
        local[bank.LocalComposition.Plan.OutputInitialization.ReferenceSpaceId!] = inputs[bank.Reference.ArtifactId];
        return local;
    }

    private static CompositionIssue? ValidateBankFinalOutput(IReadOnlyDictionary<string, byte[]> inputs,
        ReadOnlySpan<byte> output, CompiledBankScopedValidation requirement)
    {
        CompiledReferenceBank bank = requirement.Bank;
        if (bank.OutputRange.EndExclusive > output.Length || !inputs.ContainsKey(bank.Reference.ArtifactId))
        {
            return new CompositionIssue(requirement.IssueCode, $"{bank.BankId} validation cannot read its complete output and captured Reference.", requirement.RuleId);
        }

        // These three existing validators read FWConfig Backup/authority bytes, not relocated main Header addresses.
        // The final bank slice therefore has the same validation view after B-address restoration.
        ReadOnlySpan<byte> localOutput = output.Slice(checked((int)bank.OutputRange.Start), checked((int)bank.OutputRange.Length));
        // NVT-END-FLAG-1113-01: the bank-local layout declares the end flag in local coordinates.
        FirmwareNvtEndFlagResolution endFlag = bank.LocalComposition.V2Details.Provenance.ResolvedMap.NvtEndFlagResolution;
        CompositionIssue? issue = requirement.Local switch
        {
            CompiledFirmwareConfigBackupVersionValidation version => ValidateFirmwareConfigBackupVersion(localOutput, endFlag, version),
            CompiledFirmwareConfigBackupPlacementAuthorityValidation authority =>
                ValidateFirmwareConfigBackupPlacementAuthority(BankValidationInputs(inputs, bank), localOutput, endFlag, authority),
            CompiledFirmwareConfigBackupExpectedAddressValidation expected => ValidateFirmwareConfigBackupExpectedAddress(localOutput, endFlag, expected),
            _ => new CompositionIssue(requirement.IssueCode, "Unsupported bank final-output validation.", requirement.RuleId),
        };
        return issue is null ? null : new CompositionIssue(issue.Code, $"{bank.BankId}: {issue.Message}", requirement.RuleId, issue.Severity);
    }

    private static InputLoadValidationEvaluationResult EvaluateBankInputLoad(IReadOnlyDictionary<string, byte[]> inputs,
        CompiledBankScopedValidation requirement)
    {
        InputLoadValidationEvaluationResult result = requirement.Local is CompiledUniformInputRangeValidation uniform
            ? CompiledInputLoadValidationEvaluator.Evaluate(BankValidationInputs(inputs, requirement.Bank), uniform)
            : new InputLoadValidationEvaluationResult(new CompositionIssue(requirement.IssueCode,
                "Unsupported bank input-load validation.", requirement.RuleId), DiagnosticEvidence: null);
        return new InputLoadValidationEvaluationResult(VerifiedBankReferenceInputs.ProjectIssue(requirement.Bank, result.Issue),
            VerifiedBankReferenceInputs.ProjectDiagnostic(requirement.Bank, result.DiagnosticEvidence,
                inputs[requirement.Bank.LocalComposition.Plan.OutputInitialization.ReferenceSpaceId!].LongLength));
    }
}
