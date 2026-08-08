using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests refreshable current-machine processor dependency inspection.</summary>
public sealed class ExternalProcessorRuntimeDependencyInspectorTests
{
    /// <summary>Installing or replacing a tool is observed by the next explicit refresh generation.</summary>
    [Fact]
    public async Task RefreshRechecksMissingInstalledAndReplacedExecutable()
    {
        if (!OperatingSystem.IsWindows() ||
            RuntimeInformation.OSArchitecture is not (
                Architecture.X64 or Architecture.Arm64))
        {
            return;
        }

        byte[] executableBytes = [0x4D, 0x5A, 0x01, 0x02];
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(executableBytes));
        string platform = RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? "win-arm64"
            : "win-x64";
        using var workspace = TempWorkspace.Create(
            "nfc-runtime-dependency-readiness");
        string toolRoot = workspace.PathFor("external-tools");
        string stagingRoot = workspace.PathFor("staging");
        string executablePath = Path.Combine(
            toolRoot,
            "legacy-combiner",
            "1.13.0",
            "combiner.exe");
        var registry = new ExternalCombinerToolRegistry(
        [
            new ExternalCombinerToolManifest(
                "1.0",
                "legacy-combiner-1.13.0",
                "legacy-combiner",
                "1.13.0",
                "Legacy Combiner 1.13.0",
                platform,
                "combiner.exe",
                sha256,
                "legacy-combiner-v1",
                "in-place",
                ["{staging.workBin}"],
                "staging-directory",
                5,
                []),
        ]);
        DateTimeOffset checkedAt = new(
            2026,
            7,
            27,
            12,
            0,
            0,
            TimeSpan.Zero);
        var inspector = new ExternalProcessorRuntimeDependencyInspector(
            registry,
            toolRoot,
            stagingRoot,
            new FixedTimeProvider(checkedAt));
        var request = new RuntimeDependencyReadinessRequest(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            new ResolutionToken("catalog:1"),
            new AuthoringRevision(4),
            [
                new ExternalProcessorDependencyReference(
                    "legacy-combiner-postbuild",
                    "legacy-combiner-1.13.0"),
            ]);

        RuntimeDependencyReadinessSnapshot missing = await inspector.RefreshAsync(
            request,
            generation: 1,
            TestContext.Current.CancellationToken);

        RuntimeDependencyEntry missingEntry = Assert.Single(missing.Entries);
        Assert.Equal(ResolvedChildReadiness.Blocked, missingEntry.Readiness);
        Assert.Equal("external-tool.executable.missing", missingEntry.IssueCode);
        Assert.Equal(1, missing.Generation);
        Assert.Equal(checkedAt, missing.CheckedAtUtc);

        _ = Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        await File.WriteAllBytesAsync(
            executablePath,
            executableBytes,
            TestContext.Current.CancellationToken);

        RuntimeDependencyReadinessSnapshot installed = await inspector.RefreshAsync(
            request,
            generation: 2,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ResolvedChildReadiness.Ready,
            Assert.Single(installed.Entries).Readiness);
        Assert.Equal(2, installed.Generation);

        await File.WriteAllBytesAsync(
            executablePath,
            [0x4D, 0x5A, 0xFF],
            TestContext.Current.CancellationToken);

        RuntimeDependencyReadinessSnapshot replaced = await inspector.RefreshAsync(
            request,
            generation: 3,
            TestContext.Current.CancellationToken);

        RuntimeDependencyEntry replacedEntry = Assert.Single(replaced.Entries);
        Assert.Equal(ResolvedChildReadiness.Blocked, replacedEntry.Readiness);
        Assert.Equal("external-tool.executable-sha.mismatch", replacedEntry.IssueCode);
        Assert.Equal(3, replaced.Generation);
    }

    /// <summary>A hash-valid executable for another declared platform remains blocked.</summary>
    [Fact]
    public async Task RefreshRejectsWrongPlatformWithoutStartingTool()
    {
        if (!OperatingSystem.IsWindows() ||
            RuntimeInformation.OSArchitecture is not (
                Architecture.X64 or Architecture.Arm64))
        {
            return;
        }

        byte[] executableBytes = [0x4D, 0x5A, 0x01, 0x02];
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(executableBytes));
        string otherPlatform = RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? "win-x64"
            : "win-arm64";
        using var workspace = TempWorkspace.Create(
            "nfc-runtime-dependency-platform");
        string toolRoot = workspace.PathFor("external-tools");
        string executablePath = Path.Combine(
            toolRoot,
            "legacy-combiner",
            "1.13.0",
            "combiner.exe");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        await File.WriteAllBytesAsync(
            executablePath,
            executableBytes,
            TestContext.Current.CancellationToken);
        var registry = new ExternalCombinerToolRegistry(
        [
            new ExternalCombinerToolManifest(
                "1.0",
                "legacy-combiner-1.13.0",
                "legacy-combiner",
                "1.13.0",
                "Legacy Combiner 1.13.0",
                otherPlatform,
                "combiner.exe",
                sha256,
                "legacy-combiner-v1",
                "in-place",
                ["{staging.workBin}"],
                "staging-directory",
                5,
                []),
        ]);
        var inspector = new ExternalProcessorRuntimeDependencyInspector(
            registry,
            toolRoot,
            workspace.PathFor("staging"),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var request = new RuntimeDependencyReadinessRequest(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            new ResolutionToken("catalog:1"),
            new AuthoringRevision(5),
            [
                new ExternalProcessorDependencyReference(
                    "legacy-combiner-postbuild",
                    "legacy-combiner-1.13.0"),
            ]);

        RuntimeDependencyReadinessSnapshot result = await inspector.RefreshAsync(
            request,
            generation: 1,
            TestContext.Current.CancellationToken);

        RuntimeDependencyEntry entry = Assert.Single(result.Entries);
        Assert.Equal(ResolvedChildReadiness.Blocked, entry.Readiness);
        Assert.Equal("external-tool.platform.unsupported", entry.IssueCode);
    }

    /// <summary>A processor-free composition does not probe or require staging.</summary>
    [Fact]
    public async Task EmptyDependencySetIsReadyWithoutStagingProbe()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-runtime-dependency-empty");
        string toolRoot = workspace.PathFor("missing-tools");
        string stagingRoot = workspace.PathFor("missing-staging");
        var inspector = new ExternalProcessorRuntimeDependencyInspector(
            new ExternalCombinerToolRegistry([]),
            toolRoot,
            stagingRoot,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var request = new RuntimeDependencyReadinessRequest(
            "route-no-processor",
            new string('b', 64),
            new string('c', 64),
            new ResolutionToken("catalog:1"),
            new AuthoringRevision(6),
            []);

        RuntimeDependencyReadinessSnapshot result = await inspector.RefreshAsync(
            request,
            generation: 1,
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Entries);
        Assert.False(Directory.Exists(stagingRoot));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
