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
        InputArtifactProfileSpace source = profile.Spaces.OfType<InputArtifactProfileSpace>().Single(space =>
            StringComparer.Ordinal.Equals(space.SlotId, clone.SourceSlotId));
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
        CompositionInputSlotDefinition sourceSlot = profile.InputSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, source.SlotId));
        bool hasSourceViewSnapshot = sourceSlot.LengthRequirement is
            SourceViewCoverageInputLengthDefinition or
            CompiledDeclaredPrefixWithWarningInputLengthRequirement;
        long? requiredEndExclusive = sourceSlot.LengthRequirement is
            CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix
                ? declaredPrefix.RequiredEndExclusive
                : null;
        IReadOnlyList<long> expectedOuterLengths = sourceSlot.LengthRequirement switch
        {
            SourceViewCoverageInputLengthDefinition sourceView => sourceView.ExpectedOuterLengths,
            CompiledDeclaredPrefixWithWarningInputLengthRequirement prefixWithWarning =>
                prefixWithWarning.ExpectedOuterLengths,
            _ => [],
        };
        string? unexpectedOuterLengthIssueCode = sourceSlot.LengthRequirement switch
        {
            SourceViewCoverageInputLengthDefinition sourceView => sourceView.UnexpectedOuterLengthIssueCode,
            CompiledDeclaredPrefixWithWarningInputLengthRequirement prefixWithWarning =>
                prefixWithWarning.UnexpectedOuterLengthIssueCode,
            _ => null,
        };
        bool usesSourceViewSnapshot = mutableSpace.Kind == CompositionProfileSpaceKind.WorkBuffer &&
            hasSourceViewSnapshot &&
            (requiredEndExclusive is null ||
             requiredEndExclusive == spaces[mutableSpace.SpaceId].Length) &&
            sourceAddressSpace.InputPaddingByte is null &&
            sourceAddressSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange &&
            sourceAddressSpace.AllowedInputLengths.Count == 0 &&
            sourceAddressSpace.ExpectedInputLengths.SequenceEqual(expectedOuterLengths) &&
            StringComparer.Ordinal.Equals(
                sourceAddressSpace.UnexpectedInputLengthIssueCode,
                unexpectedOuterLengthIssueCode);
        if (!usesExactGeometry && !usesSourceViewSnapshot)
        {
            error = $"mutable space '{mutableSpace.SpaceId}' clone source '{source.SpaceId}' must use exact immutable or checked equal-length source-snapshot geometry";
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
