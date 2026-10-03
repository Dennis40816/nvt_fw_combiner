using System.Reflection;
using System.Runtime.Loader;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>In-process Desktop entry coverage, using the existing copied Desktop probe host.</summary>
[Collection(UiProcessWideObservationCollection.Name)]
public sealed class DesktopLaunchExitTests
{
    /// <summary>Public help completes with stdout usage before any composition or trace file.</summary>
    [Fact]
    public void HelpExits0BeforeCompositionAndStartupTracing()
    {
        using var workspace = TempWorkspace.Create("desktop-entry-help");
        string trace = workspace.PathFor("trace.json");
        string? previousTrace = Environment.GetEnvironmentVariable("NFC_STARTUP_TRACE_PATH");
        try
        {
            Environment.SetEnvironmentVariable("NFC_STARTUP_TRACE_PATH", trace);
            (int exit, string output, string error) = BuiltDesktopEntry.Run(workspace, ["--help"]);
            Assert.Equal(0, exit);
            Assert.Contains("Usage:", output, StringComparison.Ordinal);
            Assert.Contains("--capture", output, StringComparison.Ordinal);
            Assert.DoesNotContain("--report-tab", output, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error);
            Assert.False(File.Exists(trace));
            using var tracedOutput = new StringWriter();
            using var tracedError = new StringWriter();
            int launches = 0;
            int CompleteTrace(StartupTraceSession session)
            {
                launches++;
                _ = session.Complete("test-startup-completed");
                return 0;
            }
            Assert.Equal(0, DesktopApplication.DispatchLaunch(UiLaunchOptions.Parse(["--help"]),
                tracedOutput, tracedError, CompleteTrace));
            Assert.Equal(0, launches);
            Assert.False(File.Exists(trace));
            // Positive control: the same real dispatch completes and writes an ordinary trace.
            Assert.Equal(0, DesktopApplication.DispatchLaunch(UiLaunchOptions.Empty,
                tracedOutput, tracedError, CompleteTrace));
            Assert.Equal(1, launches);
            Assert.True(File.Exists(trace));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NFC_STARTUP_TRACE_PATH", previousTrace);
        }
    }

    /// <summary>Unknown and duplicate public options return 64 rather than reaching a host.</summary>
    [Theory]
    [InlineData("--unknown")]
    [InlineData("--report-tab=Raw")]
    [InlineData("--page=home", "--page=merge")]
    [InlineData("--report=a.json", "--load-report=b.json")]
    [InlineData("--open-report")]
    public void RefusedRequestsExit64WithStderr(params string[] arguments)
    {
        using var workspace = TempWorkspace.Create("desktop-entry-refusal");
        (int exit, string output, string error) = BuiltDesktopEntry.Run(workspace, [.. arguments, "--capture=out.png"]);
        Assert.Equal(64, exit);
        Assert.Equal(string.Empty, output);
        Assert.Contains("validation:", error, StringComparison.Ordinal);
    }

    /// <summary>Capture disables trace publication even when its path aliases the PNG destination.</summary>
    [Fact]
    public void CaptureFailureDoesNotWriteStartupTrace()
    {
        using var workspace = TempWorkspace.Create("desktop-entry-trace-collision");
        string capture = workspace.PathFor("out.png");
        string? previous = Environment.GetEnvironmentVariable("NFC_STARTUP_TRACE_PATH");
        try
        {
            Environment.SetEnvironmentVariable("NFC_STARTUP_TRACE_PATH", capture);
            UiLaunchOptions options = UiLaunchOptions.Parse(
                ["--report", workspace.PathFor("report.json"), "--open-report", "--capture", capture], new LocalFileStore());
            using var output = new StringWriter();
            using var error = new StringWriter();
            int completions = 0;
            int exit = DesktopApplication.DispatchLaunch(options, output, error, session =>
            {
                completions++;
                Assert.False(session.IsEnabled);
                _ = session.Complete("startup-capture.failed");
                return 1;
            });
            Assert.Equal(1, exit);
            Assert.Equal(1, completions);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(string.Empty, error.ToString());
            Assert.False(File.Exists(capture));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NFC_STARTUP_TRACE_PATH", previous);
        }
    }

    /// <summary>A composition exception in a valid capture returns 70 with the phase and paths.</summary>
    [Fact]
    public void UnexpectedCaptureStartupExits70WithTargetPaths()
    {
        using var workspace = TempWorkspace.Create("desktop-entry-unexpected");
        string report = workspace.PathFor("report.json");
        string capture = workspace.PathFor("out.png");
        (int exit, string output, string error) = BuiltDesktopEntry.Run(workspace,
            ["--report", report, "--open-report", "--capture", capture]);
        Assert.Equal(70, exit);
        Assert.Equal(string.Empty, output);
        Assert.Contains("startup --capture", error, StringComparison.Ordinal);
        Assert.Contains(report, error, StringComparison.Ordinal);
        Assert.Contains(capture, error, StringComparison.Ordinal);
        Assert.False(File.Exists(capture));
    }

    /// <summary>Managed option refusal and invalid inherited context keep precedence over public help.</summary>
    [Theory]
    [InlineData(false, 64)]
    [InlineData(true, 22)]
    public void ManagedEntryFailuresKeepTheirExistingExitCodes(bool inherited, int expected)
    {
        using var entry = new BuiltDesktopEntry();
        const string context = "NVT_FW_COMBINER_PROCESS_LIFETIME_CONTEXT";
        string? previous = Environment.GetEnvironmentVariable(context);
        try
        {
            if (inherited) { Environment.SetEnvironmentVariable(context, "invalid-test-context"); }
            string[] args = inherited ? ["--help"] : ["--help", "--managed-root"];
            Assert.Equal(expected, entry.RunProgram(args));
        }
        finally
        {
            Environment.SetEnvironmentVariable(context, previous);
        }
    }

    private sealed class BuiltDesktopEntry : IDisposable
    {
        private readonly string _directory;

        internal BuiltDesktopEntry()
        {
            // UiSmoke already builds and copies this exact-source Desktop host for probe tests.
            _directory = Path.Combine(AppContext.BaseDirectory, "cph");
            string assembly = Path.Combine(_directory, "NvtFwCombiner.Desktop.dll");
            Assert.True(File.Exists(assembly), "The existing Desktop probe host must be built before entry tests.");
            AssemblyLoadContext.Default.Resolving += Resolve;
        }

        private Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            string path = Path.Combine(_directory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        }

        internal static (int Exit, string Output, string Error) Run(TempWorkspace workspace, string[] args)
        {
            TextWriter previousOutput = Console.Out;
            TextWriter previousError = Console.Error;
            using var output = new StringWriter();
            using var error = new StringWriter();
            try
            {
                Console.SetOut(output);
                Console.SetError(error);
                int exit = DesktopApplication.Run(
                    static () => throw new InvalidOperationException("Injected host composition failure."),
                    new LocalFileStore(), workspace.Root, args);
                return (exit, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(previousOutput);
                Console.SetError(previousError);
            }
        }

        internal int RunProgram(string[] args)
        {
            Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(
                Path.Combine(_directory, "NvtFwCombiner.Desktop.dll"));
            MethodInfo main = assembly.GetType("NvtFwCombiner.Desktop.Program", throwOnError: true)!
                .GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!;
            return (int)main.Invoke(null, [args])!;
        }

        public void Dispose()
        {
            AssemblyLoadContext.Default.Resolving -= Resolve;
        }
    }
}
