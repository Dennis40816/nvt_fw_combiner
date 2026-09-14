using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;

namespace NvtFwCombiner.Application.Tests.Configuration;

/// <summary>Configuration lifetime, recovery and concurrency without firmware execution.</summary>
public sealed class EventBufferFormatConfigurationSessionTests
{
    private const string Scope = "test-ab-scope";

    /// <summary>Missing startup and deletion are identical, including a newly constructed session.</summary>
    [Fact]
    public async Task MissingNeverActivatesDefaultsOrLastSavedAsync()
    {
        var storage = new Storage();
        using EventBufferFormatConfigurationSession session = Create(storage);
        Assert.Equal(EventBufferFormatConfigurationStatus.NotLoaded, session.Current.Status);
        Assert.Null(session.CreateSavedDraft());
        Assert.NotEmpty(session.CreateDefaultsDraft());
        Assert.Equal(0, storage.Writes);
        Assert.Equal(EventBufferFormatConfigurationFailure.Missing, (await session.ReloadAsync(TestContext.Current.CancellationToken)).Failure);
        Assert.Null(session.Current.Configuration);
        Assert.True((await session.SaveAsync(session.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        EventBufferFormatConfiguration captured = session.Current.Configuration!;
        storage.Stored = null;
        _ = await session.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Null(session.Current.Configuration);
        Assert.Same(captured, session.Current.LastSaved);
        Assert.NotNull(session.CreateSavedDraft());
        using EventBufferFormatConfigurationSession restarted = Create(storage);
        Assert.Equal(EventBufferFormatConfigurationFailure.Missing, (await restarted.ReloadAsync(TestContext.Current.CancellationToken)).Failure);
        Assert.Null(restarted.Current.Configuration);
        Assert.Null(restarted.Current.LastSaved);
        Assert.NotNull(captured.Match(Scope, 0x97));
    }

    /// <summary>Invalid drafts and storage failures do not replace an accepted publication.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedSavePreservesPublishedAndPersistedStateAsync(bool ioFailure)
    {
        var storage = new Storage();
        using EventBufferFormatConfigurationSession session = Create(storage);
        _ = await session.SaveAsync(session.CreateDefaultsDraft(), TestContext.Current.CancellationToken);
        EventBufferFormatConfigurationState before = session.Current;
        EventBufferFormatStoredConfiguration? persisted = storage.Stored;
        if (ioFailure)
        {
            storage.WriteError = new IOException("test failure");
        }

        EventBufferFormatConfigurationOperationResult result = await session.SaveAsync([new("format-a", "changed", [ioFailure ? 0xA6 : 256])], TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Equal(ioFailure ? EventBufferFormatConfigurationFailure.SaveFailed
            : EventBufferFormatConfigurationFailure.InvalidValues, result.Failure);
        Assert.Same(before, session.Current);
        Assert.Same(persisted, storage.Stored);
    }

    /// <summary>A codec rejection during Save preserves both the publication and its recovery state.</summary>
    [Fact]
    public async Task StorageRejectedUnicodeSavePreservesCurrentAndRecoveryStateAsync()
    {
        var storage = new Storage();
        using EventBufferFormatConfigurationSession session = Create(storage);
        _ = await session.SaveAsync(session.CreateDefaultsDraft(), TestContext.Current.CancellationToken);
        EventBufferFormatConfigurationState before = session.Current;
        EventBufferFormatStoredConfiguration? stored = storage.Stored;
        storage.WriteError = new EventBufferFormatConfigurationFormatException();
        EventBufferFormatConfigurationOperationResult result = await session.SaveAsync(
            [new("format-a", "\uD800", [0x97])], TestContext.Current.CancellationToken);
        Assert.Equal(EventBufferFormatConfigurationFailure.InvalidDocument, result.Failure);
        Assert.Same(before, session.Current);
        Assert.Same(stored, storage.Stored);
    }

    /// <summary>External invalidity clears effective configuration but not captured or recovery snapshots.</summary>
    [Theory]
    [InlineData("malformed", "InvalidDocument")]
    [InlineData("unreadable", "ReadFailed")]
    [InlineData("scope", "ScopeMismatch")]
    [InlineData("unknown", "InvalidValues")]
    [InlineData("conflict", "InvalidValues")]
    public async Task InvalidReloadDoesNotFallBackAsync(string failure, string expected)
    {
        var storage = new Storage();
        using EventBufferFormatConfigurationSession session = Create(storage);
        _ = await session.SaveAsync(session.CreateDefaultsDraft(), TestContext.Current.CancellationToken);
        EventBufferFormatConfigurationState before = session.Current;
        switch (failure)
        {
            case "malformed":
                storage.ReadError = new EventBufferFormatConfigurationFormatException();
                break;
            case "unreadable":
                storage.ReadError = new IOException("test read error");
                break;
            case "scope":
                storage.Stored = new(new(1, "other", []), "external");
                break;
            case "unknown":
                storage.Stored = new(new(1, Scope, [new("not-in-catalog", null, ["0x97"])]), "external");
                break;
            case "conflict":
                storage.Stored = new(new(1, Scope,
                    [new("format-a", null, ["0xA6"]), new("format-b", null, ["0xa6"])]), "external");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
        }

        EventBufferFormatConfigurationOperationResult result = await session.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Failure.ToString());
        Assert.Equal(EventBufferFormatConfigurationStatus.Invalid, result.State.Status);
        Assert.Null(result.State.Configuration);
        Assert.Same(before.LastSaved, result.State.LastSaved);
        Assert.NotNull(before.Configuration!.Match(Scope, 0x97));
        Assert.Equal(before.Generation + 1, result.State.Generation);
    }

    /// <summary>Drafts are isolated from canonical defaults and published saved state.</summary>
    [Fact]
    public async Task DraftResetAndDiscardHaveNoSideEffectsAsync()
    {
        var values = new[] { 0x97 };
        EventBufferFormatIdentity[] catalog = [new("format-a", "A")];
        var storage = new Storage();
        using var session = new EventBufferFormatConfigurationSession(Scope, catalog, [new("format-a", "original", values)], storage);
        values[0] = 0;
        catalog[0] = new("changed", "changed");
        _ = await session.SaveAsync(session.CreateDefaultsDraft(), TestContext.Current.CancellationToken);
        EventBufferFormatConfigurationState before = session.Current;
        EventBufferFormatDraftEntry?[] draft = [.. session.CreateSavedDraft()!];
        draft[0] = new("format-a", "edited", [1]);
        Assert.Equal(0x97, Assert.Single(session.CreateDefaultsDraft()[0]!.RecognitionValues!));
        Assert.Equal("original", session.CreateSavedDraft()![0]!.AliasName);
        Assert.Same(before, session.Current);
        Assert.Equal(1, storage.Writes);
        Assert.Equal(0, storage.Reads);
    }

    /// <summary>Queued save snapshots before waiting, publishes only after write, and excludes reload.</summary>
    [Fact]
    public async Task QueuedSaveCopiesDraftAndSerializesReloadAsync()
    {
        var storage = new Storage { WriteHold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using EventBufferFormatConfigurationSession session = Create(storage);
        Task<EventBufferFormatConfigurationOperationResult> first = session.SaveAsync(session.CreateDefaultsDraft(), TestContext.Current.CancellationToken).AsTask();
        Assert.Equal(1, storage.Writes);
        Assert.Null(session.Current.Configuration);
        var values = new[] { 0xA6 };
        EventBufferFormatDraftEntry?[] draft = [new("format-a", "queued", values)];
        Task<EventBufferFormatConfigurationOperationResult> second = session.SaveAsync(draft, TestContext.Current.CancellationToken).AsTask();
        values[0] = 0;
        draft[0] = new("format-b", "mutated", [1]);
        Task<EventBufferFormatConfigurationOperationResult> reload = session.ReloadAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.Equal(0, storage.Reads);
        Assert.Equal(1, storage.Writes);
        storage.WriteHold.SetResult();
        Assert.True((await first).Succeeded);
        EventBufferFormatConfigurationOperationResult secondResult = await second;
        Assert.True(secondResult.Succeeded);
        Assert.Equal("queued", secondResult.State.Configuration!.Match(Scope, 0xA6)!.AliasName);
        Assert.Null(secondResult.State.Configuration.Match(Scope, 0));
        Assert.True((await reload).Succeeded);
        Assert.Equal(2, storage.Writes);
        Assert.Equal(1, storage.Reads);
        Assert.Equal(3, session.Current.Generation);
    }

    /// <summary>Cancellation while waiting or writing is not a successful publication.</summary>
    [Fact]
    public async Task CancelledSaveAndReloadDoNotPublishAsync()
    {
        var storage = new Storage { WriteHold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using EventBufferFormatConfigurationSession session = Create(storage);
        using var cancellation = new CancellationTokenSource();
        Task<EventBufferFormatConfigurationOperationResult> save = session.SaveAsync(session.CreateDefaultsDraft(), cancellation.Token).AsTask();
        Task<EventBufferFormatConfigurationOperationResult> reload = session.ReloadAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reload);
        Assert.Null(session.Current.Configuration);
        Assert.Equal(0, session.Current.Generation);
        Assert.Null(storage.Stored);
    }

    /// <summary>Successful persistence wins over cancellation arriving immediately after commit.</summary>
    [Fact]
    public async Task CancellationAfterCommitStillPublishesSuccessAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var storage = new Storage { AfterCommit = cancellation.Cancel };
        using EventBufferFormatConfigurationSession session = Create(storage);
        Assert.True((await session.SaveAsync(session.CreateDefaultsDraft(), cancellation.Token)).Succeeded);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.NotNull(session.Current.Configuration);
        Assert.Equal(storage.Stored!.SourceSha256, session.Current.SourceSha256);
    }

    /// <summary>Same-source reload advances publication while preserving its original byte hash.</summary>
    [Fact]
    public async Task SourceHashIsNotPublicationGenerationAsync()
    {
        var storage = new Storage { Stored = new(new(1, Scope, [new("format-a", "external", ["0xa6"])]), "actual-source-hash") };
        using EventBufferFormatConfigurationSession session = Create(storage);
        EventBufferFormatConfigurationOperationResult first = await session.ReloadAsync(TestContext.Current.CancellationToken);
        EventBufferFormatConfigurationOperationResult second = await session.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("actual-source-hash", first.State.SourceSha256);
        Assert.Equal(first.State.SourceSha256, second.State.SourceSha256);
        Assert.Equal(first.State.Generation + 1, second.State.Generation);
        Assert.NotNull(second.State.Configuration!.Match(Scope, 0xA6));
    }

    private static EventBufferFormatConfigurationSession Create(Storage storage)
    {
        return new(Scope, [new("format-a", "A"), new("format-b", "B")], [new("format-a", null, [0x97])], storage);
    }

    private sealed class Storage : IEventBufferFormatConfigurationStorage
    {
        internal EventBufferFormatStoredConfiguration? Stored { get; set; }
        internal Exception? ReadError { get; set; }
        internal Exception? WriteError { get; set; }
        internal TaskCompletionSource? WriteHold { get; init; }
        internal Action? AfterCommit { get; init; }
        internal int Reads { get; private set; }
        internal int Writes { get; private set; }

        public ValueTask<EventBufferFormatStoredConfiguration?> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return ReadError is { } error ? ValueTask.FromException<EventBufferFormatStoredConfiguration?>(error) : ValueTask.FromResult(Stored);
        }

        public async ValueTask<string> WriteAsync(EventBufferFormatConfigurationDocument document, CancellationToken cancellationToken)
        {
            Writes++;
            if (WriteHold is { } hold)
            {
                await hold.Task.WaitAsync(cancellationToken);
            }

            if (WriteError is { } error)
            {
                throw error;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Stored = new(document, $"stored-{Writes}");
            AfterCommit?.Invoke();
            return Stored.SourceSha256;
        }
    }
}
