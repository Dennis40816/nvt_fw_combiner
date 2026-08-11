using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>One typed draft-admission conflict layered over an otherwise exact layout.</summary>
public sealed record MemoryLayoutConflict
{
    internal MemoryLayoutConflict(
        string conflictId,
        ByteRange range,
        string message,
        IEnumerable<string> mappingIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conflictId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(mappingIds);
        string[] ids =
        [
            .. mappingIds
                .Select(id => string.IsNullOrWhiteSpace(id)
                    ? throw new ArgumentException(
                        "Conflict mapping ids must be non-empty.",
                        nameof(mappingIds))
                    : id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        ConflictId = conflictId;
        Range = range;
        Message = message;
        MappingIds = Array.AsReadOnly(ids);
    }

    /// <summary>Stable admission-issue identity.</summary>
    public string ConflictId { get; }
    /// <summary>Exact half-open conflicting output intersection.</summary>
    public ByteRange Range { get; }
    /// <summary>Application-owned diagnostic message.</summary>
    public string Message { get; }
    /// <summary>Stable involved mapping identities.</summary>
    public IReadOnlyList<string> MappingIds { get; }
}
