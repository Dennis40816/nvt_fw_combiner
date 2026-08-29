using System.IO.Pipes;
using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedLauncherProcessTests
{
    /// <summary>A partial inherited START context exits before creating state or seed storage.</summary>
    [Fact]
    public async Task BootstrapProcessRejectsPartialStartupContextBeforeStateIo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string managedRoot = Path.Combine(workspace.Root, "untouched-managed-root");
        string statePath = Path.Combine(workspace.Root, "untouched-state", "version-manager.v1.json");
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                AppContext.BaseDirectory,
                "launcher-bootstrap",
                "NvtFwCombiner.LauncherBootstrap.exe"),
            WorkingDirectory = workspace.Root,
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
        })
        {
            _ = startInfo.Environment.Remove(key);
        }
        startInfo.Environment[BootstrapStartGate.ContextEnvironment] = BootstrapStartGate.ContextVersion;

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException(
            "Root Bootstrap process did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(timeout.Token);

        Assert.Equal(22, process.ExitCode);
        Assert.False(Directory.Exists(managedRoot));
        Assert.False(Directory.Exists(Path.GetDirectoryName(statePath)));
    }

    /// <summary>Root Bootstrap accepts exactly one complete START line and consumes both fields.</summary>
    [Fact]
    public async Task BootstrapStartGateAcceptsExactSignalAndClearsInheritedContext()
    {
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.Out,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateInheritableClientHandle(pipe);
        WithBootstrapStartEnvironment(BootstrapStartGate.ContextVersion, inheritedDuplicate, () =>
        {
            using BootstrapStartGate gate = BootstrapStartGate.Capture();
            Assert.Equal(BootstrapStartGateInheritanceOutcome.Inherited, gate.Outcome);
            Assert.Null(Environment.GetEnvironmentVariable(BootstrapStartGate.ContextEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(BootstrapStartGate.HandleEnvironment));
            pipe.Write("START\n"u8);
            pipe.Flush();
            Assert.True(gate.WaitForStartAsync(TestContext.Current.CancellationToken)
                .AsTask().GetAwaiter().GetResult());
        });
        await Task.CompletedTask;
    }

    /// <summary>EOF before START never authorizes Root Bootstrap state access.</summary>
    [Fact]
    public async Task BootstrapStartGateEofFailsClosed()
    {
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.Out,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateInheritableClientHandle(pipe);
        WithBootstrapStartEnvironment(BootstrapStartGate.ContextVersion, inheritedDuplicate, () =>
        {
            using BootstrapStartGate gate = BootstrapStartGate.Capture();
            pipe.Dispose();
            Assert.False(gate.WaitForStartAsync(TestContext.Current.CancellationToken)
                .AsTask().GetAwaiter().GetResult());
        });
        await Task.CompletedTask;
    }

    /// <summary>Partial or malformed START inheritance is typed invalid and consumed.</summary>
    [Theory]
    [InlineData(null, "123")]
    [InlineData("v1", null)]
    [InlineData("v2", "123")]
    [InlineData("v1", "invalid")]
    public void BootstrapStartGatePartialContextFailsClosed(string? context, string? handle)
    {
        WithBootstrapStartEnvironment(context, handle, () =>
        {
            using BootstrapStartGate gate = BootstrapStartGate.Capture();
            Assert.Equal(BootstrapStartGateInheritanceOutcome.Invalid, gate.Outcome);
            Assert.Null(Environment.GetEnvironmentVariable(BootstrapStartGate.ContextEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(BootstrapStartGate.HandleEnvironment));
        });
    }

    /// <summary>Once timeout aborts Pending, a late start worker can never write START.</summary>
    [Fact]
    public void BootstrapStartAuthorizationCannotAuthorizeAfterAbort()
    {
        using var authorization = new BootstrapStartAuthorization();

        Assert.True(authorization.TryAbort());
        Assert.False(authorization.TryAuthorize());
    }

    /// <summary>Concurrent worker completion and timeout produce one terminal gate decision.</summary>
    [Fact]
    public async Task BootstrapStartAuthorizationRaceHasExactlyOneWinner()
    {
        using var authorization = new BootstrapStartAuthorization();
        Task<bool> authorize = Task.Run(authorization.TryAuthorize);
        Task<bool> abort = Task.Run(authorization.TryAbort);

        bool[] results = await Task.WhenAll(authorize, abort);

        _ = Assert.Single(results, static value => value);
    }

    /// <summary>A competing exact-path writer prevents nested READY from observing two journal generations.</summary>
    [Fact]
    public async Task CompetingWriterPreventsMixedNestedReadySnapshot()
    {
        using var workspace = TempWorkspace.Create();
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        var admission = new ManagedVersionAdmission(version, "catalog-identity-0.10.6", new string('a', 64));
        var identity = ManagedLauncherIdentity.Create(
            version,
            admission.AdmissionIdentity,
            admission.ReleaseManifestSha256,
            ManagedAppVersion.Parse("1.0.0"),
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            123,
            new string('b', 64));
        var appStore = new JsonVersionManagerStateStore(statePath);
        await appStore.SaveAsync(
            VersionManagerState.Create(
                updateSource: null,
                activeVersion: version,
                lastKnownGoodVersion: version,
                admissions: [admission],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: workspace.Root),
            TestContext.Current.CancellationToken);
        LauncherBootstrapStateSaveResult launcherSaved = await new JsonLauncherBootstrapStateStore(statePath)
            .TrySaveAsync(
                LauncherBootstrapState.Create(workspace.Root, identity, identity, pending: null, failed: null),
                TestContext.Current.CancellationToken);
        Assert.True(launcherSaved.IsSuccess);

        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        string? previousHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? previousExpected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                pipe.GetClientHandleAsString());
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                LauncherReadyProtocol.CreateExpectedPrefix(identity));
            using VersionManagerWriteLeaseResult competing = await appStore.TryAcquireWriteLeaseAsync(
                TimeSpan.Zero,
                TestContext.Current.CancellationToken);
            Assert.True(competing.IsAcquired);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var stopwatch = Stopwatch.StartNew();

            LauncherReadyInheritance context = LauncherBootstrapRuntime.CaptureNestedReadyContext();
            bool reported = await LauncherBootstrapRuntime.ReportNestedReadyAsync(
                context,
                workspace.Root,
                statePath,
                cancellation.Token);
            stopwatch.Stop();
            pipe.DisposeLocalCopyOfClientHandle();
            using var reader = new StreamReader(pipe);
            string? message = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

            Assert.False(reported);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Nested READY writer contention took {stopwatch.Elapsed}.");
            Assert.Null(message);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                previousHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                previousExpected);
        }
    }
}
