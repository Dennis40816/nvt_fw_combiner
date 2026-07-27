using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<(
        bool Succeeded,
        CtrlRamReplaceRunContext RuntimeContext,
        IReadOnlyDictionary<string, byte[]> Snapshots,
        IReadOnlyList<CompositionIssue> Issues)> CreateCtrlRamInputSnapshotsAsync(
        CtrlRamReplaceRunContext context,
        int topologyCount,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken)
    {
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [context.BasePath!] = context.BaseBytes!,
        };
        if (!DiffDlmNfMaskPolicy.TryResolve(
                context.PostbuildProfile!.IcId,
                context.CommandPlan!.Branch,
                out DiffDlmNfGeometry? geometry))
        {
            return (true, context, artifacts, []);
        }

        TpCtrlRamPostbuildSource? diffDlm = context.SelectedSources.SingleOrDefault(
            static source => DiffDlmNfMaskPolicy.IsDiffDlmSource(source.SourceFileName));
        if (diffDlm is null)
        {
            return (true, context, artifacts, []);
        }

        if (diffDlm.Blocks.Count != 1 ||
            diffDlm.Blocks[0].SourceOffset != 0 ||
            diffDlm.Blocks[0].FirmwareRange != geometry!.MaximumFirmwareRange ||
            diffDlm.RequiredLength != geometry.MaximumFirmwareRange.Length)
        {
            CompositionIssue[] issues =
            [
                new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
                    "The selected postbuild plan does not match the approved Preserve-active-DiffNF geometry.",
                    CtrlRamSlotId(diffDlm.SourceId)),
            ];
            return (false, context, artifacts, issues);
        }

        if (!DiffDlmNfMaskPolicy.TryResolveActiveRange(
                geometry,
                topologyCount,
                context.BaseBytes!,
                out ByteRange activeRange,
                out CompositionIssue? rangeIssue))
        {
            return (false, context, artifacts, [rangeIssue!]);
        }

        string slotId = CtrlRamSlotId(diffDlm.SourceId);
        string sourcePath = Path.GetFullPath(slotPaths[slotId]);
        int activeLength = checked((int)activeRange.Length);
        byte[] selectedSource;
        try
        {
            FileArtifactReader reader = new(
                [Path.GetDirectoryName(sourcePath)!, Path.GetDirectoryName(context.BasePath!)!]);
            selectedSource = (await reader.ReadDistinctAsync(
                sourcePath,
                context.BasePath!,
                cancellationToken).ConfigureAwait(false)).ToArray();
        }
        catch (ArgumentException exception)
        {
            return (
                false,
                context,
                artifacts,
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
                        $"DiffDLM and Base firmware must be different physical input files: {exception.Message}",
                        slotId),
                ]);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            return (
                false,
                context,
                artifacts,
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.InputArtifactReadFailed,
                        $"DiffDLM could not be snapshotted: {exception.Message}",
                        slotId),
                ]);
        }

        if (selectedSource.AsSpan().SequenceEqual(context.BaseBytes))
        {
            return (
                false,
                context,
                artifacts,
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
                        "DiffDLM must not reuse the immutable Base firmware snapshot.",
                        slotId),
                ]);
        }

        if (!DiffDlmNfMaskPolicy.TryValidateSelectedSource(
                geometry,
                activeRange,
                selectedSource,
                out CompositionIssue? issue))
        {
            return (false, context, artifacts, [issue!]);
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
        CtrlRamReplaceRunContext runtimeContext = context with
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
        return (true, runtimeContext, artifacts, []);
    }
}
