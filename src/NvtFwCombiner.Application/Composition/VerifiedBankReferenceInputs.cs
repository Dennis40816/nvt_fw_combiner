using System.Security.Cryptography;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Shared exact Reference verification for authoring inspection and execution.</summary>
internal static class VerifiedBankReferenceInputs
{
    internal static InputDiagnosticEvidence? ProjectDiagnostic(CompiledReferenceBank bank,
        InputDiagnosticEvidence? evidence, long referenceLength)
    {
        return evidence is null || evidence.AddressSpaceId != bank.LocalComposition.Plan.OutputInitialization.ReferenceSpaceId
            ? evidence
            : new InputDiagnosticEvidence(evidence.AddressSpaceId,
                evidence.ActualLength.HasValue ? referenceLength : null,
                evidence.RequiredEndExclusive is { } end ? checked(bank.OutputRange.Start + end) : null,
                evidence.SourceRange is { } range ? new ByteRange(checked(bank.OutputRange.Start + range.Start), range.Length) : null,
                evidence.RepeatedByte);
    }

    internal static CompositionIssue? ProjectIssue(CompiledReferenceBank bank, CompositionIssue? issue)
    {
        return issue is null ? null : new CompositionIssue(issue.Code, $"{bank.BankId}: {issue.Message}",
            $"{bank.BankId}/{issue.OperationId}", issue.Severity);
    }

    internal static void Add(CompiledComposition compilation, Dictionary<string, byte[]> inputs, List<CompositionIssue> issues)
    {
        if (compilation.V2Details.Provenance.Context is not RuntimeReferenceBankReplaceV2CompilationContext context)
        {
            return;
        }

        if (!inputs.TryGetValue(context.Reference.ArtifactId, out byte[]? reference) ||
            reference.LongLength != context.Reference.LengthBytes || Hash(reference) != context.Reference.Sha256 ||
            context.Banks.Any(bank => inputs.ContainsKey(bank.Reference.ArtifactId)))
        {
            issues.Add(new CompositionIssue("input.bank-reference.identity-mismatch",
                "AB Replace requires the exact captured Reference and forbids supplied private bank inputs.", context.Reference.ArtifactId));
            return;
        }

        foreach (CompiledReferenceBank bank in context.Banks)
        {
            byte[] slice = reference.AsSpan(checked((int)bank.OutputRange.Start), checked((int)bank.OutputRange.Length)).ToArray();
            if (slice.LongLength != bank.Reference.LengthBytes || Hash(slice) != bank.Reference.Sha256)
            {
                issues.Add(new CompositionIssue("input.bank-reference.slice-mismatch",
                    $"Captured Reference slice no longer matches {bank.BankId}'s local compilation.", context.Reference.ArtifactId));
                return;
            }

            inputs.Add(bank.Reference.ArtifactId, slice);
        }
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
