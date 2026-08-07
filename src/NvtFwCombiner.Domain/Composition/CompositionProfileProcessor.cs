namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed processor purpose.</summary>
internal enum CompositionProfileProcessorPurpose
{
    Checksum,
    Header,
    HeaderAndIntegrity,
    Relocation,
    CompositePostProcess,
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
        string targetSpaceId,
        IEnumerable<string> allowedReadViewIds,
        IEnumerable<string> allowedWriteViewIds)
    {
        ProcessorStageId = CanonicalPolicyValueRules.RequireCanonicalId(
            processorStageId,
            nameof(processorStageId));
        TargetSpaceId = CanonicalPolicyValueRules.RequireCanonicalId(targetSpaceId, nameof(targetSpaceId));
        _allowedReadViewIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            allowedReadViewIds,
            nameof(allowedReadViewIds),
            requireValue: true);
        _allowedWriteViewIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            allowedWriteViewIds,
            nameof(allowedWriteViewIds),
            requireValue: false);

        AllowedReadViewIds = Array.AsReadOnly(_allowedReadViewIds);
        AllowedWriteViewIds = Array.AsReadOnly(_allowedWriteViewIds);
    }

    internal string ProcessorStageId { get; }

    internal string TargetSpaceId { get; }

    internal IReadOnlyList<string> AllowedReadViewIds { get; }

    internal IReadOnlyList<string> AllowedWriteViewIds { get; }

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
            targetSpaceId,
            allowedReadViewIds,
            [])
    {
        _ = CanonicalProfileValueRules.RequireSemanticVersion(
            contractVersion,
            nameof(contractVersion));
        _ = CanonicalPolicyValueRules.RequireCanonicalId(
            calculationSetId,
            nameof(calculationSetId));
    }

}

/// <summary>One immutable source view staged into a processor target view.</summary>
internal sealed record CompositionProfileStagedSourceBinding
{
    internal CompositionProfileStagedSourceBinding(string sourceViewId, string targetViewId)
    {
        SourceViewId = CanonicalPolicyValueRules.RequireCanonicalId(sourceViewId, nameof(sourceViewId));
        TargetViewId = CanonicalPolicyValueRules.RequireCanonicalId(targetViewId, nameof(targetViewId));
    }

    internal string SourceViewId { get; }

    internal string TargetViewId { get; }
}

/// <summary>One named processor artifact sourced from an immutable or engine-owned profile view.</summary>
internal sealed record CompositionProfileStagedArtifactBinding
{
    internal CompositionProfileStagedArtifactBinding(string artifactId, string sourceViewId)
    {
        ArtifactId = CanonicalPolicyValueRules.RequireCanonicalId(artifactId, nameof(artifactId));
        SourceViewId = CanonicalPolicyValueRules.RequireCanonicalId(sourceViewId, nameof(sourceViewId));
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
        string? targetViewId = null)
        : base(
            processorStageId,
            targetSpaceId,
            allowedReadViewIds,
            RequireWrites(allowedWriteViewIds))
    {
        ToolBindingId = CanonicalProfileValueRules.RequireExternalToolBindingId(
            toolBindingId,
            nameof(toolBindingId));
        InvocationProfileId = CanonicalProfileValueRules.RequireInvocationProfileId(
            invocationProfileId,
            nameof(invocationProfileId));
        ClosedEnum.ThrowIfUndefined(purpose, "Unknown processor purpose.");

        if (integrityDisposition is not CompositionProfileIntegrityDisposition.None and
            not CompositionProfileIntegrityDisposition.RecalculateAndWrite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(integrityDisposition),
                integrityDisposition,
                "Legacy combiner integrity must be none or recalculate-and-write.");
        }

        ValidatePurposeIntegrity(purpose, integrityDisposition);
        _stagedSourceBindings = ImmutableReferenceSnapshot.Create(
            stagedSourceBindings,
            "Staged source bindings cannot contain null.");

        if (_stagedSourceBindings.Distinct().Count() != _stagedSourceBindings.Length)
        {
            throw new ArgumentException("Staged source bindings must be unique.", nameof(stagedSourceBindings));
        }

        Array.Sort(_stagedSourceBindings, CompareBindings);
        _stagedArtifactBindings = ImmutableReferenceSnapshot.Create(
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
        _ = CanonicalPolicyValueRules.RequireCanonicalId(evidenceRef, nameof(evidenceRef));
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

    internal CompositionProfileProcessorPurpose Purpose { get; }

    internal CompositionProfileIntegrityDisposition IntegrityDisposition { get; }

    internal IReadOnlyList<CompositionProfileStagedSourceBinding> StagedSourceBindings { get; }

    internal IReadOnlyList<CompositionProfileStagedArtifactBinding> StagedArtifactBindings { get; }

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
