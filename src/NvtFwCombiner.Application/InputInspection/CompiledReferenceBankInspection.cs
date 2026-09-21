using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Version decoded by the existing local Reference reader, bound to one selected bank.</summary>
public sealed record CompiledReferenceBankObservation(
    string BankId, FirmwareArtifactIdentity Reference, CompiledInputVersionObservation Version);

internal static class CompiledReferenceBankInspection
{
    internal static CompiledInputArtifactInspectionResult Inspect(CompiledComposition composition,
        string addressSpaceId, ReadOnlyMemory<byte> bytes, CompiledInputArtifactInspectionResult inspection)
    {
        if (inspection.BlocksBuild || composition.V2Details.Provenance.Context is not RuntimeReferenceBankReplaceV2CompilationContext context)
        {
            return inspection;
        }
        Dictionary<string, byte[]>? slices = null;
        if (addressSpaceId == context.Reference.ArtifactId)
        {
            slices = new(StringComparer.Ordinal) { [addressSpaceId] = bytes.ToArray() };
            var issues = new List<CompositionIssue>();
            VerifiedBankReferenceInputs.Add(composition, slices, issues);
            if (issues.Count != 0)
            {
                return inspection with
                {
                    BlocksBuild = true,
                    Severity = CompiledInputArtifactInspectionSeverity.Blocking,
                    IssueCode = issues[0].Code,
                    AdmissionIssue = issues[0],
                    NextAction = CompiledInputArtifactInspectionNextAction.SelectCompatibleInput,
                };
            }
        }
        foreach (CompiledReferenceBank bank in context.Banks)
        {
            string localId = slices is null ? addressSpaceId : bank.LocalComposition.Plan.OutputInitialization.ReferenceSpaceId!;
            CompiledInputArtifactInspectionResult local = CompiledInputArtifactInspectionService.Inspect(
                bank.LocalComposition, localId, slices is null ? bytes : slices[bank.Reference.ArtifactId]);
            if (local.Severity > inspection.Severity || local.BlocksBuild)
            {
                inspection = inspection with
                {
                    Severity = local.Severity,
                    BlocksBuild = local.BlocksBuild,
                    IssueCode = local.IssueCode,
                    NextAction = local.NextAction,
                    DiagnosticEvidence = VerifiedBankReferenceInputs.ProjectDiagnostic(bank, local.DiagnosticEvidence, context.Reference.LengthBytes),
                    AdmissionIssue = VerifiedBankReferenceInputs.ProjectIssue(bank, local.AdmissionIssue),
                };
            }
            if (inspection.BlocksBuild)
            {
                break;
            }
        }
        return inspection;
    }

    internal static CompiledInputArtifactObservationResult Observe(CompiledComposition composition,
        ReadOnlyMemory<byte> bytes, CompiledInputArtifactInspectionResult? inspection)
    {
        if (inspection is null || inspection.BlocksBuild || bytes.IsEmpty)
        {
            return CompiledInputArtifactObservationResult.Empty;
        }
        var context = (RuntimeReferenceBankReplaceV2CompilationContext)composition.V2Details.Provenance.Context;
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal) { [context.Reference.ArtifactId] = bytes.ToArray() };
        var issues = new List<CompositionIssue>();
        VerifiedBankReferenceInputs.Add(composition, inputs, issues);
        if (issues.Count != 0)
        {
            return CompiledInputArtifactObservationResult.Empty;
        }
        var observations = new List<CompiledReferenceBankObservation>();
        foreach (CompiledReferenceBank bank in context.Banks)
        {
            byte[] slice = inputs[bank.Reference.ArtifactId];
            string id = bank.LocalComposition.Plan.OutputInitialization.ReferenceSpaceId!;
            CompiledInputArtifactInspectionResult localInspection = CompiledInputArtifactInspectionService.Inspect(bank.LocalComposition, id, slice);
            CompiledInputArtifactObservationResult local = CompiledInputArtifactObservationService.Observe(bank.LocalComposition, id, slice, localInspection);
            observations.Add(new(bank.BankId, bank.Reference, local.Versions.Single()));
        }
        return new([], [], observations);
    }
}
