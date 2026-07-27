using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateCtrlRamInputSnapshots(
        CtrlRamReplaceRunContext context,
        int topologyCount,
        IReadOnlyDictionary<string, string> slotPaths,
        out CtrlRamReplaceRunContext runtimeContext,
        out IReadOnlyDictionary<string, byte[]> snapshots,
        out IReadOnlyList<CompositionIssue> issues)
    {
        runtimeContext = context;
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [context.BasePath!] = context.BaseBytes!,
        };
        if (!DiffDlmNfMaskPolicy.TryResolve(
                context.PostbuildProfile!.IcId,
                context.CommandPlan!.Branch,
                out DiffDlmNfGeometry? geometry))
        {
            snapshots = artifacts;
            issues = [];
            return true;
        }

        TpCtrlRamPostbuildSource? diffDlm = context.SelectedSources.SingleOrDefault(
            static source => DiffDlmNfMaskPolicy.IsDiffDlmSource(source.SourceFileName));
        if (diffDlm is null)
        {
            snapshots = artifacts;
            issues = [];
            return true;
        }

        if (diffDlm.Blocks.Count != 1 ||
            diffDlm.Blocks[0].SourceOffset != 0 ||
            diffDlm.Blocks[0].FirmwareRange != geometry!.MaximumFirmwareRange ||
            diffDlm.RequiredLength != geometry.MaximumFirmwareRange.Length)
        {
            snapshots = artifacts;
            issues =
            [
                new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
                    "The selected postbuild plan does not match the approved Preserve-active-DiffNF geometry.",
                    CtrlRamSlotId(diffDlm.SourceId)),
            ];
            return false;
        }

        if (!DiffDlmNfMaskPolicy.TryResolveActiveRange(
                geometry,
                topologyCount,
                context.BaseBytes!,
                out ByteRange activeRange,
                out CompositionIssue? rangeIssue))
        {
            snapshots = artifacts;
            issues = [rangeIssue!];
            return false;
        }

        string slotId = CtrlRamSlotId(diffDlm.SourceId);
        string sourcePath = Path.GetFullPath(slotPaths[slotId]);
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(sourcePath, context.BasePath, pathComparison))
        {
            snapshots = artifacts;
            issues =
            [
                new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
                    "DiffDLM and Base firmware must be different input files.",
                    slotId),
            ];
            return false;
        }

        int activeLength = checked((int)activeRange.Length);
        byte[] selectedSource;
        try
        {
            using var stream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            selectedSource = new byte[checked((int)stream.Length)];
            stream.ReadExactly(selectedSource);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            snapshots = artifacts;
            issues =
            [
                new CompositionIssue(
                    WorkbenchIssueCodes.InputArtifactReadFailed,
                    $"DiffDLM could not be snapshotted: {exception.Message}",
                    slotId),
            ];
            return false;
        }

        if (!DiffDlmNfMaskPolicy.TryValidateSelectedSource(
                geometry,
                activeRange,
                selectedSource,
                out CompositionIssue? issue))
        {
            snapshots = artifacts;
            issues = [issue!];
            return false;
        }

        artifacts[sourcePath] = selectedSource;
        LegacyCombinerBlockArgument declaredBlock = diffDlm.Blocks[0];
        int activeRecordCount = activeLength / geometry.RecordStride;
        LegacyCombinerBlockArgument[] activeDlmBlocks =
        [
            .. Enumerable.Range(0, activeRecordCount).Select(recordIndex =>
            {
                int recordOffset = checked(recordIndex * geometry.RecordStride);
                return new LegacyCombinerBlockArgument(
                    $"{declaredBlock.BlockId}-active-{recordIndex + 1}",
                    declaredBlock.SourceKind,
                    declaredBlock.SourceFileName,
                    recordOffset,
                    new ByteRange(
                        checked(activeRange.Start + recordOffset),
                        geometry.WritableDlmLength),
                    declaredBlock.StagedArtifactId);
            }),
        ];
        TpCtrlRamPostbuildSource activeSource = diffDlm with
        {
            RequiredLength = activeLength,
            Blocks = activeDlmBlocks,
        };
        var activeLengths = new Dictionary<string, long>(
            context.SelectedSourceLengths,
            StringComparer.Ordinal)
        {
            [diffDlm.SourceId] = activeLength,
        };
        runtimeContext = context with
        {
            SelectedSources =
            [
                .. context.SelectedSources.Select(source =>
                    string.Equals(source.SourceId, diffDlm.SourceId, StringComparison.Ordinal)
                        ? activeSource
                        : source),
            ],
            SelectedSourceLengths = activeLengths,
        };
        snapshots = artifacts;
        issues = [];
        return true;
    }
}
