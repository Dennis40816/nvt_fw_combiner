using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    private static void OpenReplace(MainWindowViewModel viewModel, string mode)
    {
        viewModel.Replace.SelectedReplaceMode = mode;
        viewModel.ShowReplaceCommand.Execute(null);
    }

    private static IEnumerable<ReportLineViewModel> GetCommandOperations(ReportReviewViewModel report)
    {
        return report.Operations.Where(operation => operation.HasCodeBlock || operation.HasRuntimeCommands);
    }

    private static void AssertAcceptedPostbuildOnlyOutputDifferences(
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

    private static void AssertNoUnexpectedOutputDifferenceIssue(JsonElement root)
    {
        Assert.DoesNotContain(root.GetProperty("Issues").EnumerateArray(), issue =>
            issue.GetProperty("Code").GetString() == ReportIssueCodes.UnexpectedOutputDifference);
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + (index % 251)));
        }

        return bytes;
    }

    private static (int Start, int Length) ParseCtrlRamRegion(CtrlRamRegionViewModel region)
    {
        string startHex = region.StartAddress.Split('-', StringSplitOptions.TrimEntries)[0][2..];
        string lengthHex = region.SizeHex["len 0x".Length..];
        return (
            int.Parse(startHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(lengthHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static void AssertIconGeometry(FirmwareSlotViewModel slot)
    {
        Assert.True(HasDrawableIcon(slot));
    }

    private static bool HasDrawableIcon(FirmwareSlotViewModel slot)
    {
        return slot.SlotIconPathData.StartsWith('M') &&
            slot.SlotIconPathData.Contains('L');
    }

}
