using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static void ValidateAbMergeTopologyMetadata(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        List<CompositionIssue> issues)
    {
        if (!request.CompiledComposition.IsV2AbFunctionOpenCandidate ||
            request.AbMergeTopologySelection is not { } selected ||
            !TryGetAcceptedTpSourceView(request, inputBytes, CompositionAddressSpaceIds.TpAInput, out ReadOnlySpan<byte> tpA) ||
            !TryGetAcceptedTpSourceView(request, inputBytes, CompositionAddressSpaceIds.TpBInput, out ReadOnlySpan<byte> tpB))
        {
            return;
        }

        issues.AddRange(AbMergeTopologyAdmission.Assess(tpA, tpB, selected).Issues);
    }

    private static bool TryGetAcceptedTpSourceView(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        string addressSpaceId,
        out ReadOnlySpan<byte> prefix)
    {
        prefix = default;
        if (!inputBytes.TryGetValue(addressSpaceId, out byte[]? bytes))
        {
            return false;
        }

        CompiledInputArtifactInspectionResult inspection =
            CompiledInputArtifactInspectionService.Inspect(
                request.CompiledComposition,
                addressSpaceId,
                bytes);
        if (inspection.AcceptedSnapshotRange is not { Start: 0 } accepted ||
            accepted.Length > int.MaxValue)
        {
            return false;
        }

        prefix = bytes.AsSpan(0, checked((int)accepted.Length));
        return true;
    }

}
