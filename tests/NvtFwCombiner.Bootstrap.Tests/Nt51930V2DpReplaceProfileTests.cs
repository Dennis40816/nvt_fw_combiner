using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Candidate migration evidence for NT51930 DP Replace over its canonical Standard Merge map.</summary>
public sealed class Nt51930V2DpReplaceProfileTests
{
    private const int Capacity = 0x40000;
    private const int DpLength = 0x6000;

    /// <summary>Locks the exact V2 plan, canonical ranges, and complete deterministic output without claiming owner golden parity.</summary>
    [Fact]
    public void ProfileUsesCanonicalMapAndMatchesPublicSyntheticFullOutput()
    {
        CompiledComposition composition = Compile();

        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(composition.Authority);
        Assert.Equal("nt51930-dp-replace-flashmap", composition.ProfileId);
        Assert.Equal(CompiledIcNumberPolicy.SingleSelector, composition.IcNumberPolicy);
        Assert.Equal("nt51930-dp-replace.bin", composition.DefaultOutputFileName);
        AssertPlan(composition);

        using var document = JsonDocument.Parse(File.ReadAllText(OraclePath));
        JsonElement generator = document.RootElement.GetProperty("generator");
        byte[] reference = CreatePattern(Capacity, generator.GetProperty("baseSalt").GetByte());
        byte[] replacement = CreatePattern(Capacity, generator.GetProperty("replacementSalt").GetByte());
        byte[] expected = [.. reference];
        replacement.AsSpan(0, DpLength).CopyTo(expected);
        string expectedHash = document.RootElement
            .GetProperty("expectedRule")
            .GetProperty("expectedSha256")
            .GetString()!;

        Assert.Equal(expectedHash, Sha256Hex(expected));
        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
                [CompositionAddressSpaceIds.DpReplacement] = replacement,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(expected, execution.OutputBytes.ToArray());
        Assert.Equal(expectedHash, Sha256Hex(execution.OutputBytes.Span));
    }

    /// <summary>Uses the existing owner Standard Merge output as a self-replacement control, not as direct DP Replace golden parity.</summary>
    [Fact]
    public void ProfileSelfReplacementMatchesOwnerStandardMergeControl()
    {
        JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase("51930");
        byte[] reference = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        byte[] replacement = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"))["dp-input"];

        CompositionExecutionResult execution = CompositionEngine.Execute(
            Compile().Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
                [CompositionAddressSpaceIds.DpReplacement] = replacement,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(reference, execution.OutputBytes.ToArray());
    }

    /// <summary>Rejects a replacement that cannot cover the complete canonical DP region.</summary>
    [Fact]
    public void ProfileRejectsReplacementShorterThanDeclaredDpRegion()
    {
        CompositionExecutionResult execution = CompositionEngine.Execute(
            Compile().Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = new byte[Capacity],
                [CompositionAddressSpaceIds.DpReplacement] = new byte[DpLength - 1],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, execution.Status);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, Assert.Single(execution.Issues).Code);
    }

    /// <summary>Rejects every reference length except the canonical 256 KiB Standard Merge capacity.</summary>
    [Theory]
    [InlineData(Capacity - 1)]
    [InlineData(Capacity + 1)]
    public void ProfileRejectsNonCanonicalReferenceCapacity(long capacity)
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51930",
            capacity,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Null(composition);
        CompositionIssue issue = Assert.Single(issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, issue.Code);
        Assert.Contains("0x40000", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Projects the profile-specific input shape without reusing DP Perspective padding or TP-restore claims.</summary>
    [Fact]
    public void WorkbenchDescriptionExplainsCanonicalDpExtraction()
    {
        WorkbenchReplaceInputSlot slot = Assert.Single(
            WorkbenchCompositionService.GetReplaceInputSlots("NT51930", "single", WorkbenchReplaceModes.Dp),
            static candidate => candidate.SlotId == WorkbenchSlotIds.ReplaceDp);

        Assert.Contains("0x6000", slot.Description, StringComparison.Ordinal);
        Assert.Contains("0x40000", slot.Description, StringComparison.Ordinal);
        Assert.Contains("every other byte stays from Reference FlashCode", slot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("zero-padded", slot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("TP range is restored", slot.Description, StringComparison.Ordinal);
    }

    private static CompiledComposition Compile()
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51930",
            Capacity,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Empty(issues);
        return Assert.IsType<CompiledComposition>(composition);
    }

    private static void AssertPlan(CompiledComposition composition)
    {
        CompositionPlan plan = composition.Plan;
        Assert.Equal(ImageInitializationKind.Reference, plan.OutputInitialization.Kind);
        Assert.Equal(CompositionAddressSpaceIds.ReferenceBase, plan.OutputInitialization.ReferenceSpaceId);
        Assert.Equal(Capacity, plan.OutputInitialization.Capacity);

        AddressSpace reference = Assert.Single(plan.AddressSpaces, static space =>
            space.AddressSpaceId == CompositionAddressSpaceIds.ReferenceBase);
        Assert.Equal(Capacity, reference.Length);
        Assert.Equal(InputOversizePolicy.Reject, reference.InputOversizePolicy);
        Assert.Empty(reference.AllowedInputLengths);
        Assert.Empty(reference.ExpectedInputLengths);

        AddressSpace replacement = Assert.Single(plan.AddressSpaces, static space =>
            space.AddressSpaceId == CompositionAddressSpaceIds.DpReplacement);
        Assert.Equal(DpLength, replacement.Length);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, replacement.InputOversizePolicy);
        Assert.Empty(replacement.AllowedInputLengths);
        Assert.Equal([Capacity], replacement.ExpectedInputLengths);
        Assert.Equal("DP_SIZE_WARNING", replacement.UnexpectedInputLengthIssueCode);
        Assert.Null(replacement.InputPaddingByte);

        CompositionOperation operation = Assert.Single(plan.OrderedOperations);
        Assert.Equal("replace-dp-code", operation.OperationId);
        Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind);
        Assert.Equal(CompositionAddressSpaceIds.DpReplacement, operation.SourceSpaceId);
        Assert.Equal(new ByteRange(0, DpLength), operation.SourceRange);
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, operation.TargetSpaceId);
        Assert.Equal(new ByteRange(0, DpLength), operation.TargetRange);
        Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy);

        FirmwareImageMap map = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details)
            .Provenance.ResolvedMap.ImageMap;
        Assert.Equal(new ByteRange(0, DpLength), Assert.Single(map.Regions, static region => region.RegionId == "dp-code").Range);
        Assert.Equal(FirmwareWriteConstraint.Forbidden, Assert.Single(map.Regions, static region => region.RegionId == "unmapped-gap").WriteConstraint);
        Assert.Equal(new ByteRange(0x7000, 0x39000), Assert.Single(map.Regions, static region => region.RegionId == "tp-code").Range);
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

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string OraclePath => RepositoryPaths.FromRepositoryRoot(
        "testdata",
        "public-synthetic",
        "dp-replace",
        "nt51930-dp-replace-oracle-v1.json");
}
