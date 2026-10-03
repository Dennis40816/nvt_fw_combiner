using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>
/// Public capture exits from the built Desktop executable, including its GUI lifetime.
/// Complete directory inventories and file hashes guard both real and redirected local state.
/// </summary>
public sealed partial class DesktopCaptureProcessTests(ITestOutputHelper testOutput)
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(60);
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Standalone help returns from the actual entry without a window or local-state changes.</summary>
    [Fact]
    public async Task PublicHelpExitsZeroWithoutWindowOrLocalState()
    {
        using var workspace = TempWorkspace.Create("desktop-process-help");
        string host = CopyBuiltDesktop(workspace);
        ProcessResult result = await RunAsync(host, workspace, ["--help"]);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--capture", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.False(Directory.Exists(workspace.PathFor("local-state")));
    }

    /// <summary>Default state resolution failure returns the capture exception exit before GUI startup.</summary>
    [Fact]
    public async Task InvalidDefaultStateDirectoryExits70WithoutCaptureWrites()
    {
        using var workspace = TempWorkspace.Create("desktop-process-invalid-default-state");
        string host = CopyBuiltDesktop(workspace);
        string capture = workspace.PathFor("capture.png");

        ProcessResult result = await RunAsync(host, workspace,
            ["--report", workspace.PathFor("report.json"), "--open-report", "--capture", capture],
            localApplicationData: " ");

        Assert.Equal(70, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("startup", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("--capture", result.StandardError, StringComparison.Ordinal);
        Assert.Contains(capture, result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(capture));
        Assert.False(Directory.Exists(workspace.PathFor("local-state")));
    }

    /// <summary>A loaded report produces a real PNG and the GUI process terminates successfully.</summary>
    [Fact]
    public async Task SavedReportCapturePublishesPngAndProcessExitsZero()
    {
        using var workspace = TempWorkspace.Create("desktop-process-success");
        string host = CopyBuiltDesktop(workspace);
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(SavedReport));
        string capture = workspace.PathFor("report.png");

        ProcessResult result = await RunAsync(host, workspace,
            ["--report", report, "--open-report", "--capture", capture]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.True(File.Exists(capture));
        Assert.True(new FileInfo(capture).Length > PngSignature.Length);
        Assert.Equal(PngSignature, File.ReadAllBytes(capture).AsSpan(0, PngSignature.Length).ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(SavedReport), File.ReadAllBytes(report));
        Assert.Empty(Directory.EnumerateFiles(workspace.Root, "*.tmp", SearchOption.AllDirectories));
    }

    /// <summary>Startup reads a real pending journal without recovering it or creating a persistent writer-lock file.</summary>
    [Fact]
    public async Task SavedReportCapturePreservesPendingVersionRecoveryWithoutWriterFiles()
    {
        using var workspace = TempWorkspace.Create("desktop-process-version-recovery");
        string host = CopyBuiltDesktop(workspace);
        var prepared = new VersionManagerStateDocument(
            SchemaVersion: 1, UpdateSource: null, ActiveVersion: null, LastKnownGoodVersion: null, Admissions: [],
            PendingActivation: null, FailedActivationVersion: null, RetentionReviewDue: false,
            ManagedRootIdentity: host, PendingMutation: new("install",
                new("1.2.3", "capture-pending",
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")));
        string statePath = workspace.Write(Path.Combine("local-state", CompositionHostServices.LocalStateFolderName,
            "version-manager.v1.json"), JsonSerializer.SerializeToUtf8Bytes(prepared, JsonSerializerOptions.Web));
        byte[] before = File.ReadAllBytes(statePath);
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(SavedReport));
        string capture = workspace.PathFor("report.png");

        ProcessResult result = await RunAsync(host, workspace,
            ["--report", report, "--open-report", "--capture", capture]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.True(File.Exists(capture));
        Assert.Equal(before, File.ReadAllBytes(statePath));
        VersionManagementSnapshot loaded = await CompositionHostServices.CreateVersionManagementExperience(
            "1.2.3", host, statePath).InitializeAsync(TestContext.Current.CancellationToken, isReadOnly: true);
        Assert.Equal(VersionManagerStateLoadIssue.None, loaded.StateIssue);
        Assert.Equal(new PendingManagedVersionMutation(ManagedVersionMutationKind.Install,
            new(ManagedAppVersion.Parse("1.2.3"), prepared.PendingMutation!.Admission!.AdmissionIdentity!,
                prepared.PendingMutation.Admission.ReleaseManifestSha256!)), loaded.State!.PendingMutation);
    }

    /// <summary>A rejected saved report is a completed capture failure, with no PNG or hanging window.</summary>
    [Fact]
    public async Task MalformedSavedReportExitsOneWithReportDiagnostic()
    {
        using var workspace = TempWorkspace.Create("desktop-process-bad-report");
        string host = CopyBuiltDesktop(workspace);
        string report = workspace.Write("bad-report.json", "{"u8.ToArray());
        string capture = workspace.PathFor("rejected.png");

        ProcessResult result = await RunAsync(host, workspace,
            ["--report", report, "--open-report", "--capture", capture]);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("report", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(report, result.StandardError, StringComparison.Ordinal);
        Assert.Contains(capture, result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(capture));
    }

    /// <summary>Invalid public options fail before GUI startup and never create a screenshot.</summary>
    [Fact]
    public async Task InvalidCaptureRequestExits64WithValidationDiagnostic()
    {
        using var workspace = TempWorkspace.Create("desktop-process-invalid-request");
        string host = CopyBuiltDesktop(workspace);
        string capture = workspace.PathFor("invalid.png");

        ProcessResult result = await RunAsync(host, workspace,
            ["--report", workspace.PathFor("missing.json"), "--open-report", "--capture", capture, "--unknown"]);

        Assert.Equal(64, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("validation:", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("--unknown", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(capture));
    }

    /// <summary>An external window close cancels capture through the real lifetime and exits 70.</summary>
    [Fact]
    public async Task ClosingCaptureWindowBeforePublicationExits70()
    {
        Assert.True(OperatingSystem.IsWindows(), "This process-window test requires Windows user32.");
        using var workspace = TempWorkspace.Create("desktop-process-cancel");
        string host = CopyBuiltDesktop(workspace);
        // The real report projection runs on a worker. A large valid report keeps it pending
        // while the external window-close request reaches the GUI lifetime.
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(LargeSavedReport()));
        string capture = workspace.PathFor("cancelled.png");

        ProcessResult result = await RunAsync(host, workspace,
            ["--report", report, "--open-report", "--capture", capture],
            async (process, cancellationToken) =>
            {
                IntPtr window = IntPtr.Zero;
                while (window == IntPtr.Zero && !process.HasExited)
                {
                    process.Refresh();
                    window = process.MainWindowHandle;
                    if (window == IntPtr.Zero)
                    {
                        await Task.Delay(1, cancellationToken);
                    }
                }
                Assert.NotEqual(IntPtr.Zero, window);
                Assert.True(PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero));
            });

        Assert.Equal(70, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("cancel --capture", result.StandardError, StringComparison.Ordinal);
        Assert.Contains(capture, result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(capture));
    }

    private static string CopyBuiltDesktop(TempWorkspace workspace)
    {
        // UiSmoke's existing Desktop project reference builds the host in the same shard as these tests.
        // Copy the complete exact-configuration output so each child has its own runtime files and profiles.
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? string.Empty;
        string source = RepositoryPaths.FromRepositoryRoot(
            "src", "NvtFwCombiner.Desktop", "bin", configuration, "net10.0");
        string executable = Path.Combine(source, "NvtFwCombiner.Desktop.exe");
        Assert.True(File.Exists(executable), $"The freshly built Desktop executable is required: {executable}");
        RequireCurrentDesktopHost(executable, RepositoryPaths.FromRepositoryRoot(
            "src", "NvtFwCombiner.Presentation.Avalonia", "bin", configuration, "net10.0", "NvtFwCombiner.Presentation.Avalonia.dll"));
        string destination = workspace.PathFor("host");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return destination;
    }

    private static string LargeSavedReport()
    {
        const string issue = """{"Code":"capture.pending","Message":"Pending report item.","OperationId":"review","Severity":"warning"}""";
        var issues = new StringBuilder(capacity: 5_500_000);
        for (int index = 0; index < 50_000; index++)
        {
            if (index != 0) { _ = issues.Append(','); }
            _ = issues.Append(issue);
        }
        string json = SavedReport.Replace("\"Issues\": []", $"\"Issues\": [{issues}]", StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(json) < 10 * 1024 * 1024);
        return json;
    }

    private async Task<ProcessResult> RunAsync(
        string host, TempWorkspace workspace, string[] arguments,
        Func<Process, CancellationToken, Task>? afterStart = null,
        string? localApplicationData = null)
    {
        var start = new ProcessStartInfo(Path.Combine(host, "NvtFwCombiner.Desktop.exe"))
        {
            WorkingDirectory = host,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["LOCALAPPDATA"] = localApplicationData ?? workspace.PathFor("local-state");
        start.Environment["APPDATA"] = workspace.PathFor("roaming-state");
        start.Environment["XDG_DATA_HOME"] = workspace.PathFor("xdg-state");
        _ = start.Environment.Remove("NVT_FW_COMBINER_PROCESS_LIFETIME_CONTEXT");
        _ = start.Environment.Remove("NFC_STARTUP_TRACE_PATH");

        string localState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CompositionHostServices.LocalStateFolderName);
        string redirectedState = workspace.PathFor("local-state");
        LocalStateSnapshot? before = IsLocalStateWriterActive(localState) ? null : SnapshotLocalState(localState);
        if (before is null) { testOutput.WriteLine("Skipping real local-state snapshot comparison: an NFC writer-lock is held in {0}.", localState); }
        LocalStateSnapshot redirectedBefore = SnapshotLocalState(redirectedState);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Desktop did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(ProcessTimeout);
        Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            if (afterStart is not null)
            {
                await afterStart(process, timeout.Token);
            }
            await process.WaitForExitAsync(timeout.Token);
            return new(process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            AssertLocalStateUnchanged(redirectedBefore, redirectedState);
            if (before is not null)
            {
                if (IsLocalStateWriterActive(localState))
                {
                    testOutput.WriteLine("Skipping real local-state snapshot comparison: an NFC writer-lock is held in {0}.", localState);
                }
                else
                {
                    AssertLocalStateUnchanged(before, localState);
                }
            }
        }
    }

    /// <summary>A stale lock file does not skip real-state evidence; only a held writer lock does.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LocalStateWriterDetectionRequiresHeldLock(bool held)
    {
        using var workspace = TempWorkspace.Create("desktop-state-writer");
        string path = workspace.Write(".version-manager.v1.json.test.writer.lock", []);
        using FileStream? writer = held ? new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        Assert.Equal(held, IsLocalStateWriterActive(workspace.Root));
    }

    private static bool IsLocalStateWriterActive(string directory)
    {
        if (!Directory.Exists(directory)) { return false; }
        foreach (string path in Directory.EnumerateFiles(directory, "*.writer.lock", SearchOption.AllDirectories))
        {
            try
            {
                // Read existing locks only; never create or acquire a real-profile writer lease.
                using var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (IOException exception) when ((exception.HResult & 0xFFFF) is 32 or 33)
            {
                return true;
            }
        }
        return false;
    }

    private static LocalStateSnapshot SnapshotLocalState(string directory)
    {
        return new(Directory.Exists(directory),
            !Directory.Exists(directory) ? [] : [.. Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(directory, path)).Order(StringComparer.Ordinal)],
            HashLocalState(directory));
    }

    private static void AssertLocalStateUnchanged(LocalStateSnapshot before, string directory)
    {
        LocalStateSnapshot after = SnapshotLocalState(directory);
        Assert.Equal(before.Exists, after.Exists);
        Assert.Equal(before.Directories, after.Directories);
        Assert.Equal(before.Files, after.Files);
    }

    private static KeyValuePair<string, string>[] HashLocalState(string directory)
    {
        return !Directory.Exists(directory) ? [] : [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new KeyValuePair<string, string>(Path.GetRelativePath(directory, path),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))))];
    }

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr first, IntPtr second);
#pragma warning restore SYSLIB1054

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record LocalStateSnapshot(bool Exists, string[] Directories, KeyValuePair<string, string>[] Files);

    private const string SavedReport = """
        {
          "ProfileId": "nt51950-dp-replace-dp-perspective",
          "IcId": "NT51950",
          "ModeId": "dp-replace",
          "ExperienceId": "dp-replace",
          "CompositionKind": "Replace",
          "RunId": "desktop-process-capture",
          "StartedAtUtc": "2026-07-01T00:00:00Z",
          "Inputs": [],
          "Operations": [],
          "Mutations": [],
          "OutputDifferences": [],
          "Issues": [],
          "Output": {
            "FileName": "historical-dp.bin",
            "Size": 262144,
            "Committed": true,
            "Sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
          }
        }
        """;
}
