using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Proves newly closed runtime plans compile only their declared topology and byte authority.</summary>
public sealed class CtrlRamV2PlanClosureProfileTests
{
    private const string Nt51923BundleHash = "8c1318f9e83a658028b1e0a07b2c38a28bcdeb6031d3a393d6b4912c2cdba14f";
    private const string Nt51926BundleHash = "25d5adc9697eacedcf238835da197b0359c41f8cc6d82110c181496038469529";
    private const string Nt51929BundleHash = "ea9cf1fe05a1462ddff67ece4a037757375100b67d91da3eb1eac1dd0417a4a5";
    private const string Nt51928BundleHash = "bba0e65221aff3ebbd4b06f83f38295b6e315eff0741fe68952e5844ae64c634";
    private const string Nt51932BundleHash = "9a2c69c1b4bc4b5c047b9534c12f3e03b6be5492c9aa26eb626c9a657d101daf";
    private const string Nt51950BundleHash = "d3f745c68d948e7e3a3a07d5717de2114742f881444076d93d2232343f98049e";
    private const string Nt51951BundleHash = "f48429f505f71fbe7c258780dc1ef848c1d9a402d79906c1e24b3a1097192728";

    /// <summary>Normal-header profiles grant every owner-classified CRC word and no surrounding gap bytes.</summary>
    [Theory]
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
            targetLength: 1,
            profileVersion: "0.3.0");
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
        "0.4.0")]
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
        "0.4.0")]
    [InlineData(
        "nt51932-ctrlram-replace-candidate",
        Nt51932BundleHash,
        "nt51932-ctrlram-replace-fw200-cascade",
        "NT51932",
        3,
        0x1FC00,
        0x7128,
        0x1C,
        0x40000,
        "0.4.0")]
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
        "0.5.0")]
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
        "0.5.0")]
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
            targetLength: 0x8C00,
            profileVersion: "0.4.0");

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

    /// <summary>NT51932 single routes compile without their cascade-only DiffDLM ranges.</summary>
    [Theory]
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
            profileVersion: "0.5.0");
        CompiledComposition nt51951 = Compile(
            "nt51951-ctrlram-replace-candidate",
            Nt51951BundleHash,
            "nt51951-ctrlram-replace-fw1x-cascade",
            "NT51951",
            chipCount: 2,
            targetStart: 0x33200,
            targetLength: 0x1400,
            referenceCapacity: 0x80000,
            profileVersion: "0.5.0");

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
        "0.5.0",
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
        "0.5.0",
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
            [new FirmwareArtifactPayload(
                "reference-base",
                ResolutionReference(icId, chipCount, referenceCapacity))],
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

    private static byte[] ResolutionReference(
        string icId,
        int chipCount,
        long capacity)
    {
        byte[] bytes = new byte[checked((int)capacity)];
        bytes[23] = checked((byte)chipCount);
        int markerOffset = StringComparer.Ordinal.Equals(icId, "NT51926")
            ? 0x3BFFC
            : 0x0FFC;
        bytes[markerOffset] = 0x00;
        bytes[markerOffset + 1] = 0x4E;
        bytes[markerOffset + 2] = 0x56;
        bytes[markerOffset + 3] = 0x54;

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
