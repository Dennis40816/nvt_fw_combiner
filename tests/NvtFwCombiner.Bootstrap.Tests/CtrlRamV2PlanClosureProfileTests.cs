using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Proves newly closed runtime plans compile only their declared topology and byte authority.</summary>
public sealed class CtrlRamV2PlanClosureProfileTests
{
    private const string Nt51920BundleHash = "7394b8c650200fd6bb608312f1bf5177c5f1edf21cf9d485c7d5d5406d8a1b06";
    private const string Nt51923BundleHash = "a98432cdd049fde26a381534d9555b68671ef5e71604209a16b24972ad4b0cd1";
    private const string Nt51926BundleHash = "866e2d0ece6d7d6761ea16b0a2a7f607edb9a338827634a0fdc60b36ecee4dd7";
    private const string Nt51929BundleHash = "a60c51ec6a15ef32f91029bf31fca225cbb7f0081c2ee1d760cb8df2294cf74a";
    private const string Nt51928BundleHash = "bba0e65221aff3ebbd4b06f83f38295b6e315eff0741fe68952e5844ae64c634";
    private const string Nt51930BundleHash = "33d81fed0864ec319b04c4fd1442b33a4891ecbee2e54ddab179304778c06d48";
    private const string Nt51931BundleHash = "c52307476cd0df8ba4edc79b5882ca91313f265259290749e856dd8b130abe3d";
    private const string Nt51932BundleHash = "273bc2e02812a7ef60dc0a234083316466b25205bd93ebc8e1862b4c35e26603";
    private const string Nt51950BundleHash = "7dc48be0c50c94b97b208fbbc87666d71ce84b601a5af19c592155428cebff4b";
    private const string Nt51951BundleHash = "20fc2016d43941a83fdc8403249384e43008d5b5087c03104eb9e847e6787e81";

    /// <summary>Normal-header profiles grant every owner-classified CRC word and no surrounding gap bytes.</summary>
    [Theory]
    [InlineData(
        "nt51920-ctrlram-replace-candidate",
        Nt51920BundleHash,
        "nt51920-ctrlram-replace-fw120-single",
        "NT51920",
        1,
        0x22780)]
    [InlineData(
        "nt51920-ctrlram-replace-candidate",
        Nt51920BundleHash,
        "nt51920-ctrlram-replace-fw120-cascade2",
        "NT51920",
        2,
        0x22780)]
    [InlineData(
        "nt51923-ctrlram-replace-candidate",
        Nt51923BundleHash,
        "nt51923-ctrlram-replace-fw141-single",
        "NT51923",
        1,
        0x22800)]
    [InlineData(
        "nt51923-ctrlram-replace-candidate",
        Nt51923BundleHash,
        "nt51923-ctrlram-replace-fw141-cascade3",
        "NT51923",
        3,
        0x22800)]
    [InlineData(
        "nt51926-ctrlram-replace-candidate",
        Nt51926BundleHash,
        "nt51926-ctrlram-replace-fw200-runtime-single",
        "NT51926",
        1,
        0x22800)]
    [InlineData(
        "nt51926-ctrlram-replace-candidate",
        Nt51926BundleHash,
        "nt51926-ctrlram-replace-fw200-runtime-cascade",
        "NT51926",
        3,
        0x22800)]
    public void NormalHeaderProfilesCompileExactCrcWordAuthority(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string icId,
        int chipCount,
        long targetStart)
    {
        CompiledComposition composition = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart,
            targetLength: 1);
        IReadOnlyList<ByteRange> allowedWrites = Processor(composition).AllowedWriteRanges;

        ByteRange[] expectedHeaderWords =
        [
            new(0x18, 4),
            new(0x1C, 4),
            new(0x3C, 4),
            new(0x4C, 4),
            new(0x5C, 4),
            new(0xFC, 4),
        ];
        Assert.All(expectedHeaderWords, range => Assert.Contains(range, allowedWrites));
        Assert.DoesNotContain(allowedWrites, static range => range == new ByteRange(0x20, 0x1C));
        Assert.DoesNotContain(allowedWrites, static range => range == new ByteRange(0x40, 0x20));
    }

    /// <summary>Cascade profiles grant only their workbook-defined DLM CRC span.</summary>
    [Theory]
    [InlineData(
        "nt51929-ctrlram-replace-candidate",
        Nt51929BundleHash,
        "nt51919-ctrlram-replace-fw1x-cascade",
        "NT51919",
        2,
        0x2D100,
        0x7128,
        0x1C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51929-ctrlram-replace-candidate",
        Nt51929BundleHash,
        "nt51929-ctrlram-replace-fw1x-cascade",
        "NT51929",
        8,
        0x2D100,
        0x7128,
        0x1C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51930-ctrlram-replace-candidate",
        Nt51930BundleHash,
        "nt51930-ctrlram-replace-fw130-cascade3",
        "NT51930",
        3,
        0x21650,
        0x7128,
        0x30,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51931-ctrlram-replace-candidate",
        Nt51931BundleHash,
        "nt51931-ctrlram-replace-fw130-cascade6",
        "NT51931",
        6,
        0x16800,
        0x6C,
        0x4C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51932-ctrlram-replace-candidate",
        Nt51932BundleHash,
        "nt51932-ctrlram-replace-fw200-cascade3",
        "NT51932",
        3,
        0x1FC00,
        0x7128,
        0x1C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51950-ctrlram-replace-candidate",
        Nt51950BundleHash,
        "nt51950-ctrlram-replace-fw1x-cascade",
        "NT51950",
        2,
        0x33200,
        0xA134,
        0x4C,
        0x40000,
        "0.4.0")]
    [InlineData(
        "nt51951-ctrlram-replace-candidate",
        Nt51951BundleHash,
        "nt51951-ctrlram-replace-fw1x-cascade",
        "NT51951",
        2,
        0x33200,
        0xA134,
        0x4C,
        0x80000,
        "0.4.0")]
    public void CascadeProfilesCompileDlmCrcAuthority(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string icId,
        int chipCount,
        long targetStart,
        long crcStart,
        long crcLength,
        long referenceCapacity,
        string profileVersion)
    {
        CompiledComposition composition = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart,
            targetLength: 1,
            referenceCapacity,
            profileVersion: profileVersion);

        Assert.Contains(new ByteRange(crcStart, crcLength), Processor(composition).AllowedWriteRanges);
    }

    /// <summary>Single profiles do not inherit cascade-only DLM CRC authority from their family map.</summary>
    [Theory]
    [InlineData(
        "nt51929-ctrlram-replace-candidate",
        Nt51929BundleHash,
        "nt51919-ctrlram-replace-fw200-single",
        "NT51919",
        0x1FC00,
        0x7128,
        0x1C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51929-ctrlram-replace-candidate",
        Nt51929BundleHash,
        "nt51929-ctrlram-replace-fw200-single",
        "NT51929",
        0x1FC00,
        0x7128,
        0x1C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51930-ctrlram-replace-candidate",
        Nt51930BundleHash,
        "nt51930-ctrlram-replace-fw1x-runtime-single",
        "NT51930",
        0x1FC00,
        0x7128,
        0x30,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51931-ctrlram-replace-candidate",
        Nt51931BundleHash,
        "nt51931-ctrlram-replace-fw1x-single",
        "NT51931",
        0x16800,
        0x6C,
        0x4C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51932-ctrlram-replace-candidate",
        Nt51932BundleHash,
        "nt51932-ctrlram-replace-fw1x-single",
        "NT51932",
        0x1FC00,
        0x7128,
        0x1C,
        0x40000,
        "0.2.0")]
    [InlineData(
        "nt51950-ctrlram-replace-candidate",
        Nt51950BundleHash,
        "nt51950-ctrlram-replace-fw200-single",
        "NT51950",
        0x22C00,
        0xA134,
        0x4C,
        0x40000,
        "0.3.0")]
    [InlineData(
        "nt51951-ctrlram-replace-candidate",
        Nt51951BundleHash,
        "nt51951-ctrlram-replace-fw200-single",
        "NT51951",
        0x22C00,
        0xA134,
        0x4C,
        0x80000,
        "0.3.0")]
    public void SingleProfilesExcludeCascadeDlmCrcAuthority(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string icId,
        long targetStart,
        long crcStart,
        long crcLength,
        long referenceCapacity,
        string profileVersion)
    {
        CompiledComposition composition = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount: 1,
            targetStart,
            targetLength: 1,
            referenceCapacity,
            profileVersion: profileVersion);

        Assert.DoesNotContain(
            Processor(composition).AllowedWriteRanges,
            range => range.Overlaps(new ByteRange(crcStart, crcLength)));
    }

    /// <summary>NT51919/NT51929 cascade routes own DiffDLM but not the overlapping single FWConfig backup view.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ctrlram-replace-fw1x-cascade", "nfc.nt51919.ctrlram-postbuild-v1")]
    [InlineData("NT51929", "nt51929-ctrlram-replace-fw1x-cascade", "nfc.nt51929.ctrlram-postbuild-v1")]
    public void Nt51929FamilyCascadeProfilesCompileExactDiffAuthority(
        string icId,
        string profileId,
        string processorId)
    {
        CompiledComposition composition = Compile(
            "nt51929-ctrlram-replace-candidate",
            Nt51929BundleHash,
            profileId,
            icId,
            chipCount: 2,
            targetStart: 0x2D100,
            targetLength: 0x8C00);

        Assert.Equal("nt51929-ctrlram-fw1x-cascade-full-flash", composition.V2Details!.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(processorId, Processor(composition).ProcessorId);
        Assert.Contains(new ByteRange(0x2D100, 0x8C00), Processor(composition).AllowedWriteRanges);
        Assert.Contains(
            composition.V2Details.RegionAccessContract.Requirements,
            static requirement => requirement.RegionId == "diff-ctrlram" &&
                requirement.Access == CompiledRegionAccessKind.ExplicitRange);
        Assert.DoesNotContain(
            composition.V2Details.RegionAccessContract.ResolvedViews,
            static view => view.ViewId == "fw-config-backup-output");
    }

    /// <summary>NT51931/NT51932 single routes compile without their cascade-only DiffDLM ranges.</summary>
    [Theory]
    [InlineData(
        "nt51931-ctrlram-replace-candidate",
        Nt51931BundleHash,
        "NT51931",
        "nt51931-ctrlram-replace-fw1x-single",
        "nfc.nt51931.ctrlram-postbuild-v1",
        0x22800,
        0x17C00,
        0x16800,
        1,
        true)]
    [InlineData(
        "nt51932-ctrlram-replace-candidate",
        Nt51932BundleHash,
        "NT51932",
        "nt51932-ctrlram-replace-fw1x-single",
        "nfc.nt51932.ctrlram-postbuild-v1",
        0x2D100,
        0x8C00,
        0x1FC00,
        1,
        false)]
    public void SingleProfilesCompileWithoutCascadeDiffAuthority(
        string bundleDirectory,
        string bundleHash,
        string icId,
        string profileId,
        string processorId,
        long diffStart,
        long diffLength,
        long targetStart,
        long targetLength,
        bool expectsBackupCopy)
    {
        CompiledComposition composition = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount: 1,
            targetStart: targetStart,
            targetLength: targetLength);
        ExternalProcessorInvocation processor = Processor(composition);

        Assert.Equal(processorId, processor.ProcessorId);
        Assert.DoesNotContain(
            processor.AllowedWriteRanges,
            range => range.Overlaps(new ByteRange(diffStart, diffLength)));
        Assert.DoesNotContain(
            composition.V2Details!.RegionAccessContract.Requirements,
            static requirement => requirement.RegionId == "diff-ctrlram");
        Assert.Equal(
            expectsBackupCopy,
            composition.V2Details.RegionAccessContract.ResolvedViews.Any(
                static view => view.ViewId == "fw-config-backup-copy-output"));
    }

    /// <summary>NT51928 keeps the NT51927 TP plans for all non-NB shapes inside its 512 KiB image.</summary>
    [Theory]
    [InlineData("nt51928-ctrlram-replace-fw141-single", 1, "nt51928-ctrlram-fw141-single-full-flash")]
    [InlineData("nt51928-ctrlram-replace-fw132-twochip", 2, "nt51928-ctrlram-fw132-twochip-full-flash")]
    [InlineData("nt51928-ctrlram-replace-fw140-threechip", 3, "nt51928-ctrlram-fw140-threechip-full-flash")]
    public void Nt51928ProfilesCompileMatchingTpBranchAndPreserve512KiBImage(
        string profileId,
        int chipCount,
        string expectedMapId)
    {
        CompiledComposition composition = Compile(
            "nt51928-ctrlram-replace-candidate",
            Nt51928BundleHash,
            profileId,
            "NT51928",
            chipCount,
            targetStart: 0x16800,
            targetLength: 1,
            referenceCapacity: 0x80000,
            profileVersion: chipCount == 2 ? "0.2.0" : "0.3.0");

        Assert.Equal(expectedMapId, composition.V2Details!.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(0x80000, composition.V2Details.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal("nfc.nt51928.ctrlram-postbuild-v1", Processor(composition).ProcessorId);
        Assert.Contains(
            composition.V2Details.RegionAccessContract.Requirements,
            static requirement => requirement.RegionId == "nf-master" &&
                requirement.Access == CompiledRegionAccessKind.ExplicitRange);
    }

    /// <summary>NT51950 and NT51951 cascade use identical TP authority despite different preserved image tails.</summary>
    [Fact]
    public void Nt51950AndNt51951CascadeCompileSameTpWriteAuthority()
    {
        CompiledComposition nt51950 = Compile(
            "nt51950-ctrlram-replace-candidate",
            Nt51950BundleHash,
            "nt51950-ctrlram-replace-fw1x-cascade",
            "NT51950",
            chipCount: 2,
            targetStart: 0x33200,
            targetLength: 0x1400,
            referenceCapacity: 0x40000,
            profileVersion: "0.4.0");
        CompiledComposition nt51951 = Compile(
            "nt51951-ctrlram-replace-candidate",
            Nt51951BundleHash,
            "nt51951-ctrlram-replace-fw1x-cascade",
            "NT51951",
            chipCount: 2,
            targetStart: 0x33200,
            targetLength: 0x1400,
            referenceCapacity: 0x80000,
            profileVersion: "0.4.0");

        Assert.Equal(0x40000, nt51950.V2Details!.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal(0x80000, nt51951.V2Details!.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal(
            Processor(nt51950).AllowedWriteRanges,
            Processor(nt51951).AllowedWriteRanges);
        Assert.Contains(new ByteRange(0x33200, 0x1400), Processor(nt51950).AllowedWriteRanges);
    }

    /// <summary>TP FW bases stage only the declared 0x37000-byte prefix for NT51950/NT51951.</summary>
    [Theory]
    [InlineData(
        "nt51950-ctrlram-replace-candidate",
        Nt51950BundleHash,
        "nt51950-ctrlram-replace-fw200-single",
        "NT51950",
        1,
        "0.3.0",
        0x22C00,
        "nt51950-ctrlram-fw200-single-tp-work")]
    [InlineData(
        "nt51950-ctrlram-replace-candidate",
        Nt51950BundleHash,
        "nt51950-ctrlram-replace-fw1x-cascade",
        "NT51950",
        2,
        "0.4.0",
        0x33200,
        "nt51950-ctrlram-fw1x-cascade-tp-work")]
    [InlineData(
        "nt51951-ctrlram-replace-candidate",
        Nt51951BundleHash,
        "nt51951-ctrlram-replace-fw200-single",
        "NT51951",
        1,
        "0.3.0",
        0x22C00,
        "nt51951-ctrlram-fw200-single-tp-work")]
    [InlineData(
        "nt51951-ctrlram-replace-candidate",
        Nt51951BundleHash,
        "nt51951-ctrlram-replace-fw1x-cascade",
        "NT51951",
        2,
        "0.4.0",
        0x33200,
        "nt51951-ctrlram-fw1x-cascade-tp-work")]
    public void Nt51950FamilyTpFirmwareBasesCompileWithPrefixOnlyProcessorAuthority(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string icId,
        int chipCount,
        string profileVersion,
        long targetStart,
        string expectedMapId)
    {
        const long tpPrefixLength = 0x37000;
        CompiledComposition composition = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart: targetStart,
            targetLength: 0x1400,
            referenceCapacity: tpPrefixLength,
            profileVersion: profileVersion);

        Assert.Equal(expectedMapId, composition.V2Details!.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(tpPrefixLength, composition.V2Details.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal([new ByteRange(0, tpPrefixLength)], Processor(composition).AllowedReadRanges);
        Assert.Contains(new ByteRange(targetStart, 0x1400), Processor(composition).AllowedWriteRanges);
        Assert.All(
            Processor(composition).AllowedWriteRanges,
            range => Assert.True(range.EndExclusive <= tpPrefixLength));
    }

    private static CompiledComposition Compile(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string icId,
        int chipCount,
        long targetStart,
        long targetLength,
        long referenceCapacity = 0x40000,
        CompiledCompositionEligibility expectedEligibility = CompiledCompositionEligibility.V2PlanCompiled,
        string profileVersion = "0.2.0")
    {
        using var workspace = TempWorkspace.Create("nfc-ctrlram-plan-closure");
        TrustedProfileBundleCatalog catalog = BuiltInProfileMaterializationTestSupport.LoadSourceCandidateCatalog(
            workspace,
            bundleDirectory,
            bundleHash);
        var requestedTopology = new TopologySelection(
            chipCount,
            chipCount == 1 ? "single" : "cascade",
            TopologySelectionSource.Requested,
            "number-selector");
        V2CompositionPlanCompileResult compile = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            catalog,
            profileId,
            profileVersion,
            icId,
            ExperienceIds.CtrlRamReplace,
            requestedTopology,
            [new FirmwareArtifactPayload("reference-base", ResolutionReference(chipCount, referenceCapacity))],
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2RuntimeReferenceReplaceInputBinding("reference-base", "reference-base", referenceCapacity),
                    new V2RuntimeReferenceReplaceInputBinding("ctrlram-source-1", "ctrlram-source", targetLength),
                ],
                [new ExplicitMapping(
                    "replace-ctrlram-byte",
                    1,
                    ExplicitMappingOperationKind.ReplaceRange,
                    "ctrlram-source-1",
                    new ByteRange(0, targetLength),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(targetStart, targetLength),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "Plan-closure contract mapping")]));

        Assert.True(compile.IsCompiled, string.Join(Environment.NewLine, compile.Issues.Select(static issue => issue.Message)));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compile.CompiledComposition);
        Assert.Equal(expectedEligibility, composition.Eligibility);
        Assert.Equal(profileId, composition.ProfileId);
        Assert.Equal(icId, composition.IcId);
        return composition;
    }

    private static byte[] ResolutionReference(int chipCount, long capacity)
    {
        byte[] bytes = new byte[checked((int)capacity)];
        bytes[23] = checked((byte)chipCount);
        bytes[4092] = 0x00;
        bytes[4093] = 0x4E;
        bytes[4094] = 0x56;
        bytes[4095] = 0x54;
        return bytes;
    }

    private static ExternalProcessorInvocation Processor(CompiledComposition composition)
    {
        CompositionOperation operation = Assert.Single(
            composition.Plan.OrderedOperations,
            static candidate => candidate.Kind == CompositionOperationKind.RunExternalProcessor);
        return Assert.IsType<ExternalProcessorInvocation>(operation.ExternalProcessorInvocation);
    }
}
