using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for hash-anchored built-in V2 Standard Merge bundles.</summary>
public sealed class BuiltInV2StandardMergeRoutingTests
{
    /// <summary>Verifies every registered IC selects one deployed V2 artifact without legacy fallback.</summary>
    [Theory]
    [InlineData("NT51917", "nt51917-standard-merge-gen-flash-alias", "nt51927-standard-merge", "631bf40e6f5f6aee14be7a5b834243def7c6a37cdb88f49e0d854471d5de6015", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias", "nt51929-standard-merge", "14a3b2808a5377af39b683fe44f60f152e9c7f4a15c18e5c9e264ad6ea2b0827", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51923", "nt51923-standard-merge-gen-flash", "nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51926", "nt51926-standard-merge-gen-flash", "nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51927", "nt51927-standard-merge-gen-flash", "nt51927-standard-merge", "631bf40e6f5f6aee14be7a5b834243def7c6a37cdb88f49e0d854471d5de6015", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51928", "nt51928-standard-merge-gen-flash", "nt51928-standard-merge", "8078a46da81b650436d25ce444904b88b29f5e4a0a7c7c169da73115f890a188", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash", "nt51929-standard-merge", "14a3b2808a5377af39b683fe44f60f152e9c7f4a15c18e5c9e264ad6ea2b0827", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51932", "nt51932-standard-merge-gen-flash", "nt51929-standard-merge", "14a3b2808a5377af39b683fe44f60f152e9c7f4a15c18e5c9e264ad6ea2b0827", "dp-input,tp-input", "DpFirmware,TpFirmware")]
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
        bool compiled = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(artifact.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal(bundleContentHash, details.Provenance.Bundle.ContentHash);
        Assert.Equal(profileId, artifact.ProfileId);
        Assert.Equal(icId, artifact.IcId);
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
        bool compiled = WorkbenchCompositionService.TryCompileStandardMerge(
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
        CompiledInputSelectionGroup group = Assert.Single(artifact.V2Details!.InputContract.SelectionGroups);
        Assert.Equal("ldc-selection", group.GroupId);
        Assert.Equal([CompositionAddressSpaceIds.LdcInput], group.ApplicableMemberSlotIds);
        Assert.Equal([CompositionAddressSpaceIds.LdcInput], group.SelectedSlotIds);
        Assert.Empty(group.NotApplicableReasons);
    }

    /// <summary>Exact byte oracles cover the no-LDC 256-KiB and selected-LDC 512-KiB Standard Merge variants.</summary>
    [Theory]
    [InlineData(false, 0x40000)]
    [InlineData(true, 0x80000)]
    public async Task Nt51928StandardMergeBuildsTheSelectedCapacityWithoutImplicitLdcAsync(
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

        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51928",
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
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
        bool compiled = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
            dpInputLength,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(artifact.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal("714bd7460c15da708be3a297ca8681bba6986262e5fbbc2589b3a8fab15779a1", details.Provenance.Bundle.ContentHash);
        Assert.Equal(profileId, artifact.ProfileId);
        Assert.Equal(icId, artifact.IcId);
        Assert.Equal(dpInputLength, artifact.Plan.OutputInitialization.Capacity);
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
        string projectFile = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Bootstrap",
            "NvtFwCombiner.Bootstrap.csproj");
        string[] bundleDirectories =
        [
            .. XDocument.Load(projectFile)
                .Descendants("BuiltInProfileBundle")
                .Select(static bundle => bundle.Attribute("Include")?.Value)
                .Where(static bundleDirectory => !string.IsNullOrWhiteSpace(bundleDirectory))
                .Cast<string>()
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

        var progress = new Application.Composition.CompositionRunProgressFeed();
        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeWithProgressAsync(
            "NT51929",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = dpPath,
                ["tp-input"] = tpPath,
            },
            build: false,
            progress,
            TestContext.Current.CancellationToken);
        List<Application.Composition.CompositionRunProgressSnapshot> snapshots = [];
        await foreach (Application.Composition.CompositionRunProgressSnapshot snapshot in
            progress.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            snapshots.Add(snapshot);
        }

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(progress.IsAttached);
        Assert.Equal(
            [
                Application.Composition.CompositionRunPhase.Preparing,
                Application.Composition.CompositionRunPhase.ReadingInputs,
                Application.Composition.CompositionRunPhase.ExecutingComposition,
                Application.Composition.CompositionRunPhase.ValidatingOutput,
                Application.Composition.CompositionRunPhase.PreparingReport,
            ],
            snapshots.Select(static snapshot => snapshot.CurrentPhase));
        Assert.Equal(0x40000, result.OutputSize);
        using var report = JsonDocument.Parse(result.ReportJson);
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
