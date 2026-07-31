using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static ImageInitialization[] LowerInitializations(
        CompositionProfileDefinition profile,
        Dictionary<string, AddressSpace> spaces,
        List<CompositionIssue> issues)
    {
        var initializations = new List<ImageInitialization>();
        foreach (MutableCompositionProfileSpace mutableSpace in profile.Spaces.OfType<MutableCompositionProfileSpace>())
        {
            long capacity = spaces[mutableSpace.SpaceId].Length;
            switch (mutableSpace.Initializer)
            {
                case BlankProfileInitializer blank:
                    initializations.Add(ImageInitialization.Blank(mutableSpace.SpaceId, capacity, blank.FillByte));
                    break;
                case CloneProfileInitializer clone:
                    if (!TryResolveCloneInitialization(
                            profile,
                            mutableSpace,
                            clone,
                            spaces,
                            out string? sourceSpaceId,
                            out string? error))
                    {
                        AddUnsupported(issues, error!);
                        continue;
                    }

                    initializations.Add(ImageInitialization.Reference(mutableSpace.SpaceId, sourceSpaceId!, capacity));
                    break;
                default:
                    throw new InvalidOperationException("Validated V2 lowering encountered an unsupported mutable initializer.");
            }
        }

        return [.. initializations];
    }

    private static long ResolveMutableSpaceCapacity(MutableCompositionProfileSpace space, long resolvedMapCapacity)
    {
        return space.Capacity switch
        {
            ResolvedMapProfileCapacity => resolvedMapCapacity,
            FixedProfileCapacity fixedCapacity => fixedCapacity.Bytes,
            _ => throw new InvalidOperationException("Validated V2 lowering encountered an unsupported mutable capacity."),
        };
    }

    private static bool TryResolveCloneInitialization(
        CompositionProfileDefinition profile,
        MutableCompositionProfileSpace mutableSpace,
        CloneProfileInitializer clone,
        Dictionary<string, AddressSpace> spaces,
        out string? sourceSpaceId,
        out string? error)
    {
        sourceSpaceId = null;
        error = null;
        InputArtifactProfileSpace? source = profile.Spaces.OfType<InputArtifactProfileSpace>().SingleOrDefault(space =>
            StringComparer.Ordinal.Equals(space.SlotId, clone.SourceSlotId));
        if (source is null)
        {
            error = $"mutable space '{mutableSpace.SpaceId}' clone source slot '{clone.SourceSlotId}' has no immutable input space";
            return false;
        }

        if (spaces[source.SpaceId].Length != spaces[mutableSpace.SpaceId].Length)
        {
            error = $"mutable space '{mutableSpace.SpaceId}' clone source '{source.SpaceId}' does not match its capacity";
            return false;
        }

        AddressSpace sourceAddressSpace = spaces[source.SpaceId];
        bool usesExactGeometry = sourceAddressSpace.InputPaddingByte is null &&
            sourceAddressSpace.InputOversizePolicy == InputOversizePolicy.Reject &&
            sourceAddressSpace.AllowedInputLengths.Count == 0 &&
            sourceAddressSpace.ExpectedInputLengths.Count == 0;
        CompositionProfileInputSlot sourceSlot = profile.InputSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, source.SlotId));
        bool usesDeclaredPrefixSnapshot = mutableSpace.Kind == CompositionProfileSpaceKind.WorkBuffer &&
            sourceSlot.LengthRule is DeclaredPrefixWithWarningLengthRule declaredPrefix &&
            declaredPrefix.RequiredEndExclusive == spaces[mutableSpace.SpaceId].Length &&
            sourceAddressSpace.InputPaddingByte is null &&
            sourceAddressSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange &&
            sourceAddressSpace.AllowedInputLengths.Count == 0 &&
            sourceAddressSpace.ExpectedInputLengths.SequenceEqual(declaredPrefix.ExpectedOuterLengths) &&
            StringComparer.Ordinal.Equals(
                sourceAddressSpace.UnexpectedInputLengthIssueCode,
                declaredPrefix.UnexpectedOuterLengthIssueCode);
        bool usesSourceViewSnapshot = mutableSpace.Kind == CompositionProfileSpaceKind.WorkBuffer &&
            sourceSlot.LengthRule is SourceViewCoverageLengthRule sourceView &&
            sourceAddressSpace.InputPaddingByte is null &&
            sourceAddressSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange &&
            sourceAddressSpace.AllowedInputLengths.Count == 0 &&
            sourceAddressSpace.ExpectedInputLengths.SequenceEqual(sourceView.ExpectedOuterLengths) &&
            StringComparer.Ordinal.Equals(
                sourceAddressSpace.UnexpectedInputLengthIssueCode,
                sourceView.UnexpectedOuterLengthIssueCode);
        if (!usesExactGeometry && !usesDeclaredPrefixSnapshot && !usesSourceViewSnapshot)
        {
            error = $"mutable space '{mutableSpace.SpaceId}' clone source '{source.SpaceId}' must use exact immutable or checked equal-length source-snapshot geometry";
            return false;
        }

        if (mutableSpace.Kind == CompositionProfileSpaceKind.OutputImage &&
            ResolveCloneReferenceSourceSpaceId(profile, clone) != source.SpaceId)
        {
            error = "the output image clone source does not meet the Replace reference-image contract";
            return false;
        }

        sourceSpaceId = source.SpaceId;
        return true;
    }

    private static bool IsCloneSourceSlot(CompositionProfileDefinition profile, string slotId)
    {
        return profile.Spaces.OfType<MutableCompositionProfileSpace>().Any(space =>
            space.Initializer is CloneProfileInitializer clone &&
            StringComparer.Ordinal.Equals(clone.SourceSlotId, slotId));
    }
}
