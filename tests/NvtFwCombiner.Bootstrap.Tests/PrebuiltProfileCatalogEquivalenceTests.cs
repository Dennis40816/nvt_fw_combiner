using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Each compared subject is the real selected registry in a fresh process.</summary>
public sealed class PrebuiltProfileCatalogEquivalenceTests
{
    /// <summary>All 24 selected bundles and the entire pinned publication must agree across sources.</summary>
    [Fact]
    public async Task SelectedSourcesPreserveAllDocumentsAndPinnedPublication()
    {
        using var prebuilt = new CatalogProbeCopy();
        using var json = new CatalogProbeCopy();
        File.Delete(json.PackPath);
        JsonNode first = await prebuilt.RunAsync();
        JsonNode second = await json.RunAsync();
        Assert.Equal("prebuilt", first["source"]!.GetValue<string>());
        Assert.Equal("json", second["source"]!.GetValue<string>());
        Assert.Equal(24, first["bundles"]!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(first["bundles"], second["bundles"]));
        Assert.True(JsonNode.DeepEquals(first["publication"], second["publication"]));
        Assert.True(JsonNode.DeepEquals(first["parents"], second["parents"]));
        Assert.Equal(11, first["parents"]!.AsArray().Count);
        Assert.True(first["sharedMetadata"]!.GetValue<bool>());
        Assert.True(second["sharedMetadata"]!.GetValue<bool>());
        Assert.All(first["bundles"]!.AsArray(), b => Assert.All(b!["normalized"]!["profiles"]!.AsArray(),
            p => Assert.True(p!["sameFamilyInstance"]!.GetValue<bool>())));
        AssertPinned(first);
        AssertPinned(second);
        Assert.Equal(0, first["entryEvaluations"]!.GetValue<long>());
        Assert.True(second["entryEvaluations"]!.GetValue<long>() > 0);
    }

    /// <summary>All five on-demand contracts compare complete plans and exact failure issues on selected catalogs.</summary>
    [Fact]
    public async Task SelectedSourcesPreserveOnDemandSuccessAndFailureContracts()
    {
        using var prebuilt = new CatalogProbeCopy();
        using var json = new CatalogProbeCopy();
        File.Delete(json.PackPath);
        JsonNode first = await prebuilt.RunAsync("compilation");
        JsonNode second = await json.RunAsync("compilation");
        Assert.Equal("prebuilt", first["source"]!.GetValue<string>());
        Assert.Equal("json", second["source"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(first["compilations"], second["compilations"]));
        Assert.True(JsonNode.DeepEquals(first["reports"], second["reports"]));
        Assert.Equal(2, first["reports"]!.AsArray().Count);
        Assert.All(first["reports"]!.AsArray(), r => Assert.Contains("ReportClassification", r!.GetValue<string>(), StringComparison.Ordinal));
        Assert.Equal(5, first["compilations"]!.AsArray().Select(c => c!["group"]!.GetValue<string>()).Distinct().Count());
        Assert.All(first["compilations"]!.AsArray(), c =>
        {
            Assert.Equal(c!["expectedSuccess"]!.GetValue<bool>(), c["success"]!.GetValue<bool>());
            if (c["success"]!.GetValue<bool>()) { Assert.NotNull(c["fingerprint"]); Assert.NotNull(c["plan"]); }
            else { Assert.NotEmpty(c["issues"]!.AsArray()); }
        });
    }

    /// <summary>Destruction after selection detects a source label concealing JSON evidence loads.</summary>
    [Fact]
    public async Task SelectedPrebuiltEvidenceDoesNotReopenAnyBundleIncludingOnDemand()
    {
        using var intact = new CatalogProbeCopy();
        using var damaged = new CatalogProbeCopy();
        JsonNode expected = await intact.RunAsync("compilation");
        JsonNode actual = await damaged.RunAsync("damage-after-selection");
        Assert.Equal("prebuilt", actual["source"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expected["bundles"], actual["bundles"]));
        Assert.True(JsonNode.DeepEquals(expected["compilations"], actual["compilations"]));
        Assert.True(JsonNode.DeepEquals(expected["reports"], actual["reports"]));
        AssertPinned(actual);
    }

    /// <summary>Selection runs on the serial warm-up thread and is completed before any worker entry.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WarmupSelectsOnceBeforeWorkersAndCancellationCannotReselect(bool json)
    {
        using var copy = new CatalogProbeCopy();
        if (json) { File.Delete(copy.PackPath); }
        JsonNode result = await copy.RunAsync("lifecycle");
        Assert.True(result["passiveBeforeLoad"]!.GetValue<bool>());
        Assert.True(result["cancelledBeforeSelection"]!.GetValue<bool>());
        Assert.True(result["warmupThread"]!.GetValue<bool>());
        Assert.True(result["resolvedBeforeWorkers"]!.GetValue<bool>());
        Assert.True(result["sameDecisionAfterReload"]!.GetValue<bool>());
        AssertPinned(result);
    }

    /// <summary>Concurrent first access and on-disk replacement cannot reset the process decision.</summary>
    [Theory]
    [InlineData(false, "race")]
    [InlineData(true, "race")]
    [InlineData(false, "replace-after-selection")]
    [InlineData(true, "replace-after-selection")]
    public async Task ProcessDecisionSurvivesRacesAndDiskReplacement(bool json, string mode)
    {
        using var copy = new CatalogProbeCopy();
        File.Copy(copy.PackPath, Path.Combine(copy.Root, "original.pack"));
        if (json) { File.Delete(copy.PackPath); }
        JsonNode result = await copy.RunAsync(mode);
        Assert.Equal(json ? "json" : "prebuilt", result["source"]!.GetValue<string>());
        Assert.True(result["sameDecisionAfterReload"]!.GetValue<bool>());
        AssertPinned(result);
        Assert.All(result["bundles"]!.AsArray(), b => Assert.Null(b!["error"]));
    }

    /// <summary>Fresh children regenerate the exact build bytes twice; Unix also verifies a changed local time zone.</summary>
    [Fact]
    public async Task GeneratorReproducesBuildBytesInFreshEnvironments()
    {
        using var baseline = new CatalogProbeCopy();
        using var alternate = new CatalogProbeCopy();
        JsonNode first = await baseline.RunAsync("reproduce", "en-US", "UTC");
        JsonNode second = await alternate.RunAsync("reproduce", "tr-TR", "Pacific/Honolulu");
        Assert.Equal("en-US", first["culture"]!.GetValue<string>());
        Assert.Equal("tr-TR", second["culture"]!.GetValue<string>());
        Assert.All(first["matches"]!.AsArray(), m => Assert.True(m!.GetValue<bool>()));
        Assert.All(second["matches"]!.AsArray(), m => Assert.True(m!.GetValue<bool>()));
        // Windows ignores process TZ. Changing the machine zone is outside a test's authority.
        // The actual alternate-zone evidence is required from the Unix run, not claimed here.
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(0, first["utcOffsetMinutes"]!.GetValue<double>());
            Assert.Equal(-600, second["utcOffsetMinutes"]!.GetValue<double>());
        }
    }

    /// <summary>A genuinely compiled synthetic identity accepts DTO-valid bytes whose later normalization fails, without fallback.</summary>
    [Fact]
    public async Task AcceptedCatalogNormalizationFailureStaysPrebuiltAndMatchesJsonFailure()
    {
        // Keep the copied SDK graph below Windows MSBuild's legacy content-copy path bound.
        using var fixture = TempWorkspace.Create("cn");
        string output = await CatalogProbeCopy.BuildNormalizationFailureHostAsync(fixture.Root);
        using var prebuilt = new CatalogProbeCopy(output);
        using var json = new CatalogProbeCopy(output);
        File.Delete(json.PackPath);
        JsonNode first = await prebuilt.RunAsync();
        JsonNode second = await json.RunAsync();
        Assert.Equal("prebuilt", first["source"]!.GetValue<string>());
        Assert.Null(first["reason"]);
        Assert.Equal("json", second["source"]!.GetValue<string>());
        const string directory = "nt51923-nt51926-general-merge-logical-candidate";
        static string Error(JsonNode result)
        {
            return result["bundles"]!.AsArray()
                .Single(b => b!["directory"]!.GetValue<string>() == directory)!["error"]!.GetValue<string>();
        }
        Assert.Contains("exact trusted firmware family", Error(first), StringComparison.Ordinal);
        Assert.Equal(Error(first), Error(second));
        JsonNode failedBundle = first["bundles"]!.AsArray().Single(b => b!["directory"]!.GetValue<string>() == directory)!;
        Assert.Equal("profile-bundle.catalog.profile-family-missing", failedBundle["errorCode"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(failedBundle,
            second["bundles"]!.AsArray().Single(b => b!["directory"]!.GetValue<string>() == directory)));
        Assert.True(first["sameDecisionAfterReload"]!.GetValue<bool>());
        Assert.Equal(0, first["entryValidationCalls"]!.GetValue<long>());
        Assert.True(second["entryValidationCalls"]!.GetValue<long>() > 0);
    }

    internal static void AssertPinned(JsonNode result)
    {
        Assert.True(result["loaded"]!.GetValue<bool>(), result.ToJsonString());
        // The published catalog snapshot pinned by CanonicalCatalogSnapshotDigestTests (C-7 TP SVN metadata).
        Assert.Equal("2039f8287e34d954c42ce534cc4c8e2e895509469a15f7d1fdbfbba0b56d5c0c", result["publication"]!["sha256"]!.GetValue<string>());
        Assert.Equal(493864, result["publication"]!["text"]!.GetValue<string>().Length);
        Assert.Equal(7, result["publication"]!["sections"]!.AsArray().Count);
    }
}

internal sealed class CatalogProbeCopy : IDisposable
{
    private readonly TempWorkspace _workspace = TempWorkspace.Create("catalog-evidence-child");
    internal string Root { get; }
    internal string PackPath => Path.Combine(Root, "profiles", "built-in", "prebuilt-profile-catalog.pack");
    internal string IndexPath => Path.Combine(Root, "profiles", "built-in", "package-trust-index.json");

    internal CatalogProbeCopy(string? source = null)
    {
        source ??= Path.Combine(AppContext.BaseDirectory, "catalog-evidence-host");
        Assert.True(Directory.Exists(source), "The checkpoint-5 CatalogProbe child must be built and copied.");
        Root = _workspace.PathFor("host");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(Root, Path.GetRelativePath(source, file));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    internal static async Task<string> BuildNormalizationFailureHostAsync(string root)
    {
        foreach (string directory in new[] { "src", "eng", "profiles", "docs/contracts", "tests/NvtFwCombiner.CatalogProbe" })
        {
            string source = RepositoryPaths.FromRepositoryRoot(directory);
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                if (relative.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj") || Path.GetFileName(file) == "AGENTS.md") { continue; }
                string target = Path.Combine(root, directory, relative);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }
        foreach (string relative in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                     "global.json", "VERSION", ".editorconfig", "tests/NvtFwCombiner.Bootstrap.Tests/CanonicalCatalogSnapshotDigest.cs" })
        {
            string target = Path.Combine(root, relative);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(RepositoryPaths.FromRepositoryRoot(relative), target);
        }
        const string directoryName = "nt51923-nt51926-general-merge-logical-candidate";
        string bundles = Path.Combine(root, "profiles", "built-in");
        string manifestPath = Path.Combine(bundles, directoryName, "profile-bundle.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        JsonNode entry = manifest["entries"]!.AsArray().First(e => e!["kind"]!.GetValue<string>() == "composition-profile")!;
        string profilePath = Path.Combine(bundles, directoryName, entry["path"]!.GetValue<string>());
        JsonNode profile = JsonNode.Parse(File.ReadAllText(profilePath))!;
        profile["logicalOutputBinding"]!["familyId"] = "unresolved-family";
        File.WriteAllText(profilePath, profile.ToJsonString());
        entry["contentHash"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(profilePath)));
        string hash = ProfileBundleEntryArrayHasher.CalculateContentHash(manifest["entries"]!.AsArray().Select(e => new ProfileBundleEntryDocument(
            e!["entryId"]!.GetValue<string>(), e["kind"]!.GetValue<string>(), e["path"]!.GetValue<string>(),
            e["schemaId"]!.GetValue<string>(), e["contentHash"]!.GetValue<string>())));
        manifest["contentHash"] = hash;
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        string indexPath = Path.Combine(bundles, "package-trust-index.json");
        JsonNode index = JsonNode.Parse(File.ReadAllText(indexPath))!;
        index["bundles"]!.AsArray().Single(b => b!["bundleDirectory"]!.GetValue<string>() == directoryName)!["contentHash"] = hash;
        File.WriteAllText(indexPath, index.ToJsonString());
        string project = Path.Combine(root, "tests/NvtFwCombiner.CatalogProbe/NvtFwCombiner.CatalogProbe.csproj");
        string cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        foreach (string dependency in Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            JsonNode locked = JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(dependency)!, "packages.lock.json")))!;
            string[] rid = locked["dependencies"]!.AsObject().ContainsKey("net10.0/win-x64") ? ["-r", "win-x64"] : [];
            await RunBuildAsync(root, ["restore", dependency, "--no-dependencies", "--locked-mode", "--source", cache, "-p:NuGetAudit=false", .. rid]);
        }
        await RunBuildAsync(root, ["build", project, "--no-restore", "-nologo", "-m:1", "-p:UseSharedCompilation=false"]);
        return Path.Combine(root, "tests/NvtFwCombiner.CatalogProbe/bin/Debug/net10.0");
    }

    private static async Task RunBuildAsync(string root, string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        using Process process = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        Task<string> output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        Task<string> error = ReadBoundedAsync(process.StandardError, timeout.Token);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(timeout.Token), output, error);
            Assert.True(process.ExitCode == 0, (await output) + (await error));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
        }
    }

    internal async Task<JsonNode> RunAsync(string mode = "evidence", string? culture = null, string? timeZone = null)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(Path.Combine(Root, "NvtFwCombiner.CatalogProbe.dll"));
        start.ArgumentList.Add(mode);
        if (culture is not null) { start.Environment["CATALOG_PROBE_CULTURE"] = culture; }
        if (timeZone is not null) { start.Environment["TZ"] = timeZone; }
        using Process process = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        Task<string> output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        Task<string> error = ReadBoundedAsync(process.StandardError, timeout.Token);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(timeout.Token), output, error);
            Assert.Equal(string.Empty, await error);
            Assert.True(process.ExitCode == 0, await output);
            return JsonNode.Parse(await output)!;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        char[] buffer = new char[8192];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
        {
            if (output.Length + count > 16 * 1024 * 1024) { throw new InvalidDataException("Child evidence exceeds 16 MiB."); }
            _ = output.Append(buffer, 0, count);
        }
        return output.ToString();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
