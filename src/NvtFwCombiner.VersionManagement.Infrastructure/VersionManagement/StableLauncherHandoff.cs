using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Starts only the exact stable launcher located at the managed root.</summary>
public sealed class StableLauncherHandoff : IStableLauncherHandoff, IImmutableBootstrapHandoff
{
    private readonly string _managedRoot;
    private readonly string _statePath;
    private readonly Action<string>? _beforeProcessStart;
    private readonly Action? _afterExecutableAcquired;
    private readonly IManagedProcessTermination _termination;
    private readonly ManagedImmutableBootstrapIdentity? _expectedIdentity;

    /// <summary>Creates a launcher handoff for one exact managed root.</summary>
    /// <param name="managedRoot">Stable launcher-owned root.</param>
    /// <param name="statePath">Exact custom version-state path to propagate to Bootstrap.</param>
    /// <param name="expectedIdentity">Inherited exact Bootstrap authority, or null to disable restart.</param>
    public StableLauncherHandoff(
        string managedRoot,
        string? statePath = null,
        ManagedImmutableBootstrapIdentity? expectedIdentity = null)
        : this(
            managedRoot,
            statePath,
            ManagedProcessTermination.Instance,
            expectedIdentity: expectedIdentity)
    {
    }

    internal StableLauncherHandoff(
        string managedRoot,
        string? statePath,
        IManagedProcessTermination termination,
        Action<string>? beforeProcessStart = null,
        Action? afterExecutableAcquired = null,
        ManagedImmutableBootstrapIdentity? expectedIdentity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        if (!Path.IsPathFullyQualified(managedRoot))
        {
            throw new ArgumentException("Managed root must be an absolute path.", nameof(managedRoot));
        }
        if (statePath is not null && !Path.IsPathFullyQualified(statePath))
        {
            throw new ArgumentException("State path must be an absolute path.", nameof(statePath));
        }
        _managedRoot = Path.GetFullPath(managedRoot);
        _statePath = Path.GetFullPath(statePath ?? JsonVersionManagerStateStore.GetDefaultPath());
        _termination = termination ?? throw new ArgumentNullException(nameof(termination));
        _beforeProcessStart = beforeProcessStart;
        _afterExecutableAcquired = afterExecutableAcquired;
        _expectedIdentity = expectedIdentity;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (_expectedIdentity is null ||
                !string.Equals(
                    _expectedIdentity.FileName,
                    "NvtFwCombiner.Bootstrap.exe",
                    StringComparison.Ordinal))
            {
                return false;
            }
            string launcher = Path.Combine(_managedRoot, "NvtFwCombiner.Bootstrap.exe");
            if (!ManagedPathSafety.IsSafeExistingDirectory(_managedRoot))
            {
                return false;
            }
            ManagedExecutableLaunchLeaseResult acquired =
                await StableManagedExecutableLaunchLease.TryAcquireAsync(
                    launcher,
                    _expectedIdentity.Length,
                    _expectedIdentity.Sha256,
                    cancellationToken).ConfigureAwait(false);
            if (!acquired.IsAcquired)
            {
                return false;
            }
            using IManagedExecutableLaunchLease lease = acquired.Lease!;
            _beforeProcessStart?.Invoke(lease.ExecutablePath);
            if (!lease.TryValidateForStart())
            {
                return false;
            }
            Process? process = Process.Start(CreateBootstrapStartInfo(
                lease.ExecutablePath,
                _managedRoot,
                admissionPipeHandle: null,
                startGate: null,
                lifetime: null,
                identity: _expectedIdentity));
            process?.Dispose();
            return process is not null;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Successful process ownership transfers into the returned completion receipt.")]
    public async ValueTask<ImmutableBootstrapStartResult> StartAsync(
        string managedRoot,
        ManagedImmutableBootstrapIdentity expectedIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(managedRoot))
        {
            return StartFailure(ImmutableBootstrapStartIssue.Damaged);
        }
        string requestedRoot = Path.GetFullPath(managedRoot);
        if (!string.Equals(
                expectedIdentity.FileName,
                "NvtFwCombiner.Bootstrap.exe",
                StringComparison.Ordinal) ||
            !ManagedPathSafety.PathComparer.Equals(requestedRoot, _managedRoot) ||
            FileSystemManagedInstallationRootProbe.AdmitRoot(requestedRoot, out string root) !=
                ManagedInstallationRootStatus.Absent ||
            !ManagedPathSafety.IsSafeExistingDirectory(root))
        {
            return StartFailure(ImmutableBootstrapStartIssue.Damaged);
        }

        string bootstrap = Path.Combine(root, expectedIdentity.FileName);
        ManagedExecutableLaunchLeaseResult acquired = await StableManagedExecutableLaunchLease
            .TryAcquireAsync(
                bootstrap,
                expectedIdentity.Length,
                expectedIdentity.Sha256,
                cancellationToken).ConfigureAwait(false);
        if (!acquired.IsAcquired)
        {
            acquired.Lease?.Dispose();
            return StartFailure(acquired.Issue switch
            {
                ManagedExecutableLaunchIssue.Tampered or
                ManagedExecutableLaunchIssue.UnsafePath => ImmutableBootstrapStartIssue.Damaged,
                ManagedExecutableLaunchIssue.Unavailable => ImmutableBootstrapStartIssue.Unavailable,
                ManagedExecutableLaunchIssue.None => throw new InvalidOperationException(
                    "A successful executable acquisition did not return custody."),
                _ => throw new InvalidOperationException(
                    "Executable custody returned an undefined issue."),
            });
        }

        IManagedExecutableLaunchLease lease = acquired.Lease!;
        try
        {
            _afterExecutableAcquired?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            lease.Dispose();
            throw;
        }
        AnonymousPipeServerStream? admissionPipe = null;
        BootstrapStartAuthorization? startGate = null;
        ManagedProcessLifetimeLease? lifetime = null;
        try
        {
            ManagedProcessLifetimeLeaseAcquisitionOutcome lifetimeAcquisition =
                ManagedProcessLifetimeLease.Acquire(
                _statePath,
                ManagedProcessLifetimeKind.Bootstrap,
                out lifetime);
            if (lifetimeAcquisition != ManagedProcessLifetimeLeaseAcquisitionOutcome.Acquired)
            {
                lease.Dispose();
                return StartFailure(lifetimeAcquisition switch
                {
                    ManagedProcessLifetimeLeaseAcquisitionOutcome.Busy =>
                        ImmutableBootstrapStartIssue.Busy,
                    ManagedProcessLifetimeLeaseAcquisitionOutcome.Unavailable =>
                        ImmutableBootstrapStartIssue.Unavailable,
                    ManagedProcessLifetimeLeaseAcquisitionOutcome.Acquired =>
                        throw new InvalidOperationException(
                            "Acquired Bootstrap lifetime did not return custody."),
                    _ => throw new InvalidOperationException(
                        "Bootstrap lifetime acquisition returned an undefined outcome."),
                });
            }
            lifetime = lifetime ?? throw new InvalidOperationException(
                "Acquired Bootstrap lifetime did not return custody.");
            admissionPipe = new AnonymousPipeServerStream(
                PipeDirection.In,
                HandleInheritability.Inheritable);
            startGate = new BootstrapStartAuthorization();
            ProcessStartInfo startInfo = CreateBootstrapStartInfo(
                    lease.ExecutablePath,
                    root,
                    admissionPipe.GetClientHandleAsString(),
                    startGate,
                    lifetime,
                    expectedIdentity);
            Task<Process?> startTask = Task.Run(() =>
            {
                try
                {
                    _beforeProcessStart?.Invoke(lease.ExecutablePath);
                    return !lease.TryValidateForStart()
                        ? throw new InvalidDataException(
                            "Bootstrap tree changed after executable verification.")
                        : Process.Start(startInfo);
                }
                finally
                {
                    DisposeLocalClientHandle(admissionPipe);
                    startGate.DisposeLocalClientHandle();
                    lease.Dispose();
                }
            });
            return new(
                new ImmutableBootstrapProcessLaunch(
                    startTask,
                    admissionPipe,
                    startGate,
                    lifetime,
                    _termination),
                ImmutableBootstrapStartIssue.None);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            lease.Dispose();
            admissionPipe?.Dispose();
            startGate?.Dispose();
            lifetime?.Dispose();
            return StartFailure(ImmutableBootstrapStartIssue.StartFailed);
        }
    }

    private ProcessStartInfo CreateBootstrapStartInfo(
        string executable,
        string managedRoot,
        string? admissionPipeHandle,
        BootstrapStartAuthorization? startGate,
        ManagedProcessLifetimeLease? lifetime,
        ManagedImmutableBootstrapIdentity identity)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = managedRoot,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--managed-root");
        startInfo.ArgumentList.Add(managedRoot);
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(_statePath);
        if (admissionPipeHandle is not null)
        {
            startInfo.Environment[
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment] =
                admissionPipeHandle;
        }
        startGate?.ApplyInheritedContext(startInfo);
        lifetime?.ApplyInheritedContext(startInfo);
        InheritedManagedBootstrapIdentityContext.Apply(startInfo, identity);
        return startInfo;
    }

    private static ImmutableBootstrapStartResult StartFailure(ImmutableBootstrapStartIssue issue)
    {
        return new(null, issue);
    }

    private static void DisposeLocalClientHandle(AnonymousPipeServerStream pipe)
    {
        try
        {
            pipe.DisposeLocalCopyOfClientHandle();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
        }
    }

    internal sealed class ImmutableBootstrapProcessLaunch : IImmutableBootstrapLaunch
    {
        private const int MaximumAdmissionLineCharacters = 32;
        private static readonly TimeSpan BackgroundCleanupBudget = TimeSpan.FromSeconds(5);
        private readonly AnonymousPipeServerStream _admissionPipe;
        private readonly BootstrapStartAuthorization _startGate;
        private readonly ManagedProcessLifetimeLease _lifetime;
        private readonly Task<Process?> _startTask;
        private readonly IManagedProcessTermination _termination;
        private readonly Lock _cleanupSync = new();
        private Process? _process;
        private Task<bool>? _cleanupTask;
        private int _resourcesDisposed;
        private int _state;

        internal ImmutableBootstrapProcessLaunch(
            Task<Process?> startTask,
            AnonymousPipeServerStream admissionPipe,
            BootstrapStartAuthorization startGate,
            ManagedProcessLifetimeLease lifetime,
            IManagedProcessTermination termination)
        {
            _startTask = startTask ?? throw new ArgumentNullException(nameof(startTask));
            _admissionPipe = admissionPipe ?? throw new ArgumentNullException(nameof(admissionPipe));
            _startGate = startGate ?? throw new ArgumentNullException(nameof(startGate));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _termination = termination ?? throw new ArgumentNullException(nameof(termination));
        }

        public void Dispose()
        {
            if (Volatile.Read(ref _state) != 3)
            {
                _ = StartCleanup();
                return;
            }
            DisposeResources();
        }

        public async ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            long budgetStarted = Stopwatch.GetTimestamp();
            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            {
                return new(ImmutableBootstrapAdmissionOutcome.HealthUnavailable);
            }
            using var operationDeadline = new CancellationTokenSource(budget.RemainingOperation);
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                operationDeadline.Token);
            try
            {
                try
                {
                    _process = await _startTask.WaitAsync(operation.Token).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is
                    IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
                {
                    return await FailBeforeAdmissionAsync(
                        ImmutableBootstrapAdmissionOutcome.LaunchFailed,
                        exitCode: null,
                        budgetStarted,
                        budget.RemainingTotal).ConfigureAwait(false);
                }
                if (_process is null)
                {
                    return await FailBeforeAdmissionAsync(
                        ImmutableBootstrapAdmissionOutcome.LaunchFailed,
                        exitCode: null,
                        budgetStarted,
                        budget.RemainingTotal).ConfigureAwait(false);
                }
                if (operation.IsCancellationRequested ||
                    Stopwatch.GetElapsedTime(budgetStarted) >= budget.RemainingOperation ||
                    !_startGate.TryAuthorize())
                {
                    return await FailBeforeAdmissionAsync(
                        ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                        exitCode: null,
                        budgetStarted,
                        budget.RemainingTotal).ConfigureAwait(false);
                }
                Task<string?> signalTask = BoundedUtf8LineReader.ReadAsync(
                    _admissionPipe,
                    MaximumAdmissionLineCharacters,
                    bufferSize: 64,
                    operation.Token);
                Task exitTask = _process.WaitForExitAsync(operation.Token);
                Task completed = await Task.WhenAny(signalTask, exitTask).ConfigureAwait(false);
                if (signalTask.IsCompletedSuccessfully && IsAdmitted(signalTask.Result) &&
                    !operation.IsCancellationRequested &&
                    IsWithinBudget(budgetStarted, budget.RemainingOperation))
                {
                    return AcceptAdmission();
                }
                if (exitTask.IsCompletedSuccessfully)
                {
                    string? signal = await signalTask.ConfigureAwait(false);
                    if (IsAdmitted(signal) &&
                        !operation.IsCancellationRequested &&
                        IsWithinBudget(budgetStarted, budget.RemainingOperation))
                    {
                        return AcceptAdmission();
                    }
                    int exitCode = _process.ExitCode;
                    return await FailBeforeAdmissionAsync(
                        MapExitBeforeAdmission(exitCode).Outcome,
                        exitCode,
                        budgetStarted,
                        budget.RemainingTotal).ConfigureAwait(false);
                }
                if (signalTask.IsCompletedSuccessfully)
                {
                    return await FailBeforeAdmissionAsync(
                        ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                        exitCode: null,
                        budgetStarted,
                        budget.RemainingTotal).ConfigureAwait(false);
                }
                await completed.ConfigureAwait(false);
                return await FailBeforeAdmissionAsync(
                    ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                    exitCode: null,
                    budgetStarted,
                    budget.RemainingTotal).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return await FailBeforeAdmissionAsync(
                    ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                    exitCode: null,
                    budgetStarted,
                    budget.RemainingTotal).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is
                DecoderFallbackException or IOException or InvalidOperationException or Win32Exception)
            {
                return await FailBeforeAdmissionAsync(
                    ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                    exitCode: null,
                    budgetStarted,
                    budget.RemainingTotal).ConfigureAwait(false);
            }
        }

        public async ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            long budgetStarted = Stopwatch.GetTimestamp();
            if (Volatile.Read(ref _state) != 2)
            {
                return new(ImmutableBootstrapCompletionOutcome.Unavailable);
            }
            using var operationDeadline = new CancellationTokenSource(budget.RemainingOperation);
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                operationDeadline.Token);
            try
            {
                Process process = _process ?? throw new InvalidOperationException(
                    "An admitted Bootstrap has no process receipt.");
                long completionStarted = Stopwatch.GetTimestamp();
                await process.WaitForExitAsync(operation.Token).ConfigureAwait(false);
                int exitCode = process.ExitCode;
                if (operation.IsCancellationRequested ||
                    Stopwatch.GetElapsedTime(completionStarted) >= budget.RemainingOperation ||
                    !IsWithinBudget(budgetStarted, budget.RemainingOperation))
                {
                    return await FailBeforeCompletionAsync(
                        budgetStarted,
                        budget.RemainingTotal).ConfigureAwait(false);
                }
                if (exitCode is 0 or 1)
                {
                    if (!_lifetime.TryReleaseAcceptedTree())
                    {
                        return await FailBeforeCompletionAsync(
                            budgetStarted,
                            budget.RemainingTotal).ConfigureAwait(false);
                    }
                    _ = Interlocked.Exchange(ref _state, 3);
                    DisposeResources();
                    return new(
                        exitCode == 0
                            ? ImmutableBootstrapCompletionOutcome.Ready
                            : ImmutableBootstrapCompletionOutcome.RolledBack,
                        exitCode);
                }
                bool confirmed = await ObserveCleanupAsync(
                    budgetStarted,
                    budget.RemainingTotal).ConfigureAwait(false);
                return new(
                    exitCode == 19 || !confirmed
                        ? ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed
                        : ImmutableBootstrapCompletionOutcome.Failed,
                    confirmed ? exitCode : null);
            }
            catch (OperationCanceledException)
            {
                return await FailBeforeCompletionAsync(
                    budgetStarted,
                    budget.RemainingTotal).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is
                InvalidOperationException or Win32Exception)
            {
                return await FailBeforeCompletionAsync(
                    budgetStarted,
                    budget.RemainingTotal).ConfigureAwait(false);
            }
        }

        internal static ImmutableBootstrapAdmissionResult MapExitBeforeAdmission(int exitCode)
        {
            return new(
                exitCode switch
                {
                    2 => ImmutableBootstrapAdmissionOutcome.Busy,
                    10 or 11 or 12 or 13 or 14 => ImmutableBootstrapAdmissionOutcome.RecoveryRequired,
                    15 or 16 or 17 => ImmutableBootstrapAdmissionOutcome.LaunchFailed,
                    19 => ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed,
                    _ => ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                },
                exitCode);
        }

        private ImmutableBootstrapAdmissionResult AcceptAdmission()
        {
            _admissionPipe.Dispose();
            _startGate.Dispose();
            _ = Interlocked.Exchange(ref _state, 2);
            return new(ImmutableBootstrapAdmissionOutcome.Admitted);
        }

        private static bool IsAdmitted(string? signal)
        {
            return string.Equals(signal, "ADMITTED", StringComparison.Ordinal);
        }

        private async ValueTask<ImmutableBootstrapAdmissionResult> FailBeforeAdmissionAsync(
            ImmutableBootstrapAdmissionOutcome confirmedOutcome,
            int? exitCode,
            long budgetStarted,
            TimeSpan totalBudget)
        {
            bool confirmed = await ObserveCleanupAsync(budgetStarted, totalBudget).ConfigureAwait(false);
            return new(
                confirmed
                    ? confirmedOutcome
                    : ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed,
                confirmed ? exitCode : null);
        }

        private async ValueTask<ImmutableBootstrapCompletionResult> FailBeforeCompletionAsync(
            long budgetStarted,
            TimeSpan totalBudget)
        {
            bool confirmed = await ObserveCleanupAsync(budgetStarted, totalBudget).ConfigureAwait(false);
            return new(
                confirmed
                    ? ImmutableBootstrapCompletionOutcome.Unavailable
                    : ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed);
        }

        private async ValueTask<bool> ObserveCleanupAsync(long budgetStarted, TimeSpan totalBudget)
        {
            Task<bool> cleanup = StartCleanup();
            TimeSpan remaining = Remaining(totalBudget, Stopwatch.GetElapsedTime(budgetStarted));
            TimeSpan observation = remaining < ManagedLauncherEntryCoordinator.DefaultCleanupObservationBudget
                ? remaining
                : ManagedLauncherEntryCoordinator.DefaultCleanupObservationBudget;
            if (observation <= TimeSpan.Zero)
            {
                return cleanup.IsCompletedSuccessfully && cleanup.Result;
            }
            try
            {
                return await cleanup.WaitAsync(observation).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        private static bool IsWithinBudget(long started, TimeSpan totalBudget)
        {
            return Stopwatch.GetElapsedTime(started) < totalBudget;
        }

        private static TimeSpan Remaining(TimeSpan total, TimeSpan elapsed)
        {
            return elapsed >= total ? TimeSpan.Zero : total - elapsed;
        }

        private Task<bool> StartCleanup()
        {
            _ = _startGate.TryAbort();
            lock (_cleanupSync)
            {
                return _cleanupTask ??= Task.Run(CleanupCore);
            }
        }

        private bool CleanupCore()
        {
            try
            {
                Process? process;
                try
                {
                    process = _startTask.GetAwaiter().GetResult();
                    _process ??= process;
                }
                catch (Exception exception) when (exception is
                    AggregateException or IOException or UnauthorizedAccessException or
                    InvalidOperationException or Win32Exception)
                {
                    process = null;
                }
                bool treeExited = _lifetime.TerminateTreeAndConfirmEmpty(BackgroundCleanupBudget);
                bool processExited = process is null || _termination.ConfirmExited(process).IsExitConfirmed;
                return treeExited && processExited;
            }
            finally
            {
                _ = Interlocked.Exchange(ref _state, 3);
                DisposeResources();
            }
        }

        private void DisposeResources()
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
            {
                return;
            }
            _admissionPipe.Dispose();
            _startGate.Dispose();
            _process?.Dispose();
            _lifetime.Dispose();
        }

    }
}
