namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed compilation context declared by one trusted V2 profile.</summary>
internal enum CompositionProfileCompilationContextKind
{
    ResolvedMap,
    LogicalOutput,
}

/// <summary>Common exact trusted-family identity retained by every V2 profile context.</summary>
internal abstract class CompositionProfileCompilationContext
{
    protected CompositionProfileCompilationContext(
        CompositionProfileCompilationContextKind kind,
        string familyId,
        string familyVersion,
        string familyContentHash)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown composition-profile compilation context kind.");
        }

        Kind = kind;
        FamilyId = CompositionProfileValueRules.RequireId(familyId, nameof(familyId));
        FamilyVersion = CompositionProfileValueRules.RequireSemanticVersion(familyVersion, nameof(familyVersion));
        if (!CompositionProfileValueRules.IsLowercaseSha256(familyContentHash))
        {
            throw new ArgumentException(
                "Family content hash must be 64 lowercase hexadecimal characters.",
                nameof(familyContentHash));
        }

        FamilyContentHash = familyContentHash;
    }

    internal CompositionProfileCompilationContextKind Kind { get; }

    internal string FamilyId { get; }

    internal string FamilyVersion { get; }

    internal string FamilyContentHash { get; }
}

/// <summary>V2 context whose compilation is admitted only after canonical map resolution.</summary>
internal sealed class ResolvedMapProfileCompilationContext : CompositionProfileCompilationContext
{
    internal ResolvedMapProfileCompilationContext(CompositionProfileMapBinding mapBinding)
        : base(
            CompositionProfileCompilationContextKind.ResolvedMap,
            RequireBinding(mapBinding).FamilyId,
            mapBinding.FamilyVersion,
            mapBinding.FamilyContentHash)
    {
        MapBinding = mapBinding;
    }

    internal CompositionProfileMapBinding MapBinding { get; }

    private static CompositionProfileMapBinding RequireBinding(CompositionProfileMapBinding mapBinding)
    {
        return mapBinding ?? throw new ArgumentNullException(nameof(mapBinding));
    }
}

/// <summary>V2 context for General Merge logical output without a physical image-map claim.</summary>
internal sealed class LogicalOutputProfileCompilationContext : CompositionProfileCompilationContext
{
    private readonly string[] _memberIds;

    internal LogicalOutputProfileCompilationContext(
        string familyId,
        string familyVersion,
        string familyContentHash,
        IEnumerable<string> memberIds)
        : base(
            CompositionProfileCompilationContextKind.LogicalOutput,
            familyId,
            familyVersion,
            familyContentHash)
    {
        _memberIds = CompositionProfileValueRules.SnapshotIds(memberIds, nameof(memberIds), requireValue: true);
        MemberIds = Array.AsReadOnly(_memberIds);
    }

    internal IReadOnlyList<string> MemberIds { get; }
}
