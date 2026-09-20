using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Trusted profile or family-view source retained by one metadata plan.</summary>
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
    public string? ProfileId { get; }

    /// <summary>Exact profile version which authored the metadata plan.</summary>
    public string? ProfileVersion { get; }

    /// <summary>Exact trusted bundle which authored the metadata plan.</summary>
    public string TrustedDefinitionSha256 { get; }

    /// <summary>Exact family identity for a full-image view; null for profile plans.</summary>
    public string? FamilyId { get; }

    /// <summary>Exact family version for a full-image view.</summary>
    public string? FamilyVersion { get; }

    /// <summary>Hash of the canonical family source, distinct from its owning bundle hash.</summary>
    public string? FamilyContentHash { get; }

    /// <summary>Exact declared full-image view identity.</summary>
    public string? ViewId { get; }

    private MetadataPlanSourceIdentity(string familyId, string familyVersion,
        string familyContentHash, string viewId, string trustedBundleSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
        if (!CapabilityRouteIdentity.IsSha256(familyContentHash) ||
            !CapabilityRouteIdentity.IsSha256(trustedBundleSha256))
        {
            throw new ArgumentException("Full-image sources require exact family and bundle SHA-256 identities.");
        }

        FamilyId = familyId;
        FamilyVersion = familyVersion;
        FamilyContentHash = familyContentHash;
        ViewId = viewId;
        TrustedDefinitionSha256 = trustedBundleSha256;
    }

    /// <summary>Creates a family-view identity without inventing a profile.</summary>
    public static MetadataPlanSourceIdentity ForFullImageView(string familyId,
        string familyVersion, string familyContentHash, string viewId, string trustedBundleSha256)
    {
        return new(familyId, familyVersion, familyContentHash, viewId, trustedBundleSha256);
    }
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
        : this(entries, sourceIdentity, reportProjections, null)
    {
    }

    /// <summary>Creates a complete inspection-only plan, retaining even an explicitly empty view.</summary>
    public MetadataPlanDefinition(CanonicalFullImageMetadataContext context,
        IEnumerable<MetadataPlanEntry> entries)
        : this(entries, (context ?? throw new ArgumentNullException(nameof(context))).SourceIdentity, null, context)
    {
    }

    private MetadataPlanDefinition(IEnumerable<MetadataPlanEntry> entries,
        MetadataPlanSourceIdentity? sourceIdentity,
        IEnumerable<MetadataPlanReportProjection>? reportProjections,
        CanonicalFullImageMetadataContext? fullImageContext)
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
                    !ReferenceEquals(entry.ImageMap, first.ImageMap) ||
                    entry.MemberId != first.MemberId ||
                    !ReferenceEquals(entry.FullImageContext, fullImageContext) ||
                    (fullImageContext is null && !ReferenceEquals(entry.ResolvedMap, first.ResolvedMap))))
            {
                throw new ArgumentException(
                    "One metadata plan cannot mix family or map resolutions.",
                    nameof(entries));
            }
        }

        if (fullImageContext is not null &&
            (_entries.Length != fullImageContext.View.MetadataBindings.Count ||
             _entries.Any(entry => !ReferenceEquals(entry.FullImageContext, fullImageContext))))
        {
            throw new ArgumentException("A full-image plan must retain every exact selected view binding.", nameof(entries));
        }
        if (sourceIdentity?.ViewId is not null && fullImageContext is null)
        {
            throw new ArgumentException("Family-view identities require their checked full-image context.", nameof(sourceIdentity));
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

        if (_reportProjections.Length != 0 && (sourceIdentity is null || sourceIdentity.ViewId is not null))
        {
            throw new ArgumentException(
                "Metadata report projections require an exact trusted source identity.",
                nameof(sourceIdentity));
        }

        SourceIdentity = sourceIdentity;
        FullImageContext = fullImageContext;
        Entries = Array.AsReadOnly(_entries);
        ReportProjections = Array.AsReadOnly(_reportProjections);
    }

    /// <summary>An empty plan for compatibility routes with no migrated metadata.</summary>
    public static MetadataPlanDefinition Empty { get; } = new([]);

    /// <summary>Canonical reference-only entries in stable binding order.</summary>
    public IReadOnlyList<MetadataPlanEntry> Entries { get; }

    /// <summary>Exact trusted profile source, when retained by this plan.</summary>
    public MetadataPlanSourceIdentity? SourceIdentity { get; }

    /// <summary>Exact full-image view/member identity, including explicitly empty plans.</summary>
    public CanonicalFullImageMetadataContext? FullImageContext { get; }

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
