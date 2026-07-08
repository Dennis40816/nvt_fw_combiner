using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class BuiltInStandardMergeProfilesTests
{
    /// <summary>Verifies NT51950/NT51951 Standard Merge copies DP first, then overlays the TP range.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void DpPerspectiveProfilesCopyDpThenOverlayTp(string icId)
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == icId);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(DpPerspectiveCatalog.MaxContainerLength, profile.Initialization.Capacity);
        AddressSpace dpInput = Assert.Single(profile.AddressSpaces, space => space.AddressSpaceId == "dp-input");
        Assert.Equal(DpPerspectiveCatalog.MaxContainerLength, dpInput.Length);
        Assert.Null(dpInput.InputPaddingByte);
        Assert.Equal([DpPerspectiveCatalog.MaxContainerLength], dpInput.AllowedInputLengths);
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "dp-input" &&
            space.Length == DpPerspectiveCatalog.MaxContainerLength &&
            space.InputPaddingByte is null);
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "tp-input" &&
            space.Length == DpPerspectiveCatalog.TpInputLength &&
            space.InputPaddingByte is null);
        Assert.Equal(
            ["copy-dp-container", "overlay-tp"],
            result.Plan!.OrderedOperations.Select(operation => operation.OperationId));

        CompositionOperation dpCopy = result.Plan.OrderedOperations[0];
        CompositionOperation tpOverlay = result.Plan.OrderedOperations[1];
        Assert.Equal(new ByteRange(0, DpPerspectiveCatalog.MaxContainerLength), dpCopy.TargetRange);
        Assert.Equal(DpPerspectiveCatalog.TpOverlayRange, tpOverlay.TargetRange);
        Assert.Equal(OverlapPolicy.ReplaceExisting, tpOverlay.OverlapPolicy);
    }

    /// <summary>Verifies DP Perspective Standard Merge preserves customer information from the DP input.</summary>
    [Fact]
    public void DpPerspectiveExecutionPreservesCustomerInfoFromDp()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == "NT51950");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        byte[] dp = new byte[0x100000];
        byte[] tp = new byte[0x37000];
        Array.Fill(dp, (byte)0x11);
        Array.Fill(tp, (byte)0x22);
        Array.Fill(dp, (byte)0xCA, 0x37000, 0x1000);
        Array.Fill(tp, (byte)0xDD, 0x0A000, 0x2D000);

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] output = result.OutputBytes.ToArray();
        Assert.All(output[..0x0A000], value => Assert.Equal(0x11, value));
        Assert.All(output[0x0A000..0x37000], value => Assert.Equal(0xDD, value));
        Assert.All(output[0x37000..0x38000], value => Assert.Equal(0xCA, value));
        Assert.All(output[0x38000..], value => Assert.Equal(0x11, value));
    }

    /// <summary>Verifies approved NT51950/NT51951 DP variants create matching output-length profiles.</summary>
    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x80000)]
    [InlineData(0x100000)]
    public void DpPerspectiveExecutionAcceptsDeclaredDpInputLengths(int dpLength)
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(
            "NT51951",
            dpLength);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        byte[] dp = new byte[dpLength];
        byte[] tp = new byte[0x37000];
        Array.Fill(dp, (byte)0x11);
        Array.Fill(tp, (byte)0xDD);
        Array.Fill(dp, (byte)0xCA, 0x37000, 0x1000);

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] output = result.OutputBytes.ToArray();
        Assert.Equal(dpLength, output.Length);
        Assert.All(output[..0x0A000], value => Assert.Equal(0x11, value));
        Assert.All(output[0x0A000..0x37000], value => Assert.Equal(0xDD, value));
        Assert.All(output[0x37000..0x38000], value => Assert.Equal(0xCA, value));
        Assert.All(output[0x38000..], value => Assert.Equal(0x11, value));
    }

    /// <summary>Verifies NT51950/NT51951 DP Perspective merge rejects DP inputs outside the three approved sizes.</summary>
    [Theory]
    [InlineData(0x3FFFF)]
    [InlineData(0x60000)]
    [InlineData(0x90000)]
    public void DpPerspectiveExecutionRejectsUnexpectedDpInputSize(int dpLength)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength("NT51950", dpLength));
        Assert.Contains(DpPerspectiveCatalog.FormatSupportedLengths(), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies NT51950/NT51951 DP Perspective merge rejects DP inputs larger than the max container.</summary>
    [Fact]
    public void DpPerspectiveExecutionRejectsOversizedDpInput()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(
            "NT51950",
            0x100000);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = new byte[0x100001],
                ["tp-input"] = new byte[0x37000],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "input.address-space.length-mismatch");
    }

    /// <summary>Verifies NT51950/NT51951 TP input must contain the declared overlay window.</summary>
    [Fact]
    public void DpPerspectiveExecutionRejectsTpInputWithoutOverlayWindow()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(
            "NT51950",
            0x40000);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = new byte[0x40000],
                ["tp-input"] = new byte[0x36FFF],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.length-mismatch", issue.Code);
        Assert.Contains("declared 225280 bytes", issue.Message, StringComparison.Ordinal);
    }
}
