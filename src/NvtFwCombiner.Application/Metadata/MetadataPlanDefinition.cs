using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Trusted profile source retained by one metadata plan.</summary>
public sealed record MetadataPlanSourceIdentity
{
    /// <summary>Creates one exact profile and bundle identity.</summary>
    public MetadataPlanSourceIdentity(
        string profileId,
        string profileVersion,
        string trustedDefinitionSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(trustedDefinitionSha256);
        if (!CapabilityRouteIdentity.IsSha256(trustedDefinitionSha256))
        {
            throw new ArgumentException(
                "Metadata plan sources require an exact lowercase SHA-256 trusted definition.",
                nameof(trustedDefinitionSha256));
        }

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        TrustedDefinitionSha256 = trustedDefinitionSha256;
    }

    /// <summary>Exact profile which authored the metadata plan.</summary>
    public string ProfileId { get; }

    /// <summary>Exact profile version which authored the metadata plan.</summary>
    public string ProfileVersion { get; }

    /// <summary>Exact trusted bundle which authored the metadata plan.</summary>
    public string TrustedDefinitionSha256 { get; }
}

/// <summary>Typed report-classification projection retained by a metadata plan.</summary>
public sealed record MetadataPlanReportProjection
{
    /// <summary>Creates one exact report source-space to authoring-slot projection.</summary>
    public MetadataPlanReportProjection(string spaceId, string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        SpaceId = spaceId;
        SlotId = slotId;
    }

    /// <summary>Canonical metadata address space.</summary>
    public string SpaceId { get; }

    /// <summary>Authoring slot that supplies the report source.</summary>
    public string SlotId { get; }
}

/// <summary>Immutable pre-publication metadata plan definition.</summary>
public sealed class MetadataPlanDefinition
{
    private readonly MetadataPlanEntry[] _entries;
    private readonly MetadataPlanReportProjection[] _reportProjections;

    /// <summary>Creates one deterministic plan from canonical references.</summary>
    public MetadataPlanDefinition(
        IEnumerable<MetadataPlanEntry> entries,
        MetadataPlanSourceIdentity? sourceIdentity = null,
        IEnumerable<MetadataPlanReportProjection>? reportProjections = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [.. entries];
        if (_entries.Any(static entry => entry is null) ||
            _entries.Select(static entry => entry.BindingId)
                .Distinct(StringComparer.Ordinal).Count() != _entries.Length)
        {
            throw new ArgumentException(
                "Metadata plan bindings must be non-null and unique.",
                nameof(entries));
        }

        if (_entries.Length != 0)
        {
            MetadataPlanEntry first = _entries[0];
            if (_entries.Any(entry =>
                    !ReferenceEquals(
                        entry.FamilyDefinition,
                        first.FamilyDefinition) ||
                    !ReferenceEquals(entry.ResolvedMap, first.ResolvedMap)))
            {
                throw new ArgumentException(
                    "One metadata plan cannot mix family or map resolutions.",
                    nameof(entries));
            }
        }

        MetadataPlanReportProjection[] entryReportProjections =
        [
            .. _entries
                .Where(static entry => entry.Purposes.Contains(
                    MetadataReferencePurpose.ReportClassification))
                .Select(static entry => new MetadataPlanReportProjection(
                    entry.SpaceId,
                    entry.SlotId)),
        ];
        _reportProjections =
        [
            .. reportProjections ?? entryReportProjections,
        ];
        if (_reportProjections.Any(static projection => projection is null) ||
            _reportProjections
                .Select(static projection =>
                    (projection.SpaceId, projection.SlotId))
                .Distinct()
                .Count() != _reportProjections.Length)
        {
            throw new ArgumentException(
                "Metadata report projections must be non-null and unique.",
                nameof(reportProjections));
        }

        Array.Sort(_entries, static (left, right) =>
            StringComparer.Ordinal.Compare(left.BindingId, right.BindingId));
        Array.Sort(_reportProjections, CompareReportProjections);
        Array.Sort(entryReportProjections, CompareReportProjections);
        if (!_reportProjections.SequenceEqual(entryReportProjections))
        {
            throw new ArgumentException(
                "Explicit report projections must match every report-classification metadata entry.",
                nameof(reportProjections));
        }

        if (_reportProjections.Length != 0 && sourceIdentity is null)
        {
            throw new ArgumentException(
                "Metadata report projections require an exact trusted source identity.",
                nameof(sourceIdentity));
        }

        SourceIdentity = sourceIdentity;
        Entries = Array.AsReadOnly(_entries);
        ReportProjections = Array.AsReadOnly(_reportProjections);
    }

    /// <summary>An empty plan for compatibility routes with no migrated metadata.</summary>
    public static MetadataPlanDefinition Empty { get; } = new([]);

    /// <summary>Canonical reference-only entries in stable binding order.</summary>
    public IReadOnlyList<MetadataPlanEntry> Entries { get; }

    /// <summary>Exact trusted profile source, when retained by this plan.</summary>
    public MetadataPlanSourceIdentity? SourceIdentity { get; }

    /// <summary>Typed report-classification projections in stable order.</summary>
    public IReadOnlyList<MetadataPlanReportProjection> ReportProjections { get; }

    /// <summary>Binds the plan to one immutable catalog publication.</summary>
    public ResolvedMetadataPlan Resolve(ResolutionToken resolutionToken)
    {
        resolutionToken.EnsureValid(nameof(resolutionToken));
        return new ResolvedMetadataPlan(this, resolutionToken);
    }

    private static int CompareReportProjections(
        MetadataPlanReportProjection left,
        MetadataPlanReportProjection right)
    {
        int space = StringComparer.Ordinal.Compare(
            left.SpaceId,
            right.SpaceId);
        return space != 0
            ? space
            : StringComparer.Ordinal.Compare(left.SlotId, right.SlotId);
    }
}
