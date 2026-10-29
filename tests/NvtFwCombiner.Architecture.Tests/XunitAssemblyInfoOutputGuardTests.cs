using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Tests the workaround that removes the xUnit 3.2.2 shutdown message from the assembly-info query.</summary>
public sealed class XunitAssemblyInfoOutputGuardTests
{
    private const string AssemblyInfoJson =
        "{\"arch-os\":\"X64\",\"core-framework\":\"3.2.2\",\"test-framework\":\"xUnit.net v3 3.2.2+728c1dce01\"}";

    /// <summary>
    /// Verifies that the late runner shutdown message, which made VSTest discovery list no tests on CI, never
    /// follows the assembly-info JSON.
    /// </summary>
    [Fact]
    public void AssemblyInfoOutputDropsTheExactRunnerShutdownMessage()
    {
        string output = Filter(writer =>
        {
            writer.WriteLine(AssemblyInfoJson);
            writer.WriteLine(XunitAssemblyInfoOutputGuard.ShutdownMessage);
        });

        Assert.Equal(AssemblyInfoJson + Environment.NewLine, output);
    }

    /// <summary>Verifies that unexpected trailing output stays visible, so the adapter still rejects it.</summary>
    [Fact]
    public void AssemblyInfoOutputKeepsUnexpectedTrailingOutput()
    {
        string output = Filter(writer =>
        {
            writer.WriteLine(AssemblyInfoJson);
            writer.WriteLine("Unhandled exception. System.InvalidOperationException: boom");
            writer.WriteLine(XunitAssemblyInfoOutputGuard.ShutdownMessage);
        });

        Assert.Equal(
            AssemblyInfoJson + Environment.NewLine +
            "Unhandled exception. System.InvalidOperationException: boom" + Environment.NewLine,
            output);
    }

    /// <summary>Verifies that only the exact message is removed; a near match is forwarded.</summary>
    [Theory]
    [InlineData("Waiting 10 seconds for foreground threads to exit")]
    [InlineData("Waiting 11 seconds for foreground threads to exit...")]
    [InlineData(" Waiting 10 seconds for foreground threads to exit...")]
    [InlineData("Waiting 10 seconds for foreground threads to exit... ")]
    [InlineData("waiting 10 seconds for foreground threads to exit...")]
    public void AssemblyInfoOutputForwardsANearMatchOfTheShutdownMessage(string nearMatch)
    {
        string output = Filter(writer =>
        {
            writer.WriteLine(AssemblyInfoJson);
            writer.WriteLine(nearMatch);
        });

        Assert.Equal(AssemblyInfoJson + Environment.NewLine + nearMatch + Environment.NewLine, output);
    }

    /// <summary>Verifies that a line written in parts is forwarded whole and a line without a line feed is kept.</summary>
    [Fact]
    public void AssemblyInfoOutputForwardsPartialWritesAndAnUnfinishedLine()
    {
        string output = Filter(writer =>
        {
            writer.Write(AssemblyInfoJson[..10]);
            writer.Write(AssemblyInfoJson[10..]);
            writer.Write('\n');
            writer.Write("unfinished");
        });

        Assert.Equal(AssemblyInfoJson + "\nunfinished", output);
    }

    /// <summary>Verifies that only the assembly-info query installs the guard, in any letter case.</summary>
    [Theory]
    [InlineData(new[] { "-assemblyInfo" }, true)]
    [InlineData(new[] { "-ASSEMBLYINFO" }, true)]
    [InlineData(new[] { "-automated", "-sync" }, false)]
    [InlineData(new[] { "--server" }, false)]
    [InlineData(new string[0], false)]
    public void OnlyTheAssemblyInfoQueryInstallsTheGuard(string[] arguments, bool expected)
    {
        Assert.Equal(expected, IsAssemblyInfoQuery(arguments, []));
    }

    /// <summary>
    /// Verifies the form VSTest discovery uses: the xUnit out-of-process launcher passes "@@ file" and writes
    /// "-assemblyInfo" into that response file.
    /// </summary>
    [Theory]
    [InlineData(new[] { "-assemblyInfo" }, true)]
    [InlineData(new[] { "  -assemblyInfo  ", "" }, true)]
    [InlineData(new[] { "-automated", "-sync" }, false)]
    public void AssemblyInfoQueryInAResponseFileInstallsTheGuard(string[] responseLines, bool expected)
    {
        Assert.Equal(expected, IsAssemblyInfoQuery(["@@", "response.txt"], responseLines));
    }

    /// <summary>Verifies that a missing response file never installs the guard.</summary>
    [Fact]
    public void MissingResponseFileDoesNotInstallTheGuard()
    {
        Assert.False(XunitAssemblyInfoOutputGuard.IsAssemblyInfoQuery(
            ["@@", "missing.txt"],
            _ => false,
            _ => throw new InvalidOperationException("A missing file must not be read.")));
    }

    private static string Filter(Action<TextWriter> write)
    {
        using var captured = new StringWriter();
        using (var filter = new XunitAssemblyInfoOutputGuard.ShutdownMessageFilter(captured))
        {
            write(filter);
        }

        return captured.ToString();
    }

    private static bool IsAssemblyInfoQuery(string[] arguments, string[] responseLines)
    {
        return XunitAssemblyInfoOutputGuard.IsAssemblyInfoQuery(
            arguments,
            path => path == "response.txt",
            _ => responseLines);
    }
}
