using System.IO.Pipes;
using System.ComponentModel;
using System.Diagnostics;
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
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.Ready, result.Outcome);
    }

    /// <summary>The launcher propagates the exact managed root and custom state path to Desktop.</summary>
    [Fact]
    public async Task ExactCustomStatePathReachesManagedDesktop()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);
        string statePath = Path.Combine(workspace.Root, "state", "custom state.json");
        string argumentsPath = Path.Combine(workspace.Root, "application-arguments.txt");
        string? previousBehavior = Environment.GetEnvironmentVariable(BehaviorEnvironment);
        string? previousArguments = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH");
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, "ready");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", argumentsPath);
            ManagedProcessStartResult result = await new AnonymousPipeManagedApplicationProcess(statePath)
                .StartUntilReadyAsync(
                    workspace.Root,
                    version,
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);

            Assert.Equal(ManagedProcessStartOutcome.Ready, result.Outcome);
            Assert.Equal(
                ["--managed-root", Path.GetFullPath(workspace.Root), "--state-path", Path.GetFullPath(statePath)],
                await File.ReadAllLinesAsync(argumentsPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", previousArguments);
        }
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
            TimeSpan.FromMilliseconds(200),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.ReadyTimeout, result.Outcome);
    }

    /// <summary>A Win32 kill failure cannot authorize Application rollback in the same invocation.</summary>
    [Fact]
    public async Task KillFailureReturnsUnconfirmedTermination()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);
        string? previous = Environment.GetEnvironmentVariable(BehaviorEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, "timeout");
            var termination = new ManagedProcessTermination(
                new FailingTerminationOperations(failKill: true, failWait: false));

            ManagedProcessStartResult result = await new AnonymousPipeManagedApplicationProcess(
                statePath: null,
                termination).StartUntilReadyAsync(
                    workspace.Root,
                    version,
                    TimeSpan.FromMilliseconds(200),
                    TestContext.Current.CancellationToken);

            Assert.Equal(ManagedProcessStartOutcome.TerminationUnconfirmed, result.Outcome);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, previous);
        }
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
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

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
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.InvalidReadySignal, result.Outcome);
    }

    /// <summary>A child exit is distinguished from a deadline and retains its exit code.</summary>
    [Fact]
    public async Task ExitBeforeReadyIsReportedWithExitCode()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);

        ManagedProcessStartResult result = await RunAsync(
            workspace.Root,
            version,
            "exit",
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.ExitedBeforeReady, result.Outcome);
        Assert.Equal(7, result.ExitCode);
    }

    /// <summary>An oversized ready line cannot be truncated into a valid authenticated signal.</summary>
    [Fact]
    public async Task OversizedReadySignalIsRejected()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);

        ManagedProcessStartResult result = await RunAsync(
            workspace.Root,
            version,
            "oversized",
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.InvalidReadySignal, result.Outcome);
    }

    /// <summary>A missing admitted executable is a bounded start failure.</summary>
    [Fact]
    public async Task MissingExecutableFailsBeforeProcessStart()
    {
        using var workspace = TempWorkspace.Create();

        ManagedProcessStartResult result = await new AnonymousPipeManagedApplicationProcess().StartUntilReadyAsync(
            workspace.Root,
            ManagedAppVersion.Parse("0.10.6"),
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.StartFailed, result.Outcome);
        Assert.Null(result.ExitCode);
    }

    /// <summary>A manifest-admitted path containing invalid PE bytes is a typed start failure.</summary>
    [Fact]
    public async Task InvalidPeExecutableIsTypedStartFailure()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        string versionRoot = Path.Combine(workspace.Root, "versions", version.ToString());
        _ = Directory.CreateDirectory(versionRoot);
        await File.WriteAllBytesAsync(
            Path.Combine(versionRoot, "NvtFwCombiner.exe"),
            "MZ-invalid-managed-application"u8.ToArray(),
            TestContext.Current.CancellationToken);

        ManagedProcessStartResult result = await new AnonymousPipeManagedApplicationProcess().StartUntilReadyAsync(
            workspace.Root,
            version,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.StartFailed, result.Outcome);
        Assert.Null(result.ExitCode);
    }

    /// <summary>Caller cancellation propagates and terminates the supervised child rather than becoming a timeout.</summary>
    [Fact]
    public async Task CallerCancellationPropagates()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await RunAsync(
                workspace.Root,
                version,
                "timeout",
                TimeSpan.FromSeconds(5),
                cancellation.Token));
    }

    /// <summary>The application-side inherited channel is version-bound and consumed exactly once.</summary>
    [Fact]
    public async Task ApplicationReadySignalIsVersionBoundAndOneUse()
    {
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        string? priorHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment);
        string? priorVersion = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                pipe.GetClientHandleAsString());
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                version.ToString());
            var signal = new InheritedPipeApplicationReadySignal();

            ApplicationReadySignalOutcome first = await signal.ReportReadyAsync(
                version,
                TestContext.Current.CancellationToken);
            pipe.DisposeLocalCopyOfClientHandle();
            using var reader = new StreamReader(pipe);
            string? message = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            ApplicationReadySignalOutcome second = await signal.ReportReadyAsync(
                version,
                TestContext.Current.CancellationToken);

            Assert.Equal(ApplicationReadySignalOutcome.Reported, first);
            Assert.Equal("READY:0.10.6", message);
            Assert.Equal(ApplicationReadySignalOutcome.NotInherited, second);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                priorHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                priorVersion);
        }
    }

    /// <summary>A mismatched expected version consumes no untrusted handle and clears both ambient values.</summary>
    [Fact]
    public async Task ApplicationReadySignalRejectsWrongExpectedVersion()
    {
        string? priorHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment);
        string? priorVersion = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                "not-opened");
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                "0.10.5");

            ApplicationReadySignalOutcome result = await new InheritedPipeApplicationReadySignal().ReportReadyAsync(
                ManagedAppVersion.Parse("0.10.6"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ApplicationReadySignalOutcome.InvalidInheritedContext, result);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                priorHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                priorVersion);
        }
    }

    /// <summary>An invalid inherited handle is consumed and reported without escaping an I/O exception.</summary>
    [Fact]
    public async Task ApplicationReadySignalRejectsInvalidInheritedHandle()
    {
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        string? priorHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment);
        string? priorVersion = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                "not-a-pipe-handle");
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                version.ToString());

            ApplicationReadySignalOutcome result = await new InheritedPipeApplicationReadySignal().ReportReadyAsync(
                version,
                TestContext.Current.CancellationToken);

            Assert.NotEqual(ApplicationReadySignalOutcome.Reported, result);
            Assert.NotEqual(ApplicationReadySignalOutcome.NotInherited, result);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                priorHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                priorVersion);
        }
    }

    /// <summary>Missing launcher inheritance is distinct from a partial untrusted context.</summary>
    [Fact]
    public async Task ApplicationReadySignalDistinguishesUnmanagedAndPartialInheritance()
    {
        string? priorHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment);
        string? priorVersion = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                null);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                null);
            var signal = new InheritedPipeApplicationReadySignal();

            ApplicationReadySignalOutcome unmanaged = await signal.ReportReadyAsync(
                ManagedAppVersion.Parse("0.10.6"),
                TestContext.Current.CancellationToken);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                "0.10.6");
            ApplicationReadySignalOutcome partial = await signal.ReportReadyAsync(
                ManagedAppVersion.Parse("0.10.6"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ApplicationReadySignalOutcome.NotInherited, unmanaged);
            Assert.Equal(ApplicationReadySignalOutcome.InvalidInheritedContext, partial);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                priorHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                priorVersion);
        }
    }

    /// <summary>The desktop handoff starts only the exact stable launcher under its configured managed root.</summary>
    [Fact]
    public async Task StableLauncherHandoffRejectsMissingAndStartsExactLauncher()
    {
        using var workspace = TempWorkspace.Create();
        var handoff = new StableLauncherHandoff(workspace.Root);

        bool missing = await handoff.TryStartLauncherAsync(TestContext.Current.CancellationToken);
        string probe = Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe");
        File.Copy(probe, Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe"));
        bool started = await handoff.TryStartLauncherAsync(TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.False(missing);
        Assert.True(started);
    }

    /// <summary>Launcher handoff honors caller cancellation before touching the process boundary.</summary>
    [Fact]
    public async Task StableLauncherHandoffHonorsCancellation()
    {
        using var workspace = TempWorkspace.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new StableLauncherHandoff(workspace.Root).TryStartLauncherAsync(cancellation.Token));
    }

    /// <summary>A residual Win32 start failure is converted to the handoff's fail-closed result.</summary>
    [Fact]
    public async Task StableLauncherHandoffConvertsWin32StartFailureToFalse()
    {
        using var workspace = TempWorkspace.Create();
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe"),
            "not-a-windows-executable",
            TestContext.Current.CancellationToken);

        bool started = await new StableLauncherHandoff(workspace.Root)
            .TryStartLauncherAsync(TestContext.Current.CancellationToken);

        Assert.False(started);
    }

    private static async ValueTask<ManagedProcessStartResult> RunAsync(
        string managedRoot,
        ManagedAppVersion version,
        string behavior,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        string? previous = Environment.GetEnvironmentVariable(BehaviorEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, behavior);
            return await new AnonymousPipeManagedApplicationProcess().StartUntilReadyAsync(
                managedRoot,
                version,
                deadline,
                cancellationToken);
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

    private sealed class FailingTerminationOperations(bool failKill, bool failWait)
        : IManagedProcessTerminationOperations
    {
        public bool HasExited(Process process)
        {
            return false;
        }

        public void Kill(Process process)
        {
            process.Kill(entireProcessTree: true);
            if (failKill)
            {
                throw new Win32Exception("Injected kill failure.");
            }
        }

        public void WaitForExit(Process process)
        {
            if (failWait)
            {
                throw new InvalidOperationException("Injected wait failure.");
            }
            process.WaitForExit();
        }

        public int GetExitCode(Process process)
        {
            return process.ExitCode;
        }
    }
}
