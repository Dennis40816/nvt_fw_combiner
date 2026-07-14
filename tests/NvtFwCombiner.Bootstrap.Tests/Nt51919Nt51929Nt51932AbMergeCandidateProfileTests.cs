using System.Buffers.Binary;
using System.Security.Cryptography;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Executable-candidate evidence for the fixed-capacity NT51919/NT51929/NT51932 AB Merge profiles.</summary>
public sealed class Nt51919Nt51929Nt51932AbMergeCandidateProfileTests
{
    private const string BundleDirectory = "nt51919-nt51929-nt51932-ab-merge";
    private const string BundleContentHash = "b5035b9c4afa8691adb98632b4ce9a1088d74d04948ea1f20690aade889445fb";
    private const int Capacity = 0x80000;
    private const int TpCodeStart = 0x7000;
    private const int TpCodeLength = 0x39000;
    private const int TpbOutputStart = 0x47000;
    private const int TpbScalarOffset = 0x7164;
    private const uint Relocation = 0x40000;
    private const string ReferenceSyntheticOutputSha256 = "cd54e124b02f2a91a5f43836ab49cc28db811a4a8e1ff407eb98e47437de10ce";

    /// <summary>Verifies every known member resolves one fixed 512 KiB AB map without granting runtime support.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias", "nt51919-ab-merge-512k")]
    [InlineData("NT51929", "nt51929-ab-merge", "nt51929-ab-merge-512k")]
    [InlineData("NT51932", "nt51932-ab-merge", "nt51932-ab-merge-512k")]
    public void CandidateProfilesCompileOnlyForTheFixedAbMap(
        string icId,
        string profileId,
        string expectedMapId)
    {
        CompiledComposition composition = CompileCandidate(icId, profileId);

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Equal(
            ["firmware-owner-review", "production-golden-evidence"],
            details.Provenance.Promotion.Blockers.Select(static blocker => blocker.BlockerId));
        Assert.Equal(expectedMapId, details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            ["copy-dp-ab-image", "copy-tpa", "relocate-tpb-ilm", "relocate-tpb-dlm", "relocate-tpb-diff", "copy-tpb"],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.Equal(
            [
                CompositionOperationKind.CopyRange,
                CompositionOperationKind.CopyRange,
                CompositionOperationKind.TransformScalar,
                CompositionOperationKind.TransformScalar,
                CompositionOperationKind.TransformScalar,
                CompositionOperationKind.CopyRange,
            ],
            composition.Plan.OrderedOperations.Select(static operation => operation.Kind));

        V2CompositionPlanCompileResult wrongCapacity = TrustedV2CompositionCompiler.Compile(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            profileId,
            "0.1.0",
            icId,
            ExperienceIds.AbMerge,
            requestedMapCapacity: 0x40000);
        Assert.False(wrongCapacity.IsCompiled);
        Assert.Contains(
            wrongCapacity.Issues,
            static issue => StringComparer.Ordinal.Equals(issue.Code, "profile.v2.compile.map-capacity-unavailable"));
    }

    /// <summary>Verifies full DP placement, TP overlays, TPB relocation, and immutable TPB source behavior.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias")]
    [InlineData("NT51929", "nt51929-ab-merge")]
    [InlineData("NT51932", "nt51932-ab-merge")]
    public void CandidatePlansProduceTheDeclaredAbLayoutWithoutMutatingInputs(string icId, string profileId)
    {
        CompiledComposition composition = CompileCandidate(icId, profileId);
        byte[] dp = CreatePattern(Capacity, 0x31);
        byte[] tpA = CreatePattern(0x40000, 0x57);
        byte[] tpB = CreatePattern(0x40000, 0x83);
        WriteUInt32(tpB, TpbScalarOffset, 0x00123456);
        WriteUInt32(tpB, TpbScalarOffset + sizeof(uint), 0x00ABCDEF);
        WriteUInt32(tpB, TpbScalarOffset + (2 * sizeof(uint)), 0x0000C0DE);
        byte[] originalTpB = [.. tpB];

        CompositionExecutionResult result = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-ab-input"] = dp,
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] output = result.OutputBytes.ToArray();
        Assert.Equal(Capacity, output.Length);
        AssertRangeEquals(dp, 0, output, 0, TpCodeStart);
        AssertRangeEquals(tpA, TpCodeStart, output, TpCodeStart, TpCodeLength);
        AssertRangeEquals(dp, 0x40000, output, 0x40000, TpCodeStart);

        byte[] expectedTpb = tpB.AsSpan(TpCodeStart, TpCodeLength).ToArray();
        WriteUInt32(expectedTpb, TpbScalarOffset - TpCodeStart, 0x00123456 + Relocation);
        WriteUInt32(expectedTpb, TpbScalarOffset - TpCodeStart + sizeof(uint), 0x00ABCDEF + Relocation);
        WriteUInt32(expectedTpb, TpbScalarOffset - TpCodeStart + (2 * sizeof(uint)), 0x0000C0DE + Relocation);
        AssertRangeEquals(expectedTpb, 0, output, TpbOutputStart, TpCodeLength);
        Assert.Equal(originalTpB, tpB);
    }

    /// <summary>
    /// Locks the complete candidate output to the immutable uploaded AB reference over address-sensitive synthetic inputs.
    /// This is migration evidence only and does not replace an owner product golden.
    /// </summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias")]
    [InlineData("NT51929", "nt51929-ab-merge")]
    [InlineData("NT51932", "nt51932-ab-merge")]
    public void CandidatePlansMatchReferenceSyntheticOutput(string icId, string profileId)
    {
        CompiledComposition composition = CompileCandidate(icId, profileId);
        byte[] dp = CreatePattern(Capacity, 0x31);
        byte[] tpA = CreatePattern(0x40000, 0x57);
        byte[] tpB = CreatePattern(0x40000, 0x83);
        WriteUInt32(tpB, TpbScalarOffset, 0x00123456);
        WriteUInt32(tpB, TpbScalarOffset + sizeof(uint), 0x00ABCDEF);
        WriteUInt32(tpB, TpbScalarOffset + (2 * sizeof(uint)), 0x0000C0DE);

        CompositionExecutionResult result = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-ab-input"] = dp,
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(
            ReferenceSyntheticOutputSha256,
            Convert.ToHexString(SHA256.HashData(result.OutputBytes.Span)).ToLowerInvariant());
    }

    /// <inheritdoc/>
    private static CompiledComposition CompileCandidate(string icId, string profileId)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            profileId,
            "0.1.0",
            icId,
            ExperienceIds.AbMerge,
            Capacity);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            int wordOffset = index & ~3;
            uint word = unchecked(
                ((uint)wordOffset * 0x9E3779B9U)
                ^ (salt * 0x01010101U)
                ^ 0xA5A5A5A5U);
            bytes[index] = unchecked((byte)(word >> ((index & 3) * 8)));
        }

        return bytes;
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);
    }

    private static void AssertRangeEquals(byte[] expected, int expectedStart, byte[] actual, int actualStart, int length)
    {
        Assert.Equal(
            expected.AsSpan(expectedStart, length).ToArray(),
            actual.AsSpan(actualStart, length).ToArray());
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
