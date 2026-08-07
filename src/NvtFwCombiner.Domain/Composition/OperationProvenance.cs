namespace NvtFwCombiner.Domain.Composition;

/// <summary>Traceable origin for a planned operation.</summary>
public sealed class OperationProvenance
{
    private const string BuiltInProfileKind = "built-in-profile";
    private const string RuntimeGeneralMappingKind = "runtime-general-mapping";
    private const string SavedRuleKind = "saved-rule";

    private OperationProvenance(string kind, string? sourceId, string? sourceVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        DomainInvariant.Reject(
            kind is not (BuiltInProfileKind or RuntimeGeneralMappingKind or SavedRuleKind),
            $"Unsupported operation provenance kind '{kind}'.", nameof(kind));

        if (kind is RuntimeGeneralMappingKind or SavedRuleKind)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        }

        if (kind == SavedRuleKind)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceVersion);
        }

        Kind = kind;
        SourceId = string.IsNullOrWhiteSpace(sourceId) ? null : sourceId;
        SourceVersion = string.IsNullOrWhiteSpace(sourceVersion) ? null : sourceVersion;
    }

    /// <summary>Canonical provenance for operations declared by a compiled profile.</summary>
    public static OperationProvenance BuiltInProfile { get; } = new(BuiltInProfileKind, null, null);

    /// <summary>Creates provenance for a runtime General Merge/Replace mapping row.</summary>
    public static OperationProvenance RuntimeGeneralMapping(string mappingId)
    {
        return new OperationProvenance(RuntimeGeneralMappingKind, mappingId, null);
    }

    /// <summary>Creates provenance for a reviewed saved rule fragment.</summary>
    public static OperationProvenance SavedRule(string ruleId, string ruleVersion)
    {
        return new OperationProvenance(SavedRuleKind, ruleId, ruleVersion);
    }

    /// <summary>Stable provenance kind used by JSON reports.</summary>
    public string Kind { get; }

    /// <summary>Mapping id, rule id, or null for built-in profile operations.</summary>
    public string? SourceId { get; }

    /// <summary>Rule version when <see cref="Kind"/> is <c>saved-rule</c>.</summary>
    public string? SourceVersion { get; }
}
