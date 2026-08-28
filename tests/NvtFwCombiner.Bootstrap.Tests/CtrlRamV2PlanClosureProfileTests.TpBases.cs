using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class CtrlRamV2PlanClosureProfileTests
{
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

}
