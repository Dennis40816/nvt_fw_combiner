namespace NvtFwCombiner.Application.Configuration;

/// <summary>A canonical identity and display label supplied for one resolved scope, not an effect definition.</summary>
internal sealed record EventBufferFormatIdentity(string UniqueId, string DisplayName);

/// <summary>Untrusted editor values; integers intentionally retain invalid byte values for admission.</summary>
internal sealed record EventBufferFormatDraftEntry(
    string? UniqueId,
    string? AliasName,
    IReadOnlyList<int>? RecognitionValues);

/// <summary>One immutable admitted identity with display-only alias and normalized recognition bytes.</summary>
internal sealed class EventBufferFormatEntry
{
    internal EventBufferFormatEntry(EventBufferFormatIdentity identity, string? aliasName, IEnumerable<byte> values)
    {
        UniqueId = identity.UniqueId;
        AliasName = aliasName;
        DisplayName = string.IsNullOrWhiteSpace(aliasName) ? identity.DisplayName : aliasName;
        RecognitionValues = Array.AsReadOnly(values.Order().ToArray());
    }

    internal string UniqueId { get; }
    internal string? AliasName { get; }
    internal string DisplayName { get; }
    internal IReadOnlyList<byte> RecognitionValues { get; }
}

/// <summary>Configuration-valid for a supplied canonical scope; not firmware support or Build authority.</summary>
internal sealed class EventBufferFormatConfiguration
{
    internal EventBufferFormatConfiguration(string scopeId, IEnumerable<EventBufferFormatEntry> entries)
    {
        ScopeId = scopeId;
        Entries = Array.AsReadOnly(entries.OrderBy(entry => entry.UniqueId, StringComparer.Ordinal).ToArray());
    }

    internal string ScopeId { get; }
    internal IReadOnlyList<EventBufferFormatEntry> Entries { get; }

    /// <summary>Returns a configured match only in this exact scope; a nonmatch never implies Common.</summary>
    internal EventBufferFormatEntry? Match(string scopeId, byte value)
    {
        return StringComparer.Ordinal.Equals(ScopeId, scopeId)
            ? Entries.FirstOrDefault(entry => entry.RecognitionValues.Contains(value))
            : null;
    }
}
