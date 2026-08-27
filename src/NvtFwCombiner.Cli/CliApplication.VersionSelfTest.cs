using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Cli;

public static partial class CliApplication
{
    private static async Task<int> RunVersionSelfTestCommandAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<IReadOnlyList<string>, IVersionManagementExperience> createVersionManagement,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(createVersionManagement);
        IReadOnlyList<string> locators;
        if (args.Length == 0)
        {
            locators = UpdateSourceRegistryLocator.ResolveAll(
                explicitLocatorSupplied: false,
                explicitLocator: null,
                Environment.GetEnvironmentVariable);
        }
        else if (args is ["--registry", string explicitLocator] &&
                 !string.IsNullOrWhiteSpace(explicitLocator))
        {
            locators = UpdateSourceRegistryLocator.ResolveAll(
                explicitLocatorSupplied: true,
                explicitLocator,
                Environment.GetEnvironmentVariable);
        }
        else
        {
            await error.WriteLineAsync(
                "usage: nvt_fw_combiner version-self-test [--registry <https-uri-or-absolute-path>]")
                .ConfigureAwait(false);
            return UsageError;
        }

        IVersionManagementExperience versionManagement = createVersionManagement(locators);
        try
        {
            return await RunVersionEnvironmentSelfTestAsync(
                versionManagement.RunEnvironmentSelfTestAsync,
                output,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            (versionManagement as IDisposable)?.Dispose();
        }
    }

    internal static async Task<int> RunVersionEnvironmentSelfTestAsync(
        Func<CancellationToken, ValueTask<VersionEnvironmentSelfTestResult>> runSelfTest,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runSelfTest);
        ArgumentNullException.ThrowIfNull(output);

        VersionEnvironmentSelfTestResult result = await runSelfTest(cancellationToken)
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Registry: {result.RegistryIssue}").ConfigureAwait(false);
        if (result.AuthorityIssue != UpdateSourceRegistryIssue.None)
        {
            await output.WriteLineAsync($"Registry authority: {result.AuthorityIssue}")
                .ConfigureAwait(false);
        }
        if (result.AcceptedRegistryRevision is { } acceptedRevision)
        {
            await output.WriteLineAsync($"Accepted Registry revision: {acceptedRevision}")
                .ConfigureAwait(false);
        }
        foreach (UpdateSourceRegistryReplicaObservation replica in result.Replicas)
        {
            string role = replica.Position == 1 ? "Primary" : $"Backup {replica.Position - 1}";
            await output.WriteLineAsync(
                $"Registry replica {replica.Position}: Role={role}; Issue={replica.Issue}; " +
                $"Revision={replica.RegistryRevision}; Selected={replica.IsSelected}")
                .ConfigureAwait(false);
        }
        for (int index = 0; index < result.Attempts.Count; index++)
        {
            VersionEnvironmentSelfTestAttempt attempt = result.Attempts[index];
            await output.WriteLineAsync(
                $"Candidate {index + 1}: Status={attempt.Status}; " +
                $"Catalog={attempt.CatalogIssue}; Package={attempt.PackageIssue}; " +
                $"Newest={attempt.NewestVersion}; Verified={attempt.IsVerified}")
                .ConfigureAwait(false);
        }

        return result.IsSuccess ? Success : CompositionFailed;
    }
}
