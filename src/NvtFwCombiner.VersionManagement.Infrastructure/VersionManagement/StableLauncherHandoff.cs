using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Platform.Processes;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Starts only the exact stable launcher located at the managed root.</summary>
public sealed class StableLauncherHandoff :
    IStableLauncherHandoff,
    IImmutableBootstrapHandoff,
    IImmutableBootstrapLeaseHandoff
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
            Process? process = ProcessLaunchGate.StartContained(CreateBootstrapStartInfo(
                lease.ExecutablePath,
                _managedRoot,
                admissionPipeHandle: null,
                startGate: null,
                lifetime: null,
                identity: _expectedIdentity), [], () =>
                {
                    _beforeProcessStart?.Invoke(lease.ExecutablePath);
                    return lease.TryValidateForStart();
                });
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

        return StartWithOwnedLease(root, expectedIdentity, acquired.Lease!, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<ImmutableBootstrapStartResult> StartAsync(
        string managedRoot,
        ManagedImmutableBootstrapIdentity expectedIdentity,
        IManagedExecutableLaunchLease ownedLease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownedLease);
        IManagedExecutableLaunchLease? pendingLease = ownedLease;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateOwnedLease(
                    managedRoot,
                    expectedIdentity,
                    pendingLease,
                    out string root))
            {
                return ValueTask.FromResult(StartFailure(ImmutableBootstrapStartIssue.Damaged));
            }
            IManagedExecutableLaunchLease transferredLease = pendingLease;
            pendingLease = null;
            return ValueTask.FromResult(StartWithOwnedLease(
                root,
                expectedIdentity,
                transferredLease,
                cancellationToken));
        }
        finally
        {
            pendingLease?.Dispose();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "The owned launch lease transfers to the process-start task and is disposed on every exit.")]
    private ImmutableBootstrapStartResult StartWithOwnedLease(
        string root,
        ManagedImmutableBootstrapIdentity expectedIdentity,
        IManagedExecutableLaunchLease lease,
        CancellationToken cancellationToken)
    {
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
                HandleInheritability.None);
            startGate = new BootstrapStartAuthorization();
            ProcessStartInfo startInfo = CreateBootstrapStartInfo(
                    lease.ExecutablePath,
                    root,
                    admissionPipe.GetClientHandleAsString(),
                    startGate,
                    lifetime,
                    expectedIdentity);
            Task<Process?> startTask = Task.Run<Process?>(() =>
            {
                try
                {
                    Process? process = ProcessLaunchGate.StartContained(
                        startInfo,
                        [
                            ProcessInheritedHandle.Parse(
                                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                                admissionPipe.GetClientHandleAsString()),
                            startGate.InheritedHandle,
                            new ProcessInheritedHandle(
                                ManagedProcessLifetimeLease.HandleEnvironment,
                                lifetime.InheritedHandleValue),
                        ],
                        () =>
                        {
                            _beforeProcessStart?.Invoke(lease.ExecutablePath);
                            return lease.TryValidateForStart();
                        });
                    return process ?? throw new InvalidDataException(
                        "Bootstrap tree changed after executable verification.");
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

    private bool TryValidateOwnedLease(
        string managedRoot,
        ManagedImmutableBootstrapIdentity? expectedIdentity,
        IManagedExecutableLaunchLease lease,
        out string root)
    {
        root = string.Empty;
        if (string.IsNullOrWhiteSpace(managedRoot) || expectedIdentity is null ||
            !Path.IsPathFullyQualified(managedRoot) ||
            !string.Equals(
                expectedIdentity.FileName,
                "NvtFwCombiner.Bootstrap.exe",
                StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(managedRoot));
            string configuredRoot = Path.TrimEndingDirectorySeparator(_managedRoot);
            string workingDirectory = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(lease.WorkingDirectory));
            string executable = Path.GetFullPath(lease.ExecutablePath);
            string expectedExecutable = Path.Combine(root, expectedIdentity.FileName);
            return ManagedPathSafety.PathComparer.Equals(root, configuredRoot) &&
                ManagedPathSafety.PathComparer.Equals(workingDirectory, root) &&
                ManagedPathSafety.PathComparer.Equals(executable, expectedExecutable);
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException)
        {
            root = string.Empty;
            return false;
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
                        budget.RemainingTotal,
                        ImmutableBootstrapExitIssue.StartFailed).ConfigureAwait(false);
                }
                if (_process is null)
                {
                    return await FailBeforeAdmissionAsync(
                        ImmutableBootstrapAdmissionOutcome.LaunchFailed,
                        exitCode: null,
                        budgetStarted,
                        budget.RemainingTotal,
                        ImmutableBootstrapExitIssue.StartFailed).ConfigureAwait(false);
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
                    ImmutableBootstrapAdmissionResult exited = MapExitBeforeAdmission(exitCode);
                    return await FailBeforeAdmissionAsync(
                        exited.Outcome,
                        exited.ExitCode,
                        budgetStarted,
                        budget.RemainingTotal,
                        exited.ExitIssue).ConfigureAwait(false);
                }
                if (signalTask.IsCompletedSuccessfully)
                {
                    string? signal = signalTask.Result;
                    if (signal is { Length: 0 })
                    {
                        await exitTask.ConfigureAwait(false);
                        int exitCode = _process.ExitCode;
                        ImmutableBootstrapAdmissionResult exited = MapExitBeforeAdmission(exitCode);
                        return await FailBeforeAdmissionAsync(
                            exited.Outcome,
                            exited.ExitCode,
                            budgetStarted,
                            budget.RemainingTotal,
                            exited.ExitIssue).ConfigureAwait(false);
                    }
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
                if (exitCode is
                    ImmutableBootstrapExitCodeCodec.Ready or
                    ImmutableBootstrapExitCodeCodec.RolledBack)
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
                        ImmutableBootstrapExitCodeCodec.ClassifyCompletion(exitCode),
                        exitCode,
                        ImmutableBootstrapExitIssue.None);
                }
                bool confirmed = await ObserveCleanupAsync(
                    budgetStarted,
                    budget.RemainingTotal).ConfigureAwait(false);
                return new(
                    confirmed
                        ? ImmutableBootstrapExitCodeCodec.ClassifyCompletion(exitCode)
                        : ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed,
                    confirmed ? exitCode : null,
                    confirmed
                        ? ImmutableBootstrapExitCodeCodec.DecodeFailure(exitCode)
                        : ImmutableBootstrapExitIssue.TerminationUnconfirmed);
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
            ImmutableBootstrapExitIssue issue = ImmutableBootstrapExitCodeCodec.DecodeFailure(exitCode);
            return new(
                ImmutableBootstrapExitCodeCodec.ClassifyAdmission(issue),
                exitCode,
                issue);
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
            TimeSpan totalBudget,
            ImmutableBootstrapExitIssue exitIssue = ImmutableBootstrapExitIssue.None)
        {
            bool confirmed = await ObserveCleanupAsync(budgetStarted, totalBudget).ConfigureAwait(false);
            return new(
                confirmed
                    ? confirmedOutcome
                    : ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed,
                confirmed ? exitCode : null,
                confirmed
                    ? exitIssue
                    : ImmutableBootstrapExitIssue.TerminationUnconfirmed);
        }

        private async ValueTask<ImmutableBootstrapCompletionResult> FailBeforeCompletionAsync(
            long budgetStarted,
            TimeSpan totalBudget)
        {
            bool confirmed = await ObserveCleanupAsync(budgetStarted, totalBudget).ConfigureAwait(false);
            return new(
                confirmed
                    ? ImmutableBootstrapCompletionOutcome.Unavailable
                    : ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed,
                ExitIssue: confirmed
                    ? ImmutableBootstrapExitIssue.None
                    : ImmutableBootstrapExitIssue.TerminationUnconfirmed);
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
