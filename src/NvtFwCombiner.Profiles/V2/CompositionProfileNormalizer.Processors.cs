using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileProcessorStage NormalizeProcessorStage(
        CompositionProfileProcessorStageDocument document,
        string path = "processorStages[0]")
    {
        return document.Kind switch
        {
            "crc-worker-v1" => NormalizeCrcWorker(document, path),
            "legacy-combiner-v1" => NormalizeLegacyCombiner(document, path),
            _ => throw Error($"{path}.kind", "Unknown processor stage kind."),
        };
    }

    private static CrcWorkerProfileProcessorStage NormalizeCrcWorker(
        CompositionProfileProcessorStageDocument document,
        string path)
    {
        return Wrap(path, () => new CrcWorkerProfileProcessorStage(
            document.ProcessorStageId,
            document.ContractVersion!,
            document.CalculationSetId!,
            document.TargetSpaceId,
            document.AllowedReadViewIds));
    }

    private static LegacyCombinerProfileProcessorStage NormalizeLegacyCombiner(
        CompositionProfileProcessorStageDocument document,
        string path)
    {
        IReadOnlyList<CompositionProfileStagedSourceBindingDocument> bindingDocuments =
            document.StagedSourceBindings!;
        CompositionProfileStagedSourceBinding[] bindings = NormalizeList(
            bindingDocuments,
            $"{path}.stagedSourceBindings",
            (binding, bindingPath) => Wrap(bindingPath, () => new CompositionProfileStagedSourceBinding(
                binding.SourceViewId,
                binding.TargetViewId)));

        IReadOnlyList<CompositionProfileStagedArtifactBindingDocument> artifactBindingDocuments =
            document.StagedArtifactBindings ?? [];
        CompositionProfileStagedArtifactBinding[] artifactBindings = NormalizeList(
            artifactBindingDocuments,
            $"{path}.stagedArtifactBindings",
            (binding, bindingPath) => Wrap(bindingPath, () => new CompositionProfileStagedArtifactBinding(
                binding.ArtifactId,
                binding.SourceViewId)));

        return Wrap(path, () => new LegacyCombinerProfileProcessorStage(
            document.ProcessorStageId,
            document.ToolBindingId!,
            document.InvocationProfileId!,
            document.TargetSpaceId,
            NormalizeProcessorPurpose(document.Purpose, $"{path}.purpose"),
            NormalizeLegacyIntegrityDisposition(
                document.IntegrityDisposition,
                $"{path}.integrityDisposition"),
            document.AllowedReadViewIds,
            document.AllowedWriteViewIds,
            bindings,
            artifactBindings,
            document.EvidenceRef!,
            document.TargetViewId));
    }

    private static CompositionProfileProcessorPurpose NormalizeProcessorPurpose(string value, string path)
    {
        return value switch
        {
            "checksum" => CompositionProfileProcessorPurpose.Checksum,
            "header" => CompositionProfileProcessorPurpose.Header,
            "header-and-integrity" => CompositionProfileProcessorPurpose.HeaderAndIntegrity,
            "relocation" => CompositionProfileProcessorPurpose.Relocation,
            "composite-post-process" => CompositionProfileProcessorPurpose.CompositePostProcess,
            _ => throw Error(path, "Unknown processor purpose."),
        };
    }

    private static CompositionProfileIntegrityDisposition NormalizeLegacyIntegrityDisposition(
        string value,
        string path)
    {
        return value switch
        {
            "none" => CompositionProfileIntegrityDisposition.None,
            "recalculate-and-write" => CompositionProfileIntegrityDisposition.RecalculateAndWrite,
            _ => throw Error(path, "Unknown legacy processor integrity disposition."),
        };
    }
}
