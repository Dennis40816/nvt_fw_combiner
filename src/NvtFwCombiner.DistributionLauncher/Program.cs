using Avalonia;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.DistributionLauncher;

internal enum DistributionLauncherExitCode
{
    LaunchInstalled = 0,
    SetupRequired = 10,
    RecoveryRequired = 11,
    Busy = 12,
    HealthUnavailable = 13,
    LaunchFailed = 14,
    PayloadUnavailable = 15,
    PayloadInvalid = 16,
    TerminationUnconfirmed = 17,
    HostUnavailable = 18,
}

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        try
        {
            using ManagedDistributionLauncherHostServices host =
                ManagedDistributionLauncherHostServices.Create();
            ManagedDistributionLauncherHostResult result = host
                .RunAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            DistributionLauncherExitCode exitCode = MapExitCode(
                result.PayloadIssue,
                result.Entry?.Outcome,
                result.Setup is not null);
            return result.Setup is not null || result.Recovery is not null
                ? App.ConfigureAndRun(result, result.Entry!.ManagedRoot ?? host.ManagedRoot)
                : (int)exitCode;
        }
        catch (Exception exception) when (exception is
            ArgumentException or FormatException or InvalidOperationException or IOException or
            UnauthorizedAccessException)
        {
            return (int)DistributionLauncherExitCode.HostUnavailable;
        }
    }

    internal static Avalonia.AppBuilder BuildAvaloniaApp()
    {
        return Avalonia.AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();
    }

    internal static DistributionLauncherExitCode MapExitCode(
        ManagedDistributionPayloadIssue payloadIssue,
        ManagedLauncherEntryOutcome? entryOutcome,
        bool hasSetup)
    {
        return (payloadIssue, entryOutcome, hasSetup) switch
        {
            (ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.LaunchInstalled, false) =>
                DistributionLauncherExitCode.LaunchInstalled,
            (ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.SetupRequired, true) =>
                DistributionLauncherExitCode.SetupRequired,
            (ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.RecoveryRequired, false) =>
                DistributionLauncherExitCode.RecoveryRequired,
            (ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.Busy, false) =>
                DistributionLauncherExitCode.Busy,
            (ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.HealthUnavailable, false) =>
                DistributionLauncherExitCode.HealthUnavailable,
            (ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.LaunchFailed, false) =>
                DistributionLauncherExitCode.LaunchFailed,
            (ManagedDistributionPayloadIssue.Unavailable, null, false) =>
                DistributionLauncherExitCode.PayloadUnavailable,
            (ManagedDistributionPayloadIssue.Invalid, null, false) =>
                DistributionLauncherExitCode.PayloadInvalid,
            (ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.TerminationUnconfirmed, false) =>
                DistributionLauncherExitCode.TerminationUnconfirmed,
            _ => DistributionLauncherExitCode.HostUnavailable,
        };
    }
}
