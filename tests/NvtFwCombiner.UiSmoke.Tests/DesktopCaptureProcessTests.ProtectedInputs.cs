using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DesktopCaptureProcessTests
{
    /// <summary>The default state under redirected LOCALAPPDATA is protected by physical identity.</summary>
    [Fact]
    public async Task DefaultStateHardLinkExits64WithoutChangingAnyFiles()
    {
        using var workspace = TempWorkspace.Create("desktop-default-state-alias");
        string host = CopyBuiltDesktop(workspace);
        byte[] original = [4, 3, 2, 1];
        string state = workspace.Write(Path.Combine("local-state", CompositionHostServices.LocalStateFolderName,
            "version-manager.v1.json"), original);
        string capture = workspace.PathFor("alias.png");
        Assert.True(WindowsCreateInputHardLink(capture, state, 0) != 0,
            new Win32Exception(Marshal.GetLastPInvokeError()).Message);
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(SavedReport));
        LocalStateSnapshot before = SnapshotLocalState(workspace.Root);

        ProcessResult result = await RunAsync(host, workspace,
            ["--report", report, "--open-report", "--capture", capture, "--overwrite-capture"]);

        Assert.Equal(64, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("aliases protected input", result.StandardError, StringComparison.Ordinal);
        Assert.Contains(state, result.StandardError, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(state));
        Assert.Equal(original, File.ReadAllBytes(capture));
        AssertLocalStateUnchanged(before, workspace.Root);
    }

    /// <summary>Host-consumed local inputs remain protected before GUI startup, including physical aliases.</summary>
    [Theory]
    [InlineData("--update-source-registry-path", "same")]
    [InlineData("--update-source-registry-path", "relative")]
    [InlineData("--update-source-registry-path", "case")]
    [InlineData("--update-source-registry-path", "hard-link")]
    public async Task HostLocalInputAliasesExit64WithoutChangingInput(string option, string spelling)
    {
        using var workspace = TempWorkspace.Create("desktop-host-input-alias");
        string host = CopyBuiltDesktop(workspace);
        byte[] original = [4, 3, 2, 1];
        string input = workspace.Write("input.png", original);
        string capture = spelling switch
        {
            "relative" => Path.GetRelativePath(host, input),
            "case" when OperatingSystem.IsWindows() => input.ToUpperInvariant(),
            "hard-link" => workspace.PathFor("alias.png"),
            _ => input,
        };
        if (spelling == "hard-link")
        {
            bool linked = OperatingSystem.IsWindows()
                ? WindowsCreateInputHardLink(capture, input, 0) != 0
                : UnixCreateInputHardLink(Encoding.UTF8.GetBytes(input + '\0'), Encoding.UTF8.GetBytes(capture + '\0')) == 0;
            Assert.True(linked, new Win32Exception(Marshal.GetLastPInvokeError()).Message);
        }
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(SavedReport));
        string[] before = [.. Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

        ProcessResult result = await RunAsync(host, workspace,
            [option, input, "--report", report, "--open-report", "--capture", capture, "--overwrite-capture"]);

        Assert.Equal(64, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("aliases protected input", result.StandardError, StringComparison.Ordinal);
        Assert.Contains(input, result.StandardError, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Equal(original, File.ReadAllBytes(Path.GetFullPath(capture, host)));
        Assert.Equal(before, Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray());
        Assert.False(Directory.Exists(workspace.PathFor("local-state")));
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WindowsCreateInputHardLink(string fileName, string existingFileName, nint securityAttributes);

    [DllImport("libc", EntryPoint = "link", SetLastError = true, ExactSpelling = true)]
    private static extern int UnixCreateInputHardLink(byte[] existingFileName, byte[] fileName);
#pragma warning restore SYSLIB1054
}
