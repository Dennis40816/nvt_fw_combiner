namespace NvtFwCombiner.Profiles.V2;

/// <summary>Monotonic evidence stage for one normalized composition profile.</summary>
internal enum CompositionProfilePromotionStage
{
    Known,
    MapResolvable,
    Inspectable,
    Authorable,
    Compilable,
    ExecutableCandidate,
    Supported,
}

/// <summary>Closed reason category preventing profile promotion.</summary>
internal enum CompositionProfileBlockerKind
{
    Map,
    Metadata,
    Operation,
    Processor,
    Integrity,
    Golden,
    HumanReview,
    Ui,
    Release,
}

/// <summary>One immutable promotion blocker with evidence manifest references.</summary>
internal sealed class CompositionProfilePromotionBlocker
{
    private readonly string[] _evidenceRefs;

    internal CompositionProfilePromotionBlocker(
        string blockerId,
        CompositionProfileBlockerKind kind,
        string reason,
        IEnumerable<string> evidenceRefs)
    {
        BlockerId = CompositionProfileValueRules.RequireId(blockerId, nameof(blockerId));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown promotion blocker kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _evidenceRefs = CompositionProfileValueRules.SnapshotIds(
            evidenceRefs,
            nameof(evidenceRefs),
            requireValue: false);

        Kind = kind;
        Reason = reason;
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    internal string BlockerId { get; }

    internal CompositionProfileBlockerKind Kind { get; }

    internal string Reason { get; }

    internal IReadOnlyList<string> EvidenceRefs { get; }
}

/// <summary>Immutable promotion stage and complete blocker set.</summary>
internal sealed class CompositionProfilePromotion
{
    private readonly CompositionProfilePromotionBlocker[] _blockers;

    internal CompositionProfilePromotion(
        CompositionProfilePromotionStage stage,
        IEnumerable<CompositionProfilePromotionBlocker> blockers)
    {
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown profile promotion stage.");
        }

        ArgumentNullException.ThrowIfNull(blockers);
        _blockers = [.. blockers];
        if (_blockers.Any(static blocker => blocker is null))
        {
            throw new ArgumentException("Promotion blockers cannot contain null.", nameof(blockers));
        }

        if (_blockers.Select(static blocker => blocker.BlockerId)
            .Distinct(StringComparer.Ordinal).Count() != _blockers.Length)
        {
            throw new ArgumentException("Promotion blocker ids must be ordinally unique.", nameof(blockers));
        }

        if (stage == CompositionProfilePromotionStage.Supported && _blockers.Length != 0)
        {
            throw new ArgumentException("Supported profiles cannot retain promotion blockers.", nameof(blockers));
        }

        Array.Sort(_blockers, static (left, right) =>
            StringComparer.Ordinal.Compare(left.BlockerId, right.BlockerId));
        Stage = stage;
        Blockers = Array.AsReadOnly(_blockers);
    }

    internal CompositionProfilePromotionStage Stage { get; }

    internal IReadOnlyList<CompositionProfilePromotionBlocker> Blockers { get; }
}
