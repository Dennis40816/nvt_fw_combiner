namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunRetainedReplaceAsync(
            args,
            TestContext.Current.CancellationToken);
    }
}
