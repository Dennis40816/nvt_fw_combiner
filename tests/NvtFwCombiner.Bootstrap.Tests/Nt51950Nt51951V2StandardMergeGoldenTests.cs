using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Trusted-V2 migration evidence for NT51950/NT51951 DP Perspective Standard Merge.</summary>
public sealed class Nt51950Nt51951V2StandardMergeGoldenTests
{
    private const string BundleDirectory = "nt51950-nt51951-standard-merge";
    private const string BundleContentHash = "25a3005877d7ac29efa9197e43133f9d10265c7ab002aa9f7a82eb873e1bd129";
    private const int TpOverlayStart = 0x0A000;
    private const int TpOverlayLength = 0x2D000;
    private const int CustomerInfoStart = 0x37000;
    private const int CustomerInfoLength = 0x1000;

    /// <summary>Runs the two owner-approved DP Perspective fixtures through the V2 compiler and shared engine.</summary>
    [Theory]
    [InlineData("NT51950", "nt51950-standard-merge-dp-perspective", "51950", "nt51950-standard-merge-dp-perspective.bin")]
    [InlineData("NT51951", "nt51951-standard-merge-dp-perspective", "51951", "nt51951-standard-merge-dp-perspective.bin")]
    public async Task TrustedV2BundleMatchesOwnerApprovedDpPerspectiveGolden(
        string icId,
        string profileId,
        string goldenIc,
        string expectedOutputFileName)
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", BundleDirectory);
        System.Text.Json.JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase(goldenIc);
        Dictionary<string, byte[]> inputs = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expectedOutput = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        long capacity = inputs["dp-input"].LongLength;

        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadCatalog(bundleRoot, BundleContentHash),
            profileId,
            "0.5.1",
            icId,
            capacity);
        CompiledComposition legacy = V2StandardMergeGoldenTestSupport.CompileLegacy(icId, capacity);

        Assert.Equal(expectedOutputFileName, v2.DefaultOutputFileName);
        V2StandardMergeGoldenTestSupport.AssertPlanGeometryAndOperationParity(legacy.Plan, v2.Plan);
        Assert.Equal(capacity, v2.Plan.OutputInitialization.Capacity);
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(result, v2, expectedOutput);
    }

    /// <summary>Verifies every declared DP container capacity retains the legacy byte semantics before promotion by direct golden.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    [InlineData("NT51951", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    [InlineData("NT51951", 0x100000)]
    public async Task TrustedV2BundleMatchesLegacyDpPerspectiveAcrossDeclaredCapacities(
        string icId,
        int capacity)
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", BundleDirectory);
        string profileId = $"nt{icId[2..]}-standard-merge-dp-perspective";
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-input"] = CreatePattern(capacity, 0x31),
            ["tp-input"] = CreatePattern(CustomerInfoStart, 0xC7),
        };
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadCatalog(bundleRoot, BundleContentHash),
            profileId,
            "0.5.1",
            icId,
            capacity);
        CompiledComposition legacy = V2StandardMergeGoldenTestSupport.CompileLegacy(icId, capacity);

        V2StandardMergeGoldenTestSupport.AssertPlanGeometryAndOperationParity(legacy.Plan, v2.Plan);
        CompositionRunResult v2Result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);
        CompositionRunResult legacyResult = await V2StandardMergeGoldenTestSupport.PreviewAsync(legacy, inputs);

        Assert.Equal(CompositionExecutionStatus.Succeeded, v2Result.Status);
        Assert.Equal(CompositionExecutionStatus.Succeeded, legacyResult.Status);
        byte[] v2Output = v2Result.OutputBytes.ToArray();
        Assert.Equal(legacyResult.OutputBytes.ToArray(), v2Output);
        AssertRangeEquals(inputs["tp-input"], TpOverlayStart, v2Output, TpOverlayStart, TpOverlayLength);
        AssertRangeEquals(inputs["dp-input"], CustomerInfoStart, v2Output, CustomerInfoStart, CustomerInfoLength);
    }

    /// <summary>Verifies a TP input longer than the overlay span remains valid through the approved 256 KiB maximum.</summary>
    [Fact]
    public async Task TrustedV2BundleExtractsDeclaredTpOverlayFromInputWithin256KiBMaximum()
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", BundleDirectory);
        byte[] dp = CreatePattern(0x40000, 0x31);
        byte[] tp = CreatePattern(0x3C000, 0xC7);
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-input"] = dp,
            ["tp-input"] = tp,
        };
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadCatalog(bundleRoot, BundleContentHash),
            "nt51950-standard-merge-dp-perspective",
            "0.5.1",
            "NT51950",
            dp.LongLength);

        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] output = result.OutputBytes.ToArray();
        AssertRangeEquals(tp, TpOverlayStart, output, TpOverlayStart, TpOverlayLength);
        AssertRangeEquals(dp, CustomerInfoStart, output, CustomerInfoStart, CustomerInfoLength);
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

    private static void AssertRangeEquals(byte[] expected, int expectedStart, byte[] actual, int actualStart, int length)
    {
        Assert.Equal(
            expected.AsSpan(expectedStart, length).ToArray(),
            actual.AsSpan(actualStart, length).ToArray());
    }
}
