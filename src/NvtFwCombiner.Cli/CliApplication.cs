using System.Reflection;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

/// <summary>Runs the command-line application through the production composition services.</summary>
public static partial class CliApplication
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;

    /// <summary>Runs one command-line invocation and returns the process exit code.</summary>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var host = CompositionHostServices.Create();
        var services = new CliCompositionServices(
            host.CompositionCapabilityExperience, host.SavedRuleAuthoring,
            host.StandardMergeAuthoring, host.AbMergeAuthoring,
            host.DpReplaceAuthoring, host.CtrlRamAuthoring,
            host.GeneralAuthoring, host.CompositionOutputNaming, host.CompositionExecution);

        if (args is ["--version"] or ["version"])
        {
            await output.WriteLineAsync(Version).ConfigureAwait(false);
            return Success;
        }

        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            await WriteUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        try
        {
            _ = await host.ExternalEnvironmentLoader.LoadToCompletionAsync(
                progress: null,
                cancellationToken).ConfigureAwait(false);
            if (args is ["doctor"])
            {
                ISystemInformationService diagnostics =
                    host.CreateSystemInformationService(Version);
                return await RunDoctorAsync(diagnostics, output, cancellationToken).ConfigureAwait(false);
            }

            return args[0] switch
            {
                "profiles" => await RunProfilesAsync(
                    services.Capabilities, args[1..], output, error).ConfigureAwait(false),
                ExperienceIds.StandardMerge => await RunStandardMergeAsync(
                    services, host.LocalFiles, args[1..], output, error, cancellationToken).ConfigureAwait(false),
                ExperienceIds.AbMerge => await AbMergeCliCommandHandler.RunAsync(
                    services, host.LocalFiles, args[1..], output, error, cancellationToken).ConfigureAwait(false),
                ExperienceIds.GeneralMerge => await MergeCliCommandHandler.RunAsync(
                    services, args[1..], output, error, cancellationToken).ConfigureAwait(false),
                "saved-rule" => await SavedRuleCliCommandHandler.RunAsync(
                    services.SavedRuleAuthoring, args[1..], output, error, cancellationToken)
                    .ConfigureAwait(false),
                ExperienceIds.DpReplace or ExperienceIds.CtrlRamReplace or ExperienceIds.GeneralReplace =>
                    await ReplaceCliCommandHandler.RunAsync(
                            services, host.LocalFiles, args[0], args[1..], output, error, cancellationToken)
                        .ConfigureAwait(false),
                _ => await UnknownCommandAsync(args[0], error).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("error: operation canceled").ConfigureAwait(false);
            return SoftwareError;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync($"error: {exception.Message}").ConfigureAwait(false);
            return SoftwareError;
        }
    }

    internal static async Task<int> RunDoctorAsync(
        ISystemInformationService diagnostics,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(output);
        SystemInformationSnapshot snapshot = diagnostics.Refresh(
            reloadCatalog: true,
            cancellationToken);
        await output.WriteLineAsync($"Catalog state: {snapshot.CatalogState}").ConfigureAwait(false);
        await output.WriteLineAsync($"CLI assembly version: {Version}").ConfigureAwait(false);
        foreach (ActionableSystemDiagnostic diagnostic in snapshot.ActiveDiagnostics)
        {
            await output.WriteLineAsync(
                $"[{diagnostic.Category}] {diagnostic.Message} Action: {diagnostic.Action}")
                .ConfigureAwait(false);
        }

        return snapshot.IsBuildBlocked ? CompositionFailed : Success;
    }

    private static string Version => (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ??
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString() ??
        "unknown";
}
