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
        string familyId,
        string familyVersion,
        string familyContentHash,
        string memberId,
        string modeId)
    {
        FamilyId = RequiredValue.NotBlank(familyId);
        FamilyVersion = RequiredValue.NotBlank(familyVersion);
        MemberId = RequiredValue.NotBlank(memberId);
        ModeId = RequiredValue.NotBlank(modeId);
        _ = CanonicalSha256.Require(familyContentHash, nameof(familyContentHash));

        FamilyContentHash = familyContentHash;
    }

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
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
        : this(resolvedMap, RequireMap(resolvedMap).ModeId)
    {
    }

    // Only closed Domain contexts may distinguish a composite use case from its source layout mode.
    private protected MapBoundV2CompilationContext(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        string compilationModeId)
        : base(
            RequireMap(resolvedMap).FamilyId,
            resolvedMap.FamilyVersion,
            resolvedMap.FamilyContentHash,
            resolvedMap.MemberId,
            compilationModeId)
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
    internal ResolvedMapV2CompilationContext(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        SourceEnvelopeExtent? sourceEnvelope = null)
        : base(resolvedMap)
    {
        if (sourceEnvelope is not null &&
            (!StringComparer.Ordinal.Equals(sourceEnvelope.LayoutTemplateMapId, resolvedMap.ImageMap.MapId) ||
             sourceEnvelope.LayoutTemplateCapacity != resolvedMap.CapacityBytes))
        {
            throw new ArgumentException("Source envelope must retain its exact resolved layout template.", nameof(sourceEnvelope));
        }

        SourceEnvelope = sourceEnvelope;
    }

    /// <summary>Actual accepted DP/output extent, separate from the layout template capacity.</summary>
    public SourceEnvelopeExtent? SourceEnvelope { get; }
}

/// <summary>Closed actual extent of one immutable DP and its explicitly declared canonical layout template.</summary>
public sealed class SourceEnvelopeExtent
{
    internal SourceEnvelopeExtent(
        string sourceSlotId,
        string rootRegionId,
        string layoutTemplateMapId,
        long layoutTemplateCapacity,
        long actualOutputLength,
        IReadOnlyList<long> expectedOuterLengths,
        string unexpectedLengthIssueCode)
    {
        SourceSlotId = RequiredValue.NotBlank(sourceSlotId);
        RootRegionId = RequiredValue.NotBlank(rootRegionId);
        LayoutTemplateMapId = RequiredValue.NotBlank(layoutTemplateMapId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layoutTemplateCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actualOutputLength);
        LayoutTemplateCapacity = layoutTemplateCapacity;
        ActualOutputLength = actualOutputLength;
        ExpectedOuterLengths = Array.AsReadOnly(InputLengthPolicyLimits.SnapshotExpectedOuterLengths(
            expectedOuterLengths, nameof(expectedOuterLengths)));
        UnexpectedLengthIssueCode = RequiredValue.NotBlank(unexpectedLengthIssueCode);
    }

    /// <summary>One profile-bound immutable DP slot.</summary>
    public string SourceSlotId { get; }
    /// <summary>
    /// Top-level region identity used only as the layout-template anchor: the canonical full-container root of a
    /// profile-bound DP envelope, or, for a runtime reference-replace envelope, the top-level region at offset
    /// zero of a template whose top-level regions tile <c>[0, LayoutTemplateCapacity)</c>.
    /// </summary>
    public string RootRegionId { get; }
    /// <summary>Explicit profile-declared map identity used for layout facts.</summary>
    public string LayoutTemplateMapId { get; }
    /// <summary>Physical template capacity, not actual output capacity.</summary>
    public long LayoutTemplateCapacity { get; }
    /// <summary>Exact accepted immutable DP length and compiled output length.</summary>
    public long ActualOutputLength { get; }
    /// <summary>Advisory, non-filtering standard outer lengths.</summary>
    public IReadOnlyList<long> ExpectedOuterLengths { get; }
    /// <summary>Existing engine warning code for a nonstandard exact DP length.</summary>
    public string UnexpectedLengthIssueCode { get; }
}

/// <summary>Context for the closed map-bound runtime reference-replace candidate shape.</summary>
public sealed class RuntimeReferenceReplaceV2CompilationContext : MapBoundV2CompilationContext
{
    internal RuntimeReferenceReplaceV2CompilationContext(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        bool allowsConditionalProcessor,
        IEnumerable<string>? processorWriteViewIds = null,
        SourceEnvelopeExtent? sourceEnvelope = null)
        : base(resolvedMap)
    {
        string[] processorWriteViewIdsSnapshot = ImmutableStringSnapshot.Create(
            processorWriteViewIds ?? [],
            nameof(processorWriteViewIds),
            requiredMessage: null,
            "Runtime-reference processor write-view ids must be non-empty.",
            "Runtime-reference processor write-view ids must be ordinally unique.");
        if (sourceEnvelope is not null &&
            (!StringComparer.Ordinal.Equals(sourceEnvelope.LayoutTemplateMapId, resolvedMap.ImageMap.MapId) ||
             sourceEnvelope.LayoutTemplateCapacity != resolvedMap.CapacityBytes ||
             sourceEnvelope.ActualOutputLength <= sourceEnvelope.LayoutTemplateCapacity ||
             !StringComparer.Ordinal.Equals(
                 GetTilingTemplateRootRegionId(resolvedMap), sourceEnvelope.RootRegionId)))
        {
            throw new ArgumentException(
                "A runtime reference envelope must extend beyond a resolved layout template tiled by its top-level regions.",
                nameof(sourceEnvelope));
        }

        AllowsConditionalProcessor = allowsConditionalProcessor;
        ProcessorWriteViewIds = Array.AsReadOnly(processorWriteViewIdsSnapshot);
        SourceEnvelope = sourceEnvelope;
    }

    /// <summary>Whether the trusted profile contract can append one mapping-triggered processor stage.</summary>
    public bool AllowsConditionalProcessor { get; }

    /// <summary>Exact profile view identities that grant processor write authority before runtime narrowing.</summary>
    public IReadOnlyList<string> ProcessorWriteViewIds { get; }

    /// <summary>Captured reference extent beyond the layout template, when the Base is longer than every map.</summary>
    public SourceEnvelopeExtent? SourceEnvelope { get; }

    /// <summary>
    /// Returns the top-level region at offset zero when the map's top-level regions tile the complete template;
    /// a runtime envelope anchors on it because a CtrlRAM template need not declare one full-container root.
    /// </summary>
    internal static string? GetTilingTemplateRootRegionId(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        ArgumentNullException.ThrowIfNull(resolvedMap);
        FirmwareRegion[] topLevel =
        [
            .. resolvedMap.ImageMap.Regions
                .Where(static region => region.ParentRegionId is null)
                .OrderBy(static region => region.Range.Start),
        ];
        long cursor = 0;
        foreach (FirmwareRegion region in topLevel)
        {
            if (region.Range.Start != cursor)
            {
                return null;
            }

            cursor = region.Range.EndExclusive;
        }

        return topLevel.Length != 0 && cursor == resolvedMap.CapacityBytes ? topLevel[0].RegionId : null;
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
            familyId,
            familyVersion,
            familyContentHash,
            memberId,
            ExperienceIds.GeneralMerge)
    {
    }
}
