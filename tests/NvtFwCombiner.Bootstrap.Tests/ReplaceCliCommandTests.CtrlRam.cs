using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Locks NT51926 CtrlRAM Replace admission and postbuild when TP FW is the base image.</summary>
    [Fact]
    public async Task Nt51926CtrlRamReplaceAcceptsTpFirmwareBase()
    {
        using var workspace = TempWorkspace.Create();
        string basePath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "inputs",
            "51926",
            "tp.bin");
        string vnPath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "inputs",
            "nt51926-cascade-self-20260705",
            "vn.bin");
        string previewReport = workspace.PathFor("preview-report.json");

        CliRunResult preview = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "NT51926",
            "--ic-num",
            "cascade",
            "--base",
            basePath,
            "--ctrlram",
            $"replace-ctrlram-vn={vnPath}",
            "--report",
            previewReport,
        ]);

        Assert.Equal(0, preview.ExitCode);
        Assert.Contains("Status: Succeeded", preview.Output, StringComparison.Ordinal);
        Assert.Contains("Size: 245760 bytes", preview.Output, StringComparison.Ordinal);
        Assert.Contains("postbuild-cascade", preview.Output, StringComparison.Ordinal);
        Assert.Contains("changed=16", preview.Output, StringComparison.Ordinal);
        AssertProcessorTrace(previewReport);

        string outputPath = workspace.PathFor("nt51926-tp-base.bin");
        string buildReport = workspace.PathFor("build-report.json");
        CliRunResult build = await RunCliAsync([
            "ctrlram-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "cascade",
            "--base",
            basePath,
            "--ctrlram",
            $"replace-ctrlram-vn={vnPath}",
            "--output",
            outputPath,
            "--report",
            buildReport,
        ]);

        Assert.Equal(0, build.ExitCode);
        Assert.Contains("Status: Succeeded", build.Output, StringComparison.Ordinal);
        Assert.Contains("Size: 245760 bytes", build.Output, StringComparison.Ordinal);
        Assert.Contains("changed=16", build.Output, StringComparison.Ordinal);
        Assert.Equal(0x3C000, new FileInfo(outputPath).Length);
        Assert.Equal(
            16,
            File.ReadAllBytes(basePath)
                .Zip(File.ReadAllBytes(outputPath), (before, after) => before != after)
                .Count(changed => changed));
        AssertProcessorTrace(buildReport);
    }

    /// <summary>Verifies CtrlRAM base validation does not describe the input as Flash Code only.</summary>
    [Fact]
    public async Task CtrlRamReplaceUsesGenericBaseFirmwareDiagnostic()
    {
        using var workspace = TempWorkspace.Create();
        string vnPath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "inputs",
            "nt51926-cascade-self-20260705",
            "vn.bin");

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "NT51926",
            "--ic-num",
            "cascade",
            "--base",
            workspace.PathFor("missing-base.bin"),
            "--ctrlram",
            $"replace-ctrlram-vn={vnPath}",
        ]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Base firmware BIN path does not exist.", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Base flash", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies real IC CtrlRAM Replace accepts multiple slot-specific replacement inputs in one CLI run.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsRepeatedWorkbenchSlotInputs()
    {
        using var workspace = TempWorkspace.Create();
        string fixtureRoot = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json")));
        JsonElement fixtureCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("id").GetString() == "nt51927-2chip-self-20260705");
        string basePath = ManifestPath(fixtureRoot, fixtureCase.GetProperty("base").GetProperty("path"));
        JsonElement normalMaster = fixtureCase.GetProperty("replacementInputs")
            .EnumerateArray()
            .Single(input => input.GetProperty("slotId").GetString() == "replace-ctrlram-normal-master");
        JsonElement vnSlaveRight = fixtureCase.GetProperty("replacementInputs")
            .EnumerateArray()
            .Single(input => input.GetProperty("slotId").GetString() == "replace-ctrlram-vn-slave-r");
        string normalMasterPath = ManifestPath(fixtureRoot, normalMaster.GetProperty("file").GetProperty("path"));
        string vnSlaveRightPath = ManifestPath(fixtureRoot, vnSlaveRight.GetProperty("file").GetProperty("path"));
        string report = workspace.PathFor("ctrlram-report.json");

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "NT51927",
            "--ic-num",
            "2",
            "--base",
            basePath,
            "--ctrlram",
            $"replace-ctrlram-normal-master={normalMasterPath}",
            "--ctrlram",
            $"vn-slave-r={vnSlaveRightPath}",
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Profile: nt51927-ctrlram-replace-workbench (NT51927)", result.Output, StringComparison.Ordinal);
        Assert.Contains("postbuild-twochip", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(report), report);
        using var reportDocument = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = reportDocument.RootElement;
        Assert.Equal("nt51927-ctrlram-replace-workbench", root.GetProperty("ProfileId").GetString());
        Assert.Equal(3, root.GetProperty("Inputs").GetArrayLength());
        Assert.Contains(root.GetProperty("Inputs").EnumerateArray(), input =>
            input.GetProperty("AddressSpaceId").GetString() == "replace-ctrlram-normal-master");
        Assert.Contains(root.GetProperty("Inputs").EnumerateArray(), input =>
            input.GetProperty("AddressSpaceId").GetString() == "replace-ctrlram-vn-slave-r");
        JsonElement operation = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("RunExternalProcessor", operation.GetProperty("Kind").GetString());
    }

    private static void AssertProcessorTrace(string reportPath)
    {
        using var reportDocument = JsonDocument.Parse(File.ReadAllText(reportPath));
        JsonElement operation = Assert.Single(reportDocument.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal("postbuild-cascade", operation.GetProperty("OperationId").GetString());
        Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", operation.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", operation.GetProperty("ToolBindingId").GetString());
        Assert.Equal(2, operation.GetProperty("ExecutedCommands").GetArrayLength());

        JsonElement mutation = Assert.Single(reportDocument.RootElement.GetProperty("Mutations").EnumerateArray());
        Assert.Equal(16, mutation.GetProperty("ChangedByteCount").GetInt64());
        Assert.Equal(0x3C000, mutation.GetProperty("TargetRange").GetProperty("Length").GetInt64());
    }
}
