namespace NvtFwCombiner.Application.Configuration;

/// <summary>Stable draft-admission failure categories.</summary>
public enum EventBufferFormatConfigurationIssueCode
{
    /// <summary>The entries collection is absent.</summary>
    MissingEntries,
    /// <summary>An entry is null.</summary>
    MissingEntry,
    /// <summary>The identity is not in the canonical catalog.</summary>
    UnknownIdentity,
    /// <summary>An identity appears more than once.</summary>
    DuplicateIdentity,
    /// <summary>The recognition collection is absent.</summary>
    MissingValues,
    /// <summary>A recognition value is not a byte.</summary>
    ValueOutOfRange,
    /// <summary>A byte appears twice in one entry.</summary>
    DuplicateValue,
    /// <summary>A byte is assigned to different identities.</summary>
    ConflictingValue,
}

/// <summary>One typed draft issue with entry and optional conflicting-value locations.</summary>
public sealed record EventBufferFormatConfigurationIssue(
    EventBufferFormatConfigurationIssueCode Code,
    int EntryIndex,
    int? RecognitionValue = null,
    int? RelatedEntryIndex = null);

/// <summary>An all-or-nothing configuration admission result; rejected drafts expose no partial snapshot.</summary>
internal sealed class EventBufferFormatConfigurationAdmissionResult
{
    internal EventBufferFormatConfigurationAdmissionResult(
        EventBufferFormatConfiguration? configuration,
        IEnumerable<EventBufferFormatConfigurationIssue> issues)
    {
        Configuration = configuration;
        Issues = Array.AsReadOnly(issues.ToArray());
        if ((configuration is null) == (Issues.Count == 0))
        {
            throw new ArgumentException("Admission must contain either configuration or issues.", nameof(issues));
        }
    }

    internal EventBufferFormatConfiguration? Configuration { get; }
    internal IReadOnlyList<EventBufferFormatConfigurationIssue> Issues { get; }
}

/// <summary>Owns draft validation, not firmware catalog creation, byte effects or runtime fallback policy.</summary>
internal static class EventBufferFormatConfigurationAdmission
{
    internal static EventBufferFormatConfigurationAdmissionResult Admit(
        string scopeId,
        IReadOnlyList<EventBufferFormatIdentity> supportedIdentities,
        IReadOnlyList<EventBufferFormatDraftEntry?>? draftEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentNullException.ThrowIfNull(supportedIdentities);
        // Malformed canonical declarations are a caller invariant, not user-draft recovery.
        var catalog = new Dictionary<string, EventBufferFormatIdentity>(StringComparer.Ordinal);
        foreach (EventBufferFormatIdentity identity in supportedIdentities)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentException.ThrowIfNullOrWhiteSpace(identity.UniqueId);
            ArgumentException.ThrowIfNullOrWhiteSpace(identity.DisplayName);
            if (!catalog.TryAdd(identity.UniqueId, identity))
            {
                throw new ArgumentException("Canonical identity keys must be unique.", nameof(supportedIdentities));
            }
        }

        if (draftEntries is null)
        {
            return new(null, [new(EventBufferFormatConfigurationIssueCode.MissingEntries, -1)]);
        }

        var issues = new List<EventBufferFormatConfigurationIssue>();
        var admitted = new List<EventBufferFormatEntry>();
        var identities = new Dictionary<string, int>(StringComparer.Ordinal);
        var assignedValues = new Dictionary<byte, int>();
        for (int index = 0; index < draftEntries.Count; index++)
        {
            EventBufferFormatDraftEntry? entry = draftEntries[index];
            if (entry is null)
            {
                issues.Add(new(EventBufferFormatConfigurationIssueCode.MissingEntry, index));
                continue;
            }

            if (entry.UniqueId is null || !catalog.TryGetValue(entry.UniqueId, out EventBufferFormatIdentity? identity))
            {
                issues.Add(new(EventBufferFormatConfigurationIssueCode.UnknownIdentity, index));
                continue;
            }

            if (!identities.TryAdd(entry.UniqueId, index))
            {
                issues.Add(new(EventBufferFormatConfigurationIssueCode.DuplicateIdentity, index,
                    RelatedEntryIndex: identities[entry.UniqueId]));
                continue;
            }

            if (entry.RecognitionValues is null)
            {
                issues.Add(new(EventBufferFormatConfigurationIssueCode.MissingValues, index));
                continue;
            }

            var values = new HashSet<byte>();
            foreach (int rawValue in entry.RecognitionValues)
            {
                if (rawValue is < byte.MinValue or > byte.MaxValue)
                {
                    issues.Add(new(EventBufferFormatConfigurationIssueCode.ValueOutOfRange, index, rawValue));
                    continue;
                }

                byte value = checked((byte)rawValue);
                if (!values.Add(value))
                {
                    issues.Add(new(EventBufferFormatConfigurationIssueCode.DuplicateValue, index, value));
                }
                else if (!assignedValues.TryAdd(value, index))
                {
                    issues.Add(new(EventBufferFormatConfigurationIssueCode.ConflictingValue, index, value,
                        assignedValues[value]));
                }
            }

            admitted.Add(new(identity, entry.AliasName, values));
        }

        return issues.Count == 0
            ? new(new EventBufferFormatConfiguration(scopeId, admitted), [])
            : new(null, issues);
    }
}
