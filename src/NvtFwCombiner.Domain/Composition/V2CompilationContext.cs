using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed physical-versus-logical provenance context retained by a V2 compiled artifact.</summary>
public enum V2CompilationContextKind
{
    /// <inheritdoc/>
    ResolvedMap,
    /// <inheritdoc/>
    LogicalOutput,
    /// <inheritdoc/>
    RuntimeReferenceReplace,
}

/// <summary>Exact family and member context used to establish a profile-bundle-v2 artifact.</summary>
public abstract class V2CompilationContext
{
    /// <summary>Creates a checked exact compilation context.</summary>
    protected V2CompilationContext(
        V2CompilationContextKind kind,
        string familyId,
        string familyVersion,
        string familyContentHash,
        string memberId,
        string modeId)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown V2 compilation context kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        ArgumentNullException.ThrowIfNull(familyContentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        if (familyContentHash.Length != 64 ||
            familyContentHash.Any(static character => !char.IsAsciiHexDigit(character)) ||
            familyContentHash.Any(char.IsUpper))
        {
            throw new ArgumentException(
                "Family content hash must be 64 lowercase hexadecimal characters.",
                nameof(familyContentHash));
        }

        Kind = kind;
        FamilyId = familyId;
        FamilyVersion = familyVersion;
        FamilyContentHash = familyContentHash;
        MemberId = memberId;
        ModeId = modeId;
    }

    /// <summary>Closed provenance context kind.</summary>
    public V2CompilationContextKind Kind { get; }

    /// <summary>Exact trusted firmware family identifier.</summary>
    public string FamilyId { get; }

    /// <summary>Exact trusted firmware family version.</summary>
    public string FamilyVersion { get; }

    /// <summary>Exact trusted firmware family content hash.</summary>
    public string FamilyContentHash { get; }

    /// <summary>Effective member selected for this artifact.</summary>
    public string MemberId { get; }

    /// <summary>Profile experience mode established at compilation.</summary>
    public string ModeId { get; }
}

/// <summary>Common context backed by one uniquely resolved canonical firmware image map.</summary>
public abstract class MapBoundV2CompilationContext : V2CompilationContext
{
    /// <summary>Creates one checked map-bound context with a closed purpose discriminator.</summary>
    protected MapBoundV2CompilationContext(
        V2CompilationContextKind kind,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
        : base(
            kind,
            RequireMap(resolvedMap).FamilyId,
            resolvedMap.FamilyVersion,
            resolvedMap.FamilyContentHash,
            resolvedMap.MemberId,
            resolvedMap.ModeId)
    {
        ResolvedMap = resolvedMap;
    }

    /// <summary>Resolver-owned canonical physical map and its complete resolution provenance.</summary>
    public FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap { get; }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap RequireMap(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        return resolvedMap ?? throw new ArgumentNullException(nameof(resolvedMap));
    }
}

/// <summary>Context backed by one uniquely resolved canonical firmware image map.</summary>
public sealed class ResolvedMapV2CompilationContext : MapBoundV2CompilationContext
{
    internal ResolvedMapV2CompilationContext(FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
        : base(V2CompilationContextKind.ResolvedMap, resolvedMap)
    {
    }
}

/// <summary>Context for the closed map-bound runtime reference-replace candidate shape.</summary>
public sealed class RuntimeReferenceReplaceV2CompilationContext : MapBoundV2CompilationContext
{
    internal RuntimeReferenceReplaceV2CompilationContext(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
        : base(V2CompilationContextKind.RuntimeReferenceReplace, resolvedMap)
    {
    }
}

/// <summary>Context for a General Merge logical output that intentionally makes no physical map claim.</summary>
public sealed class LogicalOutputV2CompilationContext : V2CompilationContext
{
    internal LogicalOutputV2CompilationContext(
        string familyId,
        string familyVersion,
        string familyContentHash,
        string memberId)
        : base(
            V2CompilationContextKind.LogicalOutput,
            familyId,
            familyVersion,
            familyContentHash,
            memberId,
            ExperienceIds.GeneralMerge)
    {
    }
}
