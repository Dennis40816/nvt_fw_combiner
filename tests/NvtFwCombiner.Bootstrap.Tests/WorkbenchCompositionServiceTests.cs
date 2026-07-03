using System.Text.Json;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench facade tests for report generation around gated workflows.</summary>
public sealed class WorkbenchCompositionServiceTests
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

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
            Assert.Equal("replace.general.profile-pending", issue.GetProperty("Code").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
