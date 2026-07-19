namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunAsync(args, TestContext.Current.CancellationToken);
    }
}
