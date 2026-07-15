using System.Buffers.Binary;
using System.Numerics;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Compilable evidence for the one-mebibyte NT51951 AB Merge candidate profile.</summary>
public sealed class Nt51951AbMergeCandidateProfileTests
{
    private const string BundleDirectory = "nt51950-ab-merge";
    private const string BundleContentHash = "06a671a3a6a6cb16e5cef7ed356a61626fdbd4395cd47299b95f60bb645885af";
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
        Assert.Equal("nt51951-ab-merge-1024k", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.Compilable, details.Provenance.Promotion.Stage);
        Assert.Equal(
            ["direct-golden-evidence", "firmware-owner-review"],
            details.Provenance.Promotion.Blockers.Select(static blocker => blocker.BlockerId));
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        CompositionOperation relocation = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => StringComparer.Ordinal.Equals(operation.OperationId, "relocate-tpb-diff-for-b-bank"));
        Assert.Equal(new ByteRange(0xA120, sizeof(uint)), relocation.SourceRange);
        Assert.Equal(new BigInteger(0x80000), Assert.IsType<ScalarTransform>(relocation.ScalarTransform).Addend);

        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            composition.Plan.OrderedOperations[^1].ExternalProcessorInvocation);
        Assert.Equal("nfc-nt51951-ab-merge-combiner-v1", invocation.ProcessorId);
        Assert.Equal(
            [new ByteRange(0x8A100, 4), new ByteRange(0x8A110, 4), new ByteRange(0x8A130, 4)],
            invocation.AllowedWriteRanges);
        Assert.All(
            invocation.StagedArtifactBindings,
            static binding => Assert.Equal(new ByteRange(0, BankLength), binding.SourceRange));
    }

    /// <summary>Verifies the direct-golden-blocked candidate cannot create an application run request.</summary>
    [Fact]
    public void CandidateProfileCannotCreateApplicationRunRequest()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CompositionRunRequest("ab-candidate", composition, [], composition.DefaultOutputFileName));

        Assert.Equal("compiledComposition", exception.ParamName);
        Assert.Contains("not executable", exception.Message, StringComparison.Ordinal);
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
    }

    private static CompiledComposition CompileCandidate(TempWorkspace workspace)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash),
            "nt51951-ab-merge",
            "0.1.0",
            "NT51951",
            ExperienceIds.AbMerge,
            Capacity);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
