// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Copies the facts of one accepted memory layout into the output map. A fact the layout does not
/// publish stays absent. CRC annotations are absent because no layout fact publishes them yet.
/// </summary>
internal static class OutputMapProjection
{
    /// <summary>
    /// Creates the map for one layout and the authoring session it was projected from. Null when the
    /// layout cannot be shown whole: it publishes no steps, or it composes in a scratch space (A/B and
    /// bank workflows), whose writes and sources have no published output coordinates.
    /// </summary>
    internal static OutputMapModel? Create(MemoryLayoutSnapshot layout, ActiveSessionSnapshot authoring,
        string outputSpaceId, ShellTextResources text)
    {
        if (layout.Steps.Status == MemoryLayoutFactStatus.Unavailable || layout.Steps.Values.Any(
                step => !StringComparer.Ordinal.Equals(step.TargetSpaceId, outputSpaceId)))
        {
            return null;
        }

        var labels = new OutputMapTextResources(text.Language);
        Dictionary<string, OutputMapSource> sourcesBySpace = SelectedSources(layout, authoring);
        var sources = new List<OutputMapSource>();
        var blocks = new List<OutputMapBlock>();
        foreach (CompositionOperation step in layout.Steps.Values)
        {
            AddBlocks(step, sourcesBySpace, sources, blocks, labels);
        }

        return new OutputMapModel(layout.AddressSpaceId, layout.Capacity, Regions(layout, text), blocks, sources,
            version: Version(layout, labels));
    }

    private static Dictionary<string, OutputMapSource> SelectedSources(MemoryLayoutSnapshot layout,
        ActiveSessionSnapshot authoring)
    {
        var result = new Dictionary<string, OutputMapSource>(StringComparer.Ordinal);
        foreach (MemoryLayoutSegment segment in layout.AfterSegments.Concat(layout.BeforeSegments))
        {
            Add(segment.SourceSpaceId, segment.SourceSlotId);
            Add(segment.ContentSource?.SourceSpaceId, segment.ContentSource?.SourceSlotId);
        }

        return result;

        void Add(string? spaceId, string? slotId)
        {
            if (spaceId is null || slotId is null || result.ContainsKey(spaceId))
            {
                return;
            }

            AuthoringSlotState? slot = authoring.Slots.FirstOrDefault(
                candidate => StringComparer.Ordinal.Equals(candidate.DefinitionId, slotId));
            if (slot is { SelectedPath: { } path, FileStamp: { } stamp })
            {
                result.Add(spaceId, new OutputMapSource(spaceId, Path.GetFileName(path), stamp.AcceptedLength));
            }
        }
    }

    private static void AddBlocks(CompositionOperation step, Dictionary<string, OutputMapSource> sourcesBySpace,
        List<OutputMapSource> sources, List<OutputMapBlock> blocks, OutputMapTextResources labels)
    {
        OutputMapBlockKind kind = Kind(step.Kind);
        OutputMapSource? source = null;
        if (kind == OutputMapBlockKind.Copy && step.SourceSpaceId is { } spaceId &&
            sourcesBySpace.TryGetValue(spaceId, out source) && !sources.Contains(source))
        {
            sources.Add(source);
        }

        string label = kind == OutputMapBlockKind.Fill
            ? FormattableString.Invariant($"{labels.Kind(kind)} 0x{step.FillByte:X2}")
            : source?.FileName ?? labels.Kind(kind);
        IReadOnlyList<ByteRange> writes = step.DeclaredWriteRanges;
        for (int index = 0; index < writes.Count; index++)
        {
            ByteRange write = writes[index];
            // A source range is shown only when the write is the whole copy, so no offset is derived here.
            ByteRange? read = source is not null && write.Equals(step.TargetRange) ? step.SourceRange : null;
            string key = writes.Count == 1 ? step.OperationId : FormattableString.Invariant($"{step.OperationId}#{index}");
            blocks.Add(new OutputMapBlock(key, write, kind, label, source?.Key, read));
        }
    }

    private static OutputMapBlockKind Kind(CompositionOperationKind kind)
    {
        return kind switch
        {
            CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange => OutputMapBlockKind.Copy,
            CompositionOperationKind.FillRange => OutputMapBlockKind.Fill,
            CompositionOperationKind.PatchScalar or CompositionOperationKind.TransformScalar => OutputMapBlockKind.Patch,
            CompositionOperationKind.RunExternalProcessor => OutputMapBlockKind.Script,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static List<OutputMapRegion> Regions(MemoryLayoutSnapshot layout, ShellTextResources text)
    {
        var result = new List<OutputMapRegion>();
        if (layout.GeometryKind != MemoryLayoutGeometryKind.PhysicalMap)
        {
            return result;
        }

        // Neighbouring segments with the same content title form one labelled region.
        var titled = new List<(string Name, ByteRange Range)>();
        foreach (MemoryLayoutSegment segment in layout.AfterSegments)
        {
            string name = text.GetMemoryContentTitle(segment.ContentRole, segment.CtrlRamRegionRole);
            if (titled.Count > 0 && titled[^1].Name == name)
            {
                ByteRange previous = titled[^1].Range;
                titled[^1] = (name, new ByteRange(previous.Start, segment.Range.EndExclusive - previous.Start));
            }
            else
            {
                titled.Add((name, segment.Range));
            }
        }

        bool accessKnown = layout.ReadOnlyRanges.Status != MemoryLayoutFactStatus.Unavailable;
        ByteRange[] readOnly = [.. layout.ReadOnlyRanges.Values.Select(static value => value.OutputRange.Range)];
        foreach ((string name, ByteRange range) in titled)
        {
            foreach ((ByteRange piece, bool isReadOnly) in SplitByAccess(range, readOnly))
            {
                result.Add(new OutputMapRegion(FormattableString.Invariant($"region:{piece.Start}"), name, piece,
                    accessKnown ? isReadOnly : null));
            }
        }

        return result;
    }

    /// <summary>Cuts a region where the declared access changes, so each label states one access.</summary>
    internal static IReadOnlyList<(ByteRange Range, bool IsReadOnly)> SplitByAccess(ByteRange region,
        IReadOnlyList<ByteRange> readOnly)
    {
        long[] cuts =
        [
            .. readOnly.SelectMany(static range => new[] { range.Start, range.EndExclusive })
                .Where(cut => cut > region.Start && cut < region.EndExclusive)
                .Append(region.Start)
                .Append(region.EndExclusive)
                .Distinct()
                .Order(),
        ];
        var result = new List<(ByteRange Range, bool IsReadOnly)>();
        for (int index = 0; index + 1 < cuts.Length; index++)
        {
            var piece = new ByteRange(cuts[index], cuts[index + 1] - cuts[index]);
            bool isReadOnly = readOnly.Any(range => range.Contains(piece));
            if (result.Count > 0 && result[^1].IsReadOnly == isReadOnly)
            {
                long start = result[^1].Range.Start;
                result[^1] = (new ByteRange(start, piece.EndExclusive - start), isReadOnly);
            }
            else
            {
                result.Add((piece, isReadOnly));
            }
        }

        return result;
    }

    private static OutputMapVersion? Version(MemoryLayoutSnapshot layout, OutputMapTextResources labels)
    {
        // The map has one version marker. Several locations are left out rather than choosing one.
        return layout.DpVersionLocations.Values is [var only]
            ? new OutputMapVersion(labels.DpVersion, only.OutputRange.Range, ValueText: null)
            : null;
    }
}
