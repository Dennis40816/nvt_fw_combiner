using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Closed purpose for one selected canonical metadata structure.</summary>
public enum MetadataInspectionPurpose
{
    /// <summary>The structure selects a physical map variant.</summary>
    MapResolution,

    /// <summary>The structure supplies typed validation facts.</summary>
    Validation,

    /// <summary>The structure supplies a compiled output-name token.</summary>
    OutputNaming,

    /// <summary>The structure supplies user-facing information.</summary>
    Display,

    /// <summary>The structure supplies a firmware version fact.</summary>
    Version,
}

/// <summary>Closed state of one selection-scoped metadata evaluation.</summary>
public enum MetadataInspectionState
{
    /// <summary>The selected artifact/part declares no such structure.</summary>
    NotDeclared,

    /// <summary>The immutable artifact snapshot is still loading.</summary>
    Loading,

    /// <summary>A declared prerequisite artifact is not present.</summary>
    WaitingForArtifact,

    /// <summary>The artifact cannot satisfy the declared locator or range.</summary>
    BlockedByArtifact,

    /// <summary>The declaration exists but evidence does not establish a value.</summary>
    Unknown,

    /// <summary>The declared bytes fail atomic typed decoding or assertions.</summary>
    Invalid,

    /// <summary>The declaration resolved to complete typed facts.</summary>
    Value,
}

/// <summary>Closed readiness state published to every Application client.</summary>
public enum ResolvedChildReadiness
{
    /// <summary>The selected capability does not declare this child.</summary>
    NotApplicable,

    /// <summary>One declared input must be loaded before resolution can continue.</summary>
    PendingInput,

    /// <summary>All inputs exist, but one declared contract rejects them.</summary>
    Blocked,

    /// <summary>The child resolved from the current immutable inputs.</summary>
    Ready,
}

/// <summary>Closed prerequisite action offered consistently to UI and CLI.</summary>
public enum ResolvedPrerequisiteActionKind
{
    /// <summary>Load the exact declared artifact slot before retrying inspection.</summary>
    LoadArtifactFirst,
}

/// <summary>Typed next action for one pending child resolution.</summary>
public sealed record ResolvedPrerequisiteAction(
    ResolvedPrerequisiteActionKind Kind,
    string ArtifactBindingId,
    string SlotId);

/// <summary>
/// One profile binding that references canonical map, metadata-set, structure,
/// and field definitions without copying their firmware semantics.
/// </summary>
public sealed class MetadataPlanEntry
{
    private readonly string[] _fieldIds;
    private readonly MetadataInspectionPurpose[] _purposes;

    /// <summary>Creates one checked reference-only metadata plan entry.</summary>
    public MetadataPlanEntry(
        string bindingId,
        string spaceId,
        string slotId,
        FirmwareFamilyResolutionDefinition familyDefinition,
        ResolvedFirmwareImageMap resolvedMap,
        FirmwareMapFactBinding<FirmwareMetadataSet> metadataSetBinding,
        FirmwareMetadataStructure structureDefinition,
        IEnumerable<string> fieldIds,
        IEnumerable<MetadataInspectionPurpose> purposes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(spaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(familyDefinition);
        ArgumentNullException.ThrowIfNull(resolvedMap);
        ArgumentNullException.ThrowIfNull(metadataSetBinding);
        ArgumentNullException.ThrowIfNull(structureDefinition);
        ArgumentNullException.ThrowIfNull(fieldIds);
        ArgumentNullException.ThrowIfNull(purposes);

        if (!familyDefinition.ImageMaps.Any(map =>
                ReferenceEquals(map, resolvedMap.ImageMap)) ||
            !familyDefinition.TryResolveStructure(
                resolvedMap.ImageMap.MapId,
                structureDefinition.StructureId,
                out FirmwareMetadataStructure? selectedStructure) ||
            !ReferenceEquals(selectedStructure, structureDefinition))
        {
            throw new ArgumentException(
                "Metadata plan structures must be selected by the exact resolved family map.",
                nameof(structureDefinition));
        }

        if (!resolvedMap.ImageMap.MetadataSetBindings.Any(binding =>
                ReferenceEquals(binding, metadataSetBinding)) ||
            !metadataSetBinding.Value.Structures.Any(structure =>
                ReferenceEquals(structure, structureDefinition)) ||
            !StringComparer.Ordinal.Equals(
                metadataSetBinding.EffectiveKey.MemberId,
                resolvedMap.MemberId) ||
            !StringComparer.Ordinal.Equals(
                metadataSetBinding.EffectiveKey.MapId,
                resolvedMap.ImageMap.MapId))
        {
            throw new ArgumentException(
                "Metadata plan entries must retain the exact applicable map fact binding.",
                nameof(metadataSetBinding));
        }

        if (!StringComparer.Ordinal.Equals(
                structureDefinition.ArtifactBindingId,
                spaceId))
        {
            throw new ArgumentException(
                "Metadata plan space must match the canonical structure artifact binding.",
                nameof(spaceId));
        }

        _fieldIds = [.. fieldIds];
        if (_fieldIds.Length == 0 ||
            _fieldIds.Any(string.IsNullOrWhiteSpace) ||
            _fieldIds.Distinct(StringComparer.Ordinal).Count() !=
                _fieldIds.Length)
        {
            throw new ArgumentException(
                "Metadata plan field references must be nonempty and unique.",
                nameof(fieldIds));
        }

        HashSet<string> declaredFields = new(
            structureDefinition.Fields.Select(static field => field.FieldId),
            StringComparer.Ordinal);
        if (_fieldIds.Any(fieldId => !declaredFields.Contains(fieldId)))
        {
            throw new ArgumentException(
                "Metadata plan fields must reference the canonical structure definition.",
                nameof(fieldIds));
        }

        _purposes = [.. purposes];
        if (_purposes.Length == 0 ||
            _purposes.Any(static purpose => !Enum.IsDefined(purpose)) ||
            _purposes.Distinct().Count() != _purposes.Length)
        {
            throw new ArgumentException(
                "Metadata plan purposes must be defined and unique.",
                nameof(purposes));
        }

        Array.Sort(_purposes);
        BindingId = bindingId;
        SpaceId = spaceId;
        SlotId = slotId;
        FamilyDefinition = familyDefinition;
        ResolvedMap = resolvedMap;
        MetadataSetBinding = metadataSetBinding;
        StructureDefinition = structureDefinition;
        FieldIds = Array.AsReadOnly(_fieldIds);
        Purposes = Array.AsReadOnly(_purposes);
    }

    /// <summary>Stable profile binding identity.</summary>
    public string BindingId { get; }

    /// <summary>Exact input artifact instance/address-space reference.</summary>
    public string SpaceId { get; }

    /// <summary>Authoring slot that supplies the referenced artifact instance.</summary>
    public string SlotId { get; }

    /// <summary>Canonical family definition reference.</summary>
    public FirmwareFamilyResolutionDefinition FamilyDefinition { get; }

    /// <summary>Exact canonical map resolution reference.</summary>
    public ResolvedFirmwareImageMap ResolvedMap { get; }

    /// <summary>Applicable canonical metadata-set binding reference.</summary>
    public FirmwareMapFactBinding<FirmwareMetadataSet> MetadataSetBinding { get; }

    /// <summary>Canonical structure definition reference.</summary>
    public FirmwareMetadataStructure StructureDefinition { get; }

    /// <summary>Selected canonical field definition ids.</summary>
    public IReadOnlyList<string> FieldIds { get; }

    /// <summary>Selected inspection/validation/formatting purposes.</summary>
    public IReadOnlyList<MetadataInspectionPurpose> Purposes { get; }
}

/// <summary>Immutable pre-publication metadata plan definition.</summary>
public sealed class MetadataPlanDefinition
{
    private readonly MetadataPlanEntry[] _entries;

    /// <summary>Creates one deterministic plan from canonical references.</summary>
    public MetadataPlanDefinition(IEnumerable<MetadataPlanEntry> entries)
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

        Array.Sort(_entries, static (left, right) =>
            StringComparer.Ordinal.Compare(left.BindingId, right.BindingId));
        Entries = Array.AsReadOnly(_entries);
    }

    /// <summary>An empty plan for compatibility routes with no migrated metadata.</summary>
    public static MetadataPlanDefinition Empty { get; } = new([]);

    /// <summary>Canonical reference-only entries in stable binding order.</summary>
    public IReadOnlyList<MetadataPlanEntry> Entries { get; }

    /// <summary>Binds the plan to one immutable catalog publication.</summary>
    public ResolvedMetadataPlan Resolve(ResolutionToken resolutionToken)
    {
        return new ResolvedMetadataPlan(this, resolutionToken);
    }
}

/// <summary>One publication-bound metadata entry with initial prerequisite state.</summary>
public sealed record ResolvedMetadataPlanEntry(
    MetadataPlanEntry Definition,
    MetadataInspectionState State);

/// <summary>Application-owned metadata plan bound to one capability resolution token.</summary>
public sealed class ResolvedMetadataPlan
{
    private readonly ResolvedMetadataPlanEntry[] _entries;

    internal ResolvedMetadataPlan(
        MetadataPlanDefinition definition,
        ResolutionToken resolutionToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _entries =
        [
            .. definition.Entries.Select(static entry =>
                new ResolvedMetadataPlanEntry(
                    entry,
                    MetadataInspectionState.WaitingForArtifact)),
        ];
        Definition = definition;
        ResolutionToken = resolutionToken;
        Entries = Array.AsReadOnly(_entries);
    }

    /// <summary>Canonical reference-only plan definition.</summary>
    public MetadataPlanDefinition Definition { get; }

    /// <summary>Publication token shared by the owning resolved capability.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Selected entries and their initial prerequisite state.</summary>
    public IReadOnlyList<ResolvedMetadataPlanEntry> Entries { get; }
}

/// <summary>One evaluated metadata plan entry and its Domain resolution evidence.</summary>
public sealed record MetadataInspectionResult(
    ResolvedMetadataPlanEntry PlanEntry,
    MetadataInspectionState State,
    FirmwareMetadataStructureResolution? Resolution,
    ResolvedChildReadiness Readiness,
    ResolvedPrerequisiteAction? NextAction);

/// <summary>One immutable metadata inspection request and authoring revision.</summary>
public sealed class MetadataInspectionRequest
{
    private readonly FirmwareArtifactPayload[] _artifacts;

    /// <summary>Creates one checked request over immutable artifact snapshots.</summary>
    public MetadataInspectionRequest(
        ResolvedMetadataPlan plan,
        long authoringRevision,
        IEnumerable<FirmwareArtifactPayload> artifacts)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfNegative(authoringRevision);
        ArgumentNullException.ThrowIfNull(artifacts);
        _artifacts = [.. artifacts];
        if (_artifacts.Any(static artifact => artifact is null) ||
            _artifacts.Select(static artifact => artifact.ArtifactId)
                .Distinct(StringComparer.Ordinal).Count() != _artifacts.Length)
        {
            throw new ArgumentException(
                "Metadata inspection artifacts must be non-null and uniquely bound.",
                nameof(artifacts));
        }

        Plan = plan;
        AuthoringRevision = authoringRevision;
        Artifacts = Array.AsReadOnly(_artifacts);
    }

    /// <summary>Exact publication-bound metadata plan.</summary>
    public ResolvedMetadataPlan Plan { get; }

    /// <summary>Monotonic authoring revision for selection and artifact state.</summary>
    public long AuthoringRevision { get; }

    /// <summary>Immutable artifact payload snapshots.</summary>
    public IReadOnlyList<FirmwareArtifactPayload> Artifacts { get; }
}

/// <summary>Immutable result of inspecting one resolved plan against artifact snapshots.</summary>
public sealed class MetadataInspectionSnapshot
{
    private readonly FirmwareArtifactIdentity[] _artifactIdentities;
    private readonly MetadataInspectionResult[] _results;

    internal MetadataInspectionSnapshot(
        ResolutionToken resolutionToken,
        IEnumerable<MetadataInspectionResult> results)
        : this(
            resolutionToken,
            authoringRevision: 0,
            artifactIdentities: [],
            results)
    {
    }

    internal MetadataInspectionSnapshot(
        ResolutionToken resolutionToken,
        long authoringRevision,
        IEnumerable<FirmwareArtifactIdentity> artifactIdentities,
        IEnumerable<MetadataInspectionResult> results)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(authoringRevision);
        ArgumentNullException.ThrowIfNull(artifactIdentities);
        ArgumentNullException.ThrowIfNull(results);
        _artifactIdentities = [.. artifactIdentities];
        if (_artifactIdentities.Any(static identity => identity is null) ||
            _artifactIdentities.Select(static identity => identity.ArtifactId)
                .Distinct(StringComparer.Ordinal).Count() !=
            _artifactIdentities.Length)
        {
            throw new ArgumentException(
                "Inspection artifact identities must be non-null and unique.",
                nameof(artifactIdentities));
        }

        Array.Sort(_artifactIdentities, static (left, right) =>
            StringComparer.Ordinal.Compare(left.ArtifactId, right.ArtifactId));
        _results = [.. results];
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        ArtifactIdentities = Array.AsReadOnly(_artifactIdentities);
        Results = Array.AsReadOnly(_results);
    }

    /// <summary>Capability publication token used for this inspection.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring revision evaluated by this snapshot.</summary>
    public long AuthoringRevision { get; }

    /// <summary>Exact artifact identities evaluated by this snapshot.</summary>
    public IReadOnlyList<FirmwareArtifactIdentity> ArtifactIdentities { get; }

    /// <summary>Entry results in canonical plan order.</summary>
    public IReadOnlyList<MetadataInspectionResult> Results { get; }
}

/// <summary>Common inspector that executes only the exact resolved slot declarations.</summary>
public static class FirmwareMetadataInspector
{
    /// <summary>Evaluates all plan entries against immutable artifact snapshots.</summary>
    public static MetadataInspectionSnapshot Inspect(
        ResolvedMetadataPlan plan,
        IEnumerable<FirmwareArtifactPayload> artifacts)
    {
        return Inspect(new MetadataInspectionRequest(
            plan,
            authoringRevision: 0,
            artifacts));
    }

    /// <summary>Evaluates one revision-bound request against immutable artifacts.</summary>
    public static MetadataInspectionSnapshot Inspect(MetadataInspectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResolvedMetadataPlan plan = request.Plan;
        FirmwareArtifactPayload[] artifactSnapshot = [.. request.Artifacts];
        if (plan.Entries.Count == 0)
        {
            return new MetadataInspectionSnapshot(
                plan.ResolutionToken,
                request.AuthoringRevision,
                artifactSnapshot.Select(static artifact => artifact.Identity),
                []);
        }

        MetadataPlanEntry first = plan.Entries[0].Definition;
        TopologySelection? requestedTopology =
            first.ResolvedMap.TopologySelection is
            {
                Source: TopologySelectionSource.Requested,
            } topology
                ? topology
                : null;
        var inputs = new FirmwareMapResolutionInputs(
            first.ResolvedMap.MemberId,
            first.ResolvedMap.ModeId,
            first.ResolvedMap.CapacityBytes,
            requestedTopology,
            artifactSnapshot);
        (ResolvedMetadataPlanEntry Entry, MetadataInspectionState State,
            FirmwareMetadataStructureResolution Resolution)[] evaluations =
        [
            .. plan.Entries.Select(entry => Evaluate(entry, inputs)),
        ];
        MetadataInspectionResult[] results =
        [
            .. evaluations.Select(evaluation => CreateResult(
                evaluation.Entry,
                evaluation.State,
                evaluation.Resolution,
                plan.Entries)),
        ];
        return new MetadataInspectionSnapshot(
            plan.ResolutionToken,
            request.AuthoringRevision,
            artifactSnapshot.Select(static artifact => artifact.Identity),
            results);
    }

    private static (
        ResolvedMetadataPlanEntry Entry,
        MetadataInspectionState State,
        FirmwareMetadataStructureResolution Resolution) Evaluate(
        ResolvedMetadataPlanEntry entry,
        FirmwareMapResolutionInputs inputs)
    {
        MetadataPlanEntry definition = entry.Definition;
        FirmwareMetadataStructureResolution resolution =
            definition.FamilyDefinition.ResolveMetadataStructure(
                definition.ResolvedMap.ImageMap.MapId,
                definition.StructureDefinition.StructureId,
                inputs);
        MetadataInspectionState state = resolution.Status switch
        {
            FirmwareMetadataStructureResolutionStatus.Pending =>
                MetadataInspectionState.WaitingForArtifact,
            FirmwareMetadataStructureResolutionStatus.Rejected
                when resolution.Failure ==
                    FirmwareMetadataStructureResolutionFailure
                        .StructureDecodeFailed =>
                MetadataInspectionState.Invalid,
            FirmwareMetadataStructureResolutionStatus.Rejected =>
                MetadataInspectionState.BlockedByArtifact,
            FirmwareMetadataStructureResolutionStatus.Resolved =>
                MetadataInspectionState.Value,
            _ => throw new InvalidOperationException(
                "Unknown firmware metadata structure resolution status."),
        };
        return (entry, state, resolution);
    }

    private static MetadataInspectionResult CreateResult(
        ResolvedMetadataPlanEntry entry,
        MetadataInspectionState state,
        FirmwareMetadataStructureResolution resolution,
        IReadOnlyList<ResolvedMetadataPlanEntry> planEntries)
    {
        ResolvedChildReadiness readiness = state switch
        {
            MetadataInspectionState.NotDeclared =>
                ResolvedChildReadiness.NotApplicable,
            MetadataInspectionState.Loading or
                MetadataInspectionState.WaitingForArtifact =>
                ResolvedChildReadiness.PendingInput,
            MetadataInspectionState.Value =>
                ResolvedChildReadiness.Ready,
            MetadataInspectionState.BlockedByArtifact or
                MetadataInspectionState.Unknown or
                MetadataInspectionState.Invalid =>
                ResolvedChildReadiness.Blocked,
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Unknown metadata inspection state."),
        };
        ResolvedPrerequisiteAction? nextAction = null;
        if (readiness == ResolvedChildReadiness.PendingInput &&
            resolution.Prerequisite is { } prerequisite)
        {
            ResolvedMetadataPlanEntry? prerequisiteEntry =
                planEntries.FirstOrDefault(candidate =>
                    StringComparer.Ordinal.Equals(
                        candidate.Definition.StructureDefinition.StructureId,
                        prerequisite.StructureId) &&
                    StringComparer.Ordinal.Equals(
                        candidate.Definition.SpaceId,
                        prerequisite.ArtifactBindingId));
            string slotId = prerequisiteEntry?.Definition.SlotId ??
                            prerequisite.ArtifactBindingId;
            nextAction = new ResolvedPrerequisiteAction(
                ResolvedPrerequisiteActionKind.LoadArtifactFirst,
                prerequisite.ArtifactBindingId,
                slotId);
        }

        return new MetadataInspectionResult(
            entry,
            state,
            resolution,
            readiness,
            nextAction);
    }
}

/// <summary>Stable logical DPCMI vocabulary shared by declarations and projections.</summary>
public static class DpcmiMetadataContract
{
    /// <summary>One Initial Code structure at CMD1 Page 0 registers 16h-18h.</summary>
    public const string StructureId = "dpcmi";

    /// <summary>First CMD1 Page 0 register in the structure.</summary>
    public const int FirstRegister = 0x16;

    /// <summary>Jira bits 7:0 in register 16h.</summary>
    public const string JiraLowFieldId = "jira-low";

    /// <summary>DP major version byte in register 17h.</summary>
    public const string MajorVersionFieldId = "dp-major";

    /// <summary>DP minor version nibble in register 18h bits 7:4.</summary>
    public const string MinorVersionFieldId = "dp-minor";

    /// <summary>Jira bits 11:8 in register 18h bits 3:0.</summary>
    public const string JiraHighFieldId = "jira-high";
}

/// <summary>Derived DP Version and Jira facts from one successfully decoded DPCMI structure.</summary>
public sealed record DpcmiMetadataFacts(
    byte MajorVersion,
    byte MinorVersion,
    ushort JiraNumber,
    ByteRange ResolvedRange)
{
    /// <summary>Four uppercase hexadecimal digits used by output naming.</summary>
    public string VersionToken =>
        FormattableString.Invariant($"{MajorVersion:X2}{MinorVersion:X2}");

    /// <summary>Technical Jira badge, absent only when the declared value is zero.</summary>
    public string? JiraBadge =>
        JiraNumber == 0 ? null : $"AUTO_PRJ-{JiraNumber}";
}

/// <summary>Projects DPCMI raw fields into their accepted derived semantic facts.</summary>
public static class DpcmiMetadataProjector
{
    /// <summary>Projects exactly one successful DPCMI result.</summary>
    public static bool TryProject(
        MetadataInspectionSnapshot snapshot,
        out DpcmiMetadataFacts facts)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        facts = null!;
        MetadataInspectionResult[] matches =
        [
            .. snapshot.Results.Where(result =>
                StringComparer.Ordinal.Equals(
                    result.PlanEntry.Definition.StructureDefinition.Definition.DefinitionId,
                    DpcmiMetadataContract.StructureId)),
        ];
        if (matches.Length != 1 ||
            matches[0].State != MetadataInspectionState.Value ||
            matches[0].Resolution?.Resolved is not { } resolved)
        {
            return false;
        }

        var values =
            resolved.DecodedStructure.Facts.ToDictionary(
                static fact => fact.FieldId,
                static fact => fact.Value,
                StringComparer.Ordinal);
        if (!TryReadByte(
                values,
                DpcmiMetadataContract.JiraLowFieldId,
                out byte jiraLow) ||
            !TryReadByte(
                values,
                DpcmiMetadataContract.MajorVersionFieldId,
                out byte major) ||
            !TryReadByte(
                values,
                DpcmiMetadataContract.MinorVersionFieldId,
                out byte minor) ||
            !TryReadByte(
                values,
                DpcmiMetadataContract.JiraHighFieldId,
                out byte jiraHigh) ||
            minor > 0x0F ||
            jiraHigh > 0x0F)
        {
            return false;
        }

        facts = new DpcmiMetadataFacts(
            major,
            minor,
            checked((ushort)(jiraLow | (jiraHigh << 8))),
            resolved.LocatorOutcome.ResolvedRange.Range);
        return true;
    }

    private static bool TryReadByte(
        Dictionary<string, FirmwareMetadataValue> values,
        string fieldId,
        out byte value)
    {
        value = 0;
        if (!values.TryGetValue(
                fieldId,
                out FirmwareMetadataValue? metadata) ||
            metadata.Kind != FirmwareMetadataValueKind.UnsignedInteger ||
            metadata.UnsignedIntegerValue is not { } unsigned ||
            unsigned > byte.MaxValue)
        {
            return false;
        }

        value = (byte)unsigned;
        return true;
    }
}
