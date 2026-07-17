using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileProcessorStage NormalizeProcessorStage(
        CompositionProfileProcessorStageDocument document,
        string path = "processorStages[0]")
    {
        return NormalizeProcessorStage(document, "2.0", path);
    }

    internal static CompositionProfileProcessorStage NormalizeProcessorStage(
        CompositionProfileProcessorStageDocument document,
        string schemaVersion,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        RequireConstant(
            document.FailurePolicy,
            "fail-closed",
            $"{path}.failurePolicy",
            "Processor failure policy must fail closed.");
        return document.Kind switch
        {
            "crc-worker-v1" => NormalizeCrcWorker(document, path),
            "legacy-combiner-v1" => NormalizeLegacyCombiner(document, schemaVersion, path),
            _ => throw Error($"{path}.kind", "Unknown processor stage kind."),
        };
    }

    private static CrcWorkerProfileProcessorStage NormalizeCrcWorker(
        CompositionProfileProcessorStageDocument document,
        string path)
    {
        RequireConstant(
            document.Authority,
            "calculate",
            $"{path}.authority",
            "CRC worker authority must be calculate-only.");
        RequireConstant(
            document.Purpose,
            "checksum",
            $"{path}.purpose",
            "CRC worker purpose must be checksum.");
        RequireConstant(
            document.IntegrityDisposition,
            "verify-existing",
            $"{path}.integrityDisposition",
            "CRC worker integrity disposition must verify existing bytes.");
        IReadOnlyList<string> writeViews = RequireList(
            document.AllowedWriteViewIds,
            $"{path}.allowedWriteViewIds");
        return writeViews.Count != 0
            ? throw Error($"{path}.allowedWriteViewIds", "CRC workers cannot declare write authority.")
            : Wrap(path, () => new CrcWorkerProfileProcessorStage(
            document.ProcessorStageId,
            RequireText(
                document.ContractVersion,
                $"{path}.contractVersion",
                "CRC worker contract version is missing."),
            RequireText(
                document.CalculationSetId,
                $"{path}.calculationSetId",
                "CRC calculation set is missing."),
            document.TargetSpaceId,
            RequireList(document.AllowedReadViewIds, $"{path}.allowedReadViewIds")));
    }

    private static LegacyCombinerProfileProcessorStage NormalizeLegacyCombiner(
        CompositionProfileProcessorStageDocument document,
        string schemaVersion,
        string path)
    {
        RequireConstant(
            document.Authority,
            "transform",
            $"{path}.authority",
            "Legacy combiner authority must be transform.");
        IReadOnlyList<CompositionProfileStagedSourceBindingDocument> bindingDocuments = RequireList(
            document.StagedSourceBindings,
            $"{path}.stagedSourceBindings");
        var bindings = new CompositionProfileStagedSourceBinding[bindingDocuments.Count];
        for (int index = 0; index < bindingDocuments.Count; index++)
        {
            CompositionProfileStagedSourceBindingDocument binding = bindingDocuments[index] ?? throw Error(
                $"{path}.stagedSourceBindings[{index}]",
                "Staged source binding cannot be null.");
            string bindingPath = $"{path}.stagedSourceBindings[{index}]";
            bindings[index] = Wrap(bindingPath, () => new CompositionProfileStagedSourceBinding(
                binding.SourceViewId,
                binding.TargetViewId));
        }

        IReadOnlyList<CompositionProfileStagedArtifactBindingDocument> artifactBindingDocuments =
            document.StagedArtifactBindings ?? [];
        var artifactBindings = new CompositionProfileStagedArtifactBinding[artifactBindingDocuments.Count];
        for (int index = 0; index < artifactBindingDocuments.Count; index++)
        {
            CompositionProfileStagedArtifactBindingDocument binding = artifactBindingDocuments[index] ?? throw Error(
                $"{path}.stagedArtifactBindings[{index}]",
                "Staged artifact binding cannot be null.");
            string bindingPath = $"{path}.stagedArtifactBindings[{index}]";
            artifactBindings[index] = Wrap(bindingPath, () => new CompositionProfileStagedArtifactBinding(
                binding.ArtifactId,
                binding.SourceViewId));
        }

        return Wrap(path, () => new LegacyCombinerProfileProcessorStage(
            document.ProcessorStageId,
            CompositionProfileValueRules.RequireToolBindingIdForSchemaVersion(
                schemaVersion,
                RequireText(document.ToolBindingId, $"{path}.toolBindingId", "Tool binding is missing."),
                nameof(document.ToolBindingId)),
            RequireText(
                document.InvocationProfileId,
                $"{path}.invocationProfileId",
                "Invocation profile is missing."),
            document.TargetSpaceId,
            NormalizeProcessorPurpose(document.Purpose, $"{path}.purpose"),
            NormalizeLegacyIntegrityDisposition(
                document.IntegrityDisposition,
                $"{path}.integrityDisposition"),
            RequireList(document.AllowedReadViewIds, $"{path}.allowedReadViewIds"),
            RequireList(document.AllowedWriteViewIds, $"{path}.allowedWriteViewIds"),
            bindings,
            artifactBindings,
            RequireText(document.EvidenceRef, $"{path}.evidenceRef", "Processor evidence is missing."),
            schemaVersion,
            schemaVersion is "2.8" or "2.9"
                ? RequireText(document.TargetViewId, $"{path}.targetViewId", "Processor target view is missing.")
                : document.TargetViewId is null
                    ? null
                    : throw Error($"{path}.targetViewId", "Processor target views require schema 2.8 or 2.9.")));
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
