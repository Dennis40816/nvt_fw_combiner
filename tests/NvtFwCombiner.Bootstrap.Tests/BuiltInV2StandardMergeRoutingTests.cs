using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for hash-anchored built-in V2 Standard Merge bundles.</summary>
public sealed class BuiltInV2StandardMergeRoutingTests
{
    /// <summary>
    /// Every released Standard Merge route can read the TP source view from a complete
    /// canonical FlashCode without changing the independently approved final bytes.
    /// </summary>
    [Theory]
    [InlineData("NT51917", "51927")]
    [InlineData("NT51919", "51929")]
    [InlineData("NT51923", "51923")]
    [InlineData("NT51926", "51926")]
    [InlineData("NT51927", "51927")]
    [InlineData("NT51928", "51928")]
    [InlineData("NT51929", "51929")]
    [InlineData("NT51932", "51929")]
    [InlineData("NT51950", "51950")]
    [InlineData("NT51951", "51951")]
    public async Task ReleasedStandardMergeAcceptsCanonicalFlashCodeInTpSlot(
        string icId,
        string canonicalReferenceIc)
    {
        System.Text.Json.JsonElement goldenCase =
            V2StandardMergeGoldenTestSupport.ReadGoldenCase(canonicalReferenceIc);
        Dictionary<string, byte[]> inputs =
            V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expectedOutput =
            V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        inputs[CompositionAddressSpaceIds.TpInput] = expectedOutput;
        bool compiled = BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
            icId,
            icId is "NT51950" or "NT51951"
                ? inputs[CompositionAddressSpaceIds.DpInput].LongLength
                : null,
            inputs.Keys,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(
            artifact,
            inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(
            result,
            artifact,
            expectedOutput);
    }

    /// <summary>The complete built-in publication remains loadable after Standard Merge naming changes.</summary>
    [Fact]
    public async Task BuiltInCatalogLoadsAfterStandardMergeNamingUpgrade()
    {
        CanonicalCapabilityPolicySnapshot policy = BuiltInCanonicalCapabilityPolicy.Load();
        string[] drift =
        [
            .. policy.Routes.Select(route =>
            {
                string current = CanonicalDynamicRouteInventory.IsDynamic(route.Identity)
                    ? CanonicalDynamicRouteInventory.Resolve(route.Identity).CapabilityFingerprint
                    : CanonicalCompiledRouteInventory.Resolve(route.Identity).CapabilityFingerprint;
                return (route, current);
            })
            .Where(static item => !StringComparer.Ordinal.Equals(
                item.route.CapabilityFingerprint,
                item.current))
            .Select(static item =>
                $"{item.route.Identity.RouteId}|{item.route.CapabilityFingerprint}|{item.current}"),
        ];
        Assert.True(drift.Length == 0, string.Join(Environment.NewLine, drift));

        var host = new IsolatedBootstrapTestHost();
        CapabilityCatalogReloadResult? result = null;
        await foreach (CanonicalCapabilityCatalogLoadUpdate update in
            host.Services.CanonicalCatalogLoader.LoadAsync(
                TestContext.Current.CancellationToken))
        {
            result = update.Result ?? result;
        }

        CapabilityCatalogReloadResult terminal = Assert.IsType<CapabilityCatalogReloadResult>(result);
        Assert.True(
            terminal.Succeeded,
            string.Join(Environment.NewLine, terminal.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message} [{issue.Subject}]")));
    }

    /// <summary>Verifies every registered IC selects one deployed V2 artifact without legacy fallback.</summary>
    [Theory]
    [InlineData("NT51917", "nt51917-standard-merge-gen-flash-alias", "nt51927-standard-merge", "b1c9234e76ff6995ac362ee66a22eb3423024d116a858a93d2b733c0c380eafa", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias", "nt51929-standard-merge", "d70ee9a8534d2c91a1f674e92b888678d13ea1660a6540365abd42346c480a72", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51923", "nt51923-standard-merge-gen-flash", "nt51923-standard-merge", "9661f30be8b114cd679d08af8177d44bd372973943f2293228f85ff25ecf608c", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51926", "nt51926-standard-merge-gen-flash", "nt51923-standard-merge", "9661f30be8b114cd679d08af8177d44bd372973943f2293228f85ff25ecf608c", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51927", "nt51927-standard-merge-gen-flash", "nt51927-standard-merge", "b1c9234e76ff6995ac362ee66a22eb3423024d116a858a93d2b733c0c380eafa", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51928", "nt51928-standard-merge-gen-flash", "nt51928-standard-merge", "20ccd90376bee9a67832b3a808940017f3cab202ae5d9dfad7cb2dc4b9774c4e", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash", "nt51929-standard-merge", "d70ee9a8534d2c91a1f674e92b888678d13ea1660a6540365abd42346c480a72", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51932", "nt51932-standard-merge-gen-flash", "nt51929-standard-merge", "d70ee9a8534d2c91a1f674e92b888678d13ea1660a6540365abd42346c480a72", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    public void RegisteredStandardMergeUsesDeployedTrustedV2Artifact(
        string icId,
        string profileId,
        string bundleDirectory,
        string bundleContentHash,
        string expectedInputAddressSpaceIds,
        string expectedArtifactClasses)
    {
        ArgumentNullException.ThrowIfNull(expectedInputAddressSpaceIds);
        ArgumentNullException.ThrowIfNull(expectedArtifactClasses);
        string deployedManifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "profiles",
            "built-in",
            bundleDirectory,
            "profile-bundle.json");
        Assert.True(File.Exists(deployedManifestPath), $"Deployed bundle manifest is missing: {deployedManifestPath}");
        bool compiled = BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
            icId,
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal(bundleContentHash, details.Provenance.Bundle.ContentHash);
        Assert.Equal(profileId, artifact.V2Details.ProfileId);
        Assert.Equal(icId, artifact.V2Details.Provenance.Context.MemberId);
        Assert.Equal(
            CompiledOutputNameRendererKind.NormalFlashCodeV1,
            details.OutputNamingRequirement.RendererKind);
        Assert.Equal(
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            details.OutputNamingRequirement.FileNameTemplate);
        Assert.Equal(
            expectedInputAddressSpaceIds.Split(','),
            artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedArtifactClasses.Split(','),
            details.InputContract.Slots
                .OrderBy(static slot => slot.SlotId, StringComparer.Ordinal)
                .Select(static slot => slot.ArtifactClass.ToString()));
    }

    /// <summary>NT51928 selects its LDC-capable map only when the optional LDC slot is selected.</summary>
    [Fact]
    public void Nt51928StandardMergeLdcSelectionResolvesThe512KVariant()
    {
        bool compiled = BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
            "NT51928",
            dpInputLength: null,
            [
                CompositionAddressSpaceIds.DpInput,
                CompositionAddressSpaceIds.TpInput,
                CompositionAddressSpaceIds.LdcInput,
            ],
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(0x80000, artifact.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            [CompositionAddressSpaceIds.DpInput, CompositionAddressSpaceIds.LdcInput, CompositionAddressSpaceIds.TpInput],
            artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        CompiledInputSelectionGroup group = Assert.Single(artifact.V2Details.InputContract.SelectionGroups);
        Assert.Equal("ldc-selection", group.GroupId);
        Assert.Equal([CompositionAddressSpaceIds.LdcInput], group.ApplicableMemberSlotIds);
        Assert.Equal([CompositionAddressSpaceIds.LdcInput], group.SelectedSlotIds);
        Assert.Empty(group.NotApplicableReasons);
    }

    /// <summary>Exact byte oracles cover the no-LDC 256-KiB and selected-LDC 512-KiB Standard Merge variants.</summary>
    [Theory]
    [InlineData("NT51928", false, 0x40000)]
    [InlineData("NT51928", true, 0x80000)]
    [InlineData(" 51928 ", false, 0x40000)]
    public async Task Nt51928StandardMergeBuildsTheSelectedCapacityWithoutImplicitLdcAsync(
        string icId,
        bool selectLdc,
        int expectedCapacity)
    {
        using var workspace = TempWorkspace.Create($"nfc-nt51928-standard-{expectedCapacity:X}");
        byte[] tp = CreatePattern(0x40000, 0x31);
        byte[] dp = CreatePattern(expectedCapacity, 0x72);
        byte[] ldc = CreatePattern(0x80000, 0xB5);
        string outputPath = workspace.PathFor("output.bin");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.TpInput] = workspace.Write("tp.bin", tp),
            [CompositionAddressSpaceIds.DpInput] = workspace.Write("dp.bin", dp),
        };
        if (selectLdc)
        {
            slotPaths[CompositionAddressSpaceIds.LdcInput] = workspace.Write("ldc.bin", ldc);
        }

        CompositionRunResult result = await StandardMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            icId,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        byte[] expected = new byte[expectedCapacity];
        tp.AsSpan(0, 0x35000).CopyTo(expected.AsSpan(0, 0x35000));
        dp.AsSpan(0x3C000, 0x4000).CopyTo(expected.AsSpan(0x3C000, 0x4000));
        if (selectLdc)
        {
            ldc.AsSpan(0x40000, 0x22000).CopyTo(expected.AsSpan(0x40000, 0x22000));
        }

        Assert.Equal(expected, File.ReadAllBytes(outputPath));
    }

    /// <summary>Verifies DP Perspective routing selects the trusted map that exactly matches the supplied DP container length.</summary>
    [Theory]
    [InlineData("NT51950", "nt51950-standard-merge-dp-perspective", 0x40000)]
    [InlineData("NT51951", "nt51951-standard-merge-dp-perspective", 0x80000)]
    public void DpPerspectiveStandardMergeUsesCapacitySelectedTrustedV2Artifact(
        string icId,
        string profileId,
        long dpInputLength)
    {
        bool compiled = BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
            icId,
            dpInputLength,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal("d62b6b3f83a2350724de476d582d3a8de3483366134c39d94f144b77ae1402d7", details.Provenance.Bundle.ContentHash);
        Assert.Equal(profileId, artifact.V2Details.ProfileId);
        Assert.Equal(icId, artifact.V2Details.Provenance.Context.MemberId);
        Assert.Equal(dpInputLength, artifact.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            CompiledOutputNameRendererKind.NormalFlashCodeV1,
            details.OutputNamingRequirement.RendererKind);
        Assert.Equal(
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            details.OutputNamingRequirement.FileNameTemplate);
    }

    /// <summary>Verifies every production-materialized bundle is sourced from its manifest-pinned entries.</summary>
    [Fact]
    public void EveryProductionDeploymentMaterializesManifestPinnedEntries()
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string builtInRoot = Path.Combine(repositoryRoot, "profiles", "built-in");
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
        string materializedBuiltInRoot = Path.Combine(
            repositoryRoot,
            "src",
            "NvtFwCombiner.Bootstrap",
            "obj",
            testOutput.Parent!.Name,
            testOutput.Name,
            "materialized-profiles",
            "built-in");
        string deployedBuiltInRoot = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in");
        string[] bundleDirectories =
        [
            .. ProfileBundlePackageTrustIndexLoader.Load(
                    RepositoryPaths.FromRepositoryRoot(
                        "profiles",
                        "built-in",
                        "package-trust-index.json"))
                .Bundles
                .Select(static bundle => bundle.BundleDirectory)
                .Order(StringComparer.Ordinal),
        ];
        Assert.NotEmpty(bundleDirectories);

        foreach (string bundleDirectory in bundleDirectories)
        {
            string sourceBundleRoot = Path.Combine(builtInRoot, bundleDirectory);
            Assert.True(Directory.Exists(sourceBundleRoot), $"Production bundle has no repository source: {bundleDirectory}");
            string materializedRoot = Path.Combine(materializedBuiltInRoot, bundleDirectory);
            string deployedRoot = Path.Combine(deployedBuiltInRoot, bundleDirectory);
            string sourceManifestPath = Path.Combine(sourceBundleRoot, "profile-bundle.json");
            string materializedManifestPath = Path.Combine(materializedRoot, "profile-bundle.json");
            string deployedManifestPath = Path.Combine(deployedRoot, "profile-bundle.json");
            Assert.Equal(File.ReadAllBytes(sourceManifestPath), File.ReadAllBytes(materializedManifestPath));
            Assert.Equal(File.ReadAllBytes(sourceManifestPath), File.ReadAllBytes(deployedManifestPath));

            using var manifest = JsonDocument.Parse(File.ReadAllText(sourceManifestPath));
            Assert.False(
                Directory.Exists(Path.Combine(sourceBundleRoot, "schemas")),
                $"Source bundle must not be used as a runtime root: {sourceBundleRoot}");
            JsonElement[] entries = [.. manifest.RootElement.GetProperty("entries").EnumerateArray()];
            Assert.Contains(
                entries,
                static entry => StringComparer.Ordinal.Equals(entry.GetProperty("kind").GetString(), "schema"));

            foreach (JsonElement entry in entries)
            {
                string relativePath = entry.GetProperty("path").GetString()!;
                string contentHash = entry.GetProperty("contentHash").GetString()!;
                string sourcePath = BuiltInProfileMaterializationTestSupport.ResolveManifestEntrySource(
                    bundleDirectory,
                    entry);
                string materializedPath = Path.Combine(materializedRoot, relativePath);
                string deployedPath = Path.Combine(deployedRoot, relativePath);

                Assert.True(File.Exists(sourcePath), $"Materialization source is missing: {sourcePath}");
                Assert.True(File.Exists(materializedPath), $"Materialized bundle entry is missing: {materializedPath}");
                Assert.True(File.Exists(deployedPath), $"Deployed bundle entry is missing: {deployedPath}");
                byte[] sourceBytes = File.ReadAllBytes(sourcePath);
                Assert.Equal(contentHash, Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant());
                Assert.Equal(sourceBytes, File.ReadAllBytes(materializedPath));
                Assert.Equal(sourceBytes, File.ReadAllBytes(deployedPath));
            }

            _ = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
                bundleDirectory,
                manifest.RootElement.GetProperty("contentHash").GetString()!);

            V2StandardMergeGoldenTestSupport.AssertSourceCatalogIsRejected(
                bundleDirectory,
                manifest.RootElement.GetProperty("contentHash").GetString()!);
        }
    }

    /// <summary>Verifies the second bundle reaches the shared engine with original input trace names.</summary>
    [Fact]
    public async Task Nt51929WorkbenchPreviewUsesTrustedV2InputBindings()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x40000];
        dp[0] = 0x11;
        tp[0x7000] = 0x22;
        string dpPath = workspace.Write("nt51929-dp.bin", dp);
        string tpPath = workspace.Write("nt51929-tp.bin", tp);

        var progress = new CompositionRunProgressFeed();
        CompositionRunResult result = await StandardMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            "NT51929",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = dpPath,
                ["tp-input"] = tpPath,
            },
            build: false,
            TestContext.Current.CancellationToken,
            progress: progress);
        List<CompositionRunProgressSnapshot> snapshots = [];
        await foreach (CompositionRunProgressSnapshot snapshot in
            progress.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            snapshots.Add(snapshot);
        }

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.True(progress.IsAttached);
        Assert.Equal(
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.PreparingReport,
            ],
            snapshots.Select(static snapshot => snapshot.CurrentPhase));
        Assert.Equal(0x40000, result.OutputSize);
        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        Assert.Equal("nt51929-standard-merge-gen-flash", report.RootElement.GetProperty("ProfileId").GetString());
        Assert.Equal(
            ["nt51929-dp.bin", "nt51929-tp.bin"],
            report.RootElement.GetProperty("Inputs")
                .EnumerateArray()
                .Select(static input => input.GetProperty("OriginalFileName").GetString())
                .Order(StringComparer.Ordinal));
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

}
