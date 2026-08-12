namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Keeps startup diagnostics opt-in, Presentation-local, and firmware-neutral.</summary>
    [Fact]
    public void StartupDiagnosticsStayPresentationLocalAndOptIn()
    {
        string session = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/StartupTraceSession.cs");
        string sink = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/StartupTraceFileSink.cs");
        string program = ReadText("src/NvtFwCombiner.Presentation.Avalonia/DesktopApplication.cs");
        string application = ReadText("src/NvtFwCombiner.Presentation.Avalonia/App.axaml.cs");
        string window = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");
        string runner = ReadText("scripts/measure-startup.ps1");
        string diagnostics = string.Join(Environment.NewLine, session, sink);

        Assert.Contains("NFC_STARTUP_TRACE_PATH", session, StringComparison.Ordinal);
        Assert.Contains("FileMode.CreateNew", sink, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.CreateDirectory", sink, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Domain", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Func<PresentationHostServices>", program, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(program, "hostServicesFactory()"));
        AssertStartupStageOrder(
            program,
            "StartFromEnvironment()",
            "host-services.started",
            "hostServicesFactory()",
            "host-services.ready",
            "launch-options.parsed");
        Assert.Contains("launch-options.parsed", program, StringComparison.Ordinal);
        Assert.Contains("application-xaml.ready", application, StringComparison.Ordinal);
        Assert.Contains("shell-view-model.created", window, StringComparison.Ordinal);
        Assert.Contains("shell-data-context.assigned", window, StringComparison.Ordinal);
        Assert.Contains("shell-initial-content.ready", window, StringComparison.Ordinal);
        Assert.Contains("main-window.opened", window, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.completed", window, StringComparison.Ordinal);
        Assert.Contains("EnvironmentVariables[$TracePathEnvironmentVariable]", runner, StringComparison.Ordinal);
        Assert.Contains("workingSetBytesAtWindow", runner, StringComparison.Ordinal);
        Assert.Contains("workingSetBytesAtTrace", runner, StringComparison.Ordinal);
        Assert.Contains("peakWorkingSetBytes", runner, StringComparison.Ordinal);
        Assert.Contains("AllocatedBytesSinceManagedEntry", session, StringComparison.Ordinal);
        Assert.Contains("allocatedBytesSinceManagedEntry", sink, StringComparison.Ordinal);
        Assert.Contains("allocationDeltaBytes", sink, StringComparison.Ordinal);
        Assert.Contains("uiThreadWork", runner, StringComparison.Ordinal);
        Assert.Contains("firstFrameUiSynchronousWorkMilliseconds", runner, StringComparison.Ordinal);
        Assert.Contains("backgroundUiMaterializationMilliseconds", runner, StringComparison.Ordinal);
        Assert.Contains("nfc-startup-measurement-v2", runner, StringComparison.Ordinal);
        Assert.Contains("return [pscustomobject][ordered]@{", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet tool", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-WebRequest", runner, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertStartupStageOrder(string source, params string[] stages)
    {
        int previous = -1;
        foreach (string stage in stages)
        {
            int current = source.IndexOf(stage, StringComparison.Ordinal);
            Assert.True(current > previous, $"Startup stage '{stage}' is missing or out of order.");
            previous = current;
        }
    }
}
