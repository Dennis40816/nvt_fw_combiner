using System.Buffers.Binary;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-admission evidence for the fixed NT51919/NT51929/NT51932 AB Merge profiles.</summary>
public sealed class Nt51919Nt51929Nt51932AbMergeSupportedProfileTests
{
    private const string BundleDirectory = "nt51919-nt51929-nt51932-ab-merge";
    private const string BundleContentHash = "390743408fbaa172a6e4dc073c0c9f515de94faf502b996bd0380af0fb388680";
    private const int Capacity = 0x80000;
    private const int TpCodeStart = 0x7000;
    private const int TpCodeLength = 0x39000;
    private const int TpbOutputStart = 0x47000;
    private const int TpbScalarOffset = 0x7164;
    private const uint Relocation = 0x40000;

    /// <summary>Verifies every approved member resolves one supported fixed 512 KiB AB map.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias", "nt51919-ab-merge-512k")]
    [InlineData("NT51929", "nt51929-ab-merge", "nt51929-ab-merge-512k")]
    [InlineData("NT51932", "nt51932-ab-merge", "nt51932-ab-merge-512k")]
    public void SupportedProfilesCompileOnlyForTheFixedAbMap(
        string icId,
        string profileId,
        string expectedMapId)
    {
        CompiledComposition composition = CompileProfile(icId, profileId);

        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal(CompiledProfilePromotionStage.Supported, details.Provenance.Promotion.Stage);
        Assert.Empty(details.Provenance.Promotion.Blockers);
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
            "0.2.0",
            icId,
            ExperienceIds.AbMerge,
            requestedMapCapacity: 0x40000);
        Assert.False(wrongCapacity.IsCompiled);
        Assert.Contains(
            wrongCapacity.Issues,
            static issue => StringComparer.Ordinal.Equals(issue.Code, "profile.v2.compile.map-capacity-unavailable"));
    }

    /// <summary>Verifies approved AB profiles cross the Application boundary with typed bindings.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias")]
    [InlineData("NT51929", "nt51929-ab-merge")]
    [InlineData("NT51932", "nt51932-ab-merge")]
    public void SupportedProfilesCreateApplicationRunRequest(string icId, string profileId)
    {
        CompiledComposition composition = CompileProfile(icId, profileId);
        InputArtifactBinding[] bindings =
        [
            .. composition.Plan.RequiredInputAddressSpaceIds.Select(addressSpaceId =>
                CompiledCompositionInputBindingFactory.Create(
                    composition,
                    addressSpaceId,
                    Path.Combine(Path.GetTempPath(), $"{addressSpaceId}.bin"))),
        ];

        var request = new CompositionRunRequest(
            "ab-runtime",
            composition,
            bindings,
            composition.DefaultOutputFileName);

        Assert.Equal(composition, request.CompiledComposition);
        Assert.Equal(3, request.ArtifactBindings.Count);
    }

    /// <summary>Verifies full DP placement, TP overlays, TPB relocation, and immutable TPB source behavior.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias")]
    [InlineData("NT51929", "nt51929-ab-merge")]
    [InlineData("NT51932", "nt51932-ab-merge")]
    public void SupportedPlansProduceTheDeclaredAbLayoutWithoutMutatingInputs(string icId, string profileId)
    {
        CompiledComposition composition = CompileProfile(icId, profileId);
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

    /// <inheritdoc/>
    private static CompiledComposition CompileProfile(string icId, string profileId)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            profileId,
            "0.2.0",
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
            bytes[index] = unchecked((byte)(salt + (index * 37)));
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
