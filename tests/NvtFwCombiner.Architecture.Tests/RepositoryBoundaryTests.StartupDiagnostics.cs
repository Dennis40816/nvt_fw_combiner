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
        string program = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Program.cs");
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
        Assert.Contains("launch-options.parsed", program, StringComparison.Ordinal);
        Assert.Contains("application-xaml.ready", application, StringComparison.Ordinal);
        Assert.Contains("shell-view-model.created", window, StringComparison.Ordinal);
        Assert.Contains("main-window.opened", window, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.completed", window, StringComparison.Ordinal);
        Assert.Contains("EnvironmentVariables[$TracePathEnvironmentVariable]", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet tool", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-WebRequest", runner, StringComparison.OrdinalIgnoreCase);
    }
}
