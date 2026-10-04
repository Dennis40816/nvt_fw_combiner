using System.Reflection;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the actual Launcher entry point's state exit codes without starting Desktop.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class LauncherProgramStateExitTests
{
    /// <summary>Missing or invalid state exits with 10; valid state without an active version exits with 11.</summary>
    [Theory]
    [InlineData(VersionManagerStateLoadIssue.Missing, 10)]
    [InlineData(VersionManagerStateLoadIssue.Invalid, 10)]
    [InlineData(VersionManagerStateLoadIssue.None, 11)]
    public async Task MissingInvalidOrNoActiveVersionStateReturnsExpectedExitCode(
        VersionManagerStateLoadIssue loadIssue,
        int expectedExitCode)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string statePath = workspace.PathFor("NvtFwCombiner/version-manager.v1.json");
        var store = new JsonVersionManagerStateStore(statePath);
        if (loadIssue == VersionManagerStateLoadIssue.Invalid)
        {
            _ = workspace.Write("NvtFwCombiner/version-manager.v1.json", "{}"u8.ToArray());
        }
        else if (loadIssue == VersionManagerStateLoadIssue.None)
        {
            VersionManagerState state = VersionManagerState.Create(
                updateSource: null,
                activeVersion: null,
                lastKnownGoodVersion: null,
                admissions: [],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: AppContext.BaseDirectory);
            await store.SaveAsync(state, TestContext.Current.CancellationToken);
        }

        VersionManagerStateLoadResult loaded = await store.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(loadIssue, loaded.Issue);
        if (loadIssue == VersionManagerStateLoadIssue.None)
        {
            Assert.NotNull(loaded.State);
            Assert.Null(loaded.State.ActiveVersion);
            Assert.True(loaded.State.IsBoundToManagedRoot(AppContext.BaseDirectory));
        }

        const string forbiddenSwitch = "NvtFwCombiner.LocalState.CurrentUserFolderForbidden";
        string[] environmentNames = LauncherEntryEnvironment.InheritedNames;
        Dictionary<string, string?> previousEnvironment = environmentNames.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);
        bool wasSet = AppContext.TryGetSwitch(forbiddenSwitch, out bool previousSwitch);
        try
        {
            foreach (string name in environmentNames)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            Environment.SetEnvironmentVariable("LOCALAPPDATA", workspace.Root);
            AppContext.SetSwitch(forbiddenSwitch, false);
            Assert.Equal(statePath, JsonVersionManagerStateStore.GetDefaultPath());

            // Empty arguments use the isolated default; every inherited READY and lifetime variable was cleared above.
            MethodInfo? main = Assembly.Load("NvtFwCombiner.Launcher").EntryPoint;
            Assert.NotNull(main);
            object? result = main.Invoke(null, [Array.Empty<string>()]);

            Assert.Equal(expectedExitCode, Assert.IsType<int>(result));
        }
        finally
        {
            foreach ((string name, string? value) in previousEnvironment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
            AppContext.SetSwitch(forbiddenSwitch, wasSet && previousSwitch);
        }
    }
}
