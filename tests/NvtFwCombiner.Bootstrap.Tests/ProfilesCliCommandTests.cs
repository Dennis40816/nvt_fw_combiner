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
            "nt51923-standard-merge-gen-flash  ic=NT51923  inputs=dp-input, tp-input  default-output=nt51923-standard-merge-gen-flash.bin",
            "nt51926-standard-merge-gen-flash  ic=NT51926  inputs=dp-input, tp-input  default-output=nt51926-standard-merge-gen-flash.bin",
            "nt51927-standard-merge-gen-flash  ic=NT51927  inputs=dp-input, tp-input  default-output=nt51927-standard-merge-gen-flash.bin",
            "nt51928-standard-merge-gen-flash  ic=NT51928  inputs=dp-input, ld-input, tp-input  default-output=nt51928-standard-merge-gen-flash.bin",
            "nt51929-standard-merge-gen-flash  ic=NT51929  inputs=dp-input, tp-input  default-output=nt51929-standard-merge-gen-flash.bin",
            "nt51932-standard-merge-gen-flash  ic=NT51932  inputs=dp-input, tp-input  default-output=nt51932-standard-merge-gen-flash.bin",
            "nt51950-standard-merge-dp-perspective  ic=NT51950  inputs=dp-input, tp-input  default-output=nt51950-standard-merge-dp-perspective.bin",
            "nt51951-standard-merge-dp-perspective  ic=NT51951  inputs=dp-input, tp-input  default-output=nt51951-standard-merge-dp-perspective.bin",
            "Built-in AB Merge profiles:",
            "nt51919-ab-merge-alias  ic=NT51919  inputs=dp-ab-input, tp-a-input, tp-b-input  default-output=NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin",
            "nt51929-ab-merge  ic=NT51929  inputs=dp-ab-input, tp-a-input, tp-b-input  default-output=NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin",
            "nt51932-ab-merge  ic=NT51932  inputs=dp-ab-input, tp-a-input, tp-b-input  default-output=NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin",
            "nt51950-ab-merge  ic=NT51950  inputs=dp-ab-input, tp-a-input, tp-b-input  default-output=NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin",
            "nt51951-ab-merge  ic=NT51951  inputs=dp-ab-input, tp-a-input, tp-b-input  default-output=NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin",
            "Built-in replace profiles:",
            "nt51917-dp-replace-gen-flash-alias  ic=NT51917  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51917-dp-replace.bin",
            "nt51919-dp-replace-gen-flash-alias  ic=NT51919  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51919-dp-replace.bin",
            "nt51923-dp-replace-gen-flash  ic=NT51923  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51923-dp-replace.bin",
            "nt51926-dp-replace-gen-flash  ic=NT51926  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51926-dp-replace.bin",
            "nt51927-dp-replace-gen-flash  ic=NT51927  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51927-dp-replace.bin",
            "nt51928-dp-replace-gen-flash  ic=NT51928  inputs=dp-replacement, ld-replacement, reference-base  ic-num=SingleSelector  default-output=nt51928-dp-replace.bin",
            "nt51929-dp-replace-gen-flash  ic=NT51929  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51929-dp-replace.bin",
            "nt51932-dp-replace-gen-flash  ic=NT51932  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51932-dp-replace.bin",
            "nt51950-dp-replace-dp-perspective  ic=NT51950  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51950-dp-replace.bin",
            "nt51951-dp-replace-dp-perspective  ic=NT51951  inputs=dp-replacement, reference-base  ic-num=SingleSelector  default-output=nt51951-dp-replace.bin",
            string.Empty,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(expected, result.Output);
    }
}
