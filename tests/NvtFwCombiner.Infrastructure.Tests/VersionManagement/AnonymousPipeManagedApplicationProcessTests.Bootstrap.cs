using System.IO.Pipes;
using System.Diagnostics;
using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedApplicationProcessTests
{
    /// <summary>The desktop handoff starts only the exact stable launcher under its configured managed root.</summary>
    [Fact]
    public async Task StableLauncherHandoffRejectsMissingAndStartsExactLauncher()
    {
        using var workspace = TempWorkspace.Create();
        string probe = Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe");
        byte[] bytes = await File.ReadAllBytesAsync(probe, TestContext.Current.CancellationToken);
        var identity = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var handoff = new StableLauncherHandoff(workspace.Root, expectedIdentity: identity);

        bool missing = await handoff.TryStartLauncherAsync(TestContext.Current.CancellationToken);
        File.Copy(probe, Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe"));
        bool started = await handoff.TryStartLauncherAsync(TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.False(missing);
        Assert.True(started);
    }

    /// <summary>The legacy detached restart is unavailable without inherited exact authority.</summary>
    [Fact]
    public async Task StableLauncherHandoffWithoutExpectedIdentityFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string probe = Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe");
        File.Copy(probe, Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe"));

        bool started = await new StableLauncherHandoff(workspace.Root)
            .TryStartLauncherAsync(TestContext.Current.CancellationToken);

        Assert.False(started);
    }

    /// <summary>A valid PE with a different digest cannot satisfy inherited Bootstrap authority.</summary>
    [Fact]
    public async Task StableLauncherHandoffRejectsTamperedValidPortableExecutable()
    {
        using var workspace = TempWorkspace.Create();
        string probe = Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe");
        string target = Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe");
        File.Copy(probe, target);
        byte[] bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        string actualSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string wrongSha256 = (actualSha256[0] == 'a' ? "b" : "a") + actualSha256[1..];
        var wrongIdentity = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            bytes.LongLength,
            wrongSha256);
        var handoff = new StableLauncherHandoff(workspace.Root, expectedIdentity: wrongIdentity);

        bool started = await handoff.TryStartLauncherAsync(TestContext.Current.CancellationToken);

        Assert.False(started);
    }

    /// <summary>The legacy restart keeps every executable path component stable through Process.Start.</summary>
    [Fact]
    public async Task StableLauncherHandoffRetainsAncestorCustodyThroughLegacyStart()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create();
        string higherAncestor = Path.Combine(workspace.Root, "higher");
        string managedRoot = Path.Combine(higherAncestor, "managed");
        _ = Directory.CreateDirectory(managedRoot);
        string probe = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        File.Copy(probe, Path.Combine(managedRoot, "NvtFwCombiner.Bootstrap.exe"));
        int blocked = 0;
        var handoff = new StableLauncherHandoff(
            managedRoot,
            statePath: null,
            termination: ManagedProcessTermination.Instance,
            beforeProcessStart: _ =>
            {
                foreach (string path in new[] { managedRoot, higherAncestor })
                {
                    try
                    {
                        Directory.Move(path, path + ".displaced");
                    }
                    catch (IOException)
                    {
                        blocked++;
                    }
                }
            },
            expectedIdentity: CreateBootstrapIdentity(managedRoot));

        bool started = await handoff.TryStartLauncherAsync(TestContext.Current.CancellationToken);

        Assert.True(started);
        Assert.Equal(2, blocked);
    }

    private static ManagedImmutableBootstrapIdentity CreateBootstrapIdentity(string managedRoot)
    {
        string executable = Path.Combine(managedRoot, "NvtFwCombiner.Bootstrap.exe");
        byte[] bytes = File.ReadAllBytes(executable);
        return new(
            "NvtFwCombiner.Bootstrap.exe",
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
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

    /// <summary>Constructor authority is never derived from a caller-relative root or state path.</summary>
    [Fact]
    public void StableLauncherHandoffRejectsRelativeAuthorityPaths()
    {
        _ = Assert.Throws<ArgumentException>(() => new StableLauncherHandoff("relative-root"));
        using var workspace = TempWorkspace.Create();
        _ = Assert.Throws<ArgumentException>(() =>
            new StableLauncherHandoff(workspace.Root, "relative-state.json"));
    }

    /// <summary>The typed setup handoff cannot launch a different root under its bound state authority.</summary>
    [Fact]
    public async Task StableLauncherHandoffRejectsCallerRootDifferentFromBoundRoot()
    {
        using var workspace = TempWorkspace.Create();
        string boundRoot = workspace.PathFor("bound");
        string otherRoot = workspace.PathFor("other");
        _ = Directory.CreateDirectory(boundRoot);
        _ = Directory.CreateDirectory(otherRoot);
        var identity = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            68,
            new string('a', 64));
        var handoff = new StableLauncherHandoff(
            boundRoot,
            workspace.PathFor("state/version-manager.v1.json"));

        ImmutableBootstrapStartResult result = await handoff.StartAsync(
            otherRoot,
            identity,
            TestContext.Current.CancellationToken);

        Assert.Null(result.Launch);
        Assert.Equal(ImmutableBootstrapStartIssue.Damaged, result.Issue);
    }

    /// <summary>Cancellation after executable acquisition releases every held path before propagating.</summary>
    [Fact]
    public async Task StableLauncherHandoffCancellationAfterAcquisitionReleasesCustody()
    {
        using var workspace = TempWorkspace.Create();
        string source = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        string target = Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe");
        File.Copy(source, target);
        byte[] bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var identity = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var cancellation = new CancellationTokenSource();
        var handoff = new StableLauncherHandoff(
            workspace.Root,
            workspace.PathFor("state/version-manager.v1.json"),
            ManagedProcessTermination.Instance,
            beforeProcessStart: null,
            afterExecutableAcquired: cancellation.Cancel);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await handoff.StartAsync(workspace.Root, identity, cancellation.Token));

        string displaced = target + ".displaced";
        File.Move(target, displaced);
        Assert.True(File.Exists(displaced));
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

    /// <summary>The shared handoff admits exact Bootstrap bytes once and returns a process receipt.</summary>
    [Fact]
    public async Task StableLauncherHandoffReturnsReceiptOnlyForExactBootstrapIdentity()
    {
        using var workspace = TempWorkspace.Create();
        string source = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        string higherAncestor = Path.Combine(workspace.Root, "higher");
        string managedRoot = Path.Combine(higherAncestor, "managed");
        _ = Directory.CreateDirectory(managedRoot);
        string target = Path.Combine(managedRoot, "NvtFwCombiner.Bootstrap.exe");
        File.Copy(source, target);
        byte[] bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var exact = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var tampered = new ManagedImmutableBootstrapIdentity(
            exact.FileName,
            exact.Length,
            new string('a', 64));
        int blocked = 0;
        var handoff = new StableLauncherHandoff(
            managedRoot,
            Path.Combine(workspace.Root, "state", "version-manager.v1.json"),
            ManagedProcessTermination.Instance,
            _ =>
            {
                foreach (string path in new[] { managedRoot, higherAncestor })
                {
                    try
                    {
                        Directory.Move(path, path + ".displaced");
                    }
                    catch (IOException)
                    {
                        blocked++;
                    }
                }
            });

        ImmutableBootstrapStartResult rejected = await handoff.StartAsync(
            managedRoot,
            tampered,
            TestContext.Current.CancellationToken);
        ImmutableBootstrapStartResult started = await handoff.StartAsync(
            managedRoot,
            exact,
            TestContext.Current.CancellationToken);
        using IImmutableBootstrapLaunch launch = Assert.IsType<IImmutableBootstrapLaunch>(
            started.Launch,
            exactMatch: false);
        ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            TestContext.Current.CancellationToken);

        Assert.Equal(ImmutableBootstrapStartIssue.Damaged, rejected.Issue);
        Assert.True(started.IsStarted);
        Assert.Equal(2, blocked);
        Assert.Equal(ImmutableBootstrapAdmissionOutcome.HealthUnavailable, admission.Outcome);
        Assert.NotEqual(0, admission.ExitCode);
    }

    /// <summary>A concurrent Root Bootstrap invocation is reported as typed contention.</summary>
    [Fact]
    public async Task StableLauncherHandoffReportsConcurrentBootstrapAsBusy()
    {
        using var workspace = TempWorkspace.Create();
        string source = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        string target = Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe");
        File.Copy(source, target);
        byte[] bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var exact = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var handoff = new StableLauncherHandoff(
            workspace.Root,
            Path.Combine(workspace.Root, "state", "version-manager.v1.json"));

        ImmutableBootstrapStartResult first = await handoff.StartAsync(
            workspace.Root,
            exact,
            TestContext.Current.CancellationToken);
        using IImmutableBootstrapLaunch launch = Assert.IsType<IImmutableBootstrapLaunch>(
            first.Launch,
            exactMatch: false);
        ImmutableBootstrapStartResult second = await handoff.StartAsync(
            workspace.Root,
            exact,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsStarted);
        Assert.Equal(ImmutableBootstrapStartIssue.Busy, second.Issue);

        _ = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Every Root Bootstrap exit before ADMITTED keeps its stable entry classification.</summary>
    [Theory]
    [InlineData(2, ImmutableBootstrapAdmissionOutcome.Busy)]
    [InlineData(10, ImmutableBootstrapAdmissionOutcome.RecoveryRequired)]
    [InlineData(11, ImmutableBootstrapAdmissionOutcome.RecoveryRequired)]
    [InlineData(12, ImmutableBootstrapAdmissionOutcome.RecoveryRequired)]
    [InlineData(13, ImmutableBootstrapAdmissionOutcome.RecoveryRequired)]
    [InlineData(14, ImmutableBootstrapAdmissionOutcome.RecoveryRequired)]
    [InlineData(15, ImmutableBootstrapAdmissionOutcome.LaunchFailed)]
    [InlineData(16, ImmutableBootstrapAdmissionOutcome.LaunchFailed)]
    [InlineData(17, ImmutableBootstrapAdmissionOutcome.LaunchFailed)]
    [InlineData(18, ImmutableBootstrapAdmissionOutcome.HealthUnavailable)]
    [InlineData(19, ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed)]
    [InlineData(99, ImmutableBootstrapAdmissionOutcome.HealthUnavailable)]
    public void BootstrapExitBeforeAdmissionMapsExactly(
        int exitCode,
        ImmutableBootstrapAdmissionOutcome expected)
    {
        ImmutableBootstrapAdmissionResult result = StableLauncherHandoff
            .ImmutableBootstrapProcessLaunch.MapExitBeforeAdmission(exitCode);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(exitCode, result.ExitCode);
    }

    /// <summary>Admission cancellation kills and confirms the already-started Root Bootstrap.</summary>
    [Fact]
    public async Task BootstrapAdmissionCancellationTerminatesStartedProcess()
    {
        using var workspace = TempWorkspace.Create();
        using Process process = StartSilentProbe(workspace.Root);
        int processId = process.Id;
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var client = new AnonymousPipeClientStream(
            PipeDirection.Out,
            pipe.GetClientHandleAsString());
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            CreateBootstrapLaunch(process, pipe, workspace.Root);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        ImmutableBootstrapAdmissionResult result = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            cancellation.Token);

        Assert.Equal(ImmutableBootstrapAdmissionOutcome.HealthUnavailable, result.Outcome);
        AssertProcessExited(processId);
    }

    /// <summary>A token cancelled before the wait still aborts and confirms the started tree.</summary>
    [Fact]
    public async Task BootstrapAdmissionPreCancellationStillTerminatesStartedProcess()
    {
        using var workspace = TempWorkspace.Create();
        using Process process = StartSilentProbe(workspace.Root);
        int processId = process.Id;
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var client = new AnonymousPipeClientStream(
            PipeDirection.Out,
            pipe.GetClientHandleAsString());
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            CreateBootstrapLaunch(process, pipe, workspace.Root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ImmutableBootstrapAdmissionResult result = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            cancellation.Token);

        Assert.Equal(ImmutableBootstrapAdmissionOutcome.HealthUnavailable, result.Outcome);
        AssertProcessExited(processId);
    }

    /// <summary>Completion cancellation after ADMITTED still terminates Root Bootstrap.</summary>
    [Fact]
    public async Task BootstrapCompletionCancellationTerminatesAdmittedProcess()
    {
        using var workspace = TempWorkspace.Create();
        using Process process = StartSilentProbe(workspace.Root);
        int processId = process.Id;
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var client = new AnonymousPipeClientStream(
            PipeDirection.Out,
            pipe.GetClientHandleAsString());
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            CreateBootstrapLaunch(process, pipe, workspace.Root);
        await client.WriteAsync("ADMITTED\n"u8.ToArray(), TestContext.Current.CancellationToken);
        await client.FlushAsync(TestContext.Current.CancellationToken);

        ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        ImmutableBootstrapCompletionResult completion =
            await launch.WaitForCompletionAsync(CompletionBudget, cancellation.Token);

        Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
        Assert.Equal(ImmutableBootstrapCompletionOutcome.Unavailable, completion.Outcome);
        AssertProcessExited(processId);
    }

    /// <summary>Slow cleanup cannot extend the complete admission wall clock beyond two seconds.</summary>
    [Fact]
    public async Task BootstrapAdmissionSlowCleanupReturnsTypedUncertaintyWithinTotalDeadline()
    {
        using var workspace = TempWorkspace.Create();
        using Process process = StartSilentProbe(workspace.Root);
        int processId = process.Id;
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var client = new AnonymousPipeClientStream(
            PipeDirection.Out,
            pipe.GetClientHandleAsString());
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            CreateBootstrapLaunch(
                process,
                pipe,
                workspace.Root,
                new SlowThenRealTermination(TimeSpan.FromSeconds(1)));
        using var operationCutoff = new CancellationTokenSource(
            ManagedLauncherEntryCoordinator.DefaultAdmissionOperationCutoff);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            ImmutableBootstrapAdmissionResult result =
                await launch.WaitForAdmissionAsync(AdmissionBudget, operationCutoff.Token);
            stopwatch.Stop();

            Assert.Equal(ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed, result.Outcome);
            Assert.InRange(
                stopwatch.Elapsed,
                TimeSpan.FromMilliseconds(1800),
                TimeSpan.FromMilliseconds(3000));
            await WaitForProcessExitAsync(processId);
            await WaitForBootstrapLifetimeExitAsync(Path.Combine(workspace.Root, "state.json"));
        }
        finally
        {
            TerminateProcess(processId);
        }
    }

    /// <summary>Slow completion cleanup consumes only its reserved caller-visible half second.</summary>
    [Fact]
    public async Task BootstrapCompletionSlowCleanupReturnsTypedUncertaintyWithinCleanupBudget()
    {
        using var workspace = TempWorkspace.Create();
        using Process process = StartSilentProbe(workspace.Root);
        int processId = process.Id;
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var client = new AnonymousPipeClientStream(
            PipeDirection.Out,
            pipe.GetClientHandleAsString());
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            CreateBootstrapLaunch(
                process,
                pipe,
                workspace.Root,
                new SlowThenRealTermination(TimeSpan.FromSeconds(1)));
        await client.WriteAsync("ADMITTED\n"u8.ToArray(), TestContext.Current.CancellationToken);
        await client.FlushAsync(TestContext.Current.CancellationToken);
        ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            TestContext.Current.CancellationToken);
        var completionBudget = new ImmutableBootstrapWaitBudget(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(600));
        using var operationCutoff = new CancellationTokenSource(completionBudget.RemainingOperation);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            ImmutableBootstrapCompletionResult completion =
                await launch.WaitForCompletionAsync(completionBudget, operationCutoff.Token);
            stopwatch.Stop();

            Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
            Assert.Equal(ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed, completion.Outcome);
            Assert.InRange(
                stopwatch.Elapsed,
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(1500));
            await WaitForProcessExitAsync(processId);
            await WaitForBootstrapLifetimeExitAsync(Path.Combine(workspace.Root, "state.json"));
        }
        finally
        {
            TerminateProcess(processId);
        }
    }

    /// <summary>An ADMITTED line wins even when Root Bootstrap exit zero is observed first.</summary>
    [Fact]
    public async Task BootstrapAdmissionWrittenBeforeImmediateExitRemainsAdmittedAndReady()
    {
        string command = Environment.GetEnvironmentVariable("ComSpec") ??
            throw new InvalidOperationException("Command processor is unavailable.");
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("exit /b 0");
        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Exit probe did not start.");
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var client = new AnonymousPipeClientStream(
            PipeDirection.Out,
            pipe.GetClientHandleAsString());
        using var workspace = TempWorkspace.Create();
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            CreateBootstrapLaunch(process, pipe, workspace.Root);
        await client.WriteAsync("ADMITTED\n"u8.ToArray(), TestContext.Current.CancellationToken);
        await client.FlushAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            TestContext.Current.CancellationToken);
        ImmutableBootstrapCompletionResult completion = await launch.WaitForCompletionAsync(
            CompletionBudget,
            TestContext.Current.CancellationToken);

        Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
        Assert.Equal(ImmutableBootstrapCompletionOutcome.Ready, completion.Outcome);
        Assert.Equal(0, completion.ExitCode);
    }

    /// <summary>An admitted Root Bootstrap exit 19 preserves typed termination uncertainty.</summary>
    [Fact]
    public async Task BootstrapAdmissionThenExitNineteenRemainsTerminationUnconfirmed()
    {
        string command = Environment.GetEnvironmentVariable("ComSpec") ??
            throw new InvalidOperationException("Command processor is unavailable.");
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("exit /b 19");
        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Exit probe did not start.");
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var client = new AnonymousPipeClientStream(
            PipeDirection.Out,
            pipe.GetClientHandleAsString());
        using var workspace = TempWorkspace.Create();
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            CreateBootstrapLaunch(process, pipe, workspace.Root);
        await client.WriteAsync("ADMITTED\n"u8.ToArray(), TestContext.Current.CancellationToken);
        await client.FlushAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            TestContext.Current.CancellationToken);
        ImmutableBootstrapCompletionResult completion = await launch.WaitForCompletionAsync(
            CompletionBudget,
            TestContext.Current.CancellationToken);

        Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
        Assert.Equal(
            ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed,
            completion.Outcome);
        Assert.Equal(19, completion.ExitCode);
    }

}
