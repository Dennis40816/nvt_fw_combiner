using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable v2 processor-stage and output naming values.</summary>
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

        Assert.Equal(CompositionProfileProcessorKind.CrcWorkerV1, stage.Kind);
        Assert.Equal(CompositionProfileProcessorAuthority.Calculate, stage.Authority);
        Assert.Equal(CompositionProfileProcessorPurpose.Checksum, stage.Purpose);
        Assert.Equal(CompositionProfileIntegrityDisposition.VerifyExisting, stage.IntegrityDisposition);
        Assert.Equal(CompositionProfileProcessorFailurePolicy.FailClosed, CompositionProfileProcessorStage.FailurePolicy);
        Assert.Equal(["view-a", "view-z"], stage.AllowedReadViewIds);
        Assert.Empty(stage.AllowedWriteViewIds);
        Assert.Equal("1.0.0", stage.ContractVersion);
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
            "combiner-evidence");
        reads.Clear();
        writes.Clear();
        bindings.Clear();

        Assert.Equal(CompositionProfileProcessorAuthority.Transform, stage.Authority);
        Assert.Equal(["crc", "header"], stage.AllowedWriteViewIds);
        Assert.Equal(["dp-source", "tp-source"], stage.StagedSourceBindings.Select(static item => item.SourceViewId));
        Assert.Equal("combiner-1-13", stage.ToolBindingId);
        Assert.Equal("combiner-evidence", stage.EvidenceRef);
        Assert.Equal(CompositionProfileProcessorFailurePolicy.FailClosed, CompositionProfileProcessorStage.FailurePolicy);
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
        _ = Assert.Throws<ArgumentException>(() => new CrcWorkerProfileProcessorStage(
            "crc-check",
            "1.0",
            "display-crc",
            "output",
            ["output-image"]));
    }

    /// <summary>Verifies output naming policy snapshots canonical token ids without rendering paths.</summary>
    [Fact]
    public void OutputNamingPolicyIsImmutableAndDoesNotRender()
    {
        var tokens = new List<string> { "version", "original-name" };
        var output = new CompositionProfileOutput(
            "{original-name}_{version}.bin",
            allowOverride: false,
            CompositionProfileInvalidCharacterPolicy.ReplaceUnderscore,
            tokens);
        tokens.Clear();

        Assert.Equal("{original-name}_{version}.bin", output.FileNameTemplate);
        Assert.False(output.AllowOverride);
        Assert.Equal(CompositionProfileInvalidCharacterPolicy.ReplaceUnderscore, output.InvalidCharacterPolicy);
        Assert.Equal(["original-name", "version"], output.RequiredTokenIds);
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileOutput(
            "name.bin",
            false,
            CompositionProfileInvalidCharacterPolicy.Reject,
            ["Version"]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompositionProfileOutput(
            "name.bin",
            false,
            (CompositionProfileInvalidCharacterPolicy)99,
            []));
    }

    private static LegacyCombinerProfileProcessorStage Legacy(
        CompositionProfileProcessorPurpose purpose,
        CompositionProfileIntegrityDisposition integrityDisposition,
        IEnumerable<string>? writes = null,
        IEnumerable<CompositionProfileStagedSourceBinding>? bindings = null)
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
            "combiner-evidence");
    }
}
