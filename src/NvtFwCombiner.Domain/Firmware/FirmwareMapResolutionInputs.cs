namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable identity of one named firmware artifact used for map resolution.</summary>
public sealed record FirmwareArtifactIdentity
{
    /// <summary>Creates a named artifact identity from verified bytes.</summary>
    public FirmwareArtifactIdentity(string artifactId, string sha256, long lengthBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lengthBytes);
        if (!IsLowercaseSha256(sha256))
        {
            throw new ArgumentException("Artifact SHA-256 must be 64 lowercase hexadecimal characters.", nameof(sha256));
        }

        ArtifactId = artifactId;
        Sha256 = sha256;
        LengthBytes = lengthBytes;
    }

    /// <summary>Stable artifact binding identifier.</summary>
    public string ArtifactId { get; }

    /// <summary>Lowercase SHA-256 of the immutable artifact bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Exact immutable artifact length.</summary>
    public long LengthBytes { get; }

    private static bool IsLowercaseSha256(string value)
    {
        return value.Length == 64 && value.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}

/// <summary>One decoded metadata value with exact artifact and structure provenance.</summary>
public sealed record FirmwareDecodedMetadataFact
{
    /// <summary>Creates one provenance-linked decoded metadata fact.</summary>
    public FirmwareDecodedMetadataFact(
        string factId,
        string artifactId,
        string metadataStructureId,
        string fieldId,
        FirmwareMetadataValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataStructureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        ArgumentNullException.ThrowIfNull(value);
        FactId = factId;
        ArtifactId = artifactId;
        MetadataStructureId = metadataStructureId;
        FieldId = fieldId;
        Value = value;
    }

    /// <summary>Stable decoded-fact identity used by derived selections.</summary>
    public string FactId { get; }

    /// <summary>Named immutable artifact that produced this fact.</summary>
    public string ArtifactId { get; }

    /// <summary>Canonical metadata structure whose locator produced this fact.</summary>
    public string MetadataStructureId { get; }

    /// <summary>Canonical field identifier inside the metadata structure.</summary>
    public string FieldId { get; }

    /// <summary>Typed decoded field value.</summary>
    public FirmwareMetadataValue Value { get; }
}

/// <summary>Derived Common FW category plus the decoded fact that selected it.</summary>
public sealed record FirmwareCommonCategorySelection
{
    /// <summary>Creates a category selection linked to one decoded fact.</summary>
    public FirmwareCommonCategorySelection(string categoryId, string sourceFactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFactId);
        CategoryId = categoryId;
        SourceFactId = sourceFactId;
    }

    /// <summary>Selected Common FW category id.</summary>
    public string CategoryId { get; }

    /// <summary>Decoded fact from which the category was derived.</summary>
    public string SourceFactId { get; }
}

/// <summary>Single public atomic input boundary for firmware-map resolution.</summary>
public sealed class FirmwareMapResolutionInputs
{
    private readonly FirmwareArtifactIdentity[] _artifacts;
    private readonly FirmwareDecodedMetadataFact[] _decodedFacts;

    /// <summary>Creates immutable resolution inputs from artifact and locator provenance.</summary>
    public FirmwareMapResolutionInputs(
        string memberId,
        string modeId,
        long capacityBytes,
        TopologySelection? topologySelection,
        FirmwareCommonCategorySelection? commonFirmwareCategory,
        IEnumerable<FirmwareArtifactIdentity> artifacts,
        IEnumerable<FirmwareDecodedMetadataFact> decodedFacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);

        ArgumentNullException.ThrowIfNull(artifacts);
        _artifacts = [.. artifacts];
        if (_artifacts.Length == 0)
        {
            throw new ArgumentException("Map resolution requires a named firmware artifact.", nameof(artifacts));
        }

        if (_artifacts.Any(static artifact => artifact is null))
        {
            throw new ArgumentException("Resolution artifacts cannot contain null.", nameof(artifacts));
        }

        EnsureUniqueIds(
            _artifacts.Select(static artifact => artifact.ArtifactId),
            nameof(artifacts),
            "Resolution artifact ids");
        Array.Sort(_artifacts, static (left, right) =>
            StringComparer.Ordinal.Compare(left.ArtifactId, right.ArtifactId));

        ArgumentNullException.ThrowIfNull(decodedFacts);
        _decodedFacts = [.. decodedFacts];
        if (_decodedFacts.Any(static fact => fact is null))
        {
            throw new ArgumentException("Decoded metadata facts cannot contain null.", nameof(decodedFacts));
        }

        EnsureUniqueIds(
            _decodedFacts.Select(static fact => fact.FactId),
            nameof(decodedFacts),
            "Decoded metadata fact ids");
        EnsureUniqueFactSources(_decodedFacts, nameof(decodedFacts));
        foreach (FirmwareDecodedMetadataFact fact in _decodedFacts)
        {
            if (!_artifacts.Any(artifact =>
                StringComparer.Ordinal.Equals(artifact.ArtifactId, fact.ArtifactId)))
            {
                throw new ArgumentException(
                    $"Decoded fact '{fact.FactId}' references unknown artifact '{fact.ArtifactId}'.",
                    nameof(decodedFacts));
            }
        }

        Array.Sort(_decodedFacts, static (left, right) =>
            StringComparer.Ordinal.Compare(left.FactId, right.FactId));
        ValidateDerivedSelections(topologySelection, commonFirmwareCategory, _decodedFacts);

        MemberId = memberId;
        ModeId = modeId;
        CapacityBytes = capacityBytes;
        TopologySelection = topologySelection;
        CommonFirmwareCategory = commonFirmwareCategory;
        Artifacts = Array.AsReadOnly(_artifacts);
        DecodedFacts = Array.AsReadOnly(_decodedFacts);
    }

    /// <summary>Selected IC member id.</summary>
    public string MemberId { get; }

    /// <summary>Selected firmware mode id.</summary>
    public string ModeId { get; }

    /// <summary>Selected exact image capacity.</summary>
    public long CapacityBytes { get; }

    /// <summary>Requested or derived topology, when required.</summary>
    public TopologySelection? TopologySelection { get; }

    /// <summary>Derived Common FW category, when available.</summary>
    public FirmwareCommonCategorySelection? CommonFirmwareCategory { get; }

    /// <summary>Named immutable artifact identities in ordinal id order.</summary>
    public IReadOnlyList<FirmwareArtifactIdentity> Artifacts { get; }

    /// <summary>Decoded locator facts in ordinal fact-id order.</summary>
    public IReadOnlyList<FirmwareDecodedMetadataFact> DecodedFacts { get; }

    private static void ValidateDerivedSelections(
        TopologySelection? topologySelection,
        FirmwareCommonCategorySelection? commonFirmwareCategory,
        IReadOnlyList<FirmwareDecodedMetadataFact> decodedFacts)
    {
        if (topologySelection?.Source == TopologySelectionSource.Derived &&
            !decodedFacts.Any(fact => StringComparer.Ordinal.Equals(fact.FactId, topologySelection.SourceId)))
        {
            throw new ArgumentException(
                "Derived topology source must reference a decoded metadata fact.",
                nameof(topologySelection));
        }

        if (commonFirmwareCategory is not null &&
            !decodedFacts.Any(fact =>
                StringComparer.Ordinal.Equals(fact.FactId, commonFirmwareCategory.SourceFactId)))
        {
            throw new ArgumentException(
                "Common FW category source must reference a decoded metadata fact.",
                nameof(commonFirmwareCategory));
        }
    }

    private static void EnsureUniqueIds(
        IEnumerable<string> ids,
        string parameterName,
        string subject)
    {
        string[] snapshot = [.. ids];
        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException($"{subject} must be ordinally unique.", parameterName);
        }
    }

    private static void EnsureUniqueFactSources(
        IEnumerable<FirmwareDecodedMetadataFact> facts,
        string parameterName)
    {
        HashSet<(string ArtifactId, string MetadataStructureId, string FieldId)> sources = [];
        foreach (FirmwareDecodedMetadataFact fact in facts)
        {
            if (!sources.Add((fact.ArtifactId, fact.MetadataStructureId, fact.FieldId)))
            {
                throw new ArgumentException(
                    "Decoded metadata fact sources must be ordinally unique.",
                    parameterName);
            }
        }
    }
}
