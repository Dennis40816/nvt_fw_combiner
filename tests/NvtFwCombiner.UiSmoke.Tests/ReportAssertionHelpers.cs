using System.Text.Json;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class ReportAssertionHelpers
{
    internal static IEnumerable<ReportLineViewModel> GetCommandOperations(ReportReviewViewModel report)
    {
        return report.Operations.Where(operation => operation.HasCodeBlock || operation.HasRuntimeCommands);
    }

    internal static void AssertAcceptedPostbuildOnlyOutputDifferences(
        JsonElement root,
        string expectedOperationId)
    {
        AssertNoUnexpectedOutputDifferenceIssue(root);
        JsonElement[] differences = [.. root.GetProperty("OutputDifferences").EnumerateArray()];
        Assert.NotEmpty(differences);
        Assert.All(differences, difference =>
        {
            Assert.Equal(OutputDifferenceClassifications.PostbuildCrcHeader, difference.GetProperty("Classification").GetString());
            Assert.True(difference.GetProperty("IsAccepted").GetBoolean());
            Assert.Contains(
                expectedOperationId,
                difference.GetProperty("Evidence").GetString(),
                StringComparison.Ordinal);
            Assert.True(difference.GetProperty("ChangedByteCount").GetInt64() > 0);
            Assert.True(difference.GetProperty("Range").GetProperty("Length").GetInt64() > 0);
        });
    }

    internal static void AssertNoUnexpectedOutputDifferenceIssue(JsonElement root)
    {
        Assert.DoesNotContain(root.GetProperty("Issues").EnumerateArray(), issue =>
            issue.GetProperty("Code").GetString() == ReportIssueCodes.UnexpectedOutputDifference);
    }
}
