using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

public static partial class MemoryLayoutProjector
{
    // Attribution is a read-only projection of accepted inputs and the compiled plan.
    // It never replaces the segment's exact writer, operations, or logical group.
    private sealed class ContentSourceProjection
    {
        private readonly CompositionPlan _plan;
        private readonly bool _abMerge;
        private readonly IReadOnlyList<FirmwareRegion> _canonicalRegions;
        private readonly Dictionary<string, MemoryLayoutContentSource> _inputs = new(StringComparer.Ordinal);

        public ContentSourceProjection(
            CompiledComposition composition,
            Dictionary<string, string> slotsBySpace,
            Dictionary<string, AuthoringSlotState> statesById)
        {
            _plan = composition.Plan;
            _abMerge = composition.V2Details.ExperienceId == ExperienceIds.AbMerge &&
                composition.V2Details.Provenance.Context is MapBoundV2CompilationContext;
            _canonicalRegions = composition.V2Details.Provenance.Context is MapBoundV2CompilationContext map
                ? map.ResolvedMap.ImageMap.Regions
                : [];
            StringComparer pathComparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var identities = new Dictionary<string, Dictionary<FileStamp, string>>(pathComparer);
            foreach ((string spaceId, string slotId) in slotsBySpace.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                AuthoringSlotState state = statesById[slotId];
                if (!IsAdmitted(state) || state.SelectedPath is null || state.FileStamp is not { } stamp)
                {
                    continue;
                }

                string path = Path.GetFullPath(state.SelectedPath);
                if (!identities.TryGetValue(path, out Dictionary<FileStamp, string>? stamps))
                {
                    stamps = [];
                    identities.Add(path, stamps);
                }

                if (!stamps.TryGetValue(stamp, out string? identity))
                {
                    identity = FormattableString.Invariant($"artifact-{_inputs.Count}");
                    stamps.Add(stamp, identity);
                }

                _inputs.Add(spaceId, new MemoryLayoutContentSource(spaceId, slotId, identity));
            }
        }

        public MemoryLayoutContentSource? Initial(ByteRange range)
        {
            return Resolve(_plan.OutputSpaceId, range, 0, allowRoundTrip: false);
        }

        public MemoryLayoutContentSource? After(ByteRange range)
        {
            return Resolve(_plan.OutputSpaceId, range, _plan.OrderedOperations.Count,
                allowRoundTrip: _abMerge && _canonicalRegions.Any(region =>
                    region.Kind == FirmwareRegionKind.Code && region.Range.Contains(range)));
        }

        private MemoryLayoutContentSource? Resolve(
            string spaceId, ByteRange range, int before, bool allowRoundTrip)
        {
            if (_inputs.TryGetValue(spaceId, out MemoryLayoutContentSource? input))
            {
                return input;
            }

            int writerIndex = LastWriter(spaceId, range, before);
            if (writerIndex < 0)
            {
                ImageInitialization? initialization = _plan.Initializations.FirstOrDefault(
                    candidate => candidate.TargetSpaceId == spaceId);
                return initialization?.ReferenceSpaceId is { } reference && _inputs.TryGetValue(reference, out input)
                    ? input
                    : null;
            }

            CompositionOperation writer = _plan.OrderedOperations[writerIndex];
            if (writer.Kind is CompositionOperationKind.RunExternalProcessor or
                CompositionOperationKind.PatchScalar or CompositionOperationKind.TransformScalar)
            {
                return Resolve(spaceId, range, writerIndex, allowRoundTrip);
            }

            if (!OperationWritesRange(writer, range))
            {
                // A mixed slice has no single proven content owner.
                return null;
            }

            if (writer.Kind is not (CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange) ||
                writer.SourceSpaceId is not { } sourceSpace || writer.SourceRange is not { } sourceRange ||
                sourceSpace == _plan.OutputSpaceId)
            {
                return null;
            }

            var sourceSlice = new ByteRange(checked(sourceRange.Start + range.Start - writer.TargetRange.Start), range.Length);
            MemoryLayoutContentSource? source = Resolve(sourceSpace, sourceSlice, writerIndex, allowRoundTrip: false);
            return source ?? (allowRoundTrip && spaceId == _plan.OutputSpaceId &&
                writer.Kind == CompositionOperationKind.CopyRange
                    ? RoundTripOwner(writerIndex, range, sourceSlice)
                    : null);
        }

        private MemoryLayoutContentSource? RoundTripOwner(int importIndex, ByteRange outputSlice, ByteRange workSlice)
        {
            CompositionOperation import = _plan.OrderedOperations[importIndex];
            string workSpace = import.SourceSpaceId!;
            if (!_plan.Initializations.Any(initialization => initialization.TargetSpaceId == workSpace))
            {
                return null;
            }

            int processorIndex = LastWriter(workSpace, workSlice, importIndex);
            if (processorIndex < 0)
            {
                return null;
            }

            CompositionOperation processor = _plan.OrderedOperations[processorIndex];
            if (processor.Kind != CompositionOperationKind.RunExternalProcessor ||
                !OperationWritesRange(processor, workSlice))
            {
                return null;
            }

            int stagingIndex = LastWriter(workSpace, workSlice, processorIndex);
            if (stagingIndex < 0)
            {
                return null;
            }

            CompositionOperation staging = _plan.OrderedOperations[stagingIndex];
            if (staging.Kind != CompositionOperationKind.CopyRange ||
                staging.SourceSpaceId != _plan.OutputSpaceId || staging.SourceRange is not { } stagedOutput ||
                !staging.TargetRange.Contains(workSlice) ||
                checked(stagedOutput.Start + workSlice.Start - staging.TargetRange.Start) != outputSlice.Start)
            {
                return null;
            }

            // The effective staging writer must still describe the current output owner.
            // Any later overlapping output write makes this proof ambiguous.
            for (int index = stagingIndex + 1; index < importIndex; index++)
            {
                CompositionOperation intervening = _plan.OrderedOperations[index];
                if (intervening.TargetSpaceId == _plan.OutputSpaceId &&
                    intervening.DeclaredWriteRanges.Any(outputSlice.Overlaps))
                {
                    return null;
                }
            }

            return Resolve(_plan.OutputSpaceId, outputSlice, stagingIndex, allowRoundTrip: false);
        }

        private int LastWriter(string spaceId, ByteRange range, int before)
        {
            for (int index = before - 1; index >= 0; index--)
            {
                CompositionOperation operation = _plan.OrderedOperations[index];
                if (operation.TargetSpaceId == spaceId && operation.DeclaredWriteRanges.Any(range.Overlaps))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
