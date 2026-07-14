using NvtFwCombiner.Contracts.Profiles;
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

        Assert.Equal(CompositionProfileProcessorKind.CrcWorkerV1, stage.Kind);
        Assert.Equal(CompositionProfileProcessorAuthority.Calculate, stage.Authority);
        Assert.Equal(CompositionProfileProcessorPurpose.Checksum, stage.Purpose);
        Assert.Equal(CompositionProfileIntegrityDisposition.VerifyExisting, stage.IntegrityDisposition);
        Assert.Equal(["view-a", "view-z"], stage.AllowedReadViewIds);
        Assert.Empty(stage.AllowedWriteViewIds);
        Assert.Equal("1.0.0", stage.ContractVersion);
        Assert.Equal("display-crc", stage.CalculationSetId);
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

        Assert.Equal(CompositionProfileProcessorKind.LegacyCombinerV1, stage.Kind);
        Assert.Equal(CompositionProfileProcessorAuthority.Transform, stage.Authority);
        Assert.Equal(CompositionProfileProcessorPurpose.HeaderAndIntegrity, stage.Purpose);
        Assert.Equal(CompositionProfileIntegrityDisposition.RecalculateAndWrite, stage.IntegrityDisposition);
        Assert.Equal(["header", "integrity"], stage.AllowedWriteViewIds);
        Assert.Equal(["dp-source", "tp-source"], stage.StagedSourceBindings.Select(static item => item.SourceViewId));
        Assert.Equal(["a-bank", "b-bank"], stage.StagedArtifactBindings.Select(static item => item.ArtifactId));
        Assert.Equal("combiner-1-13", stage.ToolBindingId);
        Assert.Equal("combiner-evidence", stage.EvidenceRef);
    }

    /// <summary>Verifies direct 2.0 normalization does not gain the schema-2.2 tool binding grammar.</summary>
    [Fact]
    public void ProcessorRejectsVersionedToolBindingOutsideLegacySchemaGrammar()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy(
                "relocation",
                "none",
                toolBindingId: "legacy-combiner-1.13.0")));

        Assert.Equal("processorStages[0]", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    /// <summary>Verifies schema 2.3 retains the dot-versioned external-tool binding grammar from 2.2.</summary>
    [Theory]
    [InlineData("2.2")]
    [InlineData("2.3")]
    public void ProcessorMapsPublishedCombinerToolBindingInVersionedSchemas(string schemaVersion)
    {
        LegacyCombinerProfileProcessorStage stage = Assert.IsType<LegacyCombinerProfileProcessorStage>(
            CompositionProfileNormalizer.NormalizeProcessorStage(
                Legacy("relocation", "none", toolBindingId: "legacy-combiner-1.13.0"),
                schemaVersion,
                "processorStages[0]"));

        Assert.Equal("legacy-combiner-1.13.0", stage.ToolBindingId);
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

    /// <summary>Verifies fixed authority, integrity, and failure policies cannot be silently ignored.</summary>
    [Fact]
    public void ProcessorRejectsInvalidFixedPoliciesWithPaths()
    {
        CompositionProfileNormalizationException crcAuthority = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker(authority: "transform")));
        CompositionProfileNormalizationException crcPurpose = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker(purpose: "header")));
        CompositionProfileNormalizationException crcIntegrity = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker(integrity: "none")));
        CompositionProfileNormalizationException crcWrites = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker(writes: ["header"])));
        CompositionProfileNormalizationException legacyAuthority = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy(
                "relocation",
                "none",
                authority: "calculate")));
        CompositionProfileNormalizationException failure = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker(failurePolicy: "continue")));

        Assert.Equal("processorStages[0].authority", crcAuthority.Path);
        Assert.Equal("processorStages[0].purpose", crcPurpose.Path);
        Assert.Equal("processorStages[0].integrityDisposition", crcIntegrity.Path);
        Assert.Equal("processorStages[0].allowedWriteViewIds", crcWrites.Path);
        Assert.Equal("processorStages[0].authority", legacyAuthority.Path);
        Assert.Equal("processorStages[0].failurePolicy", failure.Path);
    }

    /// <summary>Verifies required union members and lists fail at exact source paths.</summary>
    [Fact]
    public void ProcessorRejectsMissingMembersWithPaths()
    {
        CompositionProfileNormalizationException contract = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker(contractVersion: null)));
        CompositionProfileNormalizationException reads = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(CrcWorker() with { AllowedReadViewIds = null! }));
        CompositionProfileNormalizationException tool = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy(
                "relocation",
                "none",
                toolBindingId: null)));
        CompositionProfileNormalizationException bindings = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(
                Legacy("relocation", "none") with { StagedSourceBindings = null! }));
        CompositionProfileNormalizationException evidence = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeProcessorStage(Legacy(
                "relocation",
                "none",
                evidenceRef: null)));

        Assert.Equal("processorStages[0].contractVersion", contract.Path);
        Assert.Equal("processorStages[0].allowedReadViewIds", reads.Path);
        Assert.Equal("processorStages[0].toolBindingId", tool.Path);
        Assert.Equal("processorStages[0].stagedSourceBindings", bindings.Path);
        Assert.Equal("processorStages[0].evidenceRef", evidence.Path);
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
        string authority = "calculate",
        string purpose = "checksum",
        string integrity = "verify-existing",
        IReadOnlyList<string>? writes = null,
        string failurePolicy = "fail-closed",
        string? contractVersion = "1.0.0")
    {
        return new CompositionProfileProcessorStageDocument(
            "crc-check",
            kind,
            "output",
            authority,
            purpose,
            integrity,
            ["view-z", "view-a"],
            writes ?? [],
            failurePolicy,
            ContractVersion: contractVersion,
            CalculationSetId: "display-crc");
    }

    private static CompositionProfileProcessorStageDocument Legacy(
        string purpose,
        string integrity,
        string authority = "transform",
        IReadOnlyList<CompositionProfileStagedSourceBindingDocument>? bindings = default,
        IReadOnlyList<CompositionProfileStagedArtifactBindingDocument>? artifactBindings = default,
        string? toolBindingId = "combiner-1-13",
        string? evidenceRef = "combiner-evidence")
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
            InvocationProfileId: "profile",
            StagedSourceBindings: bindings ?? [],
            EvidenceRef: evidenceRef,
            StagedArtifactBindings: artifactBindings);
    }
}
