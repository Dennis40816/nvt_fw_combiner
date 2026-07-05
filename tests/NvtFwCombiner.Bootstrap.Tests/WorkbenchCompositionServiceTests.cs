using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench facade tests for report generation around gated workflows.</summary>
public sealed class WorkbenchCompositionServiceTests
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const int FirmwareVersionBarOffset = 0x001;

    /// <summary>Verifies General Replace build writes a profile-approved DP explicit mapping.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWritesDpExplicitMapping()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-workbench-general-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            byte[] baseBytes = CreatePattern(0x40000, 0x20);
            byte[] replacementBytes = [0xA5, 0x5A];
            string basePath = Path.Combine(tempRoot, "base.bin");
            string replacementPath = Path.Combine(tempRoot, "replacement.bin");
            string outputPath = Path.Combine(tempRoot, "out.bin");
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(replacementPath, replacementBytes);
            Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
            };

            WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                "NT51950",
                "single",
                "General",
                slotPaths,
                [new WorkbenchGeneralReplaceMappingInput("general-map-1", replacementPath, "0x00100", "0x00101")],
                build: true,
                TestContext.Current.CancellationToken,
                outputPath);

            Assert.True(result.Succeeded, result.ReportJson);
            byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Equal(baseBytes.Length, output.Length);
            Assert.Equal(0xA5, output[0x100]);
            Assert.Equal(0x5A, output[0x101]);
            Assert.Equal(baseBytes[0x102], output[0x102]);

            using var document = JsonDocument.Parse(result.ReportJson);
            JsonElement operation = Assert.Single(document.RootElement.GetProperty("Operations").EnumerateArray());
            Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString());
            Assert.Equal("general-map-1", operation.GetProperty("OperationId").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies General Replace runs postbuild when an explicit mapping touches TP/CtrlRAM.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRunsPostbuildForTpRange()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-workbench-general-tp-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = GoldenPath("expected/51950/dp-256k/flash.bin");
            string replacementPath = Path.Combine(tempRoot, "replacement.bin");
            byte[] baseBytes = File.ReadAllBytes(basePath);
            File.WriteAllBytes(replacementPath, baseBytes[0x22C00..0x22C02]);
            Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
            };

            WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                "NT51950",
                "single",
                "General",
                slotPaths,
                [new WorkbenchGeneralReplaceMappingInput("general-map-1", replacementPath, "0x22C00", "0x22C01")],
                build: false,
                TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded, result.ReportJson);
            using var document = JsonDocument.Parse(result.ReportJson);
            Assert.Empty(document.RootElement.GetProperty("Issues").EnumerateArray());
            Assert.Collection(
                document.RootElement.GetProperty("Operations").EnumerateArray(),
                operation => Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString()),
                operation =>
                {
                    Assert.Equal("RunExternalProcessor", operation.GetProperty("Kind").GetString());
                    Assert.Equal("nfc.nt51950.ctrlram-postbuild-v1", operation.GetProperty("ProcessorId").GetString());
                    Assert.Equal("legacy-combiner-1.13.0", operation.GetProperty("ToolBindingId").GetString());
                });
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies versioned CtrlRAM postbuild fails closed when FWConfig FW/bar is invalid.</summary>
    [Fact]
    public async Task CtrlRamReplaceRejectsInvalidFwVersionBarBeforePostbuildCategorySelection()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-workbench-invalid-fwbar-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            byte[] baseBytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
            Assert.True(TpFlashMapCatalog.TryGetFirmwareConfigStart("NT51926", out long firmwareConfigStart));
            baseBytes[checked((int)firmwareConfigStart + FirmwareVersionBarOffset)] ^= 0x01;

            string basePath = Path.Combine(tempRoot, "base-invalid-fwbar.bin");
            string replacementPath = Path.Combine(tempRoot, "normal.bin");
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(replacementPath, baseBytes[0x22800..0x25400]);

            Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
                ["replace-ctrlram-normal"] = replacementPath,
            };

            WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                "NT51926",
                "single",
                "CtrlRAM",
                slotPaths,
                build: false,
                TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            using var document = JsonDocument.Parse(result.ReportJson);
            Assert.Contains(
                document.RootElement.GetProperty("Issues").EnumerateArray(),
                issue => issue.GetProperty("Code").GetString() == "replace.ctrlram.postbuild-category-unknown");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies gated Replace reports can summarize missing inputs without throwing.</summary>
    [Fact]
    public async Task GeneralReplacePlanningReportIncludesMissingInputSummary()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-workbench-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string missingBase = Path.Combine(tempRoot, "missing-base.bin");
            Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
            {
                ["replace-base"] = missingBase,
            };

            WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                "NT51927",
                "single",
                "General",
                slotPaths,
                build: false,
                CancellationToken.None);

            Assert.False(result.Succeeded);
            using var document = JsonDocument.Parse(result.ReportJson);
            JsonElement input = Assert.Single(document.RootElement.GetProperty("Inputs").EnumerateArray());
            Assert.Equal("replace-base", input.GetProperty("AddressSpaceId").GetString());
            Assert.Equal("missing-base.bin", input.GetProperty("ArtifactId").GetString());
            Assert.Equal(0, input.GetProperty("Size").GetInt64());
            Assert.Equal(EmptySha256, input.GetProperty("Sha256").GetString());

            JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
            Assert.Equal("input.artifact.read-failed", issue.GetProperty("Code").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + index));
        }

        return bytes;
    }

    private static string GoldenPath(string relativePath)
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "SPEC.md")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
