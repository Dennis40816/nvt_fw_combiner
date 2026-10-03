using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DesktopCaptureTests
{
    /// <summary>The executable protects state and configured local registries, excluding remote/default replicas.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostProtectedInputsContainOnlyCaptureInputs(bool registryOverrideSupplied)
    {
        string[] registries = [@"G:\unreachable\registry.json", "registry.json", "file:///registry.json", "https://example.invalid/registry.json"];
        string[] expected = registryOverrideSupplied ? ["state.json", .. registries.Take(3)] : ["state.json"];

        Assert.Equal(expected, CaptureHostInputs(["--capture=out.png"], "state.json", registryOverrideSupplied, registries));
    }

    /// <summary>Help and ordinary requests never resolve the default state path forbidden in test processes.</summary>
    [Theory]
    [InlineData("--help")]
    [InlineData("--help=value")]
    [InlineData("--page=home")]
    public void NonCaptureHostInputsDoNotResolveDefaultState(string argument)
    {
        Assert.Empty(CaptureHostInputs([argument], null, true, ["registry.json"]));
    }

    /// <summary>The executable's explicit state input refuses every alias before composition or capture writes.</summary>
    [Theory]
    [InlineData("same")]
    [InlineData("relative")]
    [InlineData("case")]
    [InlineData("hard-link")]
    public void ExplicitManagedStateAliasesExit64WithoutChangingState(string spelling)
    {
        using var workspace = TempWorkspace.Create("capture-managed-state-alias");
        string state = workspace.Write("state.png", [4, 3, 2, 1]);
        string capture = spelling switch
        {
            "relative" => Path.GetRelativePath(Environment.CurrentDirectory, state),
            "case" when OperatingSystem.IsWindows() => state.ToUpperInvariant(),
            "hard-link" => workspace.PathFor("alias.png"),
            _ => state,
        };
        if (spelling == "hard-link")
        {
            bool linked = OperatingSystem.IsWindows() ? CreateHardLink(capture, state, IntPtr.Zero)
                : CreateUnixHardLink(UnixPath(state), UnixPath(capture)) == 0;
            Assert.True(linked, new Win32Exception(Marshal.GetLastPInvokeError()).Message);
        }
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        string[] arguments = ["--report", report, "--open-report", "--capture", capture, "--overwrite-capture"];
        string[] before = [.. Directory.GetFiles(workspace.Root).Order(StringComparer.Ordinal)];
        UiLaunchOptions options = UiLaunchOptions.Parse(arguments, new NvtFwCombiner.Infrastructure.Files.LocalFileStore(),
            CaptureHostInputs(arguments, state, false, []));
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(64, DesktopApplication.CompletePublicRequest(options, output, error));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("aliases protected input", error.ToString(), StringComparison.Ordinal);
        Assert.Contains(state, error.ToString(), StringComparison.Ordinal);
        Assert.Equal([4, 3, 2, 1], File.ReadAllBytes(state));
        Assert.Equal([4, 3, 2, 1], File.ReadAllBytes(Path.GetFullPath(capture)));
        Assert.Equal(before, Directory.GetFiles(workspace.Root).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>A missing explicit state remains absent when the same PNG destination is requested.</summary>
    [Fact]
    public void MissingManagedStateCaptureExits64WithoutCreatingState()
    {
        using var workspace = TempWorkspace.Create("capture-missing-managed-state");
        string state = workspace.PathFor("state.png");
        string[] arguments = ["--report=report.json", "--open-report", "--capture", state, "--overwrite-capture"];
        UiLaunchOptions options = UiLaunchOptions.Parse(arguments, new NvtFwCombiner.Infrastructure.Files.LocalFileStore(),
            CaptureHostInputs(arguments, state, false, []));
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(64, DesktopApplication.CompletePublicRequest(options, output, error));
        Assert.Contains("aliases protected input", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(workspace.Root));
    }

    /// <summary>Unreachable default replicas are never probed while an unrelated PNG request is validated.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostInputsPermitUnrelatedCaptureWithoutProbingDefaultReplicas(bool registryOverrideSupplied)
    {
        using var workspace = TempWorkspace.Create("capture-distinct-host-inputs");
        string state = workspace.Write("state.json", [4, 3, 2, 1]);
        string registry = workspace.Write("registry.json", [8, 7, 6, 5]);
        string unreachable = workspace.PathFor("unreachable/registry.json");
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        string capture = workspace.Write("capture.png", [1, 2, 3]);
        string[] arguments = ["--report", report, "--open-report", "--capture", capture, "--overwrite-capture"];
        var files = new PendingReportFiles(new NvtFwCombiner.Infrastructure.Files.LocalFileStore(), report, passThrough: true)
        {
            UnreachableIdentityPath = unreachable,
        };
        UiLaunchOptions options = UiLaunchOptions.Parse(arguments, files,
            CaptureHostInputs(arguments, state, registryOverrideSupplied, [registryOverrideSupplied ? registry : unreachable]));
        Assert.Empty(options.Issues);
    }

    private static IReadOnlyList<string> CaptureHostInputs(string[] arguments, string? state,
        bool registryOverrideSupplied, IReadOnlyList<string> registries)
    {
        // The existing build-only Desktop reference supplies the artifact without adding a project dependency.
        Assembly desktop = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "cph", "NvtFwCombiner.Desktop.dll"));
        MethodInfo method = desktop.GetType("NvtFwCombiner.Desktop.Program", throwOnError: true)!
            .GetMethod("GetCaptureProtectedInputs", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IReadOnlyList<string>)method.Invoke(null, [arguments, state, registryOverrideSupplied, registries])!;
    }
}
