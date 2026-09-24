using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Configuration;

/// <summary>Serializes persistence and immutable publication, without runtime or firmware-selection authority.</summary>
internal sealed class EventBufferFormatConfigurationSession : IEventBufferFormatConfigurationSession, IDisposable
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
        : this(new EventBufferFormatConfigurationCatalog(scopeId, catalog, []), defaults, storage)
    {
    }

    internal EventBufferFormatConfigurationSession(
        FirmwareFamilyResolutionDefinition family, IEventBufferFormatConfigurationStorage storage)
        : this(EventBufferFormatConfigurationCatalog.FromFamily(family),
            [.. family.AbFormatPolicy!.Formats.Select(format => new EventBufferFormatDraftEntry(
                format.UniqueId, null, [.. format.DefaultRecognitionValues.Select(value => (int)value)]))], storage)
    {
    }

    private EventBufferFormatConfigurationSession(
        EventBufferFormatConfigurationCatalog catalog, IReadOnlyList<EventBufferFormatDraftEntry?> defaults,
        IEventBufferFormatConfigurationStorage storage)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(storage);
        Catalog = catalog;
        _scopeId = catalog.ScopeId;
        _catalog = [.. catalog.Identities];
        _defaults = EventBufferFormatConfigurationAdmission.Admit(_scopeId, _catalog, defaults).Configuration
            ?? throw new ArgumentException("Canonical default configuration must be valid.", nameof(defaults));
        _storage = storage;
    }

    public EventBufferFormatConfigurationCatalog Catalog { get; }

    public EventBufferFormatConfigurationState Current => Volatile.Read(ref _current);

    public IReadOnlyList<EventBufferFormatDraftEntry?> CreateDefaultsDraft()
    {
        return ToDraft(_defaults);
    }

    public IReadOnlyList<EventBufferFormatDraftEntry?>? CreateSavedDraft()
    {
        return Current.LastSaved is { } saved ? ToDraft(saved) : null;
    }

    public async ValueTask<EventBufferFormatConfigurationOperationResult> SaveAsync(
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

    public async ValueTask<EventBufferFormatConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EventBufferFormatStoredConfiguration? stored = await _storage.ReadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (stored is null)
            {
                return Publish(_defaults, BuiltInDefaultsHash(), EventBufferFormatConfigurationStatus.Ready,
                    null, [], usesBuiltInDefaults: true);
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
        EventBufferFormatConfigurationFailure? failure, IReadOnlyList<EventBufferFormatConfigurationIssue> issues,
        bool usesBuiltInDefaults = false)
    {
        EventBufferFormatConfigurationState previous = Current;
        var next = new EventBufferFormatConfigurationState(checked(previous.Generation + 1), status,
            configuration, hash, usesBuiltInDefaults ? previous.LastSaved : configuration ?? previous.LastSaved,
            usesBuiltInDefaults || configuration is null ? previous.LastSavedSha256 : hash)
        { UsesBuiltInDefaults = usesBuiltInDefaults };
        Volatile.Write(ref _current, next);
        return new(next, failure, issues);
    }

    private string BuiltInDefaultsHash()
    {
        // Versioned, length-prefixed canonical values; never represented as an on-disk file hash.
        var text = new StringBuilder("nfc:event-buffer-format:built-in:v1\n");
        Append(_defaults.ScopeId);
        foreach (EventBufferFormatEntry entry in _defaults.Entries)
        {
            Append(entry.UniqueId);
            Append(entry.DisplayName);
            Append(string.Join(",", entry.RecognitionValues.Select(static value => value.ToString("X2", CultureInfo.InvariantCulture))));
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));

        void Append(string value)
        {
            _ = text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }
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
