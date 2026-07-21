using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Proves newly closed runtime plans compile only their declared topology and byte authority.</summary>
public sealed class CtrlRamV2PlanClosureProfileTests
{
    private const string Nt51929BundleHash = "1f5a9f05d632b2b792d7a25a034305a0d71569f24b53ba058cc1d5b0f627710e";
    private const string Nt51928BundleHash = "bba0e65221aff3ebbd4b06f83f38295b6e315eff0741fe68952e5844ae64c634";
    private const string Nt51931BundleHash = "2ae7169f047bc016e6d82f967981b72876efb58c4b59c39f7a86cf7428931300";
    private const string Nt51932BundleHash = "7530d67111fdf3c93c1ae934f2ca0c903bfed35b2f4ab351eea18f0a5a58f3cd";
    private const string Nt51950BundleHash = "dc5031993636feb26a60ff96e3517da2fb982f39b83e92724c73dd1df8cf7b16";
    private const string Nt51951BundleHash = "497d99edcfc9ef03cd3d28e3dd7bf821a8db0c9cda5e1cab7aba18fb8d8f8bbd";

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
            profileVersion: "0.3.0");
        CompiledComposition nt51951 = Compile(
            "nt51951-ctrlram-replace-candidate",
            Nt51951BundleHash,
            "nt51951-ctrlram-replace-fw1x-cascade",
            "NT51951",
            chipCount: 2,
            targetStart: 0x33200,
            targetLength: 0x1400,
            referenceCapacity: 0x80000,
            profileVersion: "0.3.0");

        Assert.Equal(0x40000, nt51950.V2Details!.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal(0x80000, nt51951.V2Details!.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal(
            Processor(nt51950).AllowedWriteRanges,
            Processor(nt51951).AllowedWriteRanges);
        Assert.Contains(new ByteRange(0x33200, 0x1400), Processor(nt51950).AllowedWriteRanges);
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
