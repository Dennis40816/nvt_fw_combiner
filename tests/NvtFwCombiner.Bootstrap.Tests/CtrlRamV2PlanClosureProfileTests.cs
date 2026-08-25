using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Proves supported CtrlRAM runtime plans compile only their declared topology and byte authority.</summary>
public sealed class CtrlRamV2PlanClosureProfileTests
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

    /// <summary>Every admitted TP/full route selects only its exact capacity map and limits processor authority to the TP prefix.</summary>
    [Theory]
    [InlineData("nt51917-ctrlram-replace-alias-candidate", Nt51917BundleHash, "nt51917-ctrlram-replace-fw141-single", "0.3.0", "NT51917", 1, 0x35000, 0x40000, 0x16800, "nt51927-ctrlram-fw141-single-tp-work-212k", "nt51927-ctrlram-fw141-single-full-flash")]
    [InlineData("nt51917-ctrlram-replace-alias-candidate", Nt51917BundleHash, "nt51917-ctrlram-replace-fw132-twochip", "0.3.0", "NT51917", 2, 0x35000, 0x40000, 0x16800, "nt51927-ctrlram-fw132-twochip-tp-work-212k", "nt51927-ctrlram-fw132-twochip-full-flash")]
    [InlineData("nt51917-ctrlram-replace-alias-candidate", Nt51917BundleHash, "nt51917-ctrlram-replace-fw140-threechip", "0.3.0", "NT51917", 3, 0x35000, 0x40000, 0x16800, "nt51927-ctrlram-fw140-threechip-tp-work-212k", "nt51927-ctrlram-fw140-threechip-full-flash")]
    [InlineData("nt51923-ctrlram-replace-candidate", Nt51923BundleHash, "nt51923-ctrlram-replace-fw141-single", "0.4.0", "NT51923", 1, 0x3C000, 0x40000, 0x22800, "nt51923-ctrlram-fw141-single-tp-work-240k", "nt51923-ctrlram-fw141-single-full-flash")]
    [InlineData("nt51923-ctrlram-replace-candidate", Nt51923BundleHash, "nt51923-ctrlram-replace-fw141-cascade3", "0.4.0", "NT51923", 3, 0x3C000, 0x40000, 0x22800, "nt51923-ctrlram-fw141-cascade3-tp-work-240k", "nt51923-ctrlram-fw141-cascade3-full-flash")]
    [InlineData("nt51927-ctrlram-replace-candidate", Nt51927BundleHash, "nt51927-ctrlram-replace-fw141-single", "0.3.0", "NT51927", 1, 0x35000, 0x40000, 0x16800, "nt51927-ctrlram-fw141-single-tp-work-212k", "nt51927-ctrlram-fw141-single-full-flash")]
    [InlineData("nt51927-ctrlram-replace-candidate", Nt51927BundleHash, "nt51927-ctrlram-replace-fw132-twochip", "0.3.0", "NT51927", 2, 0x35000, 0x40000, 0x16800, "nt51927-ctrlram-fw132-twochip-tp-work-212k", "nt51927-ctrlram-fw132-twochip-full-flash")]
    [InlineData("nt51927-ctrlram-replace-candidate", Nt51927BundleHash, "nt51927-ctrlram-replace-fw140-threechip", "0.3.0", "NT51927", 3, 0x35000, 0x40000, 0x16800, "nt51927-ctrlram-fw140-threechip-tp-work-212k", "nt51927-ctrlram-fw140-threechip-full-flash")]
    [InlineData("nt51928-ctrlram-replace-candidate", Nt51928BundleHash, "nt51928-ctrlram-replace-fw141-single", "0.4.0", "NT51928", 1, 0x35000, 0x80000, 0x16800, "nt51928-ctrlram-fw141-single-tp-work-212k", "nt51928-ctrlram-fw141-single-full-flash")]
    [InlineData("nt51928-ctrlram-replace-candidate", Nt51928BundleHash, "nt51928-ctrlram-replace-fw132-twochip", "0.3.0", "NT51928", 2, 0x35000, 0x80000, 0x16800, "nt51928-ctrlram-fw132-twochip-tp-work-212k", "nt51928-ctrlram-fw132-twochip-full-flash")]
    [InlineData("nt51928-ctrlram-replace-candidate", Nt51928BundleHash, "nt51928-ctrlram-replace-fw140-threechip", "0.4.0", "NT51928", 3, 0x35000, 0x80000, 0x16800, "nt51928-ctrlram-fw140-threechip-tp-work-212k", "nt51928-ctrlram-fw140-threechip-full-flash")]
    public void TpAndFullRoutesSelectExactMapsWithPrefixOnlyProcessorAuthority(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string profileVersion,
        string icId,
        int chipCount,
        long tpCapacity,
        long fullCapacity,
        long targetStart,
        string expectedTpMapId,
        string expectedFullMapId)
    {
        CompiledComposition tp = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart,
            targetLength: 1,
            referenceCapacity: tpCapacity,
            profileVersion: profileVersion);
        CompiledComposition full = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart,
            targetLength: 1,
            referenceCapacity: fullCapacity,
            profileVersion: profileVersion);

        Assert.Equal(expectedTpMapId, tp.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(expectedFullMapId, full.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(tpCapacity, tp.Plan.OutputInitialization.Capacity);
        Assert.Equal(fullCapacity, full.Plan.OutputInitialization.Capacity);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, tp.Eligibility);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, full.Eligibility);
        Assert.Equal([new ByteRange(0, tpCapacity)], Processor(tp).AllowedReadRanges);
        Assert.Equal([new ByteRange(0, tpCapacity)], Processor(full).AllowedReadRanges);
        Assert.All(Processor(tp).AllowedWriteRanges, range => Assert.True(range.EndExclusive <= tpCapacity));
        Assert.All(Processor(full).AllowedWriteRanges, range => Assert.True(range.EndExclusive <= tpCapacity));
        Assert.Equal(CompiledProfilePromotionStage.Supported, tp.V2Details.Provenance.Promotion.Stage);
        Assert.Empty(tp.V2Details.Provenance.Promotion.Blockers);
    }

    /// <summary>Only the declared TP and full-container capacities are admitted, including the 512 KiB NT51928 shape.</summary>
    [Theory]
    [InlineData("nt51923-ctrlram-replace-candidate", Nt51923BundleHash, "nt51923-ctrlram-replace-fw141-single", "0.4.0", "NT51923", 1, 0x3C000, 0x40000, 0x22800)]
    [InlineData("nt51927-ctrlram-replace-candidate", Nt51927BundleHash, "nt51927-ctrlram-replace-fw141-single", "0.3.0", "NT51927", 1, 0x35000, 0x40000, 0x16800)]
    [InlineData("nt51928-ctrlram-replace-candidate", Nt51928BundleHash, "nt51928-ctrlram-replace-fw141-single", "0.4.0", "NT51928", 1, 0x35000, 0x80000, 0x16800)]
    public void TpAndFullRoutesRejectEveryAdjacentOrLegacyContainerCapacity(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string profileVersion,
        string icId,
        int chipCount,
        long tpCapacity,
        long fullCapacity,
        long targetStart)
    {
        long[] rejectedCapacities = StringComparer.Ordinal.Equals(icId, "NT51928")
            ? [tpCapacity - 1, tpCapacity + 1, 0x40000, fullCapacity - 1, fullCapacity + 1]
            : [tpCapacity - 1, tpCapacity + 1, fullCapacity - 1, fullCapacity + 1];

        foreach (long rejectedCapacity in rejectedCapacities)
        {
            V2CompositionPlanCompileResult result = CompileResult(
                bundleDirectory,
                bundleHash,
                profileId,
                icId,
                chipCount,
                targetStart,
                targetLength: 1,
                referenceCapacity: rejectedCapacity,
                profileVersion: profileVersion);

            Assert.False(result.IsCompiled);
            Assert.Null(result.CompiledComposition);
            Assert.Contains(
                result.Issues,
                static issue => issue.Code == "profile.v2.compile.map-selection-invalid");
        }
    }

    /// <summary>A metadata marker found only in the preserved full-container tail cannot authorize a TP-core plan.</summary>
    [Theory]
    [InlineData("nt51923-ctrlram-replace-candidate", Nt51923BundleHash, "nt51923-ctrlram-replace-fw141-single", "0.4.0", "NT51923", 1, 0x3C000, 0x40000, 0x22800)]
    [InlineData("nt51927-ctrlram-replace-candidate", Nt51927BundleHash, "nt51927-ctrlram-replace-fw141-single", "0.3.0", "NT51927", 1, 0x35000, 0x40000, 0x16800)]
    [InlineData("nt51928-ctrlram-replace-candidate", Nt51928BundleHash, "nt51928-ctrlram-replace-fw141-single", "0.4.0", "NT51928", 1, 0x35000, 0x80000, 0x16800)]
    public void FullContainerRejectsMetadataMarkerFoundOnlyInPreservedTail(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string profileVersion,
        string icId,
        int chipCount,
        long tpCapacity,
        long fullCapacity,
        long targetStart)
    {
        V2CompositionPlanCompileResult result = CompileResult(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart,
            targetLength: 1,
            referenceCapacity: fullCapacity,
            profileVersion: profileVersion,
            markerOffset: checked((int)tpCapacity + 0x100));

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code == "profile.v2.compile.preparation-not-admitted");
    }

    /// <summary>Full-container execution stages only the TP core, preserves the tail, and never mutates caller inputs.</summary>
    [Theory]
    [InlineData("nt51923-ctrlram-replace-candidate", Nt51923BundleHash, "nt51923-ctrlram-replace-fw141-single", "0.4.0", "NT51923", 1, 0x3C000, 0x40000, 0x22800)]
    [InlineData("nt51927-ctrlram-replace-candidate", Nt51927BundleHash, "nt51927-ctrlram-replace-fw141-single", "0.3.0", "NT51927", 1, 0x35000, 0x40000, 0x16800)]
    [InlineData("nt51928-ctrlram-replace-candidate", Nt51928BundleHash, "nt51928-ctrlram-replace-fw141-single", "0.4.0", "NT51928", 1, 0x35000, 0x80000, 0x16800)]
    public async Task FullContainerExecutionPreservesTailAndCallerInputsAsync(
        string bundleDirectory,
        string bundleHash,
        string profileId,
        string profileVersion,
        string icId,
        int chipCount,
        int tpCapacity,
        int fullCapacity,
        int targetStart)
    {
        CompiledComposition composition = Compile(
            bundleDirectory,
            bundleHash,
            profileId,
            icId,
            chipCount,
            targetStart,
            targetLength: 1,
            referenceCapacity: fullCapacity,
            profileVersion: profileVersion);
        byte[] reference = ResolutionReference(icId, chipCount, fullCapacity);
        reference.AsSpan(tpCapacity).Fill(0xA5);
        byte[] source = [0xC3];
        byte[] originalReference = [.. reference];
        byte[] originalSource = [.. source];
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["reference-base"] = reference,
            ["ctrlram-source-1"] = source,
        };

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(inputs),
            (_, inputBytes, _, _, _) =>
            {
                Assert.Equal(tpCapacity, inputBytes.Length);
                byte[] transformed = inputBytes.ToArray();
                transformed[targetStart] = source[0];
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(transformed));
            },
            TestContext.Current.CancellationToken);

        Assert.True(
            result.Status == CompositionExecutionStatus.Succeeded,
            string.Join(Environment.NewLine, result.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        Assert.Equal(fullCapacity, result.OutputBytes.Length);
        Assert.Equal((byte)0xC3, result.OutputBytes.Span[targetStart]);
        Assert.Equal(originalReference.AsSpan(tpCapacity).ToArray(), result.OutputBytes.Span[tpCapacity..].ToArray());
        Assert.Equal(originalReference, reference);
        Assert.Equal(originalSource, source);
    }

    /// <summary>Every direct TP-prefix evidence view is an immutable slice of its owner expected output.</summary>
    [Theory]
    [InlineData("nt51923-fw141-single-auto-prj-662-20260717", "expected-output", 0, 0x3C000, "d91dd6470fe6084b0d0bbf855e4c443f2911995b028b370ec8dccbef2efc3e78", "tp-input")]
    [InlineData("nt51923-fw141-cascade3-auto-prj-734-20260717", "expected-output", 0, 0x3C000, "1eb5f8647cf58dacfeea21010abb17c23c62d4adb6b790501236bf4ad1badbc7", "tp-input")]
    [InlineData("nt51926-fw141-single-auto-prj-747-20260717", "expected-output", 0, 0x3C000, "987274195623ed48d673a733fd4140fbfaadee5fa13168c42a50055e7ab8b1c3", "tp-input")]
    [InlineData("nt51926-fw141-cascade2-auto-prj-597-20260717", "expected-output", 0, 0x3C000, "9c0b7338c2178d5a250960485bea3f9390ab85173f366bae4e2c4a8e29f279ab", "tp-input")]
    [InlineData("nt51926-fw200-single-auto-prj-597-20260718", "expected-output", 0, 0x3C000, "258c1a51305edf966b8a8fb8fd64f50338385af1178076ea1c0b873db7d6999c", "tp-input")]
    [InlineData("nt51926-fw200-cascade3-auto-prj-597-20260718", "expected-output", 0, 0x3C000, "5e12cf6484d1d681826192b0c2b8aabead4098285f6a2facdfde732f4d1bc7e2", "tp-input")]
    [InlineData("nt51927-fw141-single-auto-prj-529-20260717", "expected-output", 0, 0x35000, "4569893ba45af44ae28e7401374aa549201b9b2edeb2743e9f878d650ac248ee", "tp-input")]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "expected-output", 0, 0x37000, "91d45b5696bb4e0560e96c555336811d2fc8d6691948ba6b5e6458743cf90425", null)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "expected-output", 0, 0x37000, "a852548c2d12a8e592c903e319539f531d540ec23bd271eee25a811c9ff45592", null)]
    [InlineData("nt51951-fw200-cascade2-auto-prj-599-20260731", "expected-output", 0, 0x37000, "6b8151dbb706ed7cf71c8e34a0c864fe5de5bea939ed50516e2e835e7535efcb", null)]
    public void DirectGoldenExpectedViewsRemainExactAndImmutable(
        string caseId,
        string expectedArtifactId,
        int start,
        int length,
        string expectedSha256,
        string? identicalTpArtifactId)
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        GoldenArtifact expectedArtifact = LoadGoldenArtifact(
            RequireArtifactById(goldenCase, expectedArtifactId));

        Assert.Equal(expectedArtifactId, expectedArtifact.ArtifactId);
        Assert.Equal("expected", expectedArtifact.Role);
        Assert.Equal(0, start);
        Assert.InRange(start, 0, expectedArtifact.Bytes.Length);
        Assert.InRange(length, 1, expectedArtifact.Bytes.Length - start);
        byte[] expectedView = expectedArtifact.Bytes.AsSpan(start, length).ToArray();
        Assert.Equal(expectedSha256, Hash(expectedView));

        if (identicalTpArtifactId is not null)
        {
            GoldenArtifact tpArtifact = LoadGoldenArtifact(
                RequireArtifactById(goldenCase, identicalTpArtifactId));
            Assert.Equal("input", tpArtifact.Role);
            Assert.Equal(length, tpArtifact.Bytes.Length);
            Assert.Equal(expectedView, tpArtifact.Bytes);
        }

        Assert.Equal(expectedArtifact.ManifestSha256, Hash(File.ReadAllBytes(expectedArtifact.Path)));
    }

    /// <summary>The admitted TP base produces the exact prefix of the locked full-container owner-golden execution.</summary>
    [Fact]
    public async Task Nt51927TpBaseMatchesExactCompletePrefixOfExistingFullGoldenExecutionAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51927-fw141-single-auto-prj-529-20260717");
        GoldenArtifact[] artifacts =
        [
            .. goldenCase.GetProperty("artifacts").EnumerateArray().Select(LoadGoldenArtifact),
        ];
        GoldenArtifact fullBase = Assert.Single(
            artifacts,
            static artifact => artifact.SourceRole == "expected-final-output");
        Dictionary<string, string> immutableHashes = artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51927-tp-full-golden-prefix");
        string tpBasePath = workspace.PathFor("nt51927-tp-base.bin");
        File.WriteAllBytes(tpBasePath, fullBase.Bytes.AsSpan(0, 0x35000).ToArray());

        Dictionary<string, string> FullSlots(string referencePath)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = referencePath,
                ["replace-ctrlram-normal-master"] = RequireArtifact(artifacts, "Normal_Ctrlram.bin").Path,
                ["replace-ctrlram-mp-master"] = RequireArtifact(artifacts, "MP_Ctrlram.bin").Path,
                ["replace-ctrlram-nf"] = RequireArtifact(artifacts, "NF_Ctrlram.bin").Path,
                ["replace-ctrlram-vn"] = RequireArtifact(artifacts, "VN_Ctrlram.bin").Path,
            };
        }

        string fullOutputPath = workspace.PathFor("full-output.bin");
        CompositionRunResult full = await CtrlRamReplaceTestSupport.RunAsync(
            BootstrapTestHost.Canonical,
            "NT51927",
            "single",
            ExperienceIds.CtrlRamReplace,
            FullSlots(fullBase.Path),
            build: true,
            TestContext.Current.CancellationToken,
            fullOutputPath);
        string tpOutputPath = workspace.PathFor("tp-output.bin");
        CompositionRunResult tp = await CtrlRamReplaceTestSupport.RunAsync(
            BootstrapTestHost.Canonical,
            "NT51927",
            "single",
            ExperienceIds.CtrlRamReplace,
            FullSlots(tpBasePath),
            build: true,
            TestContext.Current.CancellationToken,
            tpOutputPath);

        Assert.True(full.Succeeded, CompositionRunReportJson.Serialize(full));
        Assert.True(tp.Succeeded, CompositionRunReportJson.Serialize(tp));
        byte[] fullOutput = File.ReadAllBytes(fullOutputPath);
        byte[] tpOutput = File.ReadAllBytes(tpOutputPath);
        Assert.Equal("fdb8fef05bdb375e175091eb75d555c2b1c5ddb216a2815f02e25c6533020ab9", Hash(fullOutput));
        CanonicalGoldenDifferenceResult differences = CanonicalGoldenTestData.AssertAllowedByteDifferences(
            goldenCase,
            fullBase.Bytes,
            fullOutput);
        Assert.Equal(24, differences.DifferenceCount);
        Assert.Equal(0x35000, tpOutput.Length);
        Assert.Equal(fullOutput.AsSpan(0, 0x35000).ToArray(), tpOutput);
        Assert.All(
            artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

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
