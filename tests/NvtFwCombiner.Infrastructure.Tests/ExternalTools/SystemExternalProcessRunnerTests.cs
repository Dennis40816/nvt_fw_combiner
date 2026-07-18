using System.Diagnostics;
using System.Globalization;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Verifies external process cancellation leaves no child process running.</summary>
public sealed class SystemExternalProcessRunnerTests
{
    /// <summary>Approved external tools never allocate a visible console or use a shell.</summary>
    [Fact]
    public void CreateProcessStartInfoIsHeadlessAndShellFree()
    {
        var request = new ExternalProcessStartInfo(
            "approved-tool.exe",
            Environment.CurrentDirectory,
            ["first", "second value"],
            TimeSpan.FromSeconds(5));

        ProcessStartInfo actual = SystemExternalProcessRunner.CreateProcessStartInfo(request);

        Assert.False(actual.UseShellExecute);
        Assert.True(actual.CreateNoWindow);
        Assert.True(actual.RedirectStandardOutput);
        Assert.True(actual.RedirectStandardError);
        Assert.Equal(request.ExecutablePath, actual.FileName);
        Assert.Equal(request.WorkingDirectory, actual.WorkingDirectory);
        Assert.Equal(request.Arguments, [.. actual.ArgumentList]);
    }

    /// <summary>Cancellation kills the launched process tree before the caller receives cancellation.</summary>
    [Fact]
    public async Task RunAsyncCancellationKillsChildProcessBeforeThrowing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-process-runner");
        string parentProcessIdPath = workspace.PathFor("parent.pid");
        string childProcessIdPath = workspace.PathFor("child.pid");
        string scriptPath = CreateChildProcessScript(workspace.Root, parentProcessIdPath, childProcessIdPath);
        var runner = new SystemExternalProcessRunner();
        using var cancellation = new CancellationTokenSource();
        ExternalProcessStartInfo startInfo = CreateStartInfo(workspace.Root, scriptPath, TimeSpan.FromSeconds(30));

        Task<ExternalProcessResult>? run = null;
        TestProcessIdentity? parentProcess = null;
        TestProcessIdentity? childProcess = null;
        try
        {
            run = runner.RunAsync(startInfo, cancellation.Token).AsTask();
            parentProcess = await WaitForProcessAsync(parentProcessIdPath, TestContext.Current.CancellationToken);
            childProcess = await WaitForProcessAsync(childProcessIdPath, TestContext.Current.CancellationToken);

            cancellation.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            await AssertProcessExitedAsync(parentProcess.Value, TestContext.Current.CancellationToken);
            await AssertProcessExitedAsync(childProcess.Value, TestContext.Current.CancellationToken);
        }
        finally
        {
            cancellation.Cancel();
            if (run is not null)
            {
                try
                {
                    _ = await run;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected cleanup outcome for this test process.
                }
            }

            KillTestProcessTree(parentProcess);
            KillTestProcessTree(childProcess);
        }
    }

    /// <summary>Timeout kills the launched process tree before the timeout result is returned.</summary>
    [Fact]
    public async Task RunAsyncTimeoutKillsChildProcessBeforeReturning()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-process-runner");
        string parentProcessIdPath = workspace.PathFor("parent.pid");
        string childProcessIdPath = workspace.PathFor("child.pid");
        string scriptPath = CreateChildProcessScript(workspace.Root, parentProcessIdPath, childProcessIdPath);
        var runner = new SystemExternalProcessRunner();
        using var cancellation = new CancellationTokenSource();
        ExternalProcessStartInfo startInfo = CreateStartInfo(workspace.Root, scriptPath, TimeSpan.FromSeconds(10));
        Task<ExternalProcessResult>? run = null;
        TestProcessIdentity? parentProcess = null;
        TestProcessIdentity? childProcess = null;
        try
        {
            run = runner.RunAsync(startInfo, cancellation.Token).AsTask();
            parentProcess = await WaitForProcessAsync(parentProcessIdPath, TestContext.Current.CancellationToken);
            childProcess = await WaitForProcessAsync(childProcessIdPath, TestContext.Current.CancellationToken);

            ExternalProcessResult result = await run;

            Assert.True(result.TimedOut);
            Assert.Equal(-1, result.ExitCode);
            await AssertProcessExitedAsync(parentProcess.Value, TestContext.Current.CancellationToken);
            await AssertProcessExitedAsync(childProcess.Value, TestContext.Current.CancellationToken);
        }
        finally
        {
            cancellation.Cancel();
            if (run is not null)
            {
                try
                {
                    _ = await run;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected emergency cleanup outcome when setup fails.
                }
            }

            KillTestProcessTree(parentProcess);
            KillTestProcessTree(childProcess);
        }
    }

    /// <summary>Large stdout and stderr are drained concurrently but retained only within the diagnostic cap.</summary>
    [Fact]
    public async Task RunAsyncBoundsAndDrainsBothOutputStreams()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-process-runner-output");
        string scriptPath = workspace.PathFor("large-output.ps1");
        File.WriteAllText(
            scriptPath,
            "[Console]::Out.Write(('A' * 131072) + 'OUT-END')" + Environment.NewLine +
            "[Console]::Error.Write(('B' * 131072) + 'ERR-END')" + Environment.NewLine);
        var runner = new SystemExternalProcessRunner();
        ExternalProcessStartInfo startInfo = CreateStartInfo(workspace.Root, scriptPath, TimeSpan.FromSeconds(10));

        ExternalProcessResult result = await runner.RunAsync(
            startInfo,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Equal(BoundedProcessOutputReader.MaximumCapturedCharacters, result.StandardOutput.Length);
        Assert.Equal(BoundedProcessOutputReader.MaximumCapturedCharacters, result.StandardError.Length);
        Assert.StartsWith(new string('A', 256), result.StandardOutput, StringComparison.Ordinal);
        Assert.StartsWith(new string('B', 256), result.StandardError, StringComparison.Ordinal);
        Assert.Contains(BoundedProcessOutputReader.TruncationMarker, result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(BoundedProcessOutputReader.TruncationMarker, result.StandardError, StringComparison.Ordinal);
        Assert.EndsWith("OUT-END", result.StandardOutput, StringComparison.Ordinal);
        Assert.EndsWith("ERR-END", result.StandardError, StringComparison.Ordinal);
    }

    /// <summary>Timeout waits for killed-stream drainage and retains bounded partial diagnostics.</summary>
    [Fact]
    public async Task RunAsyncTimeoutRetainsBoundedPartialOutputAfterKill()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-process-runner-timeout-output");
        string scriptPath = workspace.PathFor("large-output-then-wait.ps1");
        File.WriteAllText(
            scriptPath,
            "[Console]::Out.Write(('O' * 131072) + 'OUT-PARTIAL-END')" + Environment.NewLine +
            "[Console]::Error.Write(('E' * 131072) + 'ERR-PARTIAL-END')" + Environment.NewLine +
            "Start-Sleep -Seconds 30" + Environment.NewLine);
        var runner = new SystemExternalProcessRunner();
        ExternalProcessStartInfo startInfo = CreateStartInfo(workspace.Root, scriptPath, TimeSpan.FromSeconds(2));

        ExternalProcessResult result = await runner.RunAsync(
            startInfo,
            TestContext.Current.CancellationToken);

        Assert.Equal(-1, result.ExitCode);
        Assert.True(result.TimedOut);
        Assert.Equal(BoundedProcessOutputReader.MaximumCapturedCharacters, result.StandardOutput.Length);
        Assert.Equal(BoundedProcessOutputReader.MaximumCapturedCharacters, result.StandardError.Length);
        Assert.EndsWith("OUT-PARTIAL-END", result.StandardOutput, StringComparison.Ordinal);
        Assert.EndsWith("ERR-PARTIAL-END", result.StandardError, StringComparison.Ordinal);
    }

    private static ExternalProcessStartInfo CreateStartInfo(string root, string scriptPath, TimeSpan timeout)
    {
        return new ExternalProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            root,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath],
            timeout);
    }

    private static string CreateChildProcessScript(string root, string parentProcessIdPath, string childProcessIdPath)
    {
        string scriptPath = Path.Combine(root, "long-running-child.ps1");
        string escapedParentPidPath = parentProcessIdPath.Replace("'", "''", StringComparison.Ordinal);
        string escapedPidPath = childProcessIdPath.Replace("'", "''", StringComparison.Ordinal);
        File.WriteAllText(
            scriptPath,
            $"$PID | Set-Content -LiteralPath '{escapedParentPidPath}'" +
            Environment.NewLine +
            "$child = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\\cmd.exe') " +
            "-ArgumentList '/d /c ping -n 30 127.0.0.1 > NUL' -PassThru" +
            Environment.NewLine +
            $"$child.Id | Set-Content -LiteralPath '{escapedPidPath}'" +
            Environment.NewLine +
            "$child.WaitForExit()" +
            Environment.NewLine);
        return scriptPath;
    }

    private static async Task<TestProcessIdentity> WaitForProcessAsync(string path, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryReadProcessId(path, out int processId))
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    return new TestProcessIdentity(processId, process.StartTime);
                }
                catch (ArgumentException)
                {
                    // The process can exit between the script write and this inspection. Retry until timeout.
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new TimeoutException("The test process did not report its process id.");
    }

    private static bool TryReadProcessId(string path, out int processId)
    {
        processId = 0;
        try
        {
            return File.Exists(path) && int.TryParse(
                File.ReadAllText(path),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out processId);
        }
        catch (IOException)
        {
            // Set-Content can publish the path before releasing its write handle. The caller retries.
            return false;
        }
    }

    private static async Task AssertProcessExitedAsync(TestProcessIdentity expected, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var process = Process.GetProcessById(expected.ProcessId);
                if (process.HasExited || process.StartTime != expected.StartTime)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        Assert.Fail($"Test process {expected.ProcessId} is still running after runner completion.");
    }

    private static void KillTestProcessTree(TestProcessIdentity? expected)
    {
        if (expected is not TestProcessIdentity value)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(value.ProcessId);
            if (!process.HasExited && process.StartTime == value.StartTime)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (ArgumentException)
        {
            // The process already exited, which is the expected outcome.
        }
    }

    private readonly record struct TestProcessIdentity(int ProcessId, DateTime StartTime);
}
