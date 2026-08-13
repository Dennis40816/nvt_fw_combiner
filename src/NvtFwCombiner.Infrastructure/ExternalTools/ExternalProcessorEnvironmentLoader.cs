using System.Text.Json;
using System.Threading.Channels;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal delegate ValueTask<ExternalProcessorRuntimeEnvironment>
    ExternalProcessorEnvironmentLoadOperation(
        Action<long, long> progress,
        CancellationToken cancellationToken);

internal sealed record ExternalProcessorRuntimeEnvironment(
    IExternalProcessor? Processor,
    IRuntimeDependencyReadinessProvider ReadinessProvider,
    int ManifestCount);

internal sealed record ExternalProcessorEnvironmentLease(
    long Generation,
    IExternalProcessor? Processor,
    IRuntimeDependencyReadinessProvider ReadinessProvider);

/// <summary>Owns bounded external-tool discovery and atomic generation publication.</summary>
internal sealed class ExternalProcessorEnvironmentLoader :
    IExternalProcessorEnvironmentLoader,
    IRuntimeDependencyReadinessLeaseProvider
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    internal const int MaximumDepth = 16;
    internal const int MaximumVisitedEntries = 4_096;
    internal const int MaximumManifestCount = 256;
    internal const int MaximumManifestBytes = 1_048_576;
    internal const int MaximumCumulativeManifestBytes = 16_777_216;

    private readonly Lock _gate = new();
    private readonly ExternalProcessorEnvironmentLoadOperation _load;
    private ExternalProcessorRuntimeEnvironment? _environment;
    private CancellationTokenSource? _activeCancellation;
    private Task _active = Task.CompletedTask;
    private long _requestGeneration;
    private long _publicationGeneration;
    private ExternalProcessorEnvironmentStatus CurrentState { get; set; } = Status(
        ExternalProcessorEnvironmentState.NotLoaded, 0, 0, 0, []);

    internal ExternalProcessorEnvironmentLoader()
        : this(LoadDefaultAsync)
    {
    }

    internal ExternalProcessorEnvironmentLoader(string toolRoot)
        : this((progress, cancellationToken) =>
            LoadAsync(() => toolRoot, progress, cancellationToken))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
    }

    internal ExternalProcessorEnvironmentLoader(ExternalProcessorEnvironmentLoadOperation load)
    {
        _load = load ?? throw new ArgumentNullException(nameof(load));
    }

    internal ExternalProcessorEnvironmentLoader(ExternalProcessorRuntimeEnvironment environment)
        : this((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(environment);
        })
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _requestGeneration = 1;
        _publicationGeneration = 1;
        CurrentState = Status(
            ExternalProcessorEnvironmentState.Current,
            requestGeneration: 1,
            publicationGeneration: 1,
            environment.ManifestCount,
            []);
    }

    /// <inheritdoc />
    public ExternalProcessorEnvironmentStatus Current
    {
        get
        {
            lock (_gate)
            {
                return CurrentState;
            }
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExternalProcessorEnvironmentLoadUpdate> LoadAsync(
        CancellationToken cancellationToken)
    {
        var updates = Channel.CreateBounded<ExternalProcessorEnvironmentLoadUpdate>(
            new BoundedChannelOptions(MaximumManifestCount + 2)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
        CancellationTokenSource requestCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationTokenSource? previousCancellation;
        Task previous;
        long requestGeneration;
        var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            requestGeneration = checked(++_requestGeneration);
            previousCancellation = _activeCancellation;
            previous = _active;
            _activeCancellation = requestCancellation;
            CurrentState = Status(
                ExternalProcessorEnvironmentState.Loading,
                requestGeneration,
                _publicationGeneration,
                _environment?.ManifestCount ?? 0,
                []);
            _active = Task.Run(async () =>
            {
                await admitted.Task.ConfigureAwait(false);
                await RunRequestAsync(
                    requestGeneration,
                    previous,
                    previousCancellation,
                    requestCancellation,
                    updates.Writer).ConfigureAwait(false);
            }, CancellationToken.None);
        }

        previousCancellation?.Cancel();
        admitted.SetResult();
        return updates.Reader.ReadAllAsync(CancellationToken.None);
    }

    internal ExternalProcessorEnvironmentLease AcquireCurrent()
    {
        lock (_gate)
        {
            if (_environment is null)
            {
                return new(
                    Generation: 0,
                    Processor: null,
                    UnpublishedReadinessProvider.Instance);
            }

            ExternalProcessorRuntimeEnvironment environment = _environment;
            return new(
                _publicationGeneration,
                environment.Processor,
                environment.ReadinessProvider);
        }
    }

    internal bool IsCurrent(long generation)
    {
        lock (_gate)
        {
            return generation == _publicationGeneration &&
                (generation == 0 ? _environment is null : _environment is not null);
        }
    }

    RuntimeDependencyReadinessLease IRuntimeDependencyReadinessLeaseProvider.AcquireCurrent()
    {
        ExternalProcessorEnvironmentLease lease = AcquireCurrent();
        return new(lease.ReadinessProvider, lease.Generation, IsCurrent);
    }

    private async Task RunRequestAsync(
        long requestGeneration,
        Task previous,
        CancellationTokenSource? previousCancellation,
        CancellationTokenSource requestCancellation,
        ChannelWriter<ExternalProcessorEnvironmentLoadUpdate> updates)
    {
        try
        {
            await previous.ConfigureAwait(false);
            previousCancellation?.Dispose();
            if (!IsCurrentRequest(requestGeneration))
            {
                WriteTerminal(updates, Superseded(requestGeneration));
                return;
            }

            long completed = -1;
            long total = -1;
            ExternalProcessorRuntimeEnvironment candidate = await _load((nextCompleted, nextTotal) =>
            {
                if (nextTotal <= 0 || nextCompleted < 0 || nextCompleted > nextTotal ||
                    (total >= 0 && nextTotal != total) || nextCompleted < completed)
                {
                    throw new InvalidOperationException(
                        "External environment progress must be monotonic with one positive total.");
                }

                completed = nextCompleted;
                total = nextTotal;
                Write(updates, new(nextCompleted, nextTotal, null));
            }, requestCancellation.Token).ConfigureAwait(false) ??
                throw new InvalidOperationException(
                    "External environment loading returned no candidate.");
            requestCancellation.Token.ThrowIfCancellationRequested();

            ExternalProcessorEnvironmentLoadResult result;
            lock (_gate)
            {
                if (requestGeneration != _requestGeneration)
                {
                    result = Superseded(requestGeneration);
                }
                else
                {
                    _publicationGeneration = checked(_publicationGeneration + 1);
                    _environment = candidate;
                    CurrentState = Status(
                        ExternalProcessorEnvironmentState.Current,
                        requestGeneration,
                        _publicationGeneration,
                        candidate.ManifestCount,
                        []);
                    result = new(
                        ExternalProcessorEnvironmentLoadOutcome.Succeeded,
                        requestGeneration,
                        _publicationGeneration,
                        candidate.ManifestCount,
                        RetainedLastKnownGood: false,
                        []);
                }
            }
            WriteTerminal(updates, result);
        }
        catch (OperationCanceledException exception)
        {
            if (!IsCurrentRequest(requestGeneration))
            {
                WriteTerminal(updates, Superseded(requestGeneration));
            }
            else
            {
                RestoreAfterCancellation(requestGeneration);
                _ = updates.TryComplete(exception);
                return;
            }
        }
        catch (Exception exception)
        {
            if (!IsCurrentRequest(requestGeneration))
            {
                WriteTerminal(updates, Superseded(requestGeneration));
            }
            else
            {
                WriteTerminal(updates, Failed(requestGeneration, IssueFor(exception)));
            }
        }
        finally
        {
            _ = updates.TryComplete();
        }
    }

    private ExternalProcessorEnvironmentLoadResult Failed(
        long requestGeneration,
        ExternalProcessorEnvironmentIssue issue)
    {
        lock (_gate)
        {
            bool retained = _environment is not null;
            int manifestCount = _environment?.ManifestCount ?? 0;
            CurrentState = Status(
                retained
                    ? ExternalProcessorEnvironmentState.LastKnownGood
                    : ExternalProcessorEnvironmentState.Unavailable,
                requestGeneration,
                _publicationGeneration,
                manifestCount,
                [issue]);
            return new(
                ExternalProcessorEnvironmentLoadOutcome.Failed,
                requestGeneration,
                _publicationGeneration,
                manifestCount,
                retained,
                [issue]);
        }
    }

    private ExternalProcessorEnvironmentLoadResult Superseded(long requestGeneration)
    {
        lock (_gate)
        {
            return new(
                ExternalProcessorEnvironmentLoadOutcome.Superseded,
                requestGeneration,
                _publicationGeneration,
                _environment?.ManifestCount ?? 0,
                RetainedLastKnownGood: _environment is not null,
                []);
        }
    }

    private void RestoreAfterCancellation(long requestGeneration)
    {
        lock (_gate)
        {
            if (requestGeneration != _requestGeneration)
            {
                return;
            }
            CurrentState = Status(
                _environment is null
                    ? ExternalProcessorEnvironmentState.NotLoaded
                    : ExternalProcessorEnvironmentState.Current,
                requestGeneration,
                _publicationGeneration,
                _environment?.ManifestCount ?? 0,
                []);
        }
    }

    private bool IsCurrentRequest(long requestGeneration)
    {
        lock (_gate)
        {
            return requestGeneration == _requestGeneration;
        }
    }

    private static void WriteTerminal(
        ChannelWriter<ExternalProcessorEnvironmentLoadUpdate> updates,
        ExternalProcessorEnvironmentLoadResult result)
    {
        Write(updates, new(null, null, result));
    }

    private static void Write(
        ChannelWriter<ExternalProcessorEnvironmentLoadUpdate> updates,
        ExternalProcessorEnvironmentLoadUpdate update)
    {
        if (!updates.TryWrite(update))
        {
            throw new InvalidOperationException(
                "The bounded external environment progress channel rejected an admitted update.");
        }
    }

    private static ExternalProcessorEnvironmentStatus Status(
        ExternalProcessorEnvironmentState state,
        long requestGeneration,
        long publicationGeneration,
        int manifestCount,
        IReadOnlyList<ExternalProcessorEnvironmentIssue> issues)
    {
        return new(state, requestGeneration, publicationGeneration, manifestCount, issues);
    }

    private static ExternalProcessorEnvironmentIssue IssueFor(Exception exception)
    {
        return exception switch
        {
            ExternalEnvironmentLoadException typed => new(typed.Code, typed.Message),
            JsonException or ArgumentException or InvalidDataException => new(
                ExternalProcessorEnvironmentIssueCodes.ManifestInvalid,
                "An external tool manifest is invalid."),
            IOException or UnauthorizedAccessException => new(
                ExternalProcessorEnvironmentIssueCodes.DiscoveryFailed,
                "External tool discovery could not complete."),
            _ => new(
                ExternalProcessorEnvironmentIssueCodes.CandidateInvalid,
                "The external tool environment candidate is invalid."),
        };
    }

    private static ValueTask<ExternalProcessorRuntimeEnvironment> LoadDefaultAsync(
        Action<long, long> progress,
        CancellationToken cancellationToken)
    {
        return LoadAsync(FindExternalToolsRoot, progress, cancellationToken);
    }

    private static async ValueTask<ExternalProcessorRuntimeEnvironment> LoadAsync(
        Func<string?> resolveRoot,
        Action<long, long> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? configuredRoot = resolveRoot();
        if (configuredRoot is null || !Directory.Exists(configuredRoot))
        {
            return CreateEnvironment([], configuredRoot, cancellationToken);
        }

        string root = FileSystemPathGuard.ResolveExistingRoot(configuredRoot);
        string[] manifests = DiscoverManifestPaths(root, cancellationToken);
        if (manifests.Length > 0)
        {
            progress(0, manifests.Length);
        }

        long cumulativeBytes = 0;
        List<ExternalCombinerToolManifest> parsed = [];
        for (int index = 0; index < manifests.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = manifests[index];
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, path);
            long admittedLength = stream.Length;
            if (admittedLength > MaximumManifestBytes)
            {
                throw Bounds("A manifest exceeds the per-file byte limit.");
            }
            cumulativeBytes = checked(cumulativeBytes + admittedLength);
            if (cumulativeBytes > MaximumCumulativeManifestBytes)
            {
                throw Bounds("Manifest data exceeds the cumulative byte limit.");
            }

            byte[] content = new byte[checked((int)admittedLength)];
            await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
            if (stream.ReadByte() != -1 || stream.Length != admittedLength)
            {
                throw new IOException("An external tool manifest changed while it was being read.");
            }
            _ = FileSystemPathGuard.ResolveExistingFileUnderRoots(path, [root]);
            cancellationToken.ThrowIfCancellationRequested();
            ExternalCombinerToolManifest manifest =
                JsonSerializer.Deserialize<ExternalCombinerToolManifest>(content, ManifestJson) ??
                    throw new InvalidDataException("An external tool manifest is empty.");
            cancellationToken.ThrowIfCancellationRequested();
            parsed.Add(manifest);
            progress(index + 1, manifests.Length);
        }

        return CreateEnvironment(parsed, root, cancellationToken);
    }

    private static string[] DiscoverManifestPaths(string root, CancellationToken cancellationToken)
    {
        List<string> manifests = [];
        var pending = new Stack<(DirectoryInfo Directory, int Depth)>();
        pending.Push((new DirectoryInfo(root), 0));
        int visited = 0;
        while (pending.TryPop(out (DirectoryInfo Directory, int Depth) next))
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<FileSystemInfo> children = [];
            foreach (FileSystemInfo child in next.Directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++visited > MaximumVisitedEntries)
                {
                    throw Bounds("External tool discovery exceeds the filesystem entry limit.");
                }
                children.Add(child);
            }
            children.Sort(static (left, right) =>
            {
                int result = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                return result != 0 ? result : StringComparer.Ordinal.Compare(left.Name, right.Name);
            });
            for (int index = children.Count - 1; index >= 0; index--)
            {
                FileSystemInfo child = children[index];
                if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new ExternalEnvironmentLoadException(
                        ExternalProcessorEnvironmentIssueCodes.CandidateInvalid,
                        "External tool discovery cannot traverse reparse points.");
                }
                if ((child.Attributes & FileAttributes.Directory) != 0)
                {
                    if (next.Depth >= MaximumDepth)
                    {
                        throw Bounds("External tool discovery exceeds the nesting-depth limit.");
                    }
                    pending.Push(((DirectoryInfo)child, next.Depth + 1));
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(child.Name, "manifest.json"))
                {
                    manifests.Add(child.FullName);
                    if (manifests.Count > MaximumManifestCount)
                    {
                        throw Bounds("External tool discovery exceeds the manifest-count limit.");
                    }
                }
            }
        }

        return
        [
            .. manifests.OrderBy(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                StringComparer.OrdinalIgnoreCase).ThenBy(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                StringComparer.Ordinal),
        ];
    }

    private static ExternalProcessorRuntimeEnvironment CreateEnvironment(
        List<ExternalCombinerToolManifest> manifests,
        string? root,
        CancellationToken cancellationToken)
    {
        var registry = new ExternalCombinerToolRegistry(manifests);
        string toolRoot = root is null ? Path.Combine(AppContext.BaseDirectory, "external-tools") :
            Path.TrimEndingDirectorySeparator(root);
        var resolver = new ExternalCombinerToolResolver(registry, toolRoot);
        foreach (ExternalCombinerToolManifest manifest in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!resolver.TryResolve(
                    manifest.ToolBindingId,
                    out _,
                    out _,
                    out _,
                    cancellationToken))
            {
                throw new ExternalEnvironmentLoadException(
                    ExternalProcessorEnvironmentIssueCodes.CandidateInvalid,
                    "An external tool candidate failed executable identity validation.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();

        string stagingRoot = Path.Combine(Path.GetTempPath(), "nvt-fw-combiner", "external-tools");
        var readiness = new ExternalProcessorRuntimeDependencyInspector(
            registry,
            toolRoot,
            stagingRoot,
            TimeProvider.System);
        if (manifests.Count == 0)
        {
            return new(null, readiness, 0);
        }

        var runner = new SystemExternalProcessRunner();
        return new(
            new ExternalProcessorRouter(
                new LegacyCombinerPostbuildProcessor(
                    registry,
                    toolRoot,
                    stagingRoot,
                    runner),
                new ExternalCombinerProcessor(
                    registry,
                    toolRoot,
                    stagingRoot,
                    runner,
                    ExternalCombinerInvocationCatalog.All)),
            readiness,
            manifests.Count);
    }

    private static string? FindExternalToolsRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "external-tools");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static ExternalEnvironmentLoadException Bounds(string message)
    {
        return new(ExternalProcessorEnvironmentIssueCodes.BoundsExceeded, message);
    }

    private sealed class ExternalEnvironmentLoadException(
        string code,
        string message) : Exception(message)
    {
        internal string Code { get; } = code;
    }

    private sealed class UnpublishedReadinessProvider : IRuntimeDependencyReadinessProvider
    {
        internal static UnpublishedReadinessProvider Instance { get; } = new();

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (generation != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(generation),
                    generation,
                    "An unpublished external environment uses generation zero.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new RuntimeDependencyReadinessSnapshot(
                request.RouteId,
                request.CapabilityFingerprint,
                request.CompilationFingerprint,
                request.ResolutionToken,
                request.AuthoringRevision,
                generation,
                TimeProvider.System.GetUtcNow().ToUniversalTime(),
                request.Dependencies.Select(static dependency =>
                    RuntimeDependencyEntry.Blocked(
                        dependency.ProcessorId,
                        dependency.ToolBindingId,
                        ExternalProcessorEnvironmentIssueCodes.EnvironmentUnavailable,
                        "The external processor environment is not currently published."))));
        }
    }
}
