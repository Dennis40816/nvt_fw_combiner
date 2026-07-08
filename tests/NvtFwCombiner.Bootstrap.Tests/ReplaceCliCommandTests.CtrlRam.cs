using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Verifies CtrlRAM Replace preview reports truncation warnings while succeeding.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsOversizedInputTruncation()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string ctrlram = workspace.Write("ctrlram.bin", [0xAA, 0xBB, 0xCC, 0xDD]);
        string report = workspace.PathFor("ctrlram-warning-report.json");

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "synthetic-ctrlram-replace",
            "--ic-family",
            "NT51",
            "--ic-num",
            "932",
            "--base",
            reference,
            "--ctrlram",
            ctrlram,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-ctrlram", result.Output, StringComparison.Ordinal);
        Assert.Contains(CompositionIssueCodes.InputAddressSpaceTruncated, result.Error, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceTruncated, issue.GetProperty("Code").GetString());
        Assert.Equal("warning", issue.GetProperty("Severity").GetString());
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
}
