using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    private static void AssertEquivalentRuntimeExecutionSemantics(
        CompiledComposition expected,
        CompiledComposition actual)
    {
        Assert.Equal(expected.Eligibility, actual.Eligibility);
        Assert.Equal(expected.CompositionKind, actual.CompositionKind);
        Assert.Equal(expected.IcNumberPolicy, actual.IcNumberPolicy);
        Assert.Equal(expected.Plan.OutputSpaceId, actual.Plan.OutputSpaceId);
        Assert.Equal(
            expected.Plan.AddressSpaces.OrderBy(static space => space.AddressSpaceId).Select(static space =>
                (space.AddressSpaceId, space.Length, space.Mutability, space.InputPaddingByte,
                    space.InputOversizePolicy, Allowed: string.Join(",", space.AllowedInputLengths),
                    Expected: string.Join(",", space.ExpectedInputLengths), space.UnexpectedInputLengthIssueCode)),
            actual.Plan.AddressSpaces.OrderBy(static space => space.AddressSpaceId).Select(static space =>
                (space.AddressSpaceId, space.Length, space.Mutability, space.InputPaddingByte,
                    space.InputOversizePolicy, Allowed: string.Join(",", space.AllowedInputLengths),
                    Expected: string.Join(",", space.ExpectedInputLengths), space.UnexpectedInputLengthIssueCode)));
        Assert.Equal(
            expected.Plan.Initializations.Select(static initialization =>
                (initialization.Kind, initialization.TargetSpaceId, initialization.Capacity,
                    initialization.FillByte, initialization.ReferenceSpaceId)),
            actual.Plan.Initializations.Select(static initialization =>
                (initialization.Kind, initialization.TargetSpaceId, initialization.Capacity,
                    initialization.FillByte, initialization.ReferenceSpaceId)));
        Assert.Equal(expected.Plan.OrderedOperations.Count, actual.Plan.OrderedOperations.Count);
        for (int index = 0; index < expected.Plan.OrderedOperations.Count; index++)
        {
            AssertEquivalentOperation(
                expected.Plan.OrderedOperations[index],
                actual.Plan.OrderedOperations[index]);
        }

        V2CompiledCompositionDetails expectedDetails = Assert.IsType<V2CompiledCompositionDetails>(expected.V2Details);
        V2CompiledCompositionDetails actualDetails = Assert.IsType<V2CompiledCompositionDetails>(actual.V2Details);
        Assert.Equal(
            expectedDetails.InputContract.Slots.Select(static slot =>
                (slot.SlotId, slot.Role, slot.ArtifactClass, slot.Required, slot.Cardinality,
                    Extensions: string.Join("|", slot.AcceptedExtensions), slot.LengthRequirement, slot.Normalization)),
            actualDetails.InputContract.Slots.Select(static slot =>
                (slot.SlotId, slot.Role, slot.ArtifactClass, slot.Required, slot.Cardinality,
                    Extensions: string.Join("|", slot.AcceptedExtensions), slot.LengthRequirement, slot.Normalization)));
        Assert.Equal(
            expectedDetails.InputContract.SpaceBindings.Select(static binding =>
                (binding.AddressSpaceId, binding.SlotId, binding.InstancePolicy)),
            actualDetails.InputContract.SpaceBindings.Select(static binding =>
                (binding.AddressSpaceId, binding.SlotId, binding.InstancePolicy)));
        Assert.Empty(expectedDetails.InputContract.SelectionGroups);
        Assert.Empty(actualDetails.InputContract.SelectionGroups);
        Assert.Equal(
            expectedDetails.RegionAccessContract.Requirements.Select(static requirement =>
                (requirement.RegionId, requirement.Access, requirement.Reason,
                    Subregions: string.Join("|", requirement.AllowedSubregionIds),
                    Chain: RegionChain(requirement.GoverningRegionChain))),
            actualDetails.RegionAccessContract.Requirements.Select(static requirement =>
                (requirement.RegionId, requirement.Access, requirement.Reason,
                    Subregions: string.Join("|", requirement.AllowedSubregionIds),
                    Chain: RegionChain(requirement.GoverningRegionChain))));
        Assert.Equal(
            expectedDetails.RegionAccessContract.ResolvedViews.Select(static view =>
                (view.ViewId, view.AddressSpaceId, view.Range, Chain: RegionChain(view.GoverningRegionChain))),
            actualDetails.RegionAccessContract.ResolvedViews.Select(static view =>
                (view.ViewId, view.AddressSpaceId, view.Range, Chain: RegionChain(view.GoverningRegionChain))));
        Assert.Empty(expectedDetails.Provenance.ValidationRequirements);
        Assert.Empty(actualDetails.Provenance.ValidationRequirements);
        Assert.Empty(expectedDetails.Provenance.RequiredCapabilities);
        Assert.Empty(actualDetails.Provenance.RequiredCapabilities);

        RuntimeReferenceReplaceV2CompilationContext expectedContext = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            expectedDetails.Provenance.Context);
        RuntimeReferenceReplaceV2CompilationContext actualContext = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            actualDetails.Provenance.Context);
        Assert.Equal(expectedContext.AllowsConditionalProcessor, actualContext.AllowsConditionalProcessor);
        Assert.Equal(expectedContext.ProcessorWriteViewIds, actualContext.ProcessorWriteViewIds);
        Assert.Equal(expectedContext.ResolvedMap.ImageMap.MapId, actualContext.ResolvedMap.ImageMap.MapId);
        Assert.Equal(expectedContext.ResolvedMap.CapacityBytes, actualContext.ResolvedMap.CapacityBytes);
        Assert.Equal(expectedContext.ResolvedMap.TopologySelection, actualContext.ResolvedMap.TopologySelection);
        Assert.Equal("cascade-map", expectedContext.ResolvedMap.ImageMap.MapId);
    }

    private static void AssertEquivalentOperation(CompositionOperation expected, CompositionOperation actual)
    {
        Assert.Equal(expected.OperationId, actual.OperationId);
        Assert.Equal(expected.Sequence, actual.Sequence);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.SourceSpaceId, actual.SourceSpaceId);
        Assert.Equal(expected.SourceRange, actual.SourceRange);
        Assert.Equal(expected.TargetSpaceId, actual.TargetSpaceId);
        Assert.Equal(expected.TargetRange, actual.TargetRange);
        Assert.Equal(expected.OverlapPolicy, actual.OverlapPolicy);
        Assert.Equal(expected.FillByte, actual.FillByte);
        Assert.Equal(expected.PatchBytes.ToArray(), actual.PatchBytes.ToArray());
        Assert.Equal(expected.Reason, actual.Reason);
        Assert.Equal(expected.Provenance, actual.Provenance);
        Assert.Equal(expected.ScalarTransform is null, actual.ScalarTransform is null);
        if (expected.ScalarTransform is { } expectedScalar && actual.ScalarTransform is { } actualScalar)
        {
            Assert.Equal(expectedScalar.Width, actualScalar.Width);
            Assert.Equal(expectedScalar.ByteOrder, actualScalar.ByteOrder);
            Assert.Equal(expectedScalar.Addend, actualScalar.Addend);
            Assert.Equal(expectedScalar.AddendSource, actualScalar.AddendSource);
            Assert.Equal(expectedScalar.ExpectedBefore, actualScalar.ExpectedBefore);
            Assert.Equal(expectedScalar.OverflowPolicy, actualScalar.OverflowPolicy);
        }

        Assert.Equal(expected.ExternalProcessorInvocation is null, actual.ExternalProcessorInvocation is null);
        if (expected.ExternalProcessorInvocation is not { } expectedProcessor ||
            actual.ExternalProcessorInvocation is not { } actualProcessor)
        {
            return;
        }

        Assert.Equal(expectedProcessor.ProcessorId, actualProcessor.ProcessorId);
        Assert.Equal(expectedProcessor.ToolBindingId, actualProcessor.ToolBindingId);
        Assert.Equal(expectedProcessor.AllowedReadRanges, actualProcessor.AllowedReadRanges);
        Assert.Equal(expectedProcessor.AllowedWriteRanges, actualProcessor.AllowedWriteRanges);
        Assert.Equal(
            expectedProcessor.AllowedWriteRangeSections.Select(static section =>
                (section.SectionId, section.Range, section.SourceRange)),
            actualProcessor.AllowedWriteRangeSections.Select(static section =>
                (section.SectionId, section.Range, section.SourceRange)));
        Assert.Equal(
            expectedProcessor.StagedSourceBindings.Select(static binding =>
                (binding.SourceSpaceId, binding.SourceRange, binding.FirmwareRange)),
            actualProcessor.StagedSourceBindings.Select(static binding =>
                (binding.SourceSpaceId, binding.SourceRange, binding.FirmwareRange)));
        Assert.Equal(
            expectedProcessor.StagedArtifactBindings.Select(static binding =>
                (binding.ArtifactId, binding.SourceSpaceId, binding.SourceRange)),
            actualProcessor.StagedArtifactBindings.Select(static binding =>
                (binding.ArtifactId, binding.SourceSpaceId, binding.SourceRange)));
        Assert.Equal(
            expectedProcessor.OutputAssertions.Select(static assertion =>
                (assertion.Range, Bytes: Convert.ToHexString(assertion.ExpectedBytes.Span))),
            actualProcessor.OutputAssertions.Select(static assertion =>
                (assertion.Range, Bytes: Convert.ToHexString(assertion.ExpectedBytes.Span))));
    }

    private static string RegionChain(IEnumerable<FirmwareRegion> chain)
    {
        return string.Join("|", chain.Select(static region =>
            $"{region.RegionId}:{region.WriteConstraint}:{region.Alignment}"));
    }
}
