using System.Diagnostics;

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

        using var process = new Process();
        process.StartInfo.FileName = startInfo.ExecutablePath;
        process.StartInfo.WorkingDirectory = startInfo.WorkingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        foreach (string argument in startInfo.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        _ = process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        Task wait = process.WaitForExitAsync(cancellationToken);
        var timeout = Task.Delay(startInfo.Timeout, cancellationToken);

        if (await Task.WhenAny(wait, timeout).ConfigureAwait(false) == timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryKill(process);
            await WaitForExitAfterKillAsync(wait).ConfigureAwait(false);
            return new ExternalProcessResult(-1, true, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
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
