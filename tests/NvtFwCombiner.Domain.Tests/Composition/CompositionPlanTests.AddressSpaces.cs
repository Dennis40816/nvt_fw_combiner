using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionPlanTests
{
    /// <summary>Verifies required seeded mutable spaces are exposed to application services.</summary>
    [Fact]
    public void RequiredSeededMutableAddressSpacesListsWorkBuffers()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 1),
                    0x11,
                    OverlapPolicy.Reject,
                    "write scratch"),
            ]);

        string addressSpaceId = Assert.Single(plan.RequiredSeededMutableAddressSpaceIds);
        Assert.Equal("scratch", addressSpaceId);
    }

    /// <summary>Verifies replace initialization cannot fabricate missing base image bytes via padding.</summary>
    [Fact]
    public void ReferenceInitializationRejectsPaddedReferenceSpace()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            []));
    }

    /// <summary>Verifies replace base image bytes cannot be truncated before initialization.</summary>
    [Fact]
    public void ReferenceInitializationRejectsTruncatedReferenceSpace()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            []));
    }

    /// <summary>Verifies mutable work buffers cannot fabricate seed bytes via padding.</summary>
    [Fact]
    public void MutableAddressSpaceRejectsInputPadding()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable, inputPaddingByte: 0xFF),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            []));
    }

    /// <summary>Verifies mutable work buffers cannot discard seed bytes via truncation.</summary>
    [Fact]
    public void MutableAddressSpaceRejectsInputTruncation()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            []));
    }

    /// <summary>Verifies immutable address spaces can carry explicit truncation policy without experience branching.</summary>
    [Fact]
    public void ImmutableAddressSpaceAcceptsInputTruncationPolicy()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("source", 4, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
        ];

        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-source",
                    10,
                    "source",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "copy source"),
            ]);

        Assert.Contains("source", plan.RequiredInputAddressSpaceIds);
    }
}
