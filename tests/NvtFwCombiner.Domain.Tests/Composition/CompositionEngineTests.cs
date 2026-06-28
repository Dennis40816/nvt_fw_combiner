using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests shared composition engine execution semantics.</summary>
public sealed class CompositionEngineTests
{
    /// <summary>Verifies blank initialization fills the output before copy operations execute.</summary>
    [Fact]
    public void BlankInitializationFillsOutputBeforeCopyRange()
    {
        CompositionPlan plan = CreateBlankPlan(
            6,
            new AddressSpace("input", 4, AddressSpaceMutability.Immutable),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(1, 2),
                "output-image",
                new ByteRange(2, 2),
                OverlapPolicy.Reject,
                "copy selected source bytes"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["input"] = [10, 20, 30, 40],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xFF, 0xFF, 20, 30, 0xFF, 0xFF], result.OutputBytes.ToArray());
        MutationRecord mutation = Assert.Single(result.Mutations);
        Assert.Equal([new ByteRange(2, 2)], mutation.ChangedRanges);
    }

    /// <summary>Verifies reference initialization clones the base image and leaves caller-owned input bytes unchanged.</summary>
    [Fact]
    public void ReferenceInitializationClonesBaseBeforeReplaceRange()
    {
        byte[] reference = [1, 2, 3, 4];
        CompositionPlan plan = CreateReferencePlan(
            4,
            new AddressSpace("replacement", 2, AddressSpaceMutability.Immutable),
            CompositionOperation.ReplaceRange(
                "replace-range",
                10,
                "replacement",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(1, 2),
                OverlapPolicy.Reject,
                "replace declared range"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["reference-base"] = reference,
            ["replacement"] = [9, 8],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([1, 9, 8, 4], result.OutputBytes.ToArray());
        Assert.Equal([1, 2, 3, 4], reference);
    }

    /// <summary>Verifies patch-scalar writes exactly the supplied bytes without implicit endian conversion.</summary>
    [Fact]
    public void PatchScalarWritesExactProfileBytes()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            CompositionOperation.PatchScalar(
                "patch-scalar",
                10,
                "output-image",
                new ByteRange(1, 2),
                [0xAA, 0xBB],
                OverlapPolicy.Reject,
                "write explicit scalar bytes"));

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xFF, 0xAA, 0xBB, 0xFF], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies missing immutable source bytes return a structured issue instead of a partial output.</summary>
    [Fact]
    public void MissingInputAddressSpaceFailsClosed()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            new AddressSpace("input", 2, AddressSpaceMutability.Immutable),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(0, 2),
                OverlapPolicy.Reject,
                "copy source"));

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.missing", issue.Code);
        Assert.Empty(result.OutputBytes.ToArray());
    }

    private static CompositionExecutionInput EmptyInput()
    {
        return new CompositionExecutionInput(new Dictionary<string, byte[]>());
    }

    private static CompositionPlan CreateBlankPlan(
        long capacity,
        params object[] declarations)
    {
        List<AddressSpace> addressSpaces =
        [
            new("output-image", capacity, AddressSpaceMutability.Mutable),
        ];
        List<CompositionOperation> operations = [];
        foreach (object declaration in declarations)
        {
            if (declaration is AddressSpace addressSpace)
            {
                addressSpaces.Add(addressSpace);
            }
            else if (declaration is CompositionOperation operation)
            {
                operations.Add(operation);
            }
        }

        return new CompositionPlan(ImageInitialization.Blank("output-image", capacity, 0xFF), addressSpaces, operations);
    }

    private static CompositionPlan CreateReferencePlan(
        long capacity,
        AddressSpace sourceSpace,
        CompositionOperation operation)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", capacity, AddressSpaceMutability.Immutable),
            new("output-image", capacity, AddressSpaceMutability.Mutable),
            sourceSpace,
        ];
        return new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            addressSpaces,
            [operation]);
    }
}
