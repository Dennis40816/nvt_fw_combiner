using System.Diagnostics;
using System.IO.Pipes;
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
        string pipeName = $"nfc-process-runner-{Guid.NewGuid():N}";
        using NamedPipeServerStream readiness = CreateReadinessPipe(pipeName);
        string scriptPath = CreateChildProcessScript(workspace.Root, pipeName);
        var runner = new SystemExternalProcessRunner();
        using var cancellation = new CancellationTokenSource();
        ExternalProcessStartInfo startInfo = CreateStartInfo(workspace.Root, scriptPath, TimeSpan.FromSeconds(30));

        Task<ExternalProcessResult>? run = null;
        TestProcessIdentity? parentProcess = null;
        TestProcessIdentity? childProcess = null;
        try
        {
            run = runner.RunAsync(startInfo, cancellation.Token).AsTask();
            (parentProcess, childProcess) = await ReadProcessIdentitiesAsync(
                readiness,
                run,
                TestContext.Current.CancellationToken);

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
        string pipeName = $"nfc-process-runner-{Guid.NewGuid():N}";
        using NamedPipeServerStream readiness = CreateReadinessPipe(pipeName);
        string scriptPath = CreateChildProcessScript(workspace.Root, pipeName);
        var runner = new SystemExternalProcessRunner();
        using var cancellation = new CancellationTokenSource();
        ExternalProcessStartInfo startInfo = CreateStartInfo(workspace.Root, scriptPath, TimeSpan.FromSeconds(10));
        Task<ExternalProcessResult>? run = null;
        TestProcessIdentity? parentProcess = null;
        TestProcessIdentity? childProcess = null;
        try
        {
            run = runner.RunAsync(startInfo, cancellation.Token).AsTask();
            (parentProcess, childProcess) = await ReadProcessIdentitiesAsync(
                readiness,
                run,
                TestContext.Current.CancellationToken);

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
        string standardOutputPath = workspace.PathFor("stdout-partial.txt");
        string standardErrorPath = workspace.PathFor("stderr-partial.txt");
        string scriptPath = workspace.PathFor("large-output-then-wait.cmd");
        File.WriteAllText(standardOutputPath, new string('O', 131072) + "OUT-PARTIAL-END");
        File.WriteAllText(standardErrorPath, new string('E', 131072) + "ERR-PARTIAL-END");
        File.WriteAllText(
            scriptPath,
            "@type stdout-partial.txt" + Environment.NewLine +
            "@type stderr-partial.txt 1>&2" + Environment.NewLine +
            "@ping -n 31 127.0.0.1 >nul" + Environment.NewLine);
        var runner = new SystemExternalProcessRunner();
        var startInfo = new ExternalProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            workspace.Root,
            ["/d", "/q", "/c", scriptPath],
            TimeSpan.FromSeconds(2));

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

    private static NamedPipeServerStream CreateReadinessPipe(string pipeName)
    {
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    private static string CreateChildProcessScript(string root, string pipeName)
    {
        string scriptPath = Path.Combine(root, "long-running-child.ps1");
        string escapedPipeName = pipeName.Replace("'", "''", StringComparison.Ordinal);
        string content = $$"""
            $child = $null
            try {
              $child = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\cmd.exe') -ArgumentList '/d /c ping -n 30 127.0.0.1 > NUL' -PassThru
              $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', '{{escapedPipeName}}', [System.IO.Pipes.PipeDirection]::Out)
              try {
                $pipe.Connect()
                $writer = [System.IO.StreamWriter]::new($pipe)
                try {
                  $writer.WriteLine("$PID|$($child.Id)")
                  $writer.Flush()
                } finally {
                  $writer.Dispose()
                }
              } finally {
                $pipe.Dispose()
              }
              $child.WaitForExit()
            } finally {
              if ($null -ne $child -and -not $child.HasExited) {
                Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue
              }
            }
            """;
        File.WriteAllText(scriptPath, content + Environment.NewLine);
        return scriptPath;
    }

    private static async Task<(TestProcessIdentity Parent, TestProcessIdentity Child)> ReadProcessIdentitiesAsync(
        NamedPipeServerStream readiness,
        Task<ExternalProcessResult> run,
        CancellationToken cancellationToken)
    {
        using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task connection = readiness.WaitForConnectionAsync(connectionCancellation.Token);
        Task completed = await Task.WhenAny(connection, run);
        if (completed == run)
        {
            connectionCancellation.Cancel();
            try
            {
                await connection;
            }
            catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
            {
                // The runner completed before the fixture published readiness.
            }

            ExternalProcessResult result = await run;
            Assert.Fail(
                "The test process exited before publishing readiness: " +
                $"exit={result.ExitCode}, timedOut={result.TimedOut}, stderr={result.StandardError}");
        }

        await connection;
        using var reader = new StreamReader(readiness, leaveOpen: true);
        string? identityLine = await reader.ReadLineAsync(cancellationToken);
        string[] fields = identityLine?.Split('|', StringSplitOptions.TrimEntries) ?? [];
        Assert.Equal(2, fields.Length);
        int parentProcessId = ParseProcessId(fields[0], identityLine);
        int childProcessId = ParseProcessId(fields[1], identityLine);

        return (CaptureProcessIdentity(parentProcessId), CaptureProcessIdentity(childProcessId));
    }

    private static int ParseProcessId(string? value, string? identityLine)
    {
        if (!int.TryParse(value, out int processId) || processId <= 0)
        {
            Assert.Fail($"The test process published an invalid readiness message: {identityLine ?? "<null>"}");
        }

        return processId;
    }

    private static TestProcessIdentity CaptureProcessIdentity(int processId)
    {
        using var process = Process.GetProcessById(processId);
        return new TestProcessIdentity(processId, process.StartTime);
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
