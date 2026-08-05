namespace NvtFwCombiner.Profiles.V2;

/// <summary>Common exact trusted-family identity retained by every V2 profile context.</summary>
internal abstract class CompositionProfileCompilationContext
{
    protected CompositionProfileCompilationContext(
        string familyId,
        string familyVersion,
        string familyContentHash)
    {
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

    internal string FamilyId { get; }

    internal string FamilyVersion { get; }

    internal string FamilyContentHash { get; }
}

/// <summary>V2 context whose compilation is admitted only after canonical map resolution.</summary>
internal sealed class ResolvedMapProfileCompilationContext : CompositionProfileCompilationContext
{
    internal ResolvedMapProfileCompilationContext(CompositionProfileMapBinding mapBinding)
        : base(
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
        string schemaVersion,
        string familyId,
        string familyVersion,
        string familyContentHash,
        IEnumerable<string> memberIds)
        : base(
            familyId,
            familyVersion,
            familyContentHash)
    {
        _memberIds = CompositionProfileValueRules.SnapshotLogicalMemberIds(
            schemaVersion,
            memberIds,
            nameof(memberIds));
        MemberIds = Array.AsReadOnly(_memberIds);
    }

    internal IReadOnlyList<string> MemberIds { get; }
}

/// <summary>Map-bound V2 declaration lowered from typed reference-clone mappings at compile time.</summary>
internal sealed class RuntimeReferenceReplaceProfileCompilationContext : CompositionProfileCompilationContext
{
    internal RuntimeReferenceReplaceProfileCompilationContext(
        string schemaVersion,
        CompositionProfileMapBinding mapBinding)
        : base(
            RequireBinding(mapBinding).FamilyId,
            mapBinding.FamilyVersion,
            mapBinding.FamilyContentHash)
    {
        MapBinding = mapBinding;
        AllowsConditionalProcessor = schemaVersion is "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15";
    }

    internal CompositionProfileMapBinding MapBinding { get; }

    /// <summary>Whether this contract version admits one mapping-triggered Legacy Combiner stage.</summary>
    internal bool AllowsConditionalProcessor { get; }

    private static CompositionProfileMapBinding RequireBinding(CompositionProfileMapBinding mapBinding)
    {
        return mapBinding ?? throw new ArgumentNullException(nameof(mapBinding));
    }
}
