using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionEngineTests
{
    /// <summary>An exact immutable DP keeps all accepted bytes and warns beyond every advisory length.</summary>
    [Fact]
    public void ExactSourceEnvelopeRetainsAllBytesAndWarnsAboveEveryExpectedLength()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 5, 0),
            [
                new AddressSpace("dp-input", 5, AddressSpaceMutability.Immutable,
                    allowedInputLengths: [5], expectedInputLengths: [2, 3, 4],
                    unexpectedInputLengthIssueCode: "DP_NONSTANDARD_SIZE_WARNING"),
                new AddressSpace("output-image", 5, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.CopyRange("copy-dp", 10, "dp-input", new ByteRange(0, 5),
                "output-image", new ByteRange(0, 5), OverlapPolicy.Reject, "retain whole DP")]);

        CompositionExecutionResult actual = CompositionEngine.Execute(plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]> { ["dp-input"] = [1, 2, 3, 4, 5] }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, actual.Status);
        Assert.Equal([1, 2, 3, 4, 5], actual.OutputBytes.ToArray());
        CompositionIssue warning = Assert.Single(actual.Issues);
        Assert.Equal("DP_NONSTANDARD_SIZE_WARNING", warning.Code);
        Assert.Equal(CompositionIssueSeverity.Warning, warning.Severity);
        Assert.Contains("retains the complete exact source", warning.Message, StringComparison.Ordinal);

        foreach (byte[] wrongLength in new byte[][] { [1, 2, 3, 4], [1, 2, 3, 4, 5, 6] })
        {
            CompositionExecutionResult rejected = CompositionEngine.Execute(plan,
                new CompositionExecutionInput(new Dictionary<string, byte[]> { ["dp-input"] = wrongLength }));
            Assert.Equal(CompositionExecutionStatus.Failed, rejected.Status);
            Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                Assert.Single(rejected.Issues).Code);
        }
    }
}
