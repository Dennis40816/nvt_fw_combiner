using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Runs real child processes through the inherited one-use ready channel.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class AnonymousPipeManagedApplicationProcessTests
{
    private const string BehaviorEnvironment = "NVT_READY_PROBE_BEHAVIOR";

    /// <summary>An exact ready message succeeds without exposing handshake material in arguments.</summary>
    [Fact]
    public async Task ExactReadySignalSucceeds()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);

        ManagedProcessStartResult result = await RunAsync(
            workspace.Root,
            version,
            "ready",
            TimeSpan.FromSeconds(5));
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.Ready, result.Outcome);
    }

    /// <summary>A missing ready signal reaches the deadline and the child is terminated.</summary>
    [Fact]
    public async Task ReadyTimeoutFailsBoundedly()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);

        ManagedProcessStartResult result = await RunAsync(
            workspace.Root,
            version,
            "timeout",
            TimeSpan.FromMilliseconds(200));

        Assert.Equal(ManagedProcessStartOutcome.ReadyTimeout, result.Outcome);
    }

    /// <summary>A wrong one-use ready message is rejected.</summary>
    [Fact]
    public async Task InvalidReadySignalIsRejected()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);

        ManagedProcessStartResult result = await RunAsync(
            workspace.Root,
            version,
            "invalid",
            TimeSpan.FromSeconds(5));

        Assert.Equal(ManagedProcessStartOutcome.InvalidReadySignal, result.Outcome);
    }

    /// <summary>Malformed UTF-8 cannot escape the process boundary as an unhandled decoder failure.</summary>
    [Fact]
    public async Task InvalidUtf8ReadySignalIsRejected()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);

        ManagedProcessStartResult result = await RunAsync(
            workspace.Root,
            version,
            "invalid-utf8",
            TimeSpan.FromSeconds(5));

        Assert.Equal(ManagedProcessStartOutcome.InvalidReadySignal, result.Outcome);
    }

    private static async ValueTask<ManagedProcessStartResult> RunAsync(
        string managedRoot,
        ManagedAppVersion version,
        string behavior,
        TimeSpan deadline)
    {
        string? previous = Environment.GetEnvironmentVariable(BehaviorEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, behavior);
            return await new AnonymousPipeManagedApplicationProcess().StartUntilReadyAsync(
                managedRoot,
                version,
                deadline,
                TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, previous);
        }
    }

    private static void PrepareProbe(string managedRoot, ManagedAppVersion version)
    {
        string probeRoot = Path.Combine(AppContext.BaseDirectory, "ready-probe");
        string versionRoot = Path.Combine(managedRoot, "versions", version.ToString());
        _ = Directory.CreateDirectory(versionRoot);
        foreach (string source in Directory.EnumerateFiles(probeRoot))
        {
            string fileName = Path.GetFileName(source);
            string targetName = string.Equals(fileName, "NvtFwCombiner.ReadyProbe.exe", StringComparison.OrdinalIgnoreCase)
                ? "NvtFwCombiner.exe"
                : fileName;
            File.Copy(source, Path.Combine(versionRoot, targetName));
        }
    }
}
