using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static (string? OutputSpaceId, string? ReferenceSpaceId, byte[]? ReferenceBytes) GetInspectionReference(
        CompositionRunRequest request,
        CompositionExecutionStatus runStatus,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        int outputLength)
    {
        if (runStatus != CompositionExecutionStatus.Succeeded ||
            request.CompiledComposition.V2Details.CompositionKind != CompositionKind.Replace ||
            request.CompiledComposition.Plan.OutputInitialization.Kind != ImageInitializationKind.Reference ||
            request.CompiledComposition.Plan.OutputInitialization.ReferenceSpaceId is not { } candidateSpaceId ||
            !inputBytes.TryGetValue(candidateSpaceId, out byte[]? candidateBytes) ||
            candidateBytes.Length != outputLength)
        {
            return (null, null, null);
        }

        return (request.CompiledComposition.Plan.OutputSpaceId, candidateSpaceId, candidateBytes);
    }
}
