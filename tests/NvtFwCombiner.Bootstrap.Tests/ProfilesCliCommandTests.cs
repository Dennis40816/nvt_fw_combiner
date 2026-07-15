namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI tests for stable profile catalog projections.</summary>
public sealed class ProfilesCliCommandTests
{
    /// <summary>The profiles list command preserves profile order and compiled display facts.</summary>
    [Fact]
    public async Task ProfilesListOutputRemainsStable()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["profiles", "list"],
            TestContext.Current.CancellationToken);
        string expected = string.Join(Environment.NewLine,
        [
            "Built-in standard merge profiles:",
            "nt51917-standard-merge-gen-flash-alias  ic=NT51917  inputs=dp-input, tp-input  default-output=nt51917-standard-merge-gen-flash-alias.bin",
            "nt51919-standard-merge-gen-flash-alias  ic=NT51919  inputs=dp-input, tp-input  default-output=nt51919-standard-merge-gen-flash-alias.bin",
            "nt51920-standard-merge-gen-flash  ic=NT51920  inputs=dp-input, tp-input  default-output=nt51920-standard-merge-gen-flash.bin",
            "nt51923-standard-merge-gen-flash  ic=NT51923  inputs=dp-input, tp-input  default-output=nt51923-standard-merge-gen-flash.bin",
            "nt51926-standard-merge-gen-flash  ic=NT51926  inputs=dp-input, tp-input  default-output=nt51926-standard-merge-gen-flash.bin",
            "nt51927-standard-merge-gen-flash  ic=NT51927  inputs=dp-input, tp-input  default-output=nt51927-standard-merge-gen-flash.bin",
            "nt51928-standard-merge-gen-flash  ic=NT51928  inputs=dp-input, ld-input, tp-input  default-output=nt51928-standard-merge-gen-flash.bin",
            "nt51929-standard-merge-gen-flash  ic=NT51929  inputs=dp-input, tp-input  default-output=nt51929-standard-merge-gen-flash.bin",
            "nt51930-standard-merge-flashmap  ic=NT51930  inputs=dp-input, tp-input  default-output=nt51930-standard-merge-flashmap.bin",
            "nt51931-standard-merge-gen-flash  ic=NT51931  inputs=dp-input, tp-input  default-output=nt51931-standard-merge-gen-flash.bin",
            "nt51932-standard-merge-gen-flash  ic=NT51932  inputs=dp-input, tp-input  default-output=nt51932-standard-merge-gen-flash.bin",
            "nt51950-standard-merge-dp-perspective  ic=NT51950  inputs=dp-input, tp-input  default-output=nt51950-standard-merge-dp-perspective.bin",
            "nt51951-standard-merge-dp-perspective  ic=NT51951  inputs=dp-input, tp-input  default-output=nt51951-standard-merge-dp-perspective.bin",
            "Built-in replace profiles:",
            "nt51950-dp-replace-dp-perspective  ic=NT51950  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51950-dp-replace.bin",
            "nt51951-dp-replace-dp-perspective  ic=NT51951  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51951-dp-replace.bin",
            string.Empty,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(expected, result.Output);
    }
}
