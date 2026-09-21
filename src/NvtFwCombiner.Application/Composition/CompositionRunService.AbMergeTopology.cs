using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static void ValidateAbMergeTopologyMetadata(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        List<CompositionIssue> issues)
    {
        if (!request.CompiledComposition.IsV2AbMergeRuntimeRoute ||
            !inputBytes.TryGetValue(CompositionAddressSpaceIds.TpAInput, out byte[]? tpA) ||
            !inputBytes.TryGetValue(CompositionAddressSpaceIds.TpBInput, out byte[]? tpB))
        {
            return;
        }

        if (AbMergeTopologyAdmission.AssessAcceptedPair(request.CompiledComposition, tpA, tpB,
                request.AbMergeTopologySelection) is { } admission)
        {
            issues.AddRange(admission.Issues);
        }
    }

}
