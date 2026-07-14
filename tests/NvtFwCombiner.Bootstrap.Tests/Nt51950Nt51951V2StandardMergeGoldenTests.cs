using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Trusted-V2 migration evidence for NT51950/NT51951 DP Perspective Standard Merge.</summary>
public sealed class Nt51950Nt51951V2StandardMergeGoldenTests
{
    private const string BundleDirectory = "nt51950-nt51951-standard-merge";
    private const string BundleContentHash = "65987f6b1e41feaca92e7b258bca282df9ae133f90db6877ba6b97c04d91f0f4";
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
        System.Text.Json.JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase(goldenIc);
        Dictionary<string, byte[]> inputs = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expectedOutput = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        long capacity = inputs["dp-input"].LongLength;

        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            profileId,
            "0.5.1",
            icId,
            capacity);

        Assert.Equal(expectedOutputFileName, v2.DefaultOutputFileName);
        AssertDeclaredDpPerspectivePlan(v2, capacity);
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(result, v2, expectedOutput);
    }

    /// <summary>Verifies every declared DP container capacity retains the direct V2 byte contract.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    [InlineData("NT51951", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    [InlineData("NT51951", 0x100000)]
    public async Task TrustedV2BundlePreservesDpPerspectiveAcrossDeclaredCapacities(
        string icId,
        int capacity)
    {
        string profileId = $"nt{icId[2..]}-standard-merge-dp-perspective";
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-input"] = CreatePattern(capacity, 0x31),
            ["tp-input"] = CreatePattern(CustomerInfoStart, 0xC7),
        };
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            profileId,
            "0.5.1",
            icId,
            capacity);

        AssertDeclaredDpPerspectivePlan(v2, capacity);
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Report.Issues);
        byte[] output = result.OutputBytes.ToArray();
        Assert.Equal(capacity, output.Length);
        AssertRangeEquals(inputs["tp-input"], TpOverlayStart, output, TpOverlayStart, TpOverlayLength);
        AssertRangeEquals(inputs["dp-input"], CustomerInfoStart, output, CustomerInfoStart, CustomerInfoLength);
    }

    /// <summary>Verifies a TP input longer than the overlay span remains valid through the approved 256 KiB maximum.</summary>
    [Fact]
    public async Task TrustedV2BundleExtractsDeclaredTpOverlayFromInputWithin256KiBMaximum()
    {
        byte[] dp = CreatePattern(0x40000, 0x31);
        byte[] tp = CreatePattern(0x3C000, 0xC7);
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-input"] = dp,
            ["tp-input"] = tp,
        };
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
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

    private static void AssertDeclaredDpPerspectivePlan(CompiledComposition composition, long capacity)
    {
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, composition.Plan.OutputSpaceId);
        Assert.Equal(ImageInitializationKind.Blank, composition.Plan.OutputInitialization.Kind);
        Assert.Equal(capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal((byte)0x00, composition.Plan.OutputInitialization.FillByte);
        AddressSpace dpInput = Assert.Single(composition.Plan.AddressSpaces, static space =>
            space.AddressSpaceId == CompositionAddressSpaceIds.DpInput);
        Assert.Equal(capacity, dpInput.Length);
        Assert.Equal(AddressSpaceMutability.Immutable, dpInput.Mutability);
        Assert.Contains(composition.Plan.AddressSpaces, static space =>
            space.AddressSpaceId == CompositionAddressSpaceIds.TpInput &&
            space.Mutability == AddressSpaceMutability.Immutable);

        CompositionOperation[] operations = [.. composition.Plan.OrderedOperations];
        Assert.Equal(["copy-dp-container", "overlay-tp"], operations.Select(static operation => operation.OperationId));
        Assert.Equal(new ByteRange(0, capacity), operations[0].SourceRange);
        Assert.Equal(new ByteRange(0, capacity), operations[0].TargetRange);
        Assert.Equal(OverlapPolicy.Reject, operations[0].OverlapPolicy);
        Assert.Equal(DpPerspectiveCatalog.TpOverlayRange, operations[1].SourceRange);
        Assert.Equal(DpPerspectiveCatalog.TpOverlayRange, operations[1].TargetRange);
        Assert.Equal(OverlapPolicy.ReplaceExisting, operations[1].OverlapPolicy);
        Assert.DoesNotContain(operations, static operation =>
            operation.TargetRange == DpPerspectiveCatalog.CustomerInfoRange);
    }

    private static void AssertRangeEquals(byte[] expected, int expectedStart, byte[] actual, int actualStart, int length)
    {
        Assert.Equal(
            expected.AsSpan(expectedStart, length).ToArray(),
            actual.AsSpan(actualStart, length).ToArray());
    }
}
