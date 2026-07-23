namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static Task<int> RunAbMergeAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        return AbMergeCliCommandHandler.RunAsync(args, output, error, cancellationToken);
    }
}
