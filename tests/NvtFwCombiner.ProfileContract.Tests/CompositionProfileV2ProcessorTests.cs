using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable v2 processor-stage values.</summary>
public sealed class CompositionProfileV2ProcessorTests
{
    /// <summary>Verifies CRC stages are structurally calculation-only with zero write authority.</summary>
    [Fact]
    public void CrcStageKeepsClosedCalculationAuthority()
    {
        var reads = new List<string> { "view-z", "view-a" };
        var stage = new CrcWorkerProfileProcessorStage(
            "crc-check",
            "1.0.0",
            "display-crc",
            "output",
            reads);
        reads.Clear();

        Assert.Equal(["view-a", "view-z"], stage.AllowedReadViewIds);
        Assert.Empty(stage.AllowedWriteViewIds);
    }

    /// <summary>Verifies legacy stages retain only approved ids, views, evidence, and staged bindings.</summary>
    [Fact]
    public void LegacyStageSnapshotsTransformAuthority()
    {
        var reads = new List<string> { "output-image" };
        var writes = new List<string> { "header", "crc" };
        var bindings = new List<CompositionProfileStagedSourceBinding>
        {
            new("tp-source", "staged-tp"),
            new("dp-source", "staged-dp"),
        };
        var artifactBindings = new List<CompositionProfileStagedArtifactBinding>
        {
            new("b-bank", "tp-source"),
            new("a-bank", "dp-source"),
        };
        var stage = new LegacyCombinerProfileProcessorStage(
            "legacy-postbuild",
            "combiner-1-13",
            "nt51950-ab-b-code",
            "output",
            CompositionProfileProcessorPurpose.HeaderAndIntegrity,
            CompositionProfileIntegrityDisposition.RecalculateAndWrite,
            reads,
            writes,
            bindings,
            artifactBindings,
            "combiner-evidence");
        reads.Clear();
        writes.Clear();
        bindings.Clear();
        artifactBindings.Clear();

        Assert.Equal(["crc", "header"], stage.AllowedWriteViewIds);
        Assert.Equal(["dp-source", "tp-source"], stage.StagedSourceBindings.Select(static item => item.SourceViewId));
        Assert.Equal(["a-bank", "b-bank"], stage.StagedArtifactBindings.Select(static item => item.ArtifactId));
        Assert.Equal("combiner-1-13", stage.ToolBindingId);
    }

    /// <summary>Verifies the closed purpose/integrity matrix rejects unsupported authority.</summary>
    [Fact]
    public void LegacyStageRejectsInvalidPurposeIntegrityCombinations()
    {
        _ = Assert.Throws<ArgumentException>(() => Legacy(
            CompositionProfileProcessorPurpose.Header,
            CompositionProfileIntegrityDisposition.None));
        _ = Assert.Throws<ArgumentException>(() => Legacy(
            CompositionProfileProcessorPurpose.CompositePostProcess,
            CompositionProfileIntegrityDisposition.None));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Legacy(
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.VerifyExisting));

        LegacyCombinerProfileProcessorStage relocation = Legacy(
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.None);
        LegacyCombinerProfileProcessorStage relocationWithIntegrity = Legacy(
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.RecalculateAndWrite);

        Assert.Equal(CompositionProfileIntegrityDisposition.None, relocation.IntegrityDisposition);
        Assert.Equal(
            CompositionProfileIntegrityDisposition.RecalculateAndWrite,
            relocationWithIntegrity.IntegrityDisposition);
    }

    /// <summary>Verifies legacy transform stages require unambiguous write and staging declarations.</summary>
    [Fact]
    public void LegacyStageRejectsMissingWritesAndDuplicateBindings()
    {
        _ = Assert.Throws<ArgumentException>(() => Legacy(
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.None,
            writes: []));
        var duplicate = new CompositionProfileStagedSourceBinding("source", "target");
        _ = Assert.Throws<ArgumentException>(() => Legacy(
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.None,
            bindings: [duplicate, duplicate]));
        _ = Assert.Throws<ArgumentException>(() => Legacy(
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.None,
            artifactBindings:
            [
                new CompositionProfileStagedArtifactBinding("bank", "source-a"),
                new CompositionProfileStagedArtifactBinding("bank", "source-b"),
            ]));
        _ = Assert.Throws<ArgumentException>(() => new CrcWorkerProfileProcessorStage(
            "crc-check",
            "1.0",
            "display-crc",
            "output",
            ["output-image"]));
    }

    private static LegacyCombinerProfileProcessorStage Legacy(
        CompositionProfileProcessorPurpose purpose,
        CompositionProfileIntegrityDisposition integrityDisposition,
        IEnumerable<string>? writes = null,
        IEnumerable<CompositionProfileStagedSourceBinding>? bindings = null,
        IEnumerable<CompositionProfileStagedArtifactBinding>? artifactBindings = null)
    {
        return new LegacyCombinerProfileProcessorStage(
            "legacy-postbuild",
            "combiner-1-13",
            "profile",
            "output",
            purpose,
            integrityDisposition,
            ["output-image"],
            writes ?? ["header"],
            bindings ?? [],
            artifactBindings ?? [],
            "combiner-evidence");
    }
}
