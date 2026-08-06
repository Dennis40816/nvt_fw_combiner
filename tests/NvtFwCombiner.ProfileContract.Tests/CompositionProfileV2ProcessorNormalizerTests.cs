using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 closed processor stages.</summary>
public sealed class CompositionProfileV2ProcessorNormalizerTests
{
    /// <summary>Verifies CRC stages remain calculation-only with no write authority.</summary>
    [Fact]
    public void ProcessorMapsCrcWorkerShape()
    {
        CrcWorkerProfileProcessorStage stage = Assert.IsType<CrcWorkerProfileProcessorStage>(
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker()));

        Assert.Equal(["view-a", "view-z"], stage.AllowedReadViewIds);
        Assert.Empty(stage.AllowedWriteViewIds);
    }

    /// <summary>Verifies legacy stages preserve all approved ids, views, evidence, and staged bindings.</summary>
    [Fact]
    public void ProcessorMapsLegacyCombinerShape()
    {
        LegacyCombinerProfileProcessorStage stage = Assert.IsType<LegacyCombinerProfileProcessorStage>(
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy(
                "header-and-integrity",
                "recalculate-and-write",
                bindings:
                [
                    new CompositionProfileStagedSourceBindingDocument("tp-source", "staged-tp"),
                    new CompositionProfileStagedSourceBindingDocument("dp-source", "staged-dp"),
                ],
                artifactBindings:
                [
                    new CompositionProfileStagedArtifactBindingDocument("b-bank", "tp-source"),
                    new CompositionProfileStagedArtifactBindingDocument("a-bank", "dp-source"),
                ])));

        Assert.Equal(CompositionProfileProcessorPurpose.HeaderAndIntegrity, stage.Purpose);
        Assert.Equal(CompositionProfileIntegrityDisposition.RecalculateAndWrite, stage.IntegrityDisposition);
        Assert.Equal(["header", "integrity"], stage.AllowedWriteViewIds);
        Assert.Equal(["dp-source", "tp-source"], stage.StagedSourceBindings.Select(static item => item.SourceViewId));
        Assert.Equal(["a-bank", "b-bank"], stage.StagedArtifactBindings.Select(static item => item.ArtifactId));
        Assert.Equal("combiner-1-13", stage.ToolBindingId);
    }

    /// <summary>Verifies published external-tool and invocation identities lower after schema admission.</summary>
    [Fact]
    public void ProcessorMapsPublishedCombinerIdentities()
    {
        LegacyCombinerProfileProcessorStage stage = Assert.IsType<LegacyCombinerProfileProcessorStage>(
            CompositionProfileNormalizer.NormalizeProcessorStage(
                Legacy(
                    "relocation",
                    "none",
                    toolBindingId: "legacy-combiner-1.13.0",
                    invocationProfileId: "nfc.nt51926.ctrlram-postbuild-fw1.4.1",
                    targetView: true),
                "processorStages[0]"));

        Assert.Equal("legacy-combiner-1.13.0", stage.ToolBindingId);
        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", stage.InvocationProfileId);
        Assert.Equal("output-image", stage.TargetViewId);
    }

    /// <summary>Verifies every byte entering a typed processor staging image is covered by read authority.</summary>
    [Fact]
    public void ProcessorRejectsTypedTargetViewOutsideReadAuthority()
    {
        CompositionProfileProcessorStageDocument document = Legacy(
            "header-and-integrity",
            "recalculate-and-write",
            targetView: true) with
        {
            TargetViewId = "undeclared-read-view",
        };

        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(document, "processorStages[0]"));

        Assert.Equal("processorStages[0]", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    /// <summary>Verifies every legacy purpose maps using an allowed integrity disposition.</summary>
    [Fact]
    public void ProcessorMapsEveryLegacyPurpose()
    {
        string[] purposes = ["checksum", "header", "header-and-integrity", "relocation", "composite-post-process"];

        Assert.Equal(
            Enum.GetValues<CompositionProfileProcessorPurpose>(),
            purposes.Select(purpose => Assert.IsType<LegacyCombinerProfileProcessorStage>(
                CompositionProfileNormalizer.NormalizeProcessorStage(Legacy(
                    purpose,
                    purpose == "relocation" ? "none" : "recalculate-and-write"))).Purpose));
    }

    /// <summary>Verifies unknown stage, purpose, and integrity tokens retain exact paths.</summary>
    [Fact]
    public void ProcessorRejectsUnknownTokensWithPaths()
    {
        CompositionProfileNormalizationException kind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker(kind: "future")));
        CompositionProfileNormalizationException purpose = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy("future", "recalculate-and-write")));
        CompositionProfileNormalizationException integrity = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy("relocation", "verify-existing")));

        Assert.Equal("processorStages[0].kind", kind.Path);
        Assert.Equal("processorStages[0].purpose", purpose.Path);
        Assert.Equal("processorStages[0].integrityDisposition", integrity.Path);
    }

    /// <summary>Verifies legacy purpose/integrity combinations remain constructor-owned invariants.</summary>
    [Fact]
    public void ProcessorRejectsInvalidLegacyPurposeIntegrityAtStagePath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy("header", "none")));

        Assert.Equal("processorStages[0]", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    private static CompositionProfileProcessorStageDocument CrcWorker(
        string kind = "crc-worker-v1",
        string purpose = "checksum",
        string integrity = "verify-existing")
    {
        return new CompositionProfileProcessorStageDocument(
            "crc-check",
            kind,
            "output",
            "calculate",
            purpose,
            integrity,
            ["view-z", "view-a"],
            [],
            "fail-closed",
            ContractVersion: "1.0.0",
            CalculationSetId: "display-crc");
    }

    private static CompositionProfileProcessorStageDocument Legacy(
        string purpose,
        string integrity,
        string authority = "transform",
        IReadOnlyList<CompositionProfileStagedSourceBindingDocument>? bindings = default,
        IReadOnlyList<CompositionProfileStagedArtifactBindingDocument>? artifactBindings = default,
        string? toolBindingId = "combiner-1-13",
        string? evidenceRef = "combiner-evidence",
        string? invocationProfileId = "profile",
        bool targetView = false)
    {
        return new CompositionProfileProcessorStageDocument(
            "legacy-postbuild",
            "legacy-combiner-v1",
            "output",
            authority,
            purpose,
            integrity,
            ["output-image"],
            ["integrity", "header"],
            "fail-closed",
            ToolBindingId: toolBindingId,
            InvocationProfileId: invocationProfileId,
            StagedSourceBindings: bindings ?? [],
            EvidenceRef: evidenceRef,
            StagedArtifactBindings: artifactBindings,
            TargetViewId: targetView ? "output-image" : null);
    }
}
