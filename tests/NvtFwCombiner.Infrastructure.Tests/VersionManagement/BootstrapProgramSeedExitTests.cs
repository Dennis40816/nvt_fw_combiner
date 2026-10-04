using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises seed failures through the actual Bootstrap executable entry point.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class BootstrapProgramSeedExitTests
{
    /// <summary>Missing or malformed state and seed exit InvalidState without publishing state.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MissingOrMalformedStateAndSeedExitInvalidStateWithoutChangingFiles(
        bool malformedState,
        bool malformedSeed)
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = workspace.PathFor("managed root");
        string statePath = workspace.PathFor("isolated state/version-manager.v1.json");
        string seedPath = Path.Combine(managedRoot, "version-manager.seed.v1.json");
        byte[] invalidBytes = "{}"u8.ToArray();
        if (malformedState)
        {
            _ = workspace.Write("isolated state/version-manager.v1.json", invalidBytes);
        }
        if (malformedSeed)
        {
            _ = workspace.Write("managed root/version-manager.seed.v1.json", invalidBytes);
        }

        int exitCode = await RunBootstrapAsync(workspace.Root, managedRoot, statePath);

        Assert.Equal(10, exitCode);
        if (malformedState)
        {
            Assert.Equal(invalidBytes, await File.ReadAllBytesAsync(
                statePath, TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.False(File.Exists(statePath));
        }
        if (malformedSeed)
        {
            Assert.Equal(invalidBytes, await File.ReadAllBytesAsync(
                seedPath, TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.False(File.Exists(seedPath));
        }
        Assert.False(File.Exists(JsonLauncherBootstrapStateStore.DerivePath(statePath)));
    }

    /// <summary>A valid state bound to another root exits ManagedRootMismatch and preserves both files.</summary>
    [Fact]
    public async Task StateBoundToAnotherRootExitsManagedRootMismatchWithoutChangingFiles()
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = workspace.PathFor("managed root");
        string statePath = workspace.PathFor("isolated state/version-manager.v1.json");
        string seedPath = workspace.Write("managed root/version-manager.seed.v1.json", "{}"u8.ToArray());
        var store = new JsonVersionManagerStateStore(statePath);
        await store.SaveAsync(
            VersionManagerState.Create(
                updateSource: null,
                activeVersion: null,
                lastKnownGoodVersion: null,
                admissions: [],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: workspace.PathFor("other managed root")),
            TestContext.Current.CancellationToken);
        Assert.True((await store.LoadAsync(TestContext.Current.CancellationToken)).IsSuccess);
        byte[] stateBefore = await File.ReadAllBytesAsync(statePath, TestContext.Current.CancellationToken);
        byte[] seedBefore = await File.ReadAllBytesAsync(seedPath, TestContext.Current.CancellationToken);

        int exitCode = await RunBootstrapAsync(workspace.Root, managedRoot, statePath);

        Assert.Equal(11, exitCode);
        Assert.Equal(stateBefore, await File.ReadAllBytesAsync(
            statePath, TestContext.Current.CancellationToken));
        Assert.Equal(seedBefore, await File.ReadAllBytesAsync(
            seedPath, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(JsonLauncherBootstrapStateStore.DerivePath(statePath)));
    }

    private static async Task<int> RunBootstrapAsync(
        string workingDirectory,
        string managedRoot,
        string statePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                AppContext.BaseDirectory,
                "launcher-bootstrap",
                "NvtFwCombiner.LauncherBootstrap.exe"),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--managed-root");
        startInfo.ArgumentList.Add(managedRoot);
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(statePath);
        foreach (string key in new[]
        {
            ManagedProcessLifetimeLease.ContextEnvironment,
            ManagedProcessLifetimeLease.HandleEnvironment,
            ManagedProcessLifetimeLease.JobEnvironment,
            ManagedProcessLifetimeLease.StatePathEnvironment,
            ManagedProcessLifetimeLease.KindEnvironment,
            BootstrapStartGate.ContextEnvironment,
            BootstrapStartGate.HandleEnvironment,
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
            InheritedManagedBootstrapIdentityContext.EnvironmentName,
        })
        {
            _ = startInfo.Environment.Remove(key);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException(
            "Root Bootstrap process did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(5_000), "Root Bootstrap process did not terminate.");
            }
        }
    }
}
