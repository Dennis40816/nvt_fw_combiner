using System.Buffers.Binary;
using System.Numerics;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Compilable evidence for the one-mebibyte NT51951 AB Merge candidate profile.</summary>
public sealed class Nt51951AbMergeCandidateProfileTests
{
    private const string BundleDirectory = "nt51950-ab-merge";
    private const string BundleContentHash = "775c42fba1fbbf1c4c8869656c83c86ce34d612dda3ceed92a93cb4e82f7cd67";
    private const int Capacity = 0x100000;
    private const int BankLength = 0x80000;
    private const int TpInputLength = 0x37000;
    private const int TpCodeStart = 0xA000;

    /// <summary>Verifies the candidate maps the fixed one-mebibyte two-bank topology without promotion.</summary>
    [Fact]
    public void CandidateProfileDeclaresOneMebibyteBanksAndCombinerAuthority()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);

        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.True(composition.IsV2AbFunctionOpenCandidate);
        Assert.Equal(
            "6a5888486d14dc81df85f30bb5529816919d31993c0001580fa0479fde777e97",
            composition.IntegrityFingerprint);
        Assert.Equal("nt51951-ab-merge-1024k", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Equal(
            ["direct-golden-evidence", "firmware-owner-review"],
            details.Provenance.Promotion.Blockers.Select(static blocker => blocker.BlockerId));
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        CompositionOperation relocation = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => StringComparer.Ordinal.Equals(operation.OperationId, "relocate-tpb-diff-for-b-bank"));
        Assert.Equal(new ByteRange(0xA120, sizeof(uint)), relocation.SourceRange);
        Assert.Equal(new BigInteger(0x80000), Assert.IsType<ScalarTransform>(relocation.ScalarTransform).Addend);
        Assert.Equal(
            [new ByteRange(TpCodeStart, 0x2D000), new ByteRange(TpCodeStart, 0x2D000)],
            composition.Plan.OrderedOperations
                .Where(static operation => operation.OperationId is
                    "overlay-tpa-into-output" or "overlay-tpb-into-output")
                .Select(static operation => operation.SourceRange));

        CompositionOperation postbuild = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => StringComparer.Ordinal.Equals(operation.OperationId, "run-nt51951-ab-combiner"));
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(postbuild.ExternalProcessorInvocation);
        Assert.Equal("ab-combiner-work", postbuild.TargetSpaceId);
        Assert.Equal("nfc-nt51951-ab-merge-combiner-v1", invocation.ProcessorId);
        Assert.Equal(
            [new ByteRange(0x8A100, 4), new ByteRange(0x8A110, 4), new ByteRange(0x8A130, 4)],
            invocation.AllowedWriteRanges);
        Assert.Equal(
            [new ByteRange(0, BankLength), new ByteRange(BankLength, BankLength)],
            invocation.StagedArtifactBindings.Select(static binding => binding.SourceRange));
        Assert.Equal(
            ["output-image", "output-image"],
            invocation.StagedArtifactBindings.Select(static binding => binding.SourceSpaceId));
        Assert.Equal(
            [
                ("copy-dp-ab-image", new ByteRange(0, Capacity)),
                ("overlay-tpa-into-output", new ByteRange(0xA000, 0x2D000)),
                ("overlay-tpb-into-output", new ByteRange(0x8A000, 0x2D000)),
                ("import-postbuild-b-ilm", new ByteRange(0x8A100, sizeof(uint))),
                ("import-postbuild-b-dlm", new ByteRange(0x8A110, sizeof(uint))),
                ("import-postbuild-b-crc", new ByteRange(0x8A130, sizeof(uint))),
            ],
            composition.Plan.OrderedOperations
                .Where(operation => operation.TargetSpaceId == composition.Plan.OutputSpaceId)
                .Select(static operation => (operation.OperationId, operation.TargetRange)));
    }

    /// <summary>Verifies the narrowly admitted function-open candidate can create an application run request.</summary>
    [Fact]
    public void FunctionOpenCandidateCanCreateApplicationRunRequest()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);

        var request = new CompositionRunRequest(
            "ab-candidate",
            composition,
            CreateRuntimeBindings(),
            composition.DefaultOutputFileName);

        Assert.Same(composition, request.CompiledComposition);
    }

    /// <summary>Verifies a selector-free AB map rejects a hidden topology request at compiler admission.</summary>
    [Fact]
    public void CandidateProfileRejectsHiddenTopologySelection()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-candidate");
        V2CompositionPlanCompileResult compilation = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash).Compile(
            "nt51951-ab-merge",
            "0.3.0",
            "NT51951",
            ExperienceIds.AbMerge,
            Capacity,
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"),
            []);

        Assert.False(compilation.IsCompiled);
        Assert.Equal(
            ["profile.v2.compile.topology-not-declared"],
            compilation.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies the engine stages complete banks, relocates only TPB DIFF, and keeps caller TPB immutable.</summary>
    [Fact]
    public async Task CandidatePlanStagesRelocatedTpbBeforeCombinerAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);
        byte[] dp = new byte[Capacity];
        byte[] tpA = new byte[TpInputLength];
        byte[] tpB = new byte[TpInputLength];
        dp[0] = 0xA1;
        dp[BankLength] = 0xB2;
        tpA[TpCodeStart] = 0xC3;
        tpB[TpCodeStart] = 0xD4;
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA120, sizeof(uint)), 0x12345678);
        byte[] originalTpB = [.. tpB];

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-ab-input"] = dp,
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }),
            (_, inputBytes, stagedSources, stagedArtifacts, _) =>
            {
                Assert.Empty(stagedSources);
                Assert.Equal(["a-bank", "b-bank"], stagedArtifacts.Select(static artifact => artifact.ArtifactId));
                Assert.Equal(BankLength, stagedArtifacts[0].Bytes.Length);
                Assert.Equal(BankLength, stagedArtifacts[1].Bytes.Length);
                Assert.Equal(0xA1, stagedArtifacts[0].Bytes.Span[0]);
                Assert.Equal(0xC3, stagedArtifacts[0].Bytes.Span[TpCodeStart]);
                Assert.Equal(0xB2, stagedArtifacts[1].Bytes.Span[0]);
                Assert.Equal(0xD4, stagedArtifacts[1].Bytes.Span[TpCodeStart]);
                Assert.Equal(
                    0x123C5678u,
                    BinaryPrimitives.ReadUInt32LittleEndian(stagedArtifacts[1].Bytes.Span.Slice(0xA120, sizeof(uint))));
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(originalTpB, tpB);
        Assert.Equal(0xC3, result.OutputBytes.Span[TpCodeStart]);
        Assert.Equal(0xD4, result.OutputBytes.Span[BankLength + TpCodeStart]);
        Assert.Equal(
            0x123C5678u,
            BinaryPrimitives.ReadUInt32LittleEndian(result.OutputBytes.Span.Slice(BankLength + 0xA120, sizeof(uint))));
        Assert.Equal(dp.AsSpan(0, TpCodeStart).ToArray(), result.OutputBytes.Span[..TpCodeStart].ToArray());
        Assert.Equal(dp.AsSpan(0x37000, 0x53000).ToArray(), result.OutputBytes.Span.Slice(0x37000, 0x53000).ToArray());
        Assert.Equal(dp.AsSpan(0xB7000).ToArray(), result.OutputBytes.Span[0xB7000..].ToArray());
    }

    private static CompiledComposition CompileCandidate(TempWorkspace workspace)
    {
        V2CompositionPlanCompileResult compilation = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash).Compile(
            "nt51951-ab-merge",
            "0.3.0",
            "NT51951",
            ExperienceIds.AbMerge,
            Capacity);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static InputArtifactBinding[] CreateRuntimeBindings()
    {
        return
        [
            new InputArtifactBinding(
                "dp-ab-input",
                "dp-ab-input",
                "dp-ab-input-artifact",
                "dp.bin",
                CompiledInputArtifactClass.DpFirmware),
            new InputArtifactBinding(
                "tp-a-input",
                "tp-a-input",
                "tp-a-input-artifact",
                "tp-a.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "tp-b-input",
                "tp-b-input",
                "tp-b-input-artifact",
                "tp-b.bin",
                CompiledInputArtifactClass.TpFirmware),
        ];
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
