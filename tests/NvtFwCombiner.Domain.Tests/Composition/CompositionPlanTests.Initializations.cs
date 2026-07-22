using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionPlanTests
{
    /// <summary>Verifies the singleton constructor delegates to the canonical initializer collection.</summary>
    [Fact]
    public void SingletonConstructorUsesCanonicalInitializationModel()
    {
        var initialization = ImageInitialization.Blank("output-image", 4, 0xFF);
        AddressSpace[] spaces = [new("output-image", 4, AddressSpaceMutability.Mutable)];

        var singleton = new CompositionPlan(initialization, spaces, []);
        var canonical = new CompositionPlan([initialization], "output-image", spaces, []);

        Assert.Same(initialization, Assert.Single(singleton.Initializations));
        Assert.Equal(canonical.OutputSpaceId, singleton.OutputSpaceId);
        Assert.Equal(
            canonical.Initializations.Select(item => item.TargetSpaceId),
            singleton.Initializations.Select(item => item.TargetSpaceId));
    }

    /// <summary>Verifies missing, duplicate, and unknown initializer targets fail closed.</summary>
    [Fact]
    public void InitializerTargetsFormExactMutableSpaceBijection()
    {
        AddressSpace[] spaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var output = ImageInitialization.Blank("output-image", 4, 0);

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [output],
            "output-image",
            spaces,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [output, output, ImageInitialization.Blank("scratch", 4, 0)],
            "output-image",
            spaces,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [
                output,
                ImageInitialization.Blank("scratch", 4, 0),
                ImageInitialization.Blank("unknown", 4, 0),
            ],
            "output-image",
            spaces,
            []));
    }

    /// <summary>Verifies output selection names one initialized mutable address space.</summary>
    [Fact]
    public void OutputSelectionRequiresDeclaredMutableInitializedSpace()
    {
        AddressSpace[] spaces =
        [
            new("immutable", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var output = ImageInitialization.Blank("output-image", 4, 0);

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [output],
            "unknown",
            spaces,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [output],
            "immutable",
            spaces,
            []));
    }

    /// <summary>Verifies initializer targets are mutable and capacities exactly match their spaces.</summary>
    [Fact]
    public void InitializersRequireMutableExactCapacityTargets()
    {
        AddressSpace[] spaces =
        [
            new("immutable", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 4, 0),
                ImageInitialization.Blank("immutable", 4, 0),
            ],
            "output-image",
            spaces,
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [ImageInitialization.Blank("output-image", 3, 0)],
            "output-image",
            spaces,
            []));
    }

    /// <summary>Verifies clone initializers require a declared immutable exact-size source.</summary>
    [Fact]
    public void CloneInitializersRequireDeclaredImmutableExactSource()
    {
        AddressSpace[] missingSourceSpaces = [new("output-image", 4, AddressSpaceMutability.Mutable)];
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [ImageInitialization.Reference("output-image", "missing", 4)],
            "output-image",
            missingSourceSpaces,
            []));

        AddressSpace[] mutableSourceSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [
                ImageInitialization.Reference("output-image", "scratch", 4),
                ImageInitialization.Blank("scratch", 4, 0),
            ],
            "output-image",
            mutableSourceSpaces,
            []));

        AddressSpace[] wrongSizeSpaces =
        [
            new("reference", 3, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [ImageInitialization.Reference("output-image", "reference", 4)],
            "output-image",
            wrongSizeSpaces,
            []));
    }

    /// <summary>Verifies clone sources reject alternate and advisory input-length policies.</summary>
    [Fact]
    public void CloneInitializersRejectInputLengthRelaxation()
    {
        AddressSpace[] alternateLength =
        [
            new("reference", 4, AddressSpaceMutability.Immutable, allowedInputLengths: [2, 4]),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        AddressSpace[] expectedLength =
        [
            new("reference", 4, AddressSpaceMutability.Immutable, expectedInputLengths: [4, 8]),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];

        foreach (AddressSpace[] spaces in new[] { alternateLength, expectedLength })
        {
            _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
                [ImageInitialization.Reference("output-image", "reference", 4)],
                "output-image",
                spaces,
                []));
        }

        AddressSpace[] extractedOutputSource =
        [
            new(
                "reference",
                4,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                expectedInputLengths: [4],
                unexpectedInputLengthIssueCode: "INPUT_OUTER_LENGTH"),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            [ImageInitialization.Reference("output-image", "reference", 4)],
            "output-image",
            extractedOutputSource,
            []));
    }
}
