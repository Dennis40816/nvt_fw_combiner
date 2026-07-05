using System.Text.Json;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench facade tests for report generation around gated workflows.</summary>
public sealed class WorkbenchCompositionServiceTests
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

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

    /// <summary>Verifies General Replace refuses TP/CtrlRAM mappings until approved postbuild refresh is wired.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRejectsTpRangeWithoutPostbuild()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-workbench-general-tp-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string replacementPath = Path.Combine(tempRoot, "replacement.bin");
            File.WriteAllBytes(basePath, CreatePattern(0x40000, 0x30));
            File.WriteAllBytes(replacementPath, [0xA5, 0x5A]);
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

            Assert.False(result.Succeeded);
            using var document = JsonDocument.Parse(result.ReportJson);
            JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
            Assert.Equal("profile.explicit-mapping.tp-processor-required", issue.GetProperty("Code").GetString());
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
}
