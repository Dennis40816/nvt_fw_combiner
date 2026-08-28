using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Locks bounded discovery and atomic external-environment publication.</summary>
public sealed class ExternalProcessorEnvironmentLoaderTests
{
    /// <summary>Before first publication, dependency-bearing authoring receives a typed blocker instead of crashing.</summary>
    [Fact]
    public async Task UnpublishedEnvironmentProvidesBlockedReadinessWithoutProcessAuthority()
    {
        using var root = new TemporaryDirectory();
        var loader = new ExternalProcessorEnvironmentLoader(root.Path);

        ExternalProcessorEnvironmentLease lease = loader.AcquireCurrent();
        RuntimeDependencyReadinessSnapshot readiness = await lease.ReadinessProvider.RefreshAsync(
            new RuntimeDependencyReadinessRequest(
                "route-test",
                new string('a', 64),
                new string('b', 64),
                new ResolutionToken("catalog:test"),
                new AuthoringRevision(1),
                [new ExternalProcessorDependencyReference("processor", "binding")]),
            lease.Generation,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, lease.Generation);
        Assert.Null(lease.Processor);
        Assert.True(loader.IsCurrent(lease.Generation));
        RuntimeDependencyEntry blocked = Assert.Single(readiness.Entries);
        Assert.Equal(ResolvedChildReadiness.Blocked, blocked.Readiness);
        Assert.Equal(ExternalProcessorEnvironmentIssueCodes.EnvironmentUnavailable, blocked.IssueCode);

        Assert.True(Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken))).Succeeded);
        Assert.False(loader.IsCurrent(lease.Generation));
        Assert.Equal(1, loader.AcquireCurrent().Generation);
    }

    /// <summary>An empty reviewed root publishes a usable generation without fake manifest work.</summary>
    [Fact]
    public async Task EmptyEnvironmentPublishesOneImmutableGeneration()
    {
        using var root = new TemporaryDirectory();
        var loader = new ExternalProcessorEnvironmentLoader(root.Path);

        List<ExternalProcessorEnvironmentLoadUpdate> updates = await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken));
        ExternalProcessorEnvironmentLoadResult result = Terminal(updates);
        ExternalProcessorEnvironmentLease lease = loader.AcquireCurrent();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RequestGeneration);
        Assert.Equal(1, result.PublicationGeneration);
        Assert.Equal(0, result.ManifestCount);
        Assert.DoesNotContain(updates, static update => update.TotalWork is not null);
        Assert.Null(lease.Processor);
        Assert.True(loader.IsCurrent(lease.Generation));
        Assert.Equal(ExternalProcessorEnvironmentState.Current, loader.Current.State);
    }

    /// <summary>One manifest reports exact work before its immutable environment becomes current.</summary>
    [Fact]
    public async Task ValidManifestReportsExactWorkBeforePublishing()
    {
        using var root = new TemporaryDirectory();
        WriteTool(root.Path, "tool-a", "1.0.0", "binding-a", [1, 2, 3]);
        var loader = new ExternalProcessorEnvironmentLoader(root.Path);

        List<ExternalProcessorEnvironmentLoadUpdate> updates = await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken));

        Assert.Collection(
            updates,
            update => Assert.Equal((0L, 1L, false), Shape(update)),
            update => Assert.Equal((1L, 1L, false), Shape(update)),
            update => Assert.Equal((null, null, true), Shape(update)));
        Assert.True(Terminal(updates).Succeeded);
        Assert.NotNull(loader.AcquireCurrent().Processor);
    }

    /// <summary>A presentation progress failure cannot rewrite a successful environment terminal.</summary>
    [Fact]
    public async Task ProgressObserverFailureDoesNotChangeThePublishedResult()
    {
        ExternalProcessorRuntimeEnvironment environment =
            Environment(new StubExternalProcessor());
        var loader = new ExternalProcessorEnvironmentLoader(environment);

        ExternalProcessorEnvironmentLoadResult result =
            await ((IExternalProcessorEnvironmentLoader)loader).LoadToCompletionAsync(
                static (_, _) => throw new InvalidOperationException("observer failed"),
                TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestGeneration);
        Assert.Equal(2, result.PublicationGeneration);
        Assert.Equal(2, loader.AcquireCurrent().Generation);
    }

    /// <summary>A typed refresh failure retains the exact prior processor lease.</summary>
    [Fact]
    public async Task FailedRefreshRetainsThePublishedLease()
    {
        int invocation = 0;
        var processor = new StubExternalProcessor();
        var loader = new ExternalProcessorEnvironmentLoader((progress, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress(0, 1);
            progress(1, 1);
            return invocation++ == 0
                ? ValueTask.FromResult(Environment(processor))
                : ValueTask.FromException<ExternalProcessorRuntimeEnvironment>(
                    new InvalidDataException("invalid manifest"));
        });

        Assert.True(Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken))).Succeeded);
        ExternalProcessorEnvironmentLease first = loader.AcquireCurrent();
        ExternalProcessorEnvironmentLoadResult failed = Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken)));
        ExternalProcessorEnvironmentLease retained = loader.AcquireCurrent();

        Assert.Equal(ExternalProcessorEnvironmentLoadOutcome.Failed, failed.Outcome);
        Assert.True(failed.RetainedLastKnownGood);
        Assert.Equal(first.Generation, retained.Generation);
        Assert.Same(first.Processor, retained.Processor);
        Assert.Equal(ExternalProcessorEnvironmentState.LastKnownGood, loader.Current.State);
    }

    /// <summary>A newer request cancels and drains an older request before materializing.</summary>
    [Fact]
    public async Task NewerRequestSupersedesAndDrainsTheOlderRequest()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int invocation = 0;
        var loader = new ExternalProcessorEnvironmentLoader(async (progress, cancellationToken) =>
        {
            int attempt = Interlocked.Increment(ref invocation);
            progress(0, 1);
            if (attempt == 1)
            {
                firstEntered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            progress(1, 1);
            return Environment(new StubExternalProcessor());
        });

        Task<List<ExternalProcessorEnvironmentLoadUpdate>> first = ReadAsync(
            loader.LoadAsync(TestContext.Current.CancellationToken));
        await firstEntered.Task;
        Task<List<ExternalProcessorEnvironmentLoadUpdate>> second = ReadAsync(
            loader.LoadAsync(TestContext.Current.CancellationToken));

        ExternalProcessorEnvironmentLoadResult firstResult = Terminal(await first);
        ExternalProcessorEnvironmentLoadResult secondResult = Terminal(await second);
        Assert.Equal(ExternalProcessorEnvironmentLoadOutcome.Superseded, firstResult.Outcome);
        Assert.True(secondResult.Succeeded);
        Assert.Equal(2, secondResult.RequestGeneration);
        Assert.Equal(1, secondResult.PublicationGeneration);
        Assert.Equal(2, invocation);
    }

    /// <summary>Cancellation never consumes a publication generation or replaces the retained lease.</summary>
    [Fact]
    public async Task CancellationRetainsThePublishedGeneration()
    {
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstProcessor = new StubExternalProcessor();
        int invocation = 0;
        var loader = new ExternalProcessorEnvironmentLoader(async (progress, cancellationToken) =>
        {
            int attempt = Interlocked.Increment(ref invocation);
            progress(0, 1);
            if (attempt == 2)
            {
                secondStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            progress(1, 1);
            return Environment(firstProcessor);
        });
        Assert.True(Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken))).Succeeded);
        ExternalProcessorEnvironmentLease published = loader.AcquireCurrent();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        Task<List<ExternalProcessorEnvironmentLoadUpdate>> refresh =
            ReadAsync(loader.LoadAsync(cancellation.Token));
        await secondStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        ExternalProcessorEnvironmentLease retained = loader.AcquireCurrent();
        Assert.Equal(published.Generation, retained.Generation);
        Assert.Same(published.Processor, retained.Processor);
        Assert.Equal(ExternalProcessorEnvironmentState.Current, loader.Current.State);
        Assert.Equal(1, loader.Current.PublicationGeneration);
    }

    /// <summary>Concurrent acquisition sees the retained generation until a complete replacement commits.</summary>
    [Fact]
    public async Task ConcurrentAcquireDuringRefreshSeesOnlyCompleteGenerations()
    {
        var replacementStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReplacement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstProcessor = new StubExternalProcessor();
        var secondProcessor = new StubExternalProcessor();
        int invocation = 0;
        var loader = new ExternalProcessorEnvironmentLoader(async (progress, cancellationToken) =>
        {
            int attempt = Interlocked.Increment(ref invocation);
            progress(0, 1);
            if (attempt == 2)
            {
                replacementStarted.SetResult();
                await releaseReplacement.Task.WaitAsync(cancellationToken);
            }
            progress(1, 1);
            return Environment(attempt == 1 ? firstProcessor : secondProcessor);
        });
        Assert.True(Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken))).Succeeded);

        Task<List<ExternalProcessorEnvironmentLoadUpdate>> refresh = ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken));
        await replacementStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        ExternalProcessorEnvironmentLease[] retained = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(loader.AcquireCurrent)));

        Assert.All(retained, lease =>
        {
            Assert.Equal(1, lease.Generation);
            Assert.Same(firstProcessor, lease.Processor);
        });
        releaseReplacement.SetResult();
        Assert.True(Terminal(await refresh).Succeeded);
        Assert.Equal(2, loader.AcquireCurrent().Generation);
        Assert.Same(secondProcessor, loader.AcquireCurrent().Processor);
    }

    /// <summary>Malformed manifests and mismatched executable identities fail without a partial publication.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidCandidateFailsWithoutPublishing(bool corruptExecutable)
    {
        using var root = new TemporaryDirectory();
        if (corruptExecutable)
        {
            WriteTool(root.Path, "tool-a", "1.0.0", "binding-a", [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(root.Path, "tool-a", "1.0.0", "Tool.exe"), [4]);
        }
        else
        {
            string directory = Directory.CreateDirectory(
                Path.Combine(root.Path, "tool-a", "1.0.0")).FullName;
            File.WriteAllText(Path.Combine(directory, "manifest.json"), "{");
        }
        var loader = new ExternalProcessorEnvironmentLoader(root.Path);

        ExternalProcessorEnvironmentLoadResult result = Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken)));

        Assert.Equal(ExternalProcessorEnvironmentLoadOutcome.Failed, result.Outcome);
        Assert.Equal(0, result.PublicationGeneration);
        Assert.Equal(ExternalProcessorEnvironmentState.Unavailable, loader.Current.State);
        Assert.Contains(result.Issues, issue => issue.Code ==
            (corruptExecutable
                ? ExternalProcessorEnvironmentIssueCodes.CandidateInvalid
                : ExternalProcessorEnvironmentIssueCodes.ManifestInvalid));
    }

    /// <summary>Every closed traversal and aggregate byte bound fails before candidate construction.</summary>
    [Theory]
    [InlineData("depth")]
    [InlineData("entries")]
    [InlineData("manifests")]
    [InlineData("cumulative-bytes")]
    public async Task ClosedDiscoveryBoundsFailBeforePublishing(string scenario)
    {
        using var root = new TemporaryDirectory();
        PrepareBoundViolation(root.Path, scenario);
        var loader = new ExternalProcessorEnvironmentLoader(root.Path);

        ExternalProcessorEnvironmentLoadResult result = Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken)));

        Assert.Equal(ExternalProcessorEnvironmentLoadOutcome.Failed, result.Outcome);
        Assert.Contains(result.Issues, issue =>
            issue.Code == ExternalProcessorEnvironmentIssueCodes.BoundsExceeded);
        Assert.Equal(0, result.PublicationGeneration);
    }

    /// <summary>A reparse-point escape is rejected before any outside manifest can be read.</summary>
    [Fact]
    public async Task ReparsePointEscapeCannotEnterTheCandidate()
    {
        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(outside.Path, "manifest.json"), "{}");
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(root.Path, "escape"), outside.Path);
        }
        catch (Exception exception) when (exception is
            UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            return;
        }
        var loader = new ExternalProcessorEnvironmentLoader(root.Path);

        ExternalProcessorEnvironmentLoadResult result = Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken)));

        Assert.Equal(ExternalProcessorEnvironmentLoadOutcome.Failed, result.Outcome);
        Assert.Contains(result.Issues, issue =>
            issue.Code == ExternalProcessorEnvironmentIssueCodes.CandidateInvalid);
        Assert.Equal(0, result.PublicationGeneration);
    }

    /// <summary>A manifest above the closed byte bound cannot publish a candidate.</summary>
    [Fact]
    public async Task OversizedManifestFailsWithoutPublishing()
    {
        using var root = new TemporaryDirectory();
        string manifestDirectory = Path.Combine(root.Path, "tool", "1.0.0");
        _ = Directory.CreateDirectory(manifestDirectory);
        await File.WriteAllBytesAsync(
            Path.Combine(manifestDirectory, "manifest.json"),
            new byte[ExternalProcessorEnvironmentLoader.MaximumManifestBytes + 1],
            TestContext.Current.CancellationToken);
        var loader = new ExternalProcessorEnvironmentLoader(root.Path);

        ExternalProcessorEnvironmentLoadResult result = Terminal(await ReadAsync(loader.LoadAsync(
            TestContext.Current.CancellationToken)));

        Assert.Equal(ExternalProcessorEnvironmentLoadOutcome.Failed, result.Outcome);
        Assert.Contains(result.Issues, issue =>
            issue.Code == ExternalProcessorEnvironmentIssueCodes.BoundsExceeded);
        Assert.Equal(ExternalProcessorEnvironmentState.Unavailable, loader.Current.State);
        ExternalProcessorEnvironmentLease unavailable = loader.AcquireCurrent();
        Assert.Equal(0, unavailable.Generation);
        Assert.Null(unavailable.Processor);
    }

    private static (long? Completed, long? Total, bool Terminal) Shape(
        ExternalProcessorEnvironmentLoadUpdate update)
    {
        return (update.CompletedWork, update.TotalWork, update.Result is not null);
    }

    private static ExternalProcessorEnvironmentLoadResult Terminal(
        IReadOnlyList<ExternalProcessorEnvironmentLoadUpdate> updates)
    {
        Assert.NotEmpty(updates);
        ExternalProcessorEnvironmentLoadUpdate terminal = Assert.Single(
            updates,
            static update => update.Result is not null);
        Assert.Same(updates[^1], terminal);
        return terminal.Result!;
    }

    private static async Task<List<ExternalProcessorEnvironmentLoadUpdate>> ReadAsync(
        IAsyncEnumerable<ExternalProcessorEnvironmentLoadUpdate> updates)
    {
        var result = new List<ExternalProcessorEnvironmentLoadUpdate>();
        await foreach (ExternalProcessorEnvironmentLoadUpdate update in updates)
        {
            result.Add(update);
        }
        return result;
    }

    private static ExternalProcessorRuntimeEnvironment Environment(IExternalProcessor processor)
    {
        return new(processor, StubReadinessProvider.Instance, 1);
    }

    private static void WriteTool(
        string root,
        string toolId,
        string version,
        string bindingId,
        byte[] executable)
    {
        string directory = Path.Combine(root, toolId, version);
        _ = Directory.CreateDirectory(directory);
        string executablePath = Path.Combine(directory, "Tool.exe");
        File.WriteAllBytes(executablePath, executable);
        string sha = Convert.ToHexString(SHA256.HashData(executable)).ToLowerInvariant();
        File.WriteAllText(Path.Combine(directory, "manifest.json"), $$"""
            {
              "schemaVersion": "1.0",
              "toolBindingId": "{{bindingId}}",
              "toolId": "{{toolId}}",
              "toolVersion": "{{version}}",
              "displayName": "Test tool",
              "platform": "win-x64",
              "executableName": "Tool.exe",
              "sha256": "{{sha}}",
              "adapterId": "legacy-combiner-postbuild-v1",
              "inputMode": "in-place",
              "argumentTemplate": ["{staging.runDir}"],
              "workingDirectoryPolicy": "staging-directory",
              "timeoutSeconds": 30,
              "allowedExtraOutputFiles": []
            }
            """);
    }

    private static void PrepareBoundViolation(string root, string scenario)
    {
        switch (scenario)
        {
            case "depth":
                string current = root;
                for (int index = 0; index <= ExternalProcessorEnvironmentLoader.MaximumDepth; index++)
                {
                    current = Directory.CreateDirectory(Path.Combine(current, $"d{index:D2}")).FullName;
                }
                return;
            case "entries":
                for (int index = 0;
                     index <= ExternalProcessorEnvironmentLoader.MaximumVisitedEntries;
                     index++)
                {
                    File.WriteAllBytes(Path.Combine(root, $"entry-{index:D4}.txt"), []);
                }
                return;
            case "manifests":
                for (int index = 0;
                     index <= ExternalProcessorEnvironmentLoader.MaximumManifestCount;
                     index++)
                {
                    string directory = Directory.CreateDirectory(
                        Path.Combine(root, $"tool-{index:D3}")).FullName;
                    File.WriteAllText(Path.Combine(directory, "manifest.json"), "{}");
                }
                return;
            case "cumulative-bytes":
                const int count = 17;
                int paddingLength = ExternalProcessorEnvironmentLoader.MaximumManifestBytes - 600;
                string padding = new('x', paddingLength);
                for (int index = 0; index < count; index++)
                {
                    string directory = Directory.CreateDirectory(
                        Path.Combine(root, $"tool-{index:D2}", "1.0.0")).FullName;
                    File.WriteAllText(
                        Path.Combine(directory, "manifest.json"),
                        ManifestJson($"tool-{index:D2}", $"binding-{index:D2}", padding));
                }
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private static string ManifestJson(string toolId, string bindingId, string padding)
    {
        return $$"""
            {
              "schemaVersion": "1.0",
              "toolBindingId": "{{bindingId}}",
              "toolId": "{{toolId}}",
              "toolVersion": "1.0.0",
              "displayName": "Test tool",
              "platform": "win-x64",
              "executableName": "Tool.exe",
              "sha256": "{{new string('a', 64)}}",
              "adapterId": "legacy-combiner-postbuild-v1",
              "inputMode": "in-place",
              "argumentTemplate": ["{staging.runDir}"],
              "workingDirectoryPolicy": "staging-directory",
              "timeoutSeconds": 30,
              "allowedExtraOutputFiles": [],
              "padding": "{{padding}}"
            }
            """;
    }

    private sealed class StubExternalProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubReadinessProvider : IRuntimeDependencyReadinessProvider
    {
        internal static StubReadinessProvider Instance { get; } = new();

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nfc-external-environment-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
