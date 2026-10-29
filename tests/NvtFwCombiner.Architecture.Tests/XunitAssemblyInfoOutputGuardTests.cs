using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Tests the workaround that keeps the xUnit assembly-info query to one JSON line.</summary>
public sealed class XunitAssemblyInfoOutputGuardTests
{
    private const string AssemblyInfoJson =
        "{\"arch-os\":\"X64\",\"core-framework\":\"3.2.2\",\"test-framework\":\"xUnit.net v3 3.2.2+728c1dce01\"}";

    /// <summary>
    /// Verifies that the late runner shutdown message, which made VSTest discovery list no tests on CI, never
    /// follows the assembly-info JSON.
    /// </summary>
    [Fact]
    public void AssemblyInfoOutputKeepsOnlyTheJsonLineWhenTheRunnerWritesItsShutdownMessage()
    {
        using var captured = new StringWriter();
        using var guard = new XunitAssemblyInfoOutputGuard.FirstLineOnlyWriter(captured);

        guard.WriteLine(AssemblyInfoJson);
        guard.WriteLine("Waiting 10 seconds for foreground threads to exit...");
        guard.Flush();

        Assert.Equal(AssemblyInfoJson + Environment.NewLine, captured.ToString());
    }

    /// <summary>Verifies that a JSON line written in parts is forwarded whole.</summary>
    [Fact]
    public void AssemblyInfoOutputForwardsTheFirstLineWrittenInParts()
    {
        using var captured = new StringWriter();
        using var guard = new XunitAssemblyInfoOutputGuard.FirstLineOnlyWriter(captured);

        guard.Write(AssemblyInfoJson[..10]);
        guard.Write(AssemblyInfoJson[10..]);
        guard.Write('\n');
        guard.Write("late");

        Assert.Equal(AssemblyInfoJson + "\n", captured.ToString());
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

    private static bool IsAssemblyInfoQuery(string[] arguments, string[] responseLines)
    {
        return XunitAssemblyInfoOutputGuard.IsAssemblyInfoQuery(
            arguments,
            path => path == "response.txt",
            _ => responseLines);
    }
}
