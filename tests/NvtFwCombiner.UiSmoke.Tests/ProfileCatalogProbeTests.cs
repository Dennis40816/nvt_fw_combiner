using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Executes the shipped host with redirected streams and traps ordinary startup side effects.</summary>
public sealed class ProfileCatalogProbeTests
{
    /// <summary>Catalog-only launch neither resolves user state nor creates trace/UI/configuration output.</summary>
    [Theory]
    [InlineData("intact")]
    [InlineData("index")]
    [InlineData("missing-pack")]
    public async Task ShippedProbeIsReadOnlyAndReportsActualCatalogPublication(string variant)
    {
        using var workspace = TempWorkspace.Create("profile-catalog-probe");
        string host = CopyHost(workspace);
        string config = Path.Combine(host, "NvtFwCombiner.Desktop.runtimeconfig.json");
        JsonNode runtime = JsonNode.Parse(File.ReadAllBytes(config))!;
        runtime["runtimeOptions"]!["configProperties"]!["NvtFwCombiner.LocalState.CurrentUserFolderForbidden"] = true;
        File.WriteAllText(config, runtime.ToJsonString());
        bool damaged = variant == "index";
        if (damaged)
        {
            File.WriteAllText(Path.Combine(host, "profiles", "built-in", "package-trust-index.json"), "{}");
        }
        if (variant == "missing-pack") { File.Delete(Path.Combine(host, "profiles", "built-in", "prebuilt-profile-catalog.pack")); }
        Dictionary<string, string> before = Inventory(workspace.Root);
        (int exit, string output, string error) = await RunAsync(host, workspace, "--profile-catalog-probe-v1");
        Assert.Equal(string.Empty, error);
        string line = Assert.Single(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using JsonDocument json = JsonDocument.Parse(line);
        Assert.Equal(["schemaVersion", "admissionSource", "rejectionCode", "catalogLoaded"],
            json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("nfc-profile-catalog-probe-v1", json.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(!damaged, json.RootElement.GetProperty("catalogLoaded").GetBoolean());
        string? source = json.RootElement.GetProperty("admissionSource").GetString();
        Assert.Equal(damaged ? 1 : 0, exit);
        Assert.Equal(damaged ? null : variant == "missing-pack" ? "json" : "prebuilt", source);
        Assert.Equal(variant == "missing-pack" ? "missing" : null, json.RootElement.GetProperty("rejectionCode").GetString());
        Assert.Equal(before.OrderBy(p => p.Key, StringComparer.Ordinal), Inventory(workspace.Root).OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    /// <summary>Malformed probes exit before managed options, and the existing runtime-trust probe keeps precedence.</summary>
    [Theory]
    [InlineData("--profile-catalog-probe-v1", "--profile-catalog-probe-v1")]
    [InlineData("--profile-catalog-probe-v1", "--managed-root")]
    [InlineData("--managed-root", "--profile-catalog-probe-v1")]
    [InlineData("--internal-runtime-trust-v1", "--profile-catalog-probe-v1")]
    public async Task ShippedProbeRejectsExtraArgumentsBeforeOrdinaryStartup(string first, string second)
    {
        using var workspace = TempWorkspace.Create("profile-catalog-probe-arguments");
        string host = CopyHost(workspace);
        (int exit, string output, string error) = await RunAsync(host, workspace, first, second);
        Assert.Equal(2, exit);
        Assert.Equal(string.Empty, output);
        Assert.Equal(string.Empty, error);
    }

    private static string CopyHost(TempWorkspace workspace)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "catalog-probe-host");
        Assert.True(Directory.Exists(source), "The shipped Desktop probe host must be built and copied.");
        string destination = workspace.PathFor("host");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return destination;
    }

    private static Dictionary<string, string> Inventory(string root)
    {
        Dictionary<string, string> result = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            p => Path.GetRelativePath(root, p), p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))), StringComparer.Ordinal);
        foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            result.Add(Path.GetRelativePath(root, directory) + "/", string.Empty);
        }
        return result;
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(string host, TempWorkspace workspace, params string[] arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(host, OperatingSystem.IsWindows() ? "NvtFwCombiner.Desktop.exe" : "NvtFwCombiner.Desktop"),
            WorkingDirectory = host,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["NFC_STARTUP_TRACE_PATH"] = workspace.PathFor("forbidden-trace.json");
        start.Environment["LOCALAPPDATA"] = workspace.PathFor("forbidden-user-state");
        start.Environment["APPDATA"] = workspace.PathFor("forbidden-roaming-state");
        start.Environment["HOME"] = workspace.PathFor("forbidden-home");
        using var networkTrap = new TcpListener(IPAddress.Loopback, 0);
        networkTrap.Start();
        string proxy = "http://127.0.0.1:" + ((IPEndPoint)networkTrap.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["HTTP_PROXY"] = proxy;
        start.Environment["HTTPS_PROXY"] = proxy;
        start.Environment["ALL_PROXY"] = proxy;
        start.Environment["NO_PROXY"] = string.Empty;
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(IntPtr.Zero, process.MainWindowHandle);
            Assert.False(networkTrap.Pending(), "The headless catalog probe attempted a network request.");
            return (process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
