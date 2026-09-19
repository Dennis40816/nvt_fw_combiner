using System.Security.Cryptography;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Exact user runtime bytes are deployed beside exact tool bytes without changing their sources.</summary>
public sealed class ExternalRuntimeDeploymentTests
{
    /// <summary>User bytes are copied into one private deployment and removed with its lease.</summary>
    [Fact]
    public async Task UserSelectionCreatesAndCleansPrivateDeployment()
    {
        using TempWorkspace workspace = TempWorkspace.Create("runtime-deployment");
        byte[] executable = [0x4D, 0x5A, 0x01];
        byte[] runtime = [0x4D, 0x5A, 0x02];
        string executablePath = workspace.Write("source/Combiner.exe", executable);
        string runtimePath = workspace.Write("source/vcruntime140.dll", runtime);
        ToolchainRuntimeConfigurationSnapshot snapshot = UserSnapshot(runtimePath, runtime);
        var deployment = new ExternalRuntimeDeployment(snapshot, new LocalFileStore(), workspace.PathFor("deployments"));

        string deployedPath;
        using (ExternalRuntimeDeploymentResult result = await deployment.PrepareAsync(
            executablePath, Hash(executable), TestContext.Current.CancellationToken))
        {
            Assert.Null(result.Issue);
            deployedPath = Assert.IsType<string>(result.ExecutablePath);
            Assert.Equal(executable, File.ReadAllBytes(deployedPath));
            Assert.Equal(runtime, File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(deployedPath)!, "vcruntime140.dll")));
            Assert.NotEqual(Path.GetDirectoryName(executablePath), Path.GetDirectoryName(deployedPath));
        }
        Assert.False(Directory.Exists(Path.GetDirectoryName(deployedPath)));
        Assert.Equal(executable, File.ReadAllBytes(executablePath));
        Assert.Equal(runtime, File.ReadAllBytes(runtimePath));
    }

    /// <summary>A changed selected runtime is rejected before any deployment directory is created.</summary>
    [Fact]
    public async Task ChangedRuntimeRejectsBeforeDeployment()
    {
        using TempWorkspace workspace = TempWorkspace.Create("runtime-deployment-changed");
        byte[] executable = [0x4D, 0x5A, 0x01];
        byte[] runtime = [0x4D, 0x5A, 0x02];
        string executablePath = workspace.Write("source/Combiner.exe", executable);
        string runtimePath = workspace.Write("source/vcruntime140.dll", runtime);
        ToolchainRuntimeConfigurationSnapshot snapshot = UserSnapshot(runtimePath, runtime);
        await File.WriteAllBytesAsync(runtimePath, [0xFF], TestContext.Current.CancellationToken);
        var deployment = new ExternalRuntimeDeployment(snapshot, new LocalFileStore(), workspace.PathFor("deployments"));

        using ExternalRuntimeDeploymentResult result = await deployment.PrepareAsync(
            executablePath, Hash(executable), TestContext.Current.CancellationToken);

        Assert.Equal("toolchain-runtime.identity.changed", result.Issue?.Code);
        Assert.Null(result.ExecutablePath);
        Assert.False(Directory.Exists(workspace.PathFor("deployments")));
    }

    private static ToolchainRuntimeConfigurationSnapshot UserSnapshot(string path, byte[] runtime)
    {
        var selection = new ToolchainRuntimeSelection(ToolchainRuntimeSource.User, path, Hash(runtime));
        return new(4, ToolchainRuntimeConfigurationStatus.Current, selection, selection, []);
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
