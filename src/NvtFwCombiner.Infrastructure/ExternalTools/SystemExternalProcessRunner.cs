using System.Diagnostics;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Default process runner that uses ProcessStartInfo.ArgumentList with shell execution disabled.</summary>
public sealed class SystemExternalProcessRunner : IExternalProcessRunner
{
    internal static ProcessStartInfo CreateProcessStartInfo(ExternalProcessStartInfo startInfo)
    {
        var processStartInfo = new ProcessStartInfo(startInfo.ExecutablePath)
        {
            WorkingDirectory = startInfo.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in startInfo.Arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        return processStartInfo;
    }

    /// <inheritdoc />
    public async ValueTask<ExternalProcessResult> RunAsync(
        ExternalProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = CreateProcessStartInfo(startInfo),
        };

        _ = process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        Task wait = process.WaitForExitAsync(CancellationToken.None);
        using var timeoutSource = new CancellationTokenSource();
        var timeout = Task.Delay(startInfo.Timeout, timeoutSource.Token);
        var cancellation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state =>
            {
                var running =
                    ((Process Process, TaskCompletionSource Completion))state!;
                _ = running.Completion.TrySetResult();
                TryKill(running.Process);
            },
            (process, cancellation));

        Task completed = await Task.WhenAny(wait, timeout, cancellation.Task).ConfigureAwait(false);
        timeoutSource.Cancel();
        if (completed != wait)
        {
            TryKill(process);
            await WaitForExitAfterKillAsync(wait).ConfigureAwait(false);
            string standardOutput = await stdout.ConfigureAwait(false);
            string standardError = await stderr.ConfigureAwait(false);
            if (completed == cancellation.Task)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return new ExternalProcessResult(-1, true, standardOutput, standardError);
        }

        await wait.ConfigureAwait(false);
        return new ExternalProcessResult(
            process.ExitCode,
            false,
            await stdout.ConfigureAwait(false),
            await stderr.ConfigureAwait(false));
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task WaitForExitAfterKillAsync(Task wait)
    {
        try
        {
            await wait.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
