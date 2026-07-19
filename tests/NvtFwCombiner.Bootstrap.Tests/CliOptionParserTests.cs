namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Regression coverage for the shared strict CLI option parser.</summary>
public sealed class CliOptionParserTests
{
    /// <summary>Values, flags, and repeatable values retain their established projection.</summary>
    [Fact]
    public void ParsesStrictValuesFlagsAndRepeatableValues()
    {
        using var error = new StringWriter();

        bool parsed = CliOptionParser.TryParse(
            ["--profile", "NT51950", "--mapping", "first", "--mapping", "second", "--overwrite"],
            ["--profile", "--mapping"],
            ["--mapping"],
            ["--overwrite"],
            error,
            out ParsedCliOptions options);

        Assert.True(parsed, error.ToString());
        Assert.Equal("NT51950", options.Values["--profile"]);
        Assert.Equal("first", options.Values["--mapping"]);
        Assert.Equal(["first", "second"], options.GetValues("--mapping"));
        Assert.Contains("--overwrite", options.Flags);
    }

    /// <summary>Malformed option sequences retain stable fail-closed diagnostics.</summary>
    [Fact]
    public void RejectsMalformedStrictOptionsWithStableMessages()
    {
        (string[] Args, string ExpectedError)[] cases =
        [
            (["--overwrite", "--overwrite"], "error: duplicate option '--overwrite'"),
            (["--profile", "first", "--profile", "second"], "error: duplicate option '--profile'"),
            (["--unknown"], "error: unknown option '--unknown'"),
            (["--profile"], "error: option '--profile' requires a value"),
            (["--profile", "--overwrite"], "error: option '--profile' requires a value"),
        ];

        foreach ((string[] args, string expectedError) in cases)
        {
            using var error = new StringWriter();
            bool parsed = CliOptionParser.TryParse(
                args,
                ["--profile"],
                [],
                ["--overwrite"],
                error,
                out _);

            Assert.False(parsed);
            Assert.Equal(expectedError + Environment.NewLine, error.ToString());
        }
    }
}
