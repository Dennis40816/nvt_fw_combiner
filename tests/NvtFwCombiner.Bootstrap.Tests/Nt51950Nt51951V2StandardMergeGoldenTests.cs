using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Trusted-V2 migration evidence for NT51950/NT51951 DP Perspective Standard Merge.</summary>
public sealed class Nt51950Nt51951V2StandardMergeGoldenTests
{
    private const string BundleDirectory = "nt51950-nt51951-standard-merge";
    private const string BundleContentHash = "17b45d347a18078e522b412d7c4c3000f01be4e81e091167cc5c78299988da49";
    private const int TpOverlayStart = 0x0A000;
    private const int TpOverlayLength = 0x2D000;
    private const int CustomerInfoStart = 0x37000;

    /// <summary>Locks the omitted 2026-07-17 owner single package to the canonical NT51950 Standard Merge route.</summary>
    [Fact]
    public async Task OwnerIntakeSingleReconstructsFinalFlashCodeFromDpAndTpAsync()
    {
        const string caseId = "nt51950-fw200-single-auto-prj-676-20260717";
        const string expectedSha256 = "ccda75d0aa08540e293f9ab4a8058c43c4e39d2dd0238238848a2f13df68e38e";
        string root = CanonicalGoldenTestData.Root;
        System.Text.Json.JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            caseId);
        System.Text.Json.JsonElement[] payloads = [
            .. goldenCase.GetProperty("artifacts").EnumerateArray(),
        ];
        string PathForRole(string role)
        {
            System.Text.Json.JsonElement entry = Assert.Single(
                payloads,
                item => StringComparer.Ordinal.Equals(item.GetProperty("sourceRole").GetString(), role));
            return RepositoryPaths.ManifestPath(root, entry);
        }

        using var workspace = TempWorkspace.Create("nfc-nt51950-owner-standard-merge");
        string outputPath = workspace.PathFor("nt51950-standard-merge.bin");
        CompositionRunResult result = await StandardMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            "NT51950",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = PathForRole("standard-merge-dp-input"),
                [CompositionAddressSpaceIds.TpInput] = PathForRole("standard-merge-tp-input"),
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal("nt51950-standard-merge-dp-perspective", ReadProfileId(CompositionRunReportJson.Serialize(result)));
        byte[] expected = File.ReadAllBytes(PathForRole("expected-final-output"));
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(expectedSha256, result.OutputSha256);
        Assert.Equal(expectedSha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expected)).ToLowerInvariant());
        Assert.Equal(expected, output);
    }

    /// <summary>Runs the two owner-approved DP Perspective fixtures through the V2 compiler and shared engine.</summary>
    [Theory]
    [InlineData("NT51950", "nt51950-standard-merge-dp-perspective", "51950", "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin")]
    [InlineData("NT51951", "nt51951-standard-merge-dp-perspective", "51951", "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin")]
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
            "0.8.0",
            icId,
            capacity);

        Assert.Equal(expectedOutputFileName, v2.V2Details.OutputNamingRequirement.FileNameTemplate);
        AssertDeclaredDpPerspectivePlan(v2.Plan, capacity);
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(result, v2, expectedOutput);
    }

    /// <summary>
    /// Uses synthetic inputs and a complete independently constructed output oracle;
    /// this is not direct owner Golden evidence.
    /// </summary>
    [Theory]
    [InlineData("NT51950", 0x40000, "10047d065c9f0ad0d7450787cce779768d686132a21c199feda13d021bba3ce6")]
    [InlineData("NT51950", 0x80000, "32d6f68d4796d4253d0ca6f3fab398deda0abf8b2853de0117888635f14c6670")]
    [InlineData("NT51950", 0x100000, "7426263628547e127463f6e408dc869281cac90131dcb5d26ca4889158c9b24f")]
    [InlineData("NT51951", 0x40000, "10047d065c9f0ad0d7450787cce779768d686132a21c199feda13d021bba3ce6")]
    [InlineData("NT51951", 0x80000, "32d6f68d4796d4253d0ca6f3fab398deda0abf8b2853de0117888635f14c6670")]
    [InlineData("NT51951", 0x100000, "7426263628547e127463f6e408dc869281cac90131dcb5d26ca4889158c9b24f")]
    public async Task SyntheticInputsMatchIndependentlyConstructedDpPerspectiveOutputAcrossCapacities(
        string icId,
        int capacity,
        string expectedSha256)
    {
        string profileId = $"nt{icId[2..]}-standard-merge-dp-perspective";
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-input"] = CreatePattern(capacity, 0x31),
            ["tp-input"] = CreatePattern(CustomerInfoStart, 0xC7),
        };
        WriteSyntheticTpBackup(inputs["tp-input"]);
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            profileId,
            "0.8.0",
            icId,
            capacity);

        AssertDeclaredDpPerspectivePlan(v2.Plan, capacity);
        CompositionRunResult v2Result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        Assert.Equal(CompositionExecutionStatus.Succeeded, v2Result.Status);
        byte[] expected = ConstructDpPerspectiveOutput(inputs["dp-input"], inputs["tp-input"]);
        Assert.Equal(expectedSha256, Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(expected)).ToLowerInvariant());
        Assert.Equal(expected, v2Result.OutputBytes.ToArray());
        Assert.Equal(expectedSha256, v2Result.OutputSha256);
    }

    /// <summary>
    /// Uses a synthetic TP input within the approved maximum and a complete independently
    /// constructed output oracle.
    /// </summary>
    [Fact]
    public async Task SyntheticTpInputWithinMaximumMatchesIndependentlyConstructedDpPerspectiveOutput()
    {
        byte[] dp = CreatePattern(0x40000, 0x31);
        byte[] tp = CreatePattern(0x3C000, 0xC7);
        WriteSyntheticTpBackup(tp);
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-input"] = dp,
            ["tp-input"] = tp,
        };
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            "nt51950-standard-merge-dp-perspective",
            "0.8.0",
            "NT51950",
            dp.LongLength);

        AssertDeclaredDpPerspectivePlan(v2.Plan, dp.LongLength);
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(ConstructDpPerspectiveOutput(dp, tp), result.OutputBytes.ToArray());
    }

    private static void WriteSyntheticTpBackup(byte[] tp)
    {
        // The Backup ends at the layout-declared NVT end flag [0x36FFC, 0x37000) inside the TP overlay (ADR 0076),
        // so the independently constructed output carries it.
        tp[0x36000] = 0x81;
        tp[0x36001] = 0x7E;
        tp[0x36017] = 1;
        "\0NVT"u8.CopyTo(tp.AsSpan(0x36FFC));
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

    private static void AssertDeclaredDpPerspectivePlan(CompositionPlan plan, long capacity)
    {
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, plan.OutputSpaceId);
        Assert.Equal(ImageInitializationKind.Blank, plan.OutputInitialization.Kind);
        Assert.Equal(capacity, plan.OutputInitialization.Capacity);
        Assert.Equal(0, plan.OutputInitialization.FillByte);
        Assert.Equal(
            ["copy-dp-container", "overlay-tp"],
            plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.Equal([100, 200], plan.OrderedOperations.Select(static operation => operation.Sequence));

        CompositionOperation dpCopy = plan.OrderedOperations[0];
        Assert.Equal(CompositionOperationKind.CopyRange, dpCopy.Kind);
        Assert.Equal("dp-input", dpCopy.SourceSpaceId);
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, dpCopy.TargetSpaceId);
        Assert.Equal(new ByteRange(0, capacity), dpCopy.SourceRange);
        Assert.Equal(dpCopy.SourceRange, dpCopy.TargetRange);
        Assert.Equal(OverlapPolicy.Reject, dpCopy.OverlapPolicy);

        CompositionOperation tpOverlay = plan.OrderedOperations[1];
        var tpOverlayRange = ByteRange.FromStartEndExclusive(TpOverlayStart, CustomerInfoStart);
        Assert.Equal(CompositionOperationKind.CopyRange, tpOverlay.Kind);
        Assert.Equal("tp-input", tpOverlay.SourceSpaceId);
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, tpOverlay.TargetSpaceId);
        Assert.Equal(tpOverlayRange, tpOverlay.SourceRange);
        Assert.Equal(tpOverlayRange, tpOverlay.TargetRange);
        Assert.Equal(OverlapPolicy.ReplaceExisting, tpOverlay.OverlapPolicy);
    }

    private static byte[] ConstructDpPerspectiveOutput(byte[] dp, byte[] tp)
    {
        byte[] expected = [.. dp];
        tp.AsSpan(TpOverlayStart, TpOverlayLength).CopyTo(
            expected.AsSpan(TpOverlayStart, TpOverlayLength));
        return expected;
    }

    private static string ReadProfileId(string reportJson)
    {
        using var report = System.Text.Json.JsonDocument.Parse(reportJson);
        return report.RootElement.GetProperty("ProfileId").GetString()!;
    }
}
