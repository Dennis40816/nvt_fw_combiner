using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Migration evidence for the supported NT51950/NT51951 V2 DP Replace plans.</summary>
public sealed class Nt51950Nt51951V2DpReplaceProfileTests
{
    private const string BundleDirectory = "nt51950-nt51951-standard-merge";
    private const string BundleContentHash = "25a3005877d7ac29efa9197e43133f9d10265c7ab002aa9f7a82eb873e1bd129";
    private const int TpOverlayStart = 0x0A000;
    private const int TpOverlayLength = 0x2D000;
    private const int CustomerInfoStart = 0x37000;
    private const int CustomerInfoLength = 0x1000;

    /// <summary>Verifies every public synthetic case retains legacy plan and engine byte semantics with static expected hashes.</summary>
    [Theory]
    [MemberData(nameof(PublicSyntheticCases))]
    public void SupportedProfilePlanMatchesLegacyDpReplaceAcrossDeclaredCapacities(
        string icId,
        int capacity,
        int replacementLength,
        byte baseSalt,
        byte replacementSalt,
        string expectedSha256)
    {
        CompiledComposition candidate = CompileSupportedProfile(icId, capacity);
        CompiledComposition legacy = CompileLegacy(icId, capacity);

        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, candidate.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(candidate.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(candidate.V2Details);
        Assert.Equal(CompiledProfilePromotionStage.Supported, details.Provenance.Promotion.Stage);
        Assert.Empty(details.Provenance.Promotion.Blockers);
        Assert.Equal(CompiledIcNumberPolicy.SingleSelector, candidate.IcNumberPolicy);
        Assert.Equal($"nt{icId[2..]}-dp-replace.bin", candidate.DefaultOutputFileName);
        AssertMapProtection(candidate);
        AssertPlanParity(legacy.Plan, candidate.Plan);

        byte[] reference = CreatePattern(capacity, baseSalt);
        byte[] replacement = CreatePattern(replacementLength, replacementSalt);
        CompositionExecutionResult candidateExecution = CompositionEngine.Execute(
            candidate.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
                [CompositionAddressSpaceIds.DpReplacement] = replacement,
            }));
        CompositionExecutionResult legacyExecution = CompositionEngine.Execute(
            legacy.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
                [CompositionAddressSpaceIds.DpReplacement] = replacement,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, candidateExecution.Status);
        Assert.Equal(CompositionExecutionStatus.Succeeded, legacyExecution.Status);
        byte[] candidateOutput = candidateExecution.OutputBytes.ToArray();
        byte[] legacyOutput = legacyExecution.OutputBytes.ToArray();
        Assert.Equal(expectedSha256, Sha256Hex(legacyOutput));
        Assert.Equal(legacyOutput, candidateOutput);
        Assert.Equal(expectedSha256, Sha256Hex(candidateOutput));
        AssertRangeEquals(reference, TpOverlayStart, candidateOutput, TpOverlayStart, TpOverlayLength);
        Assert.Equal(ReplacementOrPadding(replacement, CustomerInfoStart), candidateOutput[CustomerInfoStart]);
        Assert.Equal(
            ReplacementOrPadding(replacement, CustomerInfoStart + CustomerInfoLength - 1),
            candidateOutput[CustomerInfoStart + CustomerInfoLength - 1]);
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

    /// <summary>Locks the supported profile evidence reference to the owner-approved public legacy comparison record.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    public void SupportedProfileReferencesFrozenLegacyParityEvidence(string icId, int capacity)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "public-synthetic",
            "dp-replace",
            "nt51950-nt51951-dp-replace-oracle-v1.json")));
        string evidenceId = document.RootElement
            .GetProperty("ownerApprovedLegacyComparison")
            .GetProperty("evidenceId")
            .GetString()!;
        CompiledComposition composition = CompileSupportedProfile(icId, capacity);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);

        Assert.Equal("dp-replace-owner-approved-legacy-comparison-v1", evidenceId);
        Assert.Contains(evidenceId, details.Provenance.ProfileEvidenceRefs);
    }

    private static CompiledComposition CompileSupportedProfile(string icId, int capacity)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(BundleDirectory, BundleContentHash),
            $"nt{icId[2..]}-dp-replace-dp-perspective",
            "0.6.1",
            icId,
            ExperienceIds.DpReplace,
            capacity);

        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static CompiledComposition CompileLegacy(string icId, int capacity)
    {
        ProfileCompileResult compilation = CompositionProfileCompiler.Compile(
            BuiltInReplaceProfiles.CreateDpPerspectiveDpReplaceProfile(icId, capacity),
            []);

        Assert.True(compilation.IsSuccess, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static void AssertPlanParity(CompositionPlan legacy, CompositionPlan candidate)
    {
        Assert.Equal(legacy.OutputSpaceId, candidate.OutputSpaceId);
        Assert.Equal(legacy.OutputInitialization.Kind, candidate.OutputInitialization.Kind);
        Assert.Equal(legacy.OutputInitialization.ReferenceSpaceId, candidate.OutputInitialization.ReferenceSpaceId);
        Assert.Equal(legacy.OutputInitialization.Capacity, candidate.OutputInitialization.Capacity);

        AddressSpace[] legacySpaces = [.. legacy.AddressSpaces.OrderBy(static space => space.AddressSpaceId)];
        AddressSpace[] candidateSpaces = [.. candidate.AddressSpaces.OrderBy(static space => space.AddressSpaceId)];
        Assert.Equal(legacySpaces.Length, candidateSpaces.Length);
        foreach ((AddressSpace legacySpace, AddressSpace candidateSpace) in legacySpaces.Zip(candidateSpaces))
        {
            Assert.Equal(legacySpace.AddressSpaceId, candidateSpace.AddressSpaceId);
            Assert.Equal(legacySpace.Length, candidateSpace.Length);
            Assert.Equal(legacySpace.Mutability, candidateSpace.Mutability);
            Assert.Equal(legacySpace.InputPaddingByte, candidateSpace.InputPaddingByte);
            Assert.Equal(legacySpace.InputOversizePolicy, candidateSpace.InputOversizePolicy);
            Assert.Equal(legacySpace.AllowedInputLengths, candidateSpace.AllowedInputLengths);
            Assert.Equal(legacySpace.ExpectedInputLengths, candidateSpace.ExpectedInputLengths);
        }

        Assert.Equal(legacy.OrderedOperations.Count, candidate.OrderedOperations.Count);
        for (int index = 0; index < legacy.OrderedOperations.Count; index++)
        {
            CompositionOperation legacyOperation = legacy.OrderedOperations[index];
            CompositionOperation candidateOperation = candidate.OrderedOperations[index];
            Assert.Equal(legacyOperation.OperationId, candidateOperation.OperationId);
            Assert.Equal(legacyOperation.Sequence, candidateOperation.Sequence);
            Assert.Equal(legacyOperation.Kind, candidateOperation.Kind);
            Assert.Equal(legacyOperation.SourceSpaceId, candidateOperation.SourceSpaceId);
            Assert.Equal(legacyOperation.SourceRange, candidateOperation.SourceRange);
            Assert.Equal(legacyOperation.TargetSpaceId, candidateOperation.TargetSpaceId);
            Assert.Equal(legacyOperation.TargetRange, candidateOperation.TargetRange);
            Assert.Equal(legacyOperation.OverlapPolicy, candidateOperation.OverlapPolicy);
        }
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

    /// <summary>Loads the static public synthetic cases that constrain both legacy and V2 execution.</summary>
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

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static byte ReplacementOrPadding(byte[] replacement, int offset)
    {
        return offset < replacement.Length ? replacement[offset] : (byte)0;
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
