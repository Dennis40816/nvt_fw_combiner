using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private const string Nt51926TpBaseCaseId = "nt51926-cascade-tp-base-self-regression-20260717";

    /// <summary>Verifies malformed CtrlRAM arguments demonstrate a currently accepted physical slot id.</summary>
    [Fact]
    public async Task CtrlRamReplaceMalformedSlotUsesPhysicalVnExample()
    {
        string basePath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "base",
            "nt51926-2ic-csot-toyota-d02t06-jira0597-20260622.bin");

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "NT51926",
            "--ic-num",
            "cascade",
            "--base",
            basePath,
            "--ctrlram",
            "malformed",
        ]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--ctrlram replace-ctrlram-vn=C:\\path\\vn.bin", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("vn-master", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Locks NT51926 CtrlRAM Replace admission and postbuild when TP FW is the base image.</summary>
    [Fact]
    public async Task Nt51926CtrlRamReplaceAcceptsTpFirmwareBase()
    {
        using var workspace = TempWorkspace.Create();
        string fixtureRoot = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json")));
        JsonElement fixtureCase = manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == Nt51926TpBaseCaseId);
        string basePath = RepositoryPaths.ManifestPath(fixtureRoot, fixtureCase.GetProperty("base"));
        JsonElement replacement = Assert.Single(fixtureCase.GetProperty("replacementInputs").EnumerateArray());
        string vnPath = RepositoryPaths.ManifestPath(fixtureRoot, replacement.GetProperty("file"));
        string expectedPath = RepositoryPaths.ManifestPath(fixtureRoot, fixtureCase.GetProperty("expectedOutput"));
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

        Assert.True(
            preview.ExitCode == 0,
            $"stdout:{Environment.NewLine}{preview.Output}{Environment.NewLine}stderr:{Environment.NewLine}{preview.Error}");
        Assert.Contains("Status: Succeeded", preview.Output, StringComparison.Ordinal);
        Assert.Contains("Size: 245760 bytes", preview.Output, StringComparison.Ordinal);
        Assert.Contains("postbuild-cascade", preview.Output, StringComparison.Ordinal);
        Assert.Contains("changed=16", preview.Output, StringComparison.Ordinal);
        AssertProcessorTrace(previewReport, fixtureCase);

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
        byte[] baseBytes = File.ReadAllBytes(basePath);
        byte[] outputBytes = File.ReadAllBytes(outputPath);
        byte[] expectedBytes = File.ReadAllBytes(expectedPath);
        Assert.Equal(0x3C000, outputBytes.Length);
        Assert.Equal(expectedBytes, outputBytes);
        Assert.Equal(
            fixtureCase.GetProperty("expectedOutput").GetProperty("sha256").GetString(),
            Convert.ToHexString(SHA256.HashData(outputBytes)).ToLowerInvariant());
        AssertExactChangedRanges(baseBytes, outputBytes, ReadManifestRanges(fixtureCase));
        using (var report = JsonDocument.Parse(File.ReadAllText(buildReport)))
        {
            Assert.Equal(
                "nt51926-ctrlram-replace-fw141-runtime-cascade",
                report.RootElement.GetProperty("ProfileId").GetString());
            Assert.Contains(
                report.RootElement.GetProperty("Operations").EnumerateArray(),
                operation => operation.GetProperty("OperationId").GetString() == "replace-vn-00");
        }

        AssertProcessorTrace(buildReport, fixtureCase);
    }

    /// <summary>Locks one CtrlRAM Replace result across TP-only and full-Flash base containers.</summary>
    [Fact]
    public async Task Nt51926CtrlRamReplaceAcceptsTpAndFullFlashBasesWithTheSameTpResult()
    {
        using var workspace = TempWorkspace.Create();
        string fixtureRoot = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json")));
        JsonElement fixtureCase = manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "nt51926-cascade-self-20260705");
        string fullFlashPath = RepositoryPaths.ManifestPath(fixtureRoot, fixtureCase.GetProperty("base"));
        JsonElement replacement = fixtureCase.GetProperty("replacementInputs").EnumerateArray()
            .Single(item => item.GetProperty("slotId").GetString() == "replace-ctrlram-vn");
        string vnPath = RepositoryPaths.ManifestPath(fixtureRoot, replacement.GetProperty("file"));
        byte[] fullFlashBase = File.ReadAllBytes(fullFlashPath);
        Assert.Equal(0x40000, fullFlashBase.Length);
        string tpBasePath = workspace.Write("base-tp.bin", fullFlashBase[..0x3C000]);
        string tpOutputPath = workspace.PathFor("output-tp.bin");
        string fullFlashOutputPath = workspace.PathFor("output-flash.bin");

        WorkbenchRunResult tpRun = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            "cascade",
            WorkbenchReplaceModes.CtrlRam,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = tpBasePath,
                ["replace-ctrlram-vn"] = vnPath,
            },
            build: true,
            TestContext.Current.CancellationToken,
            tpOutputPath);
        WorkbenchRunResult fullFlashRun = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            "cascade",
            WorkbenchReplaceModes.CtrlRam,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = fullFlashPath,
                ["replace-ctrlram-vn"] = vnPath,
            },
            build: true,
            TestContext.Current.CancellationToken,
            fullFlashOutputPath);

        Assert.True(tpRun.Succeeded, tpRun.ReportJson);
        Assert.True(fullFlashRun.Succeeded, fullFlashRun.ReportJson);
        byte[] tpOutput = File.ReadAllBytes(tpOutputPath);
        byte[] fullFlashOutput = File.ReadAllBytes(fullFlashOutputPath);
        Assert.Equal(0x3C000, tpOutput.Length);
        Assert.Equal(0x40000, fullFlashOutput.Length);
        Assert.Equal(tpOutput, fullFlashOutput[..0x3C000]);
        Assert.Equal(fullFlashBase[0x3C000..], fullFlashOutput[0x3C000..]);
        Assert.Equal(fullFlashBase, File.ReadAllBytes(fullFlashPath));
    }

    /// <summary>Verifies CtrlRAM base validation does not describe the input as Flash Code only.</summary>
    [Fact]
    public async Task CtrlRamReplaceUsesGenericBaseFirmwareDiagnostic()
    {
        using var workspace = TempWorkspace.Create();
        string reportPath = workspace.PathFor("missing-base-report.json");
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
            "--report",
            reportPath,
        ]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Base firmware BIN path does not exist.", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Base flash", result.Error, StringComparison.Ordinal);
        string reportJson = await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("base flash", reportJson, StringComparison.OrdinalIgnoreCase);

        string basePath = CanonicalGoldenTestData.ArtifactPath(
            "standard-merge",
            "51926",
            "tp-input");
        WorkbenchRunResult planning = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            "cascade",
            "CtrlRAM",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
            },
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(planning.Succeeded);
        Assert.Contains("base firmware BIN for postbuild staging", planning.ReportJson, StringComparison.Ordinal);
        Assert.DoesNotContain("base flash", planning.ReportJson, StringComparison.OrdinalIgnoreCase);
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
        JsonElement vn = fixtureCase.GetProperty("replacementInputs")
            .EnumerateArray()
            .Single(input => input.GetProperty("slotId").GetString() == "replace-ctrlram-vn");
        string normalMasterPath = ManifestPath(fixtureRoot, normalMaster.GetProperty("file").GetProperty("path"));
        string vnPath = ManifestPath(fixtureRoot, vn.GetProperty("file").GetProperty("path"));
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
            $"vn={vnPath}",
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Profile: nt51927-ctrlram-replace-fw132-twochip (NT51927)", result.Output, StringComparison.Ordinal);
        Assert.Contains("postbuild-twochip", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(report), report);
        using var reportDocument = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = reportDocument.RootElement;
        Assert.Equal("nt51927-ctrlram-replace-fw132-twochip", root.GetProperty("ProfileId").GetString());
        Assert.Equal(3, root.GetProperty("Inputs").GetArrayLength());
        Assert.Contains(root.GetProperty("Inputs").EnumerateArray(), input =>
            input.GetProperty("AddressSpaceId").GetString() == "replace-ctrlram-normal-master");
        Assert.Contains(root.GetProperty("Inputs").EnumerateArray(), input =>
            input.GetProperty("AddressSpaceId").GetString() == "replace-ctrlram-vn");
        JsonElement[] operations = [.. root.GetProperty("Operations").EnumerateArray()];
        Assert.Equal(3, operations.Count(operation => operation.GetProperty("Kind").GetString() == "ReplaceRange"));
        JsonElement operation = Assert.Single(
            operations,
            operation => operation.GetProperty("Kind").GetString() == "RunExternalProcessor");
        Assert.Equal("RunExternalProcessor", operation.GetProperty("Kind").GetString());
    }

    private static void AssertProcessorTrace(string reportPath, JsonElement fixtureCase)
    {
        using var reportDocument = JsonDocument.Parse(File.ReadAllText(reportPath));
        JsonElement trace = fixtureCase.GetProperty("processorTrace");
        JsonElement operation = reportDocument.RootElement.GetProperty("Operations").EnumerateArray()
            .Single(candidate => candidate.TryGetProperty("ProcessorId", out JsonElement processorId) &&
                processorId.GetString() == trace.GetProperty("processorId").GetString());
        Assert.Equal("postbuild-cascade", operation.GetProperty("OperationId").GetString());
        Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
        Assert.Equal(trace.GetProperty("processorId").GetString(), operation.GetProperty("ProcessorId").GetString());
        Assert.Equal(trace.GetProperty("toolBindingId").GetString(), operation.GetProperty("ToolBindingId").GetString());
        JsonElement[] actualCommands = [.. operation.GetProperty("ExecutedCommands").EnumerateArray()];
        JsonElement[] expectedCommands = [.. trace.GetProperty("commands").EnumerateArray()];
        Assert.Equal(expectedCommands.Length, actualCommands.Length);

        string executablePath = RepositoryPaths.FromRepositoryRoot(
            "external-tools",
            "legacy-combiner",
            trace.GetProperty("toolVersion").GetString()!,
            trace.GetProperty("executableName").GetString()!);
        string stagingRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nvt-fw-combiner", "external-tools"));
        for (int index = 0; index < actualCommands.Length; index++)
        {
            string workingDirectory = Path.GetFullPath(actualCommands[index].GetProperty("WorkingDirectory").GetString()!);
            Assert.Equal(executablePath, actualCommands[index].GetProperty("ExecutablePath").GetString(), ignoreCase: true);
            Assert.Equal(stagingRoot, Path.GetDirectoryName(workingDirectory), ignoreCase: true);
            Assert.EndsWith(".postbuild-cascade", Path.GetFileName(workingDirectory), StringComparison.Ordinal);
            string[] arguments = [
                .. actualCommands[index].GetProperty("Arguments").EnumerateArray()
                    .Select(argument => argument.GetString()!)
                    .Select(argument => Path.IsPathRooted(argument)
                        ? Path.GetRelativePath(workingDirectory, argument).Replace('\\', '/')
                        : argument),
            ];
            Assert.DoesNotContain(arguments, argument => argument.StartsWith("../", StringComparison.Ordinal));
            Assert.Equal(
                expectedCommands[index].GetProperty("arguments").EnumerateArray().Select(argument => argument.GetString()),
                arguments);
        }
        Assert.Equal(
            trace.GetProperty("executableSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath))).ToLowerInvariant());

        JsonElement mutation = reportDocument.RootElement.GetProperty("Mutations").EnumerateArray()
            .Single(candidate => candidate.GetProperty("OperationId").GetString() == "postbuild-cascade");
        Assert.Equal(16, mutation.GetProperty("ChangedByteCount").GetInt64());
        Assert.Equal(0x3C000, mutation.GetProperty("TargetRange").GetProperty("Length").GetInt64());
        Assert.Equal(fixtureCase.GetProperty("base").GetProperty("sha256").GetString(), mutation.GetProperty("BeforeSha256").GetString());
        Assert.Equal(fixtureCase.GetProperty("expectedOutput").GetProperty("sha256").GetString(), mutation.GetProperty("AfterSha256").GetString());
    }

    private static (int Start, int EndExclusive)[] ReadManifestRanges(JsonElement fixtureCase)
    {
        return [
            .. fixtureCase.GetProperty("changedRanges").EnumerateArray().Select(range => (
                Convert.ToInt32(range.GetProperty("start").GetString()![2..], 16),
                Convert.ToInt32(range.GetProperty("endExclusive").GetString()![2..], 16))),
        ];
    }

    private static void AssertExactChangedRanges(
        byte[] before,
        byte[] after,
        (int Start, int EndExclusive)[] expectedRanges)
    {
        Assert.Equal(before.Length, after.Length);
        List<(int Start, int EndExclusive)> actualRanges = [];
        int offset = 0;
        while (offset < before.Length)
        {
            if (before[offset] == after[offset])
            {
                offset++;
                continue;
            }

            int start = offset;
            do
            {
                offset++;
            }
            while (offset < before.Length && before[offset] != after[offset]);

            actualRanges.Add((start, offset));
        }

        Assert.Equal(expectedRanges.Length, actualRanges.Count);
        for (int index = 0; index < expectedRanges.Length; index++)
        {
            Assert.Equal(expectedRanges[index], actualRanges[index]);
            Assert.Equal(4, actualRanges[index].EndExclusive - actualRanges[index].Start);
        }
    }
}
