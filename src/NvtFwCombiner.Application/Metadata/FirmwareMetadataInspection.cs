using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Firmware;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Closed read-only consumer purpose for canonical metadata references.</summary>
public enum MetadataReferencePurpose
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

    /// <summary>The generic inspector decodes the referenced target.</summary>
    Inspection,

    /// <summary>The common formatter presents the referenced target.</summary>
    Formatting,

    /// <summary>A copy policy may reference the target without gaining authority here.</summary>
    Copy,

    /// <summary>A relocation policy may reference the target without gaining authority here.</summary>
    Relocation,

    /// <summary>An integrity policy may reference the target without gaining authority here.</summary>
    Integrity,

    /// <summary>A processor policy may reference the target without gaining authority here.</summary>
    Processor,

    /// <summary>The typed memory projector consumes the target.</summary>
    MemoryProjection,

    /// <summary>Report difference classification consumes the target.</summary>
    ReportClassification,
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
    private readonly FirmwareMetadataReferenceTarget[] _targetReferences;
    private readonly MetadataReferencePurpose[] _purposes;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates one legacy field-only metadata plan entry.</summary>
    public MetadataPlanEntry(
        string bindingId,
        string spaceId,
        string slotId,
        FirmwareFamilyResolutionDefinition familyDefinition,
        ResolvedFirmwareImageMap resolvedMap,
        FirmwareMapFactBinding<FirmwareMetadataSet> metadataSetBinding,
        FirmwareMetadataStructure structureDefinition,
        IEnumerable<string> fieldIds,
        IEnumerable<MetadataReferencePurpose> purposes)
        : this(
            bindingId,
            spaceId,
            slotId,
            familyDefinition,
            resolvedMap,
            metadataSetBinding,
            structureDefinition,
            CreateFieldTargets(fieldIds),
            purposes,
            [])
    {
    }

    /// <summary>Creates one checked typed reference-only metadata plan entry.</summary>
    public MetadataPlanEntry(
        string bindingId,
        string spaceId,
        string slotId,
        FirmwareFamilyResolutionDefinition familyDefinition,
        ResolvedFirmwareImageMap resolvedMap,
        FirmwareMapFactBinding<FirmwareMetadataSet> metadataSetBinding,
        FirmwareMetadataStructure structureDefinition,
        IEnumerable<FirmwareMetadataReferenceTarget> targetReferences,
        IEnumerable<MetadataReferencePurpose> purposes,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(spaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(familyDefinition);
        ArgumentNullException.ThrowIfNull(resolvedMap);
        ArgumentNullException.ThrowIfNull(metadataSetBinding);
        ArgumentNullException.ThrowIfNull(structureDefinition);
        ArgumentNullException.ThrowIfNull(targetReferences);
        ArgumentNullException.ThrowIfNull(purposes);
        ArgumentNullException.ThrowIfNull(evidenceRefs);

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

        _targetReferences = [.. targetReferences];
        if (_targetReferences.Length == 0 ||
            _targetReferences.Any(static target => target is null) ||
            _targetReferences.Distinct().Count() != _targetReferences.Length)
        {
            throw new ArgumentException(
                "Metadata plan target references must be nonempty and unique.",
                nameof(targetReferences));
        }

        if (_targetReferences.Any(target =>
                !structureDefinition.Definition.ContainsReferenceTarget(target)))
        {
            throw new ArgumentException(
                "Metadata plan targets must reference the canonical structure definition.",
                nameof(targetReferences));
        }

        Array.Sort(_targetReferences, CompareTargets);
        _fieldIds =
        [
            .. _targetReferences
                .Where(static target =>
                    target.Kind == FirmwareMetadataReferenceTargetKind.Field)
                .Select(static target => target.TargetId),
        ];
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
        _evidenceRefs = [.. evidenceRefs];
        if (_evidenceRefs.Any(string.IsNullOrWhiteSpace) ||
            _evidenceRefs.Distinct(StringComparer.Ordinal).Count() !=
                _evidenceRefs.Length)
        {
            throw new ArgumentException(
                "Metadata plan evidence references must be nonblank and unique.",
                nameof(evidenceRefs));
        }

        Array.Sort(_evidenceRefs, StringComparer.Ordinal);
        BindingId = bindingId;
        SpaceId = spaceId;
        SlotId = slotId;
        FamilyDefinition = familyDefinition;
        ResolvedMap = resolvedMap;
        MetadataSetBinding = metadataSetBinding;
        StructureDefinition = structureDefinition;
        TargetReferences = Array.AsReadOnly(_targetReferences);
        FieldIds = Array.AsReadOnly(_fieldIds);
        Purposes = Array.AsReadOnly(_purposes);
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
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

    /// <summary>Selected canonical span/field/series/group references.</summary>
    public IReadOnlyList<FirmwareMetadataReferenceTarget> TargetReferences { get; }

    /// <summary>Legacy field-only projection derived from typed targets.</summary>
    public IReadOnlyList<string> FieldIds { get; }

    /// <summary>Selected read-only consumer purposes.</summary>
    public IReadOnlyList<MetadataReferencePurpose> Purposes { get; }

    /// <summary>Evidence supporting the typed target references.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

    private static FirmwareMetadataReferenceTarget[] CreateFieldTargets(
        IEnumerable<string> fieldIds)
    {
        ArgumentNullException.ThrowIfNull(fieldIds);
        return
        [
            .. fieldIds.Select(static fieldId =>
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Field,
                    fieldId)),
        ];
    }

    private static int CompareTargets(
        FirmwareMetadataReferenceTarget left,
        FirmwareMetadataReferenceTarget right)
    {
        int kind = left.Kind.CompareTo(right.Kind);
        return kind != 0
            ? kind
            : StringComparer.Ordinal.Compare(left.TargetId, right.TargetId);
    }
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
        resolutionToken.EnsureValid(nameof(resolutionToken));
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
        resolutionToken.EnsureValid(nameof(resolutionToken));
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
