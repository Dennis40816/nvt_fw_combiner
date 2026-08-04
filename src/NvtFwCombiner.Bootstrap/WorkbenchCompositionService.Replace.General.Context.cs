using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralReplaceRunContext(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        out GeneralReplaceRunContext? context,
        out IReadOnlyDictionary<string, string> reportSlotPaths,
        out CompositionIssue? failure)
    {
        Dictionary<string, string> reportPaths = new(slotPaths, StringComparer.Ordinal);
        foreach (GeneralMappingDraftRow mapping in mappingDraft.Rows)
        {
            if (mapping.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
                !string.IsNullOrWhiteSpace(mapping.Source.Reference))
            {
                reportPaths[mapping.MappingId] = mapping.Source.Reference;
            }
        }
        reportSlotPaths = reportPaths;
        IcNumberSelection selection = ToIcNumberSelection(number);

        if (!IcNumberChoicePolicy.IsNumberSelectionSupported(selection, GetPostbuildProfiles(icId)))
        {
            context = null;
            failure = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceGeneralIcNumberUnsupported,
                $"IC number selection '{number}' is not supported for {icId} General Replace.",
                WorkbenchReplaceModes.General.ToLowerInvariant());
            return false;
        }

        if (!slotPaths.TryGetValue(WorkbenchSlotIds.ReplaceBase, out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            context = null;
            failure = new CompositionIssue(
                WorkbenchIssueCodes.InputMissing,
                "Base flash BIN is required before General Replace can compile explicit mappings.",
                WorkbenchReplaceModes.General.ToLowerInvariant());
            return false;
        }

        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            context = null;
            failure = new CompositionIssue(
                WorkbenchIssueCodes.InputArtifactReadFailed,
                "Base flash BIN path does not exist.",
                WorkbenchReplaceModes.General.ToLowerInvariant());
            return false;
        }

        if (mappingDraft.Rows.Count == 0)
        {
            context = null;
            failure = new CompositionIssue(
                WorkbenchIssueCodes.InputMissing,
                "At least one General Replace mapping row or hexadecimal patch is required.",
                WorkbenchReplaceModes.General.ToLowerInvariant());
            return false;
        }

        long capacity = new FileInfo(fullBasePath).Length;
        if (capacity <= 0)
        {
            context = null;
            failure = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                "Base flash BIN must not be empty.",
                WorkbenchReplaceModes.General.ToLowerInvariant());
            return false;
        }

        context = new GeneralReplaceRunContext(
            selection,
            reportSlotPaths,
            fullBasePath,
            capacity,
            mappingDraft);
        failure = null;
        return true;
    }
}
