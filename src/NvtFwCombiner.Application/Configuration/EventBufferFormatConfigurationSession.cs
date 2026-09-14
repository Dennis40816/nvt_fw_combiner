using System.Globalization;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;

namespace NvtFwCombiner.Application.Configuration;

/// <summary>Serializes persistence and immutable publication, without runtime or firmware-selection authority.</summary>
internal sealed class EventBufferFormatConfigurationSession : IDisposable
{
    private readonly string _scopeId;
    private readonly EventBufferFormatIdentity[] _catalog;
    private readonly EventBufferFormatConfiguration _defaults;
    private readonly IEventBufferFormatConfigurationStorage _storage;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private EventBufferFormatConfigurationState _current = new(
        0, EventBufferFormatConfigurationStatus.NotLoaded, null, null, null, null);

    internal EventBufferFormatConfigurationSession(
        string scopeId,
        IReadOnlyList<EventBufferFormatIdentity> catalog,
        IReadOnlyList<EventBufferFormatDraftEntry?> defaults,
        IEventBufferFormatConfigurationStorage storage)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(storage);
        _scopeId = scopeId;
        _catalog = [.. catalog];
        _defaults = EventBufferFormatConfigurationAdmission.Admit(scopeId, _catalog, defaults).Configuration
            ?? throw new ArgumentException("Canonical default configuration must be valid.", nameof(defaults));
        _storage = storage;
    }

    internal EventBufferFormatConfigurationState Current => Volatile.Read(ref _current);

    internal IReadOnlyList<EventBufferFormatDraftEntry?> CreateDefaultsDraft()
    {
        return ToDraft(_defaults);
    }

    internal IReadOnlyList<EventBufferFormatDraftEntry?>? CreateSavedDraft()
    {
        return Current.LastSaved is { } saved ? ToDraft(saved) : null;
    }

    internal async ValueTask<EventBufferFormatConfigurationOperationResult> SaveAsync(
        IReadOnlyList<EventBufferFormatDraftEntry?>? draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Admission takes defensive copies before queuing or any asynchronous storage work.
        EventBufferFormatConfigurationAdmissionResult admission =
            EventBufferFormatConfigurationAdmission.Admit(_scopeId, _catalog, draft);
        if (admission.Configuration is not { } configuration)
        {
            return new(Current, EventBufferFormatConfigurationFailure.InvalidValues, admission.Issues);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string hash = await _storage.WriteAsync(ToDocument(configuration), cancellationToken).ConfigureAwait(false);
            return Publish(configuration, hash, EventBufferFormatConfigurationStatus.Ready, null, []);
        }
        catch (IOException exception)
        {
            return new(Current, exception is EventBufferFormatConfigurationFormatException
                ? EventBufferFormatConfigurationFailure.InvalidDocument
                : EventBufferFormatConfigurationFailure.SaveFailed, []);
        }
        catch (UnauthorizedAccessException)
        {
            return new(Current, EventBufferFormatConfigurationFailure.SaveFailed, []);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    internal async ValueTask<EventBufferFormatConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EventBufferFormatStoredConfiguration? stored = await _storage.ReadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (stored is null)
            {
                return Publish(null, null, EventBufferFormatConfigurationStatus.Missing,
                    EventBufferFormatConfigurationFailure.Missing, []);
            }

            if (!StringComparer.Ordinal.Equals(_scopeId, stored.Document.ScopeId))
            {
                return Publish(null, stored.SourceSha256, EventBufferFormatConfigurationStatus.Invalid,
                    EventBufferFormatConfigurationFailure.ScopeMismatch, []);
            }

            // The storage port admits lexical JSON shape; Application alone admits catalog membership.
            EventBufferFormatDraftEntry[] entries = [.. stored.Document.Entries.Select(entry => new EventBufferFormatDraftEntry(
                entry.UniqueId, entry.AliasName,
                [.. entry.RecognitionValues.Select(value => int.Parse(value.AsSpan(2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture))]))];
            EventBufferFormatConfigurationAdmissionResult admission =
                EventBufferFormatConfigurationAdmission.Admit(_scopeId, _catalog, entries);
            return admission.Configuration is { } configuration
                ? Publish(configuration, stored.SourceSha256, EventBufferFormatConfigurationStatus.Ready, null, [])
                : Publish(null, stored.SourceSha256, EventBufferFormatConfigurationStatus.Invalid,
                    EventBufferFormatConfigurationFailure.InvalidValues, admission.Issues);
        }
        catch (IOException exception)
        {
            return Publish(null, null, EventBufferFormatConfigurationStatus.Invalid,
                exception is EventBufferFormatConfigurationFormatException
                    ? EventBufferFormatConfigurationFailure.InvalidDocument
                    : EventBufferFormatConfigurationFailure.ReadFailed, []);
        }
        catch (UnauthorizedAccessException)
        {
            return Publish(null, null, EventBufferFormatConfigurationStatus.Invalid,
                EventBufferFormatConfigurationFailure.ReadFailed, []);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private EventBufferFormatConfigurationOperationResult Publish(
        EventBufferFormatConfiguration? configuration, string? hash, EventBufferFormatConfigurationStatus status,
        EventBufferFormatConfigurationFailure? failure, IReadOnlyList<EventBufferFormatConfigurationIssue> issues)
    {
        EventBufferFormatConfigurationState previous = Current;
        var next = new EventBufferFormatConfigurationState(checked(previous.Generation + 1), status,
            configuration, hash, configuration ?? previous.LastSaved,
            configuration is null ? previous.LastSavedSha256 : hash);
        Volatile.Write(ref _current, next);
        return new(next, failure, issues);
    }

    private static EventBufferFormatConfigurationDocument ToDocument(EventBufferFormatConfiguration configuration)
    {
        return new(1, configuration.ScopeId, [.. configuration.Entries.Select(entry => new EventBufferFormatConfigurationEntryDocument(
            entry.UniqueId, entry.AliasName,
            Array.AsReadOnly(entry.RecognitionValues.Select(value => $"0x{value:X2}").ToArray())))]);
    }

    private static EventBufferFormatDraftEntry[] ToDraft(EventBufferFormatConfiguration configuration)
    {
        return [.. configuration.Entries.Select(entry => new EventBufferFormatDraftEntry(
            entry.UniqueId, entry.AliasName, [.. entry.RecognitionValues.Select(value => (int)value)]))];
    }

    /// <summary>The owner must stop operations before disposing the session.</summary>
    public void Dispose()
    {
        _gate.Dispose();
    }
}
