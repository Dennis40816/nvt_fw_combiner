using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Locks the inherited managed-tree lifetime authority and recovery boundary.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class ManagedProcessLifetimeLeaseTests
{
    private const string BehaviorEnvironment = "NVT_READY_PROBE_BEHAVIOR";

    /// <summary>The OS-owned lifetime lease blocks overlap and releases authoritatively on exit.</summary>
    [Fact]
    public void ChildOwnedLifetimeLeaseTransitionsFromActiveToExited()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string statePath = workspace.PathFor("state/version-manager.v1.json");
        ManagedProcessLifetimeLease? lease = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            ManagedProcessLifetimeLease.ApplicationSuffix);

        Assert.NotNull(lease);
        Assert.Equal(
            ManagedProcessLifetimeStatus.Active,
            ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeLease.ApplicationSuffix));
        lease.Dispose();
        Assert.Equal(
            ManagedProcessLifetimeStatus.Exited,
            ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeLease.ApplicationSuffix));
    }

    /// <summary>Only total absence is unmanaged; every advertised partial or malformed context fails closed.</summary>
    [Theory]
    [InlineData(null, null, null, InheritedManagedProcessLifetimeOutcome.NotInherited)]
    [InlineData("v1", null, null, InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    [InlineData(null, "123", null, InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    [InlineData(null, null, "job", InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    [InlineData("", null, null, InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    [InlineData("v1", "", "job", InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    [InlineData("v1", "123", "", InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    [InlineData("bad", "123", "job", InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    [InlineData("v1", "123", "job", InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext)]
    public void InheritedLifetimeContextClassifiesAbsenceSeparatelyFromManagedLoss(
        string? context,
        string? handle,
        string? job,
        InheritedManagedProcessLifetimeOutcome expected)
    {
        string? priorContext = Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment);
        string? priorHandle = Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.HandleEnvironment);
        string? priorJob = Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.JobEnvironment);
        string? priorStatePath = Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.StatePathEnvironment);
        string? priorKind = Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.KindEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment, context);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.HandleEnvironment, handle);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.JobEnvironment, job);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.StatePathEnvironment, null);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.KindEnvironment, null);
            using IInheritedManagedProcessLifetimeCapture capture = InheritedManagedProcessLifetime.Capture(
                Path.GetFullPath("managed-state.json"),
                ManagedProcessLifetimeKind.Application,
                managedContextAdvertised: false);
            Assert.Equal(expected, capture.Outcome);
            Assert.Null(Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.HandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.JobEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.StatePathEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.KindEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment, priorContext);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.HandleEnvironment, priorHandle);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.JobEnvironment, priorJob);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.StatePathEnvironment, priorStatePath);
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.KindEnvironment, priorKind);
        }
    }

    /// <summary>A managed invocation cannot downgrade missing lifetime authority to unmanaged startup.</summary>
    [Fact]
    public void ManagedInvocationWithoutLifetimeContextFailsClosed()
    {
        using IInheritedManagedProcessLifetimeCapture capture = InheritedManagedProcessLifetime.Capture(
            Path.GetFullPath("managed-state.json"),
            ManagedProcessLifetimeKind.Application,
            managedContextAdvertised: true);

        Assert.Equal(InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext, capture.Outcome);
    }

    /// <summary>A real READY-advertised process with no lifetime authority exits before opening the channel.</summary>
    [Fact]
    public async Task ReadyAdvertisedProcessWithoutLifetimeExitsFailClosed()
    {
        using var workspace = TempWorkspace.Create();
        string probe = Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = probe,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(workspace.PathFor("state/version-manager.v1.json"));
        startInfo.Environment[AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment] = "0.10.6";
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Probe did not start.");

        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(24, process.ExitCode);
    }

    /// <summary>A real managed entry rejects a lifetime inherited for another path or role.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ManagedEntryRejectsWrongPathOrSwappedRole(bool wrongPath, bool wrongRole)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string statePath = workspace.PathFor("state/version-manager.v1.json");
        ManagedProcessLifetimeKind inheritedKind = wrongRole
            ? ManagedProcessLifetimeKind.Launcher
            : ManagedProcessLifetimeKind.Application;
        using ManagedProcessLifetimeLease? lease = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            inheritedKind);
        Assert.NotNull(lease);
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(wrongPath ? workspace.PathFor("state/other.json") : statePath);
        startInfo.Environment[AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment] = "0.10.6";
        lease.ApplyInheritedContext(startInfo);
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Probe did not start.");

        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(24, process.ExitCode);
    }

    /// <summary>A root exit cannot clear recovery while a real grandchild remains in the managed job.</summary>
    [Fact]
    public async Task ManagedTreeAuthorityRemainsActiveUntilGrandchildExit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string statePath = workspace.PathFor("state/version-manager.v1.json");
        string marker = workspace.PathFor("tree/grandchild.txt");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        string probe = Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe");
        using ManagedProcessLifetimeLease? lease = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            ManagedProcessLifetimeLease.ApplicationSuffix);
        Assert.NotNull(lease);
        var startInfo = new ProcessStartInfo
        {
            FileName = probe,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment[BehaviorEnvironment] = "tree-root-exit";
        startInfo.Environment["NVT_READY_PROBE_TREE_MARKER"] = marker;
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(Path.GetFullPath(statePath));
        startInfo.Environment[AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment] = "0.10.6";
        lease.ApplyInheritedContext(startInfo);

        using Process root = Process.Start(startInfo) ?? throw new InvalidOperationException("Tree probe did not start.");
        long startDeadline = Environment.TickCount64 + 5_000;
        while (!File.Exists(marker) && Environment.TickCount64 < startDeadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        await root.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, root.ExitCode);
        Assert.True(File.Exists(marker));
        Assert.Equal(
            ManagedProcessLifetimeStatus.Active,
            ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeLease.ApplicationSuffix));
        Assert.True(lease.TerminateTreeAndConfirmEmpty(TimeSpan.FromSeconds(5)));
        lease.Dispose();

        long deadline = Environment.TickCount64 + 5_000;
        while (ManagedProcessLifetimeLease.GetStatus(
                   statePath,
                   ManagedProcessLifetimeLease.ApplicationSuffix) != ManagedProcessLifetimeStatus.Exited &&
               Environment.TickCount64 < deadline)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        Assert.Equal(
            ManagedProcessLifetimeStatus.Exited,
            ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeLease.ApplicationSuffix));
        using ManagedProcessLifetimeLease? restarted = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            ManagedProcessLifetimeLease.ApplicationSuffix);
        Assert.NotNull(restarted);
    }
}
