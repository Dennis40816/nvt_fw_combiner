using System.Diagnostics;
using NvtFwCombiner.Platform.Processes;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Default process runner that uses ProcessStartInfo.ArgumentList with shell execution disabled.</summary>
public sealed class SystemExternalProcessRunner : IExternalProcessRunner
{
    /// <inheritdoc />
    public async ValueTask<ExternalProcessResult> RunAsync(
        ExternalProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        cancellationToken.ThrowIfCancellationRequested();

        using Process process = ProcessLaunchGate.Start(CreateProcessStartInfo(startInfo)) ??
            throw new InvalidOperationException("External process did not start.");
        Task<string> stdout = BoundedProcessOutputReader.ReadAsync(process.StandardOutput);
        Task<string> stderr = BoundedProcessOutputReader.ReadAsync(process.StandardError);
        Task wait = process.WaitForExitAsync(CancellationToken.None);
        using var timeoutSource = new CancellationTokenSource();
        var timeout = Task.Delay(startInfo.Timeout, timeoutSource.Token);
        var cancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            () =>
            {
                _ = cancellation.TrySetResult();
                TryKill(process);
            });

        Task completed = await Task.WhenAny(wait, timeout, cancellation.Task).ConfigureAwait(false);
        timeoutSource.Cancel();
        bool cancellationRequested = cancellationToken.IsCancellationRequested;
        if (completed != wait || cancellationRequested)
        {
            TryKill(process);
            await WaitForExitAfterKillAsync(wait).ConfigureAwait(false);
            string standardOutput = await stdout.ConfigureAwait(false);
            string standardError = await stderr.ConfigureAwait(false);
            if (cancellationRequested)
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

    internal static ProcessStartInfo CreateProcessStartInfo(ExternalProcessStartInfo startInfo)
    {
        var result = new ProcessStartInfo(startInfo.ExecutablePath)
        {
            WorkingDirectory = startInfo.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in startInfo.Arguments)
        {
            result.ArgumentList.Add(argument);
        }

        return result;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
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
