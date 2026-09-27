using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>In a test process the default resolver of the four local-state files refuses the real folder.</summary>
public sealed class LocalStateGuardTests
{
    private const string ForbiddenSwitch = "NvtFwCombiner.LocalState.CurrentUserFolderForbidden";

    /// <summary>The test assembly's runtime configuration declares the current user's folder forbidden.</summary>
    [Fact]
    public void TestProcessDeclaresTheCurrentUserFolderForbidden()
    {
        Assert.Equal(ForbiddenSwitch, CompositionHostServices.CurrentUserLocalStateForbiddenSwitch);
        Assert.True(AppContext.TryGetSwitch(ForbiddenSwitch, out bool forbidden));
        Assert.True(forbidden);
    }

    /// <summary>The default composition root refuses the current user's folder before it composes a host.</summary>
    [Fact]
    public void DefaultCompositionRootFailsClosedInTestProcesses()
    {
        _ = Assert.Throws<InvalidOperationException>(static () => CompositionHostServices.Create());
        _ = Assert.Throws<InvalidOperationException>(
            static () => CompositionHostServices.ResolveCurrentUserLocalStateDirectory());
    }

    /// <summary>The public CLI resolves local state only to compose a host, and then it fails closed.</summary>
    [Fact]
    public async Task PublicCliFailsClosedWhenItWouldComposeOverTheCurrentUserFolder()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => CliApplication.RunAsync(
            ["profiles"],
            output,
            error,
            TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>The composed directory owns the toolchain runtime file that the production host reads.</summary>
    [Fact]
    public async Task ToolchainRuntimeConfigurationIsReadFromTheComposedDirectory()
    {
        using var workspace = TempWorkspace.Create("nfc-local-state-toolchain");
        string directory = workspace.PathFor("local-state");
        _ = workspace.Write("local-state/toolchain-runtime.v1.json", "not json"u8.ToArray());

        CompositionHostServices host = CompositionHostServices.Create(directory);
        IToolchainRuntimeConfigurationSession session = await host.GetToolchainRuntimeConfigurationAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(directory, host.LocalStateDirectory);
        Assert.Equal(ToolchainRuntimeConfigurationStatus.Blocked, session.Current.Status);
        Assert.Equal("toolchain-runtime.configuration.invalid", Assert.Single(session.Current.Issues).Code);
    }

    /// <summary>The Event Buffer format default path is the composed directory, never another folder.</summary>
    [Fact]
    public async Task EventBufferFormatConfigurationDefaultsIntoTheComposedDirectory()
    {
        var files = new RecordingLocalFileStore();
        string directory = IsolatedLocalState.CreateDirectory("event-buffer");
        CompositionHostServices host = CompositionHostServices.Create(
            new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null,
            directory,
            localFiles: files);

        _ = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(directory, "event-buffer-format.v1.json"), Assert.Single(files.Reads));
        Assert.Empty(files.Writes);
    }
}
