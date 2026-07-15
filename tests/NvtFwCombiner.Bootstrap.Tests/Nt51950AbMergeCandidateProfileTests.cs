using System.Buffers.Binary;
using System.Numerics;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Compilable evidence for the fixed-bank NT51950 AB Merge Combiner profile.</summary>
public sealed class Nt51950AbMergeCandidateProfileTests
{
    private const string BundleDirectory = "nt51950-ab-merge";
    private const string BundleContentHash = "06a671a3a6a6cb16e5cef7ed356a61626fdbd4395cd47299b95f60bb645885af";
    private const int Capacity = 0x80000;
    private const int BankLength = 0x40000;
    private const int TpInputLength = 0x37000;
    private const int TpCodeStart = 0xA000;
    private const int TpCodeLength = 0x2D000;

    /// <summary>Verifies the candidate compiles the exact staged Combiner boundary for the audited 512 KiB NT51950 AB image.</summary>
    [Fact]
    public void CandidateProfileDeclaresFullBanksAndCombinerOnlyHeaderMutation()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("nt51950-ab-merge-512k", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Equal(
            ["firmware-owner-review"],
            details.Provenance.Promotion.Blockers.Select(static blocker => blocker.BlockerId));
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            [
                "copy-dp-ab-image",
                "seed-a-bank-from-dp",
                "overlay-tpa-into-a-bank",
                "seed-b-bank-from-dp",
                "relocate-tpb-diff-for-b-bank",
                "overlay-tpb-into-b-bank",
                "copy-a-bank-to-output",
                "copy-b-bank-to-output",
                "run-nt51950-ab-combiner",
            ],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        CompositionOperation diffRelocation = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => StringComparer.Ordinal.Equals(operation.OperationId, "relocate-tpb-diff-for-b-bank"));
        Assert.Equal(CompositionOperationKind.TransformScalar, diffRelocation.Kind);
        Assert.Equal(new ByteRange(0xA120, sizeof(uint)), diffRelocation.SourceRange);
        Assert.Equal(new ByteRange(0xA120, sizeof(uint)), diffRelocation.TargetRange);
        Assert.Equal(new BigInteger(0x40000), Assert.IsType<ScalarTransform>(diffRelocation.ScalarTransform).Addend);

        CompositionOperation postbuild = composition.Plan.OrderedOperations[^1];
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(postbuild.ExternalProcessorInvocation);
        Assert.Equal("nfc-nt51950-ab-merge-combiner-v1", invocation.ProcessorId);
        Assert.Equal("legacy-combiner-1.13.0", invocation.ToolBindingId);
        Assert.Equal(
            [new ByteRange(0x4A100, 4), new ByteRange(0x4A110, 4), new ByteRange(0x4A130, 4)],
            invocation.AllowedWriteRanges);
        Assert.Equal(
            ["a-bank", "b-bank"],
            invocation.StagedArtifactBindings.Select(static binding => binding.ArtifactId));
        Assert.Equal(
            ["a-bank-work", "b-bank-work"],
            invocation.StagedArtifactBindings.Select(static binding => binding.SourceSpaceId));
        Assert.All(
            invocation.StagedArtifactBindings,
            static binding => Assert.Equal(new ByteRange(0, BankLength), binding.SourceRange));
    }

    /// <summary>Verifies the repository-only Combiner candidate cannot create an application run request.</summary>
    [Fact]
    public void CandidateProfileCannotCreateApplicationRunRequest()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CompositionRunRequest("ab-candidate", composition, [], composition.DefaultOutputFileName));

        Assert.Equal("compiledComposition", exception.ParamName);
        Assert.Contains("not executable", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies the engine stages immutable A/B artifacts, relocates only TPB DIFF, and leaves header CRC to Combiner.</summary>
    [Fact]
    public async Task CandidatePlanStagesRawTpBanksAndLeavesCallerInputsUntouchedAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);
        byte[] dp = CreatePattern(Capacity, 0x11);
        byte[] tpA = CreatePattern(TpInputLength, 0x44);
        byte[] tpB = CreatePattern(TpInputLength, 0x77);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA120, sizeof(uint)), 0x12345678);
        byte[] originalTpB = [.. tpB];
        bool invoked = false;

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
                invoked = true;
                Assert.Empty(stagedSources);
                Assert.Equal(["a-bank", "b-bank"], stagedArtifacts.Select(static artifact => artifact.ArtifactId));
                Assert.Equal(BankLength, stagedArtifacts[0].Bytes.Length);
                Assert.Equal(BankLength, stagedArtifacts[1].Bytes.Length);
                AssertRangeEquals(dp, 0, stagedArtifacts[0].Bytes.Span, 0, TpCodeStart);
                AssertRangeEquals(tpA, TpCodeStart, stagedArtifacts[0].Bytes.Span, TpCodeStart, TpCodeLength);
                AssertRangeEquals(dp, BankLength, stagedArtifacts[1].Bytes.Span, 0, TpCodeStart);
                AssertRangeEquals(tpB, TpCodeStart, stagedArtifacts[1].Bytes.Span, TpCodeStart, 0x120);
                Assert.Equal(
                    0x12385678u,
                    BinaryPrimitives.ReadUInt32LittleEndian(stagedArtifacts[1].Bytes.Span.Slice(0xA120, sizeof(uint))));
                AssertRangeEquals(
                    tpB,
                    TpCodeStart + 0x124,
                    stagedArtifacts[1].Bytes.Span,
                    TpCodeStart + 0x124,
                    TpCodeLength - 0x124);
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(originalTpB, tpB);
        AssertRangeEquals(tpA, TpCodeStart, result.OutputBytes.Span, TpCodeStart, TpCodeLength);
        AssertRangeEquals(tpB, TpCodeStart, result.OutputBytes.Span, BankLength + TpCodeStart, 0x120);
        Assert.Equal(
            0x12385678u,
            BinaryPrimitives.ReadUInt32LittleEndian(result.OutputBytes.Span.Slice(BankLength + 0xA120, sizeof(uint))));
        AssertRangeEquals(
            tpB,
            TpCodeStart + 0x124,
            result.OutputBytes.Span,
            BankLength + TpCodeStart + 0x124,
            TpCodeLength - 0x124);
    }

    /// <summary>Verifies no alternate capacity can select the fixed full-bank candidate map.</summary>
    [Fact]
    public void CandidateProfileRejectsNon512KiBMapCapacity()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash),
            "nt51950-ab-merge",
            "0.1.1",
            "NT51950",
            ExperienceIds.AbMerge,
            requestedMapCapacity: BankLength);

        Assert.False(compilation.IsCompiled);
        Assert.Contains(
            compilation.Issues,
            static issue => StringComparer.Ordinal.Equals(issue.Code, "profile.v2.compile.map-capacity-unavailable"));
    }

    /// <summary>Verifies the fixed candidate rejects every one-byte input-capacity deviation before staging.</summary>
    [Theory]
    [InlineData("dp-ab-input", Capacity - 1)]
    [InlineData("dp-ab-input", Capacity + 1)]
    [InlineData("tp-a-input", TpInputLength - 1)]
    [InlineData("tp-a-input", TpInputLength + 1)]
    [InlineData("tp-b-input", TpInputLength - 1)]
    [InlineData("tp-b-input", TpInputLength + 1)]
    public async Task CandidatePlanRejectsOneByteInputCapacityDeviationAsync(string inputId, int length)
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);
        Dictionary<string, byte[]> inputs = CreateInputs();
        inputs[inputId] = new byte[length];

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(inputs),
            externalProcessor: null,
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Contains(
            result.Issues,
            static issue => StringComparer.Ordinal.Equals(
                issue.Code,
                CompositionIssueCodes.InputAddressSpaceLengthMismatch));
    }

    /// <summary>Verifies declared authority covers every byte of the three external relocation fields.</summary>
    [Fact]
    public void CandidateWriteAuthorityCoversNoCarryAndCarryBytesOfEachRelocationField()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            composition.Plan.OrderedOperations[^1].ExternalProcessorInvocation);
        var policy = new ChangedRangePolicy(invocation.AllowedWriteRanges);

        foreach (ByteRange changedByte in new[]
                 {
                     new ByteRange(0x4A102, 1),
                     new ByteRange(0x4A103, 1),
                     new ByteRange(0x4A112, 1),
                     new ByteRange(0x4A113, 1),
                     new ByteRange(0x4A130, 1),
                     new ByteRange(0x4A133, 1),
                 })
        {
            Assert.True(policy.Evaluate([changedByte]).IsAllowed);
        }
    }

    private static CompiledComposition CompileCandidate(TempWorkspace workspace)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash),
            "nt51950-ab-merge",
            "0.1.1",
            "NT51950",
            ExperienceIds.AbMerge,
            Capacity);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static Dictionary<string, byte[]> CreateInputs()
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-ab-input"] = new byte[Capacity],
            ["tp-a-input"] = new byte[TpInputLength],
            ["tp-b-input"] = new byte[TpInputLength],
        };
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(salt + (index * 31)));
        }

        return bytes;
    }

    private static void AssertRangeEquals(
        ReadOnlySpan<byte> expected,
        int expectedStart,
        ReadOnlySpan<byte> actual,
        int actualStart,
        int length)
    {
        Assert.Equal(
            expected.Slice(expectedStart, length).ToArray(),
            actual.Slice(actualStart, length).ToArray());
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
