using NvtFwCombiner.Domain.Composition;

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
        if (compilation.V2Details.Provenance.Context is not RuntimeReferenceBankReplaceV2CompilationContext context)
        {
            return;
        }

        if (!inputs.TryGetValue(context.Reference.ArtifactId, out byte[]? reference) ||
            reference.LongLength != context.Reference.LengthBytes || ToSha256Hex(reference) != context.Reference.Sha256 ||
            context.Banks.Any(bank => inputs.ContainsKey(bank.Reference.ArtifactId)))
        {
            issues.Add(new CompositionIssue("input.bank-reference.identity-mismatch",
                "AB Replace requires the exact captured Reference and forbids supplied private bank inputs.", context.Reference.ArtifactId));
            return;
        }

        foreach (CompiledReferenceBank bank in context.Banks)
        {
            byte[] slice = reference.AsSpan(checked((int)bank.OutputRange.Start), checked((int)bank.OutputRange.Length)).ToArray();
            if (slice.LongLength != bank.Reference.LengthBytes || ToSha256Hex(slice) != bank.Reference.Sha256)
            {
                issues.Add(new CompositionIssue("input.bank-reference.slice-mismatch",
                    $"Captured Reference slice no longer matches {bank.BankId}'s local compilation.", context.Reference.ArtifactId));
                return;
            }

            inputs.Add(bank.Reference.ArtifactId, slice);
        }
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
        CompositionIssue? issue = requirement.Local switch
        {
            CompiledFirmwareConfigBackupVersionValidation version => ValidateFirmwareConfigBackupVersion(localOutput, version),
            CompiledFirmwareConfigBackupPlacementAuthorityValidation authority =>
                ValidateFirmwareConfigBackupPlacementAuthority(BankValidationInputs(inputs, bank), localOutput, authority),
            CompiledFirmwareConfigBackupExpectedAddressValidation expected => ValidateFirmwareConfigBackupExpectedAddress(localOutput, expected),
            _ => new CompositionIssue(requirement.IssueCode, "Unsupported bank final-output validation.", requirement.RuleId),
        };
        return issue is null ? null : new CompositionIssue(issue.Code, $"{bank.BankId}: {issue.Message}", requirement.RuleId, issue.Severity);
    }

    private static InputLoadValidationEvaluationResult EvaluateBankInputLoad(IReadOnlyDictionary<string, byte[]> inputs,
        CompiledBankScopedValidation requirement)
    {
        return requirement.Local is CompiledUniformInputRangeValidation uniform
            ? CompiledInputLoadValidationEvaluator.Evaluate(BankValidationInputs(inputs, requirement.Bank), uniform)
            : new InputLoadValidationEvaluationResult(new CompositionIssue(requirement.IssueCode,
                "Unsupported bank input-load validation.", requirement.RuleId), DiagnosticEvidence: null);
    }
}
