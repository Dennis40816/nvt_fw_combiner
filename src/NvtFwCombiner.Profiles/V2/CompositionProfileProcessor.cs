namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed normalized processor stage kind.</summary>
internal enum CompositionProfileProcessorKind
{
    CrcWorkerV1,
    LegacyCombinerV1,
}

/// <summary>Closed processor purpose.</summary>
internal enum CompositionProfileProcessorPurpose
{
    Checksum,
    Header,
    HeaderAndIntegrity,
    Relocation,
    CompositePostProcess,
}

/// <summary>Closed processor authority over staged bytes.</summary>
internal enum CompositionProfileProcessorAuthority
{
    Calculate,
    Transform,
}

/// <summary>Closed processor failure behavior.</summary>
internal enum CompositionProfileProcessorFailurePolicy
{
    FailClosed,
}

/// <summary>Closed integrity disposition owned by one processor stage.</summary>
internal enum CompositionProfileIntegrityDisposition
{
    None,
    VerifyExisting,
    RecalculateAndWrite,
}

/// <summary>Base value for one closed normalized processor stage.</summary>
internal abstract class CompositionProfileProcessorStage
{
    private readonly string[] _allowedReadViewIds;
    private readonly string[] _allowedWriteViewIds;

    protected CompositionProfileProcessorStage(
        string processorStageId,
        CompositionProfileProcessorKind kind,
        string targetSpaceId,
        IEnumerable<string> allowedReadViewIds,
        IEnumerable<string> allowedWriteViewIds)
    {
        ProcessorStageId = CompositionProfileValueRules.RequireId(
            processorStageId,
            nameof(processorStageId));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown processor stage kind.");
        }

        TargetSpaceId = CompositionProfileValueRules.RequireId(targetSpaceId, nameof(targetSpaceId));
        _allowedReadViewIds = CompositionProfileValueRules.SnapshotIds(
            allowedReadViewIds,
            nameof(allowedReadViewIds),
            requireValue: true);
        _allowedWriteViewIds = CompositionProfileValueRules.SnapshotIds(
            allowedWriteViewIds,
            nameof(allowedWriteViewIds),
            requireValue: false);

        Kind = kind;
        AllowedReadViewIds = Array.AsReadOnly(_allowedReadViewIds);
        AllowedWriteViewIds = Array.AsReadOnly(_allowedWriteViewIds);
    }

    internal string ProcessorStageId { get; }

    internal CompositionProfileProcessorKind Kind { get; }

    internal string TargetSpaceId { get; }

    internal IReadOnlyList<string> AllowedReadViewIds { get; }

    internal IReadOnlyList<string> AllowedWriteViewIds { get; }

    internal abstract CompositionProfileProcessorAuthority Authority { get; }

    internal abstract CompositionProfileProcessorPurpose Purpose { get; }

    internal abstract CompositionProfileIntegrityDisposition IntegrityDisposition { get; }

    internal static CompositionProfileProcessorFailurePolicy FailurePolicy =>
        CompositionProfileProcessorFailurePolicy.FailClosed;
}

/// <summary>Pure CRC calculation stage with no write authority.</summary>
internal sealed class CrcWorkerProfileProcessorStage : CompositionProfileProcessorStage
{
    internal CrcWorkerProfileProcessorStage(
        string processorStageId,
        string contractVersion,
        string calculationSetId,
        string targetSpaceId,
        IEnumerable<string> allowedReadViewIds)
        : base(
            processorStageId,
            CompositionProfileProcessorKind.CrcWorkerV1,
            targetSpaceId,
            allowedReadViewIds,
            [])
    {
        ContractVersion = CompositionProfileValueRules.RequireSemanticVersion(
            contractVersion,
            nameof(contractVersion));
        CalculationSetId = CompositionProfileValueRules.RequireId(
            calculationSetId,
            nameof(calculationSetId));
    }

    internal string ContractVersion { get; }

    internal string CalculationSetId { get; }

    internal override CompositionProfileProcessorAuthority Authority =>
        CompositionProfileProcessorAuthority.Calculate;

    internal override CompositionProfileProcessorPurpose Purpose => CompositionProfileProcessorPurpose.Checksum;

    internal override CompositionProfileIntegrityDisposition IntegrityDisposition =>
        CompositionProfileIntegrityDisposition.VerifyExisting;
}

/// <summary>One immutable source view staged into a processor target view.</summary>
internal sealed record CompositionProfileStagedSourceBinding
{
    internal CompositionProfileStagedSourceBinding(string sourceViewId, string targetViewId)
    {
        SourceViewId = CompositionProfileValueRules.RequireId(sourceViewId, nameof(sourceViewId));
        TargetViewId = CompositionProfileValueRules.RequireId(targetViewId, nameof(targetViewId));
    }

    internal string SourceViewId { get; }

    internal string TargetViewId { get; }
}

/// <summary>One named processor artifact sourced from an immutable or engine-owned profile view.</summary>
internal sealed record CompositionProfileStagedArtifactBinding
{
    internal CompositionProfileStagedArtifactBinding(string artifactId, string sourceViewId)
    {
        ArtifactId = CompositionProfileValueRules.RequireId(artifactId, nameof(artifactId));
        SourceViewId = CompositionProfileValueRules.RequireId(sourceViewId, nameof(sourceViewId));
    }

    internal string ArtifactId { get; }

    internal string SourceViewId { get; }
}

/// <summary>Approved legacy combiner transform stage over a host-created staging copy.</summary>
internal sealed class LegacyCombinerProfileProcessorStage : CompositionProfileProcessorStage
{
    private readonly CompositionProfileStagedSourceBinding[] _stagedSourceBindings;
    private readonly CompositionProfileStagedArtifactBinding[] _stagedArtifactBindings;

    internal LegacyCombinerProfileProcessorStage(
        string processorStageId,
        string toolBindingId,
        string invocationProfileId,
        string targetSpaceId,
        CompositionProfileProcessorPurpose purpose,
        CompositionProfileIntegrityDisposition integrityDisposition,
        IEnumerable<string> allowedReadViewIds,
        IEnumerable<string> allowedWriteViewIds,
        IEnumerable<CompositionProfileStagedSourceBinding> stagedSourceBindings,
        IEnumerable<CompositionProfileStagedArtifactBinding> stagedArtifactBindings,
        string evidenceRef,
        string schemaVersion = "2.0",
        string? targetViewId = null)
        : base(
            processorStageId,
            CompositionProfileProcessorKind.LegacyCombinerV1,
            targetSpaceId,
            allowedReadViewIds,
            RequireWrites(allowedWriteViewIds))
    {
        ToolBindingId = CompositionProfileValueRules.RequireExternalToolBindingId(
            toolBindingId,
            nameof(toolBindingId));
        InvocationProfileId = CompositionProfileValueRules.RequireLegacyCombinerInvocationProfileIdForSchemaVersion(
            schemaVersion,
            invocationProfileId,
            nameof(invocationProfileId));
        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown processor purpose.");
        }

        if (integrityDisposition is not CompositionProfileIntegrityDisposition.None and
            not CompositionProfileIntegrityDisposition.RecalculateAndWrite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(integrityDisposition),
                integrityDisposition,
                "Legacy combiner integrity must be none or recalculate-and-write.");
        }

        ValidatePurposeIntegrity(purpose, integrityDisposition);
        _stagedSourceBindings = Domain.Composition.ImmutableReferenceSnapshot.Create(
            stagedSourceBindings,
            "Staged source bindings cannot contain null.");

        if (_stagedSourceBindings.Distinct().Count() != _stagedSourceBindings.Length)
        {
            throw new ArgumentException("Staged source bindings must be unique.", nameof(stagedSourceBindings));
        }

        Array.Sort(_stagedSourceBindings, CompareBindings);
        _stagedArtifactBindings = Domain.Composition.ImmutableReferenceSnapshot.Create(
            stagedArtifactBindings,
            "Staged artifact bindings must be non-null with unique artifact ids.");
        if (_stagedArtifactBindings.Select(static binding => binding.ArtifactId).Distinct(StringComparer.Ordinal).Count() !=
            _stagedArtifactBindings.Length)
        {
            throw new ArgumentException(
                "Staged artifact bindings must be non-null with unique artifact ids.",
                nameof(stagedArtifactBindings));
        }

        Array.Sort(_stagedArtifactBindings, CompareArtifactBindings);
        EvidenceRef = CompositionProfileValueRules.RequireId(evidenceRef, nameof(evidenceRef));
        Purpose = purpose;
        IntegrityDisposition = integrityDisposition;
        TargetViewId = targetViewId;
        if (targetViewId is not null && !AllowedReadViewIds.Contains(targetViewId, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The Legacy Combiner target view must be declared as readable because its complete bytes enter staging.",
                nameof(targetViewId));
        }

        StagedSourceBindings = Array.AsReadOnly(_stagedSourceBindings);
        StagedArtifactBindings = Array.AsReadOnly(_stagedArtifactBindings);
    }

    internal string ToolBindingId { get; }

    internal string InvocationProfileId { get; }

    internal string? TargetViewId { get; }

    internal override CompositionProfileProcessorAuthority Authority =>
        CompositionProfileProcessorAuthority.Transform;

    internal override CompositionProfileProcessorPurpose Purpose { get; }

    internal override CompositionProfileIntegrityDisposition IntegrityDisposition { get; }

    internal IReadOnlyList<CompositionProfileStagedSourceBinding> StagedSourceBindings { get; }

    internal IReadOnlyList<CompositionProfileStagedArtifactBinding> StagedArtifactBindings { get; }

    internal string EvidenceRef { get; }

    private static string[] RequireWrites(IEnumerable<string> allowedWriteViewIds)
    {
        ArgumentNullException.ThrowIfNull(allowedWriteViewIds);
        string[] snapshot = [.. allowedWriteViewIds];
        return snapshot.Length != 0
            ? snapshot
            : throw new ArgumentException(
                "Legacy combiner stages require an allowed write view.",
                nameof(allowedWriteViewIds));
    }

    private static void ValidatePurposeIntegrity(
        CompositionProfileProcessorPurpose purpose,
        CompositionProfileIntegrityDisposition integrityDisposition)
    {
        if (integrityDisposition == CompositionProfileIntegrityDisposition.None &&
            purpose != CompositionProfileProcessorPurpose.Relocation)
        {
            throw new ArgumentException("Integrity disposition none is restricted to relocation stages.");
        }

        if (purpose is CompositionProfileProcessorPurpose.Checksum or
            CompositionProfileProcessorPurpose.Header or
            CompositionProfileProcessorPurpose.HeaderAndIntegrity &&
            integrityDisposition != CompositionProfileIntegrityDisposition.RecalculateAndWrite)
        {
            throw new ArgumentException("Integrity processor purposes require recalculate-and-write.");
        }
    }

    private static int CompareBindings(
        CompositionProfileStagedSourceBinding left,
        CompositionProfileStagedSourceBinding right)
    {
        int sourceComparison = StringComparer.Ordinal.Compare(left.SourceViewId, right.SourceViewId);
        return sourceComparison != 0
            ? sourceComparison
            : StringComparer.Ordinal.Compare(left.TargetViewId, right.TargetViewId);
    }

    private static int CompareArtifactBindings(
        CompositionProfileStagedArtifactBinding left,
        CompositionProfileStagedArtifactBinding right)
    {
        return StringComparer.Ordinal.Compare(left.ArtifactId, right.ArtifactId);
    }
}

/// <summary>Closed invalid-character policy for rendered output names.</summary>
internal enum CompositionProfileInvalidCharacterPolicy
{
    Reject,
    ReplaceUnderscore,
}

/// <summary>Immutable profile-controlled output naming policy.</summary>
internal sealed class CompositionProfileOutput
{
    private readonly string[] _requiredTokenIds;

    internal CompositionProfileOutput(
        string fileNameTemplate,
        bool allowOverride,
        CompositionProfileInvalidCharacterPolicy invalidCharacterPolicy,
        IEnumerable<string> requiredTokenIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameTemplate);
        if (!Enum.IsDefined(invalidCharacterPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(invalidCharacterPolicy),
                invalidCharacterPolicy,
                "Unknown output invalid-character policy.");
        }

        _requiredTokenIds = CompositionProfileValueRules.SnapshotIds(
            requiredTokenIds,
            nameof(requiredTokenIds),
            requireValue: false);
        FileNameTemplate = fileNameTemplate;
        AllowOverride = allowOverride;
        InvalidCharacterPolicy = invalidCharacterPolicy;
        RequiredTokenIds = Array.AsReadOnly(_requiredTokenIds);
    }

    internal string FileNameTemplate { get; }

    internal bool AllowOverride { get; }

    internal CompositionProfileInvalidCharacterPolicy InvalidCharacterPolicy { get; }

    internal IReadOnlyList<string> RequiredTokenIds { get; }
}
