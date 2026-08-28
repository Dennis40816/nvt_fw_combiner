using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Proves supported CtrlRAM runtime plans compile only their declared topology and byte authority.</summary>
public sealed partial class CtrlRamV2PlanClosureProfileTests
{
    private const string Nt51917BundleHash = "b4e12745789f522c3007a346d2370161db17a76a5eb94c367fd6c6cf40c2b45b";
    private const string Nt51923BundleHash = "67ff8e3e10a3bb079aa8f25732a189baaa384bdf7554a34fc97c057d6af5af96";
    private const string Nt51926BundleHash = "8a6dc717feeb109ee265122b5796606297f1bbe14e11ec1e8da31d26678c26a7";
    private const string Nt51929BundleHash = "ef145b1fb938bd3ea91227b1d46e83d49c71c5d75fa3ce80b98f21e8f5347979";
    private const string Nt51928BundleHash = "82a7a98f4883540595a3af22887fecef5f283fdff5e2af816b294c6345bd523d";
    private const string Nt51932BundleHash = "47361d7eb2c573f86f9d802f87d0db3ad8e0d41336a937ffe57a2a28239e7f61";
    private const string Nt51950BundleHash = "86c06344e50856d590dc58bb0485de06ec9dcef825076a92a83574a3a0d6b554";
    private const string Nt51951BundleHash = "17380c4dfdc04123ee46504cf626f43365c9d506d798f2c1ada999f14c8d3c4c";
    private const string Nt51927BundleHash = "f44c1b82f3fc38905dee222a60be5b884f717b37cb3d8fafe8affd7c48353714";

    /// <summary>Normal-header profiles grant every owner-classified CRC word and no surrounding gap bytes.</summary>
    [Theory]
    [InlineData(
        "nt51923-ctrlram-replace-candidate",
        Nt51923BundleHash,
        "nt51923-ctrlram-replace-fw141-single",
        "NT51923",
        1,
        0x22800,
        "0.4.0")]
    [InlineData(
        "nt51923-ctrlram-replace-candidate",
        Nt51923BundleHash,
        "nt51923-ctrlram-replace-fw141-cascade3",
        "NT51923",
        3,
        0x22800,
        "0.4.0")]
    [InlineData(
        "nt51926-ctrlram-replace-candidate",
        Nt51926BundleHash,
        "nt51926-ctrlram-replace-fw200-runtime-single",
        "NT51926",
        1,
        0x22800,
        "0.4.0")]
    [InlineData(
        "nt51926-ctrlram-replace-candidate",
        Nt51926BundleHash,
        "nt51926-ctrlram-replace-fw200-runtime-cascade",
        "NT51926",
        3,
        0x22800,
        "0.4.0")]
    public void NormalHeaderProfilesCompileExactCrcWordAuthority(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string icId,
        int chipCount,
        long targetStart,
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
            profileVersion: profileVersion);
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
        "0.5.0")]
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
        "0.5.0")]
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
        "0.5.0")]
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
        "0.7.0")]
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
        "0.7.0")]
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
        "0.3.0")]
    [InlineData(
        "nt51929-ctrlram-replace-candidate",
        Nt51929BundleHash,
        "nt51929-ctrlram-replace-fw200-single",
        "NT51929",
        0x1FC00,
        0x7128,
        0x1C,
        0x40000,
        "0.3.0")]
    [InlineData(
        "nt51932-ctrlram-replace-candidate",
        Nt51932BundleHash,
        "nt51932-ctrlram-replace-fw1x-single",
        "NT51932",
        0x1FC00,
        0x7128,
        0x1C,
        0x40000,
        "0.3.0")]
    [InlineData(
        "nt51950-ctrlram-replace-candidate",
        Nt51950BundleHash,
        "nt51950-ctrlram-replace-fw200-single",
        "NT51950",
        0x22C00,
        0xA134,
        0x4C,
        0x40000,
        "0.5.0")]
    [InlineData(
        "nt51951-ctrlram-replace-candidate",
        Nt51951BundleHash,
        "nt51951-ctrlram-replace-fw200-single",
        "NT51951",
        0x22C00,
        0xA134,
        0x4C,
        0x80000,
        "0.5.0")]
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
            profileVersion: "0.5.0");

        Assert.Equal("nt51929-ctrlram-fw1x-cascade-full-flash", composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(processorId, Processor(composition).ProcessorId);
        Assert.Contains(new ByteRange(0x2D100, 0x8C00), Processor(composition).AllowedWriteRanges);
        Assert.Contains(
            composition.V2Details.RegionAccessContract.Requirements,
            static requirement => requirement.RegionId == "diff-ctrlram" &&
                requirement.Access == RegionAccessKind.ExplicitRange);
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
            composition.V2Details.RegionAccessContract.Requirements,
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
            profileVersion: chipCount == 2 ? "0.3.0" : "0.4.0");

        Assert.Equal(expectedMapId, composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(0x80000, composition.V2Details.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal("nfc.nt51928.ctrlram-postbuild-v1", Processor(composition).ProcessorId);
        Assert.Contains(
            composition.V2Details.RegionAccessContract.Requirements,
            static requirement => requirement.RegionId == "nf-master" &&
                requirement.Access == RegionAccessKind.ExplicitRange);
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
            profileVersion: "0.7.0");
        CompiledComposition nt51951 = Compile(
            "nt51951-ctrlram-replace-candidate",
            Nt51951BundleHash,
            "nt51951-ctrlram-replace-fw1x-cascade",
            "NT51951",
            chipCount: 2,
            targetStart: 0x33200,
            targetLength: 0x1400,
            referenceCapacity: 0x80000,
            profileVersion: "0.7.0");

        Assert.Equal(0x40000, nt51950.V2Details.Provenance.ResolvedMap.CapacityBytes);
        Assert.Equal(0x80000, nt51951.V2Details.Provenance.ResolvedMap.CapacityBytes);
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
        "0.5.0",
        0x22C00,
        "nt51950-ctrlram-fw200-single-tp-work")]
    [InlineData(
        "nt51950-ctrlram-replace-candidate",
        Nt51950BundleHash,
        "nt51950-ctrlram-replace-fw1x-cascade",
        "NT51950",
        2,
        "0.7.0",
        0x33200,
        "nt51950-ctrlram-fw1x-cascade-tp-work")]
    [InlineData(
        "nt51951-ctrlram-replace-candidate",
        Nt51951BundleHash,
        "nt51951-ctrlram-replace-fw200-single",
        "NT51951",
        1,
        "0.5.0",
        0x22C00,
        "nt51951-ctrlram-fw200-single-tp-work")]
    [InlineData(
        "nt51951-ctrlram-replace-candidate",
        Nt51951BundleHash,
        "nt51951-ctrlram-replace-fw1x-cascade",
        "NT51951",
        2,
        "0.7.0",
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

        Assert.Equal(expectedMapId, composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
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
        string profileVersion = "0.3.0")
    {
        V2CompositionPlanCompileResult compile = CompileResult(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart,
            targetLength,
            referenceCapacity,
            profileVersion);

        Assert.True(compile.IsCompiled, string.Join(Environment.NewLine, compile.Issues.Select(static issue => issue.Message)));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compile.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        Assert.Equal(profileId, composition.V2Details.ProfileId);
        Assert.Equal(icId, composition.V2Details.Provenance.Context.MemberId);
        return composition;
    }

    private static V2CompositionPlanCompileResult CompileResult(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string icId,
        int chipCount,
        long targetStart,
        long targetLength,
        long referenceCapacity,
        string profileVersion,
        int? markerOffset = null)
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
        V2CompositionPlanCompileResult compile = catalog.CompileRuntimeReferenceReplace(
            profileId,
            profileVersion,
            icId,
            ExperienceIds.CtrlRamReplace,
            requestedTopology,
            [new FirmwareArtifactPayload(
                "reference-base",
                ResolutionReference(icId, chipCount, referenceCapacity, markerOffset))],
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2ExplicitMappingInputBinding("reference-base", "reference-base", referenceCapacity),
                    new V2ExplicitMappingInputBinding("ctrlram-source-1", "ctrlram-source", targetLength),
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

        return compile;
    }

    private static byte[] ResolutionReference(
        string icId,
        int chipCount,
        long capacity,
        int? markerOffset = null)
    {
        byte[] bytes = new byte[checked((int)capacity)];
        bytes[23] = checked((byte)chipCount);
        int resolvedMarkerOffset = markerOffset ?? (StringComparer.Ordinal.Equals(icId, "NT51926")
            ? 0x3BFFC
            : 0x0FFC);
        bytes[resolvedMarkerOffset] = 0x00;
        bytes[resolvedMarkerOffset + 1] = 0x4E;
        bytes[resolvedMarkerOffset + 2] = 0x56;
        bytes[resolvedMarkerOffset + 3] = 0x54;

        return bytes;
    }

    private static ExternalProcessorInvocation Processor(CompiledComposition composition)
    {
        CompositionOperation operation = Assert.Single(
            composition.Plan.OrderedOperations,
            static candidate => candidate.Kind == CompositionOperationKind.RunExternalProcessor);
        return Assert.IsType<ExternalProcessorInvocation>(operation.ExternalProcessorInvocation);
    }

    private static GoldenArtifact RequireArtifact(
        IEnumerable<GoldenArtifact> artifacts,
        string fileName)
    {
        return Assert.Single(
            artifacts,
            artifact => StringComparer.Ordinal.Equals(artifact.FileName, fileName));
    }

    private static JsonElement RequireArtifactById(JsonElement goldenCase, string artifactId)
    {
        return Assert.Single(
            goldenCase.GetProperty("artifacts").EnumerateArray(),
            artifact => StringComparer.Ordinal.Equals(
                artifact.GetProperty("artifactId").GetString(),
                artifactId));
    }

    private static GoldenArtifact LoadGoldenArtifact(JsonElement entry)
    {
        string path = RepositoryPaths.ManifestPath(CanonicalGoldenTestData.Root, entry);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = entry.GetProperty("sha256").GetString()!;
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(sha256, Hash(bytes));
        return new GoldenArtifact(
            entry.GetProperty("artifactId").GetString()!,
            entry.GetProperty("role").GetString()!,
            entry.GetProperty("originalFileName").GetString()!,
            entry.GetProperty("sourceRole").GetString()!,
            path,
            sha256,
            bytes);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed record GoldenArtifact(
        string ArtifactId,
        string Role,
        string FileName,
        string SourceRole,
        string Path,
        string ManifestSha256,
        byte[] Bytes);
}
