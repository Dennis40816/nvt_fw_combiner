using System.Diagnostics;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.TestHost;

/// <summary>
/// Runs XunitAssemblyInfoOutputGuard in a real child process (the ReadyProbe), which exits without flushing or
/// disposing Console.Out, as the xunit.v3 3.2.2 runner does.
/// </summary>
public sealed class XunitAssemblyInfoOutputGuardProcessTests
{
    private const string AssemblyInfoJson =
        "{\"arch-os\":\"X64\",\"core-framework\":\"3.2.2\",\"test-framework\":\"xUnit.net v3 3.2.2+728c1dce01\"}";

    /// <summary>Verifies that an unfinished last line still reaches standard output when the process exits.</summary>
    [Fact]
    public async Task AssemblyInfoQueryForwardsAnUnfinishedLastLineAtProcessExit()
    {
        string output = await RunProbeAsync(["-assemblyInfo"], AssemblyInfoJson + "\nunexpected diagnostic");

        Assert.Equal(AssemblyInfoJson + "\nunexpected diagnostic", output);
    }

    /// <summary>Verifies that the installed guard removes the runner shutdown message in a real process.</summary>
    [Fact]
    public async Task AssemblyInfoQueryDropsTheRunnerShutdownMessage()
    {
        string output = await RunProbeAsync(
            ["-assemblyInfo"],
            AssemblyInfoJson + "\n" + XunitAssemblyInfoOutputGuard.ShutdownMessage + "\n");

        Assert.Equal(AssemblyInfoJson + "\n", output);
    }

    /// <summary>Verifies that a process without the assembly-info query keeps its output unchanged.</summary>
    [Fact]
    public async Task OtherInvocationsKeepTheShutdownMessage()
    {
        string text = AssemblyInfoJson + "\n" + XunitAssemblyInfoOutputGuard.ShutdownMessage + "\n";

        string output = await RunProbeAsync(["-automated"], text);

        Assert.Equal(text, output);
    }

    private static async Task<string> RunProbeAsync(string[] arguments, string standardOutput)
    {
        var start = new ProcessStartInfo(
            Path.Combine(AppContext.BaseDirectory, "ready-probe", "NvtFwCombiner.ReadyProbe.exe"))
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["NVT_READY_PROBE_BEHAVIOR"] = "xunit-assembly-info-output";
        start.Environment["NVT_READY_PROBE_STANDARD_OUTPUT"] = standardOutput;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The probe did not start.");
        try
        {
            string output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            Assert.True(
                process.ExitCode == 0,
                $"The probe exited with code {process.ExitCode}. Output tail: {output[Math.Max(0, output.Length - 400)..]}");
            return output;
        }
        finally
        {
            await ReapAsync(process);
        }
    }

    // Disposing Process does not end the child, so a timeout or cancellation above still leaves no child running.
    // Cleanup failures are swallowed to keep the original failure; the bounded wait uses its own token.
    private static async Task ReapAsync(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cleanup.Token);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or OperationCanceledException)
        {
        }
    }
}
