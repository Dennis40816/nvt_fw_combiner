using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Migration evidence for the supported NT51950/NT51951 V2 DP Replace plans.</summary>
public sealed class Nt51950Nt51951V2DpReplaceProfileTests
{
    private const string BundleDirectory = "nt51950-nt51951-standard-merge";
    private const string BundleContentHash = "56e39af41aaed8abad5da0f49274053ad2fb619949b53efd9497ed31a10ee99b";
    private const int TpOverlayStart = 0x0A000;
    private const int TpOverlayLength = 0x2D000;
    private const int CustomerInfoStart = 0x37000;
    private const int CustomerInfoLength = 0x1000;

    /// <summary>Preserves historical short-input hashes while proving production now rejects every superseded case.</summary>
    [Theory]
    [MemberData(nameof(PublicSyntheticCases))]
    public void SupportedProfileRejectsSupersededShortInputOracleCases(
        string icId,
        int capacity,
        int replacementLength,
        byte baseSalt,
        byte replacementSalt,
        string expectedSha256)
    {
        CompiledComposition candidate = CompileSupportedProfile(icId, capacity);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, candidate.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(candidate.V2Details);
        Assert.Equal(CompiledProfilePromotionStage.Supported, details.Provenance.Promotion.Stage);
        Assert.Empty(details.Provenance.Promotion.Blockers);
        Assert.Equal(CompiledIcNumberPolicy.SingleSelector, candidate.IcNumberPolicy);
        Assert.Equal($"nt{icId[2..]}-dp-replace.bin", candidate.DefaultOutputFileName);
        AssertMapProtection(candidate);
        AssertPlanContract(candidate.Plan, capacity);

        byte[] reference = CreatePattern(capacity, baseSalt);
        byte[] replacement = CreatePattern(replacementLength, replacementSalt);
        Assert.Equal(expectedSha256, Sha256Hex(CreateHistoricalExpectedOutput(reference, replacement)));

        CompositionExecutionResult candidateExecution = CompositionEngine.Execute(
            candidate.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
                [CompositionAddressSpaceIds.DpReplacement] = replacement,
            }));
        Assert.Equal(CompositionExecutionStatus.Failed, candidateExecution.Status);
        Assert.True(candidateExecution.OutputBytes.IsEmpty);
        Assert.Equal(
            CompositionIssueCodes.InputAddressSpaceLengthMismatch,
            Assert.Single(candidateExecution.Issues).Code);
    }

    /// <summary>Uses the two available owner DP Perspective outputs as self-replacement controls without claiming direct replacement-golden parity.</summary>
    [Theory]
    [InlineData("NT51950", "51950")]
    [InlineData("NT51951", "51951")]
    public void SupportedProfileSelfReplacementMatchesAvailableOwnerBaseControl(string icId, string goldenIc)
    {
        JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase(goldenIc);
        byte[] reference = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        byte[] replacement = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"))["dp-input"];
        CompiledComposition candidate = CompileSupportedProfile(icId, checked((int)reference.LongLength));

        CompositionExecutionResult execution = CompositionEngine.Execute(
            candidate.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
                [CompositionAddressSpaceIds.DpReplacement] = replacement,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(reference, execution.OutputBytes.ToArray());
    }

    /// <summary>Both supported ICs execute every owner-approved exact capacity without normalization.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    [InlineData("NT51951", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    [InlineData("NT51951", 0x100000)]
    public void SupportedProfileExecutesEveryExactCapacity(string icId, int capacity)
    {
        CompiledComposition candidate = CompileSupportedProfile(icId, capacity);
        byte[] reference = CreatePattern(capacity, 0x31);
        byte[] replacement = CreatePattern(capacity, 0xA7);
        byte[] expected = [.. replacement];
        reference.AsSpan(TpOverlayStart, TpOverlayLength).CopyTo(expected.AsSpan(TpOverlayStart));

        CompositionExecutionResult execution = CompositionEngine.Execute(
            candidate.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
                [CompositionAddressSpaceIds.DpReplacement] = replacement,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(expected, execution.OutputBytes.ToArray());
    }

    /// <summary>Short, oversized, and cross-capacity replacement pairs fail before producing output.</summary>
    [Theory]
    [MemberData(nameof(InvalidExactPairCases))]
    public void SupportedProfileRejectsEveryNonExactPair(
        string icId,
        int capacity,
        int replacementLength)
    {
        CompiledComposition candidate = CompileSupportedProfile(icId, capacity);
        CompositionExecutionResult execution = CompositionEngine.Execute(
            candidate.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = new byte[capacity],
                [CompositionAddressSpaceIds.DpReplacement] = new byte[replacementLength],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, execution.Status);
        Assert.True(execution.OutputBytes.IsEmpty);
        Assert.Equal(
            CompositionIssueCodes.InputAddressSpaceLengthMismatch,
            Assert.Single(execution.Issues).Code);
    }

    /// <summary>Locks the supported profile to the exact-pair owner decision and archives the superseded legacy comparison.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    public void SupportedProfileReferencesExactPairOwnerDecision(string icId, int capacity)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "public-synthetic",
            "dp-replace",
            "nt51950-nt51951-dp-replace-oracle-v1.json")));
        string evidenceId = document.RootElement
            .GetProperty("productionAdmissionSupersession")
            .GetProperty("evidenceId")
            .GetString()!;
        CompiledComposition composition = CompileSupportedProfile(icId, capacity);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);

        Assert.Equal("dp-replace-exact-pair-owner-decision-20260802", evidenceId);
        Assert.Contains(evidenceId, details.Provenance.ProfileEvidenceRefs);
        Assert.DoesNotContain("dp-replace-owner-approved-legacy-comparison-v1", details.Provenance.ProfileEvidenceRefs);
        Assert.DoesNotContain("dp-replace-synthetic-oracle-v1", details.Provenance.ProfileEvidenceRefs);
    }

    private static CompiledComposition CompileSupportedProfile(string icId, int capacity)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            $"nt{icId[2..]}-dp-replace-dp-perspective",
            "0.8.0",
            icId,
            ExperienceIds.DpReplace,
            capacity);

        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static void AssertPlanContract(CompositionPlan plan, int capacity)
    {
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, plan.OutputSpaceId);
        Assert.Equal(ImageInitializationKind.Reference, plan.OutputInitialization.Kind);
        Assert.Equal(CompositionAddressSpaceIds.ReferenceBase, plan.OutputInitialization.ReferenceSpaceId);
        Assert.Equal(capacity, plan.OutputInitialization.Capacity);

        AddressSpace reference = Assert.Single(plan.AddressSpaces, space => space.AddressSpaceId == CompositionAddressSpaceIds.ReferenceBase);
        Assert.Equal(capacity, reference.Length);
        Assert.Equal(AddressSpaceMutability.Immutable, reference.Mutability);
        AddressSpace replacement = Assert.Single(plan.AddressSpaces, space => space.AddressSpaceId == CompositionAddressSpaceIds.DpReplacement);
        Assert.Equal(capacity, replacement.Length);
        Assert.Equal(AddressSpaceMutability.Immutable, replacement.Mutability);
        Assert.Null(replacement.InputPaddingByte);
        Assert.Equal([capacity], replacement.AllowedInputLengths);
        Assert.Equal(InputOversizePolicy.Reject, replacement.InputOversizePolicy);
        AddressSpace output = Assert.Single(plan.AddressSpaces, space => space.AddressSpaceId == CompositionAddressSpaceIds.OutputImage);
        Assert.Equal(capacity, output.Length);
        Assert.Equal(AddressSpaceMutability.Mutable, output.Mutability);

        CompositionOperation[] operations = [.. plan.OrderedOperations];
        Assert.Equal(
            [
                "replace-dp-container",
                "restore-base-tp",
            ],
            operations.Select(static operation => operation.OperationId));
        Assert.Equal(CompositionOperationKind.ReplaceRange, operations[0].Kind);
        Assert.Equal(100, operations[0].Sequence);
        Assert.Equal(CompositionAddressSpaceIds.DpReplacement, operations[0].SourceSpaceId);
        Assert.Equal(new ByteRange(0, capacity), operations[0].SourceRange);
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, operations[0].TargetSpaceId);
        Assert.Equal(new ByteRange(0, capacity), operations[0].TargetRange);
        Assert.Equal(OverlapPolicy.Reject, operations[0].OverlapPolicy);
        Assert.Equal(CompositionOperationKind.CopyRange, operations[1].Kind);
        Assert.Equal(200, operations[1].Sequence);
        Assert.Equal(CompositionAddressSpaceIds.ReferenceBase, operations[1].SourceSpaceId);
        Assert.Equal(new ByteRange(TpOverlayStart, TpOverlayLength), operations[1].SourceRange);
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, operations[1].TargetSpaceId);
        Assert.Equal(new ByteRange(TpOverlayStart, TpOverlayLength), operations[1].TargetRange);
        Assert.Equal(OverlapPolicy.ReplaceExisting, operations[1].OverlapPolicy);
        Assert.DoesNotContain(
            operations,
            operation => operation.TargetRange == new ByteRange(CustomerInfoStart, CustomerInfoLength));
    }

    private static void AssertMapProtection(CompiledComposition candidate)
    {
        FirmwareImageMap map = Assert.IsType<V2CompiledCompositionDetails>(candidate.V2Details)
            .Provenance.ResolvedMap.ImageMap;
        FirmwareRegion customerInfo = Assert.Single(map.Regions, static region => region.RegionId == "customer-info");
        Assert.Equal(FirmwareWriteConstraint.ExplicitRange, customerInfo.WriteConstraint);
        FirmwareRegion[] unmappedRegions =
        [
            .. map.Regions.Where(static region => region.RegionId is "unmapped-prefix" or "unmapped-suffix"),
        ];
        Assert.Equal(2, unmappedRegions.Length);
        Assert.All(unmappedRegions, static region => Assert.Equal(FirmwareWriteConstraint.Forbidden, region.WriteConstraint));
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

    /// <summary>Loads the static public synthetic cases generated from the archived legacy comparison matrix.</summary>
    public static TheoryData<string, int, int, byte, byte, string> PublicSyntheticCases()
    {
        var cases = new TheoryData<string, int, int, byte, byte, string>();
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "public-synthetic",
            "dp-replace",
            "nt51950-nt51951-dp-replace-oracle-v1.json")));
        JsonElement root = document.RootElement;
        int defaultReplacementLength = root.GetProperty("generator").GetProperty("replacementLengthBytes").GetInt32();

        foreach (JsonElement testCase in root.GetProperty("cases").EnumerateArray())
        {
            cases.Add(
                testCase.GetProperty("icId").GetString()!,
                testCase.GetProperty("capacityBytes").GetInt32(),
                testCase.TryGetProperty("replacementLengthBytes", out JsonElement replacementLength)
                    ? replacementLength.GetInt32()
                    : defaultReplacementLength,
                testCase.GetProperty("baseSalt").GetByte(),
                testCase.GetProperty("replacementSalt").GetByte(),
                testCase.GetProperty("expectedSha256").GetString()!);
        }

        return cases;
    }

    /// <summary>Enumerates both sides of every supported exact-capacity boundary.</summary>
    public static TheoryData<string, int, int> InvalidExactPairCases()
    {
        var cases = new TheoryData<string, int, int>();
        foreach (string icId in new[] { "NT51950", "NT51951" })
        {
            cases.Add(icId, 0x40000, 0x3FFFF);
            cases.Add(icId, 0x40000, 0x80000);
            cases.Add(icId, 0x80000, 0x40000);
            cases.Add(icId, 0x80000, 0x80001);
            cases.Add(icId, 0x100000, 0x80000);
            cases.Add(icId, 0x100000, 0x100001);
        }

        return cases;
    }

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static byte[] CreateHistoricalExpectedOutput(byte[] reference, byte[] replacement)
    {
        byte[] output = new byte[reference.Length];
        replacement.CopyTo(output, 0);
        reference.AsSpan(TpOverlayStart, TpOverlayLength).CopyTo(output.AsSpan(TpOverlayStart));
        return output;
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
