// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Whether a General Replace build has a declared route that recomputes header and CRC values.</summary>
public enum IntegrityRecomputeAvailability
{
    /// <summary>
    /// A postbuild route is declared and resolved for this base image. It may rewrite every range in
    /// <see cref="IntegrityRecomputeFacts.WriteRanges"/>, not only the CRC words. The host write-range check applies.
    /// </summary>
    Declared,
    /// <summary>No postbuild route is declared for the IC. This does not claim that the IC has no integrity values.</summary>
    NoRecomputeRule,
    /// <summary>The route could not be resolved. See <see cref="IntegrityRecomputeFacts.Issue"/>. Unknown is never none.</summary>
    Unknown,
}

/// <summary>The user's build choice after the recompute confirmation.</summary>
public enum IntegrityRecomputeChoice
{
    /// <summary>Recompute header and CRC values after the user's segments are written.</summary>
    Recompute,
    /// <summary>Write the user's segments as they are, without recompute.</summary>
    WriteAsIs,
}

/// <summary>One declared integrity storage location, half-open, in the output address space.</summary>
public sealed record IntegrityStorageLocation(string SectionId, ByteRange StorageRange);

/// <summary>One output range the declared postbuild route may rewrite, half-open.</summary>
public sealed record IntegrityRecomputeWriteRange(string SectionId, ByteRange Range);

/// <summary>The output range one integrity value covers.</summary>
public sealed record IntegrityCoverage(string SectionId, ByteRange CoveredRange);

/// <summary>The algorithm name of one integrity value.</summary>
public sealed record IntegrityAlgorithm(string SectionId, string Name);

/// <summary>One declared Legacy Combiner command of the recompute route.</summary>
public sealed record IntegrityRecomputeCommand(
    string CommandId, LegacyCombinerCommandFamily Family, string ModeArgument, string? CrcArgument);

/// <summary>One user segment that overlaps one declared integrity storage location.</summary>
public sealed record IntegrityStorageHit(IntegrityStorageLocation Location, ByteRange UserSegment, ByteRange Overlap);

/// <summary>One user segment that overlaps one range the declared recompute route rewrites.</summary>
public sealed record IntegrityWriteRangeHit(IntegrityRecomputeWriteRange Range, ByteRange UserSegment, ByteRange Overlap);

/// <summary>
/// Read-only facts for the recompute confirmation. Storage locations and write ranges come from the existing
/// postbuild owner, <see cref="LegacyCombinerPostbuildPlanCompiler"/>. Coverage and algorithm names are not
/// declared by today's profiles, so they stay unavailable until the IC files declare them.
/// </summary>
public sealed class IntegrityRecomputeFacts
{
    private IntegrityRecomputeFacts(
        IntegrityRecomputeAvailability availability,
        CompositionIssue? issue,
        string? toolBindingId,
        IEnumerable<IntegrityRecomputeCommand> commands,
        MemoryLayoutFactSet<IntegrityStorageLocation> storageLocations,
        MemoryLayoutFactSet<IntegrityRecomputeWriteRange> writeRanges)
    {
        Availability = availability;
        Issue = issue;
        ToolBindingId = toolBindingId;
        IntegrityRecomputeCommand[] snapshot = [.. commands];
        Commands = Array.AsReadOnly(snapshot);
        StorageLocations = storageLocations;
        WriteRanges = writeRanges;
    }

    /// <summary>Whether the recompute route is declared, missing or unknown.</summary>
    public IntegrityRecomputeAvailability Availability { get; }

    /// <summary>The planning issue that explains a missing or unknown route, or null.</summary>
    public CompositionIssue? Issue { get; }

    /// <summary>The declared Legacy Combiner tool binding, or null when no route is declared or resolved.</summary>
    public string? ToolBindingId { get; }

    /// <summary>The declared commands in run order. Empty when no route is declared or resolved.</summary>
    public IReadOnlyList<IntegrityRecomputeCommand> Commands { get; }

    /// <summary>Declared CRC and header storage locations, ordered by start.</summary>
    public MemoryLayoutFactSet<IntegrityStorageLocation> StorageLocations { get; }

    /// <summary>Every range the declared route may rewrite, including the storage locations.</summary>
    public MemoryLayoutFactSet<IntegrityRecomputeWriteRange> WriteRanges { get; }

    /// <summary>Covered ranges. Today's profiles do not declare them.</summary>
    public MemoryLayoutFactSet<IntegrityCoverage> Coverage { get; } =
        MemoryLayoutFactSet<IntegrityCoverage>.Unavailable(MemoryLayoutFactUnavailableReason.NotDeclared);

    /// <summary>Algorithm names. Today's profiles do not declare them.</summary>
    public MemoryLayoutFactSet<IntegrityAlgorithm> Algorithms { get; } =
        MemoryLayoutFactSet<IntegrityAlgorithm>.Unavailable(MemoryLayoutFactUnavailableReason.NotDeclared);

    /// <summary>
    /// Projects one resolved postbuild plan for an output of the given capacity. The plan must already have
    /// passed the General Replace planning checks. A capacity below the plan's required capacity is rejected.
    /// </summary>
    public static IntegrityRecomputeFacts FromPostbuild(LegacyCombinerPostbuildCommandPlan plan, long capacity)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            capacity, LegacyCombinerPostbuildPlanCompiler.CalculateRequiredCapacity(plan, []));
        IntegrityStorageLocation[] locations =
        [
            .. LegacyCombinerPostbuildPlanCompiler.GetKnownIntegrityWriteRangeSections(plan, capacity)
                .Select(static section => new IntegrityStorageLocation(section.SectionId, section.Range))
                .OrderBy(static location => location.StorageRange.Start)
                .ThenBy(static location => location.SectionId, StringComparer.Ordinal),
        ];
        IntegrityRecomputeWriteRange[] writeRanges =
        [
            .. LegacyCombinerPostbuildPlanCompiler.GetAllowedWriteRangeSectionsForInPlaceRefresh(plan, capacity)
                .Select(static section => new IntegrityRecomputeWriteRange(section.SectionId, section.Range))
                .OrderBy(static range => range.Range.Start)
                .ThenBy(static range => range.SectionId, StringComparer.Ordinal),
        ];
        return new(
            IntegrityRecomputeAvailability.Declared,
            null,
            plan.Profile.ToolBindingId,
            plan.Commands.Select(static command => new IntegrityRecomputeCommand(
                command.CommandId, command.Family, command.ModeArgument, command.CrcArgument)),
            // The owner lists storage only for the command shapes it knows. An empty list means the plan does not
            // say where its values are stored, not that it stores none, so it must not read as known empty.
            locations.Length == 0
                ? MemoryLayoutFactSet<IntegrityStorageLocation>.Unavailable(MemoryLayoutFactUnavailableReason.NotDeclared)
                : MemoryLayoutFactSet<IntegrityStorageLocation>.Known(locations),
            MemoryLayoutFactSet<IntegrityRecomputeWriteRange>.Known(writeRanges));
    }

    /// <summary>Facts for an IC that declares no postbuild route.</summary>
    public static IntegrityRecomputeFacts NoRecomputeRule(CompositionIssue? issue)
    {
        return new(IntegrityRecomputeAvailability.NoRecomputeRule, issue, null, [],
            MemoryLayoutFactSet<IntegrityStorageLocation>.Unavailable(MemoryLayoutFactUnavailableReason.NotDeclared),
            MemoryLayoutFactSet<IntegrityRecomputeWriteRange>.Unavailable(MemoryLayoutFactUnavailableReason.NotDeclared));
    }

    /// <summary>Facts for a route that could not be resolved.</summary>
    public static IntegrityRecomputeFacts Unknown(CompositionIssue? issue)
    {
        return new(IntegrityRecomputeAvailability.Unknown, issue, null, [],
            MemoryLayoutFactSet<IntegrityStorageLocation>.Unavailable(
                MemoryLayoutFactUnavailableReason.OutputLocationNotResolved),
            MemoryLayoutFactSet<IntegrityRecomputeWriteRange>.Unavailable(
                MemoryLayoutFactUnavailableReason.OutputLocationNotResolved));
    }

    /// <summary>
    /// Lists each user segment that overlaps a declared storage location. When the storage locations are
    /// unavailable, the result is unavailable with the same reason, never an empty known list.
    /// </summary>
    public MemoryLayoutFactSet<IntegrityStorageHit> FindStorageHits(IEnumerable<ByteRange> userSegments)
    {
        ArgumentNullException.ThrowIfNull(userSegments);
        if (StorageLocations.Status == MemoryLayoutFactStatus.Unavailable)
        {
            return MemoryLayoutFactSet<IntegrityStorageHit>.Unavailable(StorageLocations.UnavailableReason);
        }

        List<IntegrityStorageHit> hits = [];
        foreach (ByteRange segment in userSegments)
        {
            foreach (IntegrityStorageLocation location in StorageLocations.Values)
            {
                if (segment.Intersect(location.StorageRange) is { } overlap)
                {
                    hits.Add(new IntegrityStorageHit(location, segment, overlap));
                }
            }
        }
        return MemoryLayoutFactSet<IntegrityStorageHit>.Known(hits);
    }

    /// <summary>
    /// Lists each user segment that overlaps a range the recompute route rewrites. The route rewrites these
    /// bytes, for example the header, even where no integrity value is stored. When the write ranges are
    /// unavailable, the result is unavailable with the same reason, never an empty known list.
    /// </summary>
    public MemoryLayoutFactSet<IntegrityWriteRangeHit> FindWriteRangeHits(IEnumerable<ByteRange> userSegments)
    {
        ArgumentNullException.ThrowIfNull(userSegments);
        if (WriteRanges.Status == MemoryLayoutFactStatus.Unavailable)
        {
            return MemoryLayoutFactSet<IntegrityWriteRangeHit>.Unavailable(WriteRanges.UnavailableReason);
        }

        List<IntegrityWriteRangeHit> hits = [];
        foreach (ByteRange segment in userSegments)
        {
            foreach (IntegrityRecomputeWriteRange range in WriteRanges.Values)
            {
                if (segment.Intersect(range.Range) is { } overlap)
                {
                    hits.Add(new IntegrityWriteRangeHit(range, segment, overlap));
                }
            }
        }
        return MemoryLayoutFactSet<IntegrityWriteRangeHit>.Known(hits);
    }
}
