using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Builds Standard Merge output to a user-selected path.</summary>
    public Task BuildStandardMergeAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunStandardMergeAsync(build: true, outputPath);
    }

    private void RefreshMergeSlotRequirements()
    {
        IReadOnlyList<string> required = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        _mergeDpSlot.IsOptional = !required.Contains("dp-input", StringComparer.Ordinal);
        _mergeTpSlot.IsOptional = !required.Contains("tp-input", StringComparer.Ordinal);
        _mergeLdSlot.IsOptional = !required.Contains("ld-input", StringComparer.Ordinal);
        MergeSlots.Clear();
        if (required.Contains("dp-input", StringComparer.Ordinal))
        {
            MergeSlots.Add(_mergeDpSlot);
        }

        if (required.Contains("tp-input", StringComparer.Ordinal))
        {
            MergeSlots.Add(_mergeTpSlot);
        }

        if (required.Contains("ld-input", StringComparer.Ordinal))
        {
            MergeSlots.Add(_mergeLdSlot);
        }
    }

    private void RefreshMergeModeState()
    {
        ActiveMergeRows.Clear();
        string? profileId = UiCompositionRunner.GetStandardMergeProfileId(SelectedIc);
        if (profileId is null)
        {
            AddMergeRows(
                $"Profile: not available for {SelectedIc}",
                "Preview and Build stay disabled until a profile is added.",
                $"{SelectedIc} / {SelectedNumber} still refreshes Replace region policy.");
            return;
        }

        AddMergeRows(
            $"Profile: {profileId}",
            $"Required slots: {GetRequiredStandardMergeSlotLabels()}",
            GetStandardMergeRangeSummary());
    }

    private void AddMergeRows(params string[] rows)
    {
        foreach (string row in rows)
        {
            ActiveMergeRows.Add(row);
        }
    }

    private string GetRequiredStandardMergeSlotLabels()
    {
        IReadOnlyList<string> required = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return required.Count == 0
            ? "none"
            : string.Join(", ", required.Select(AddressSpaceLabel));
    }

    private string GetStandardMergeRangeSummary()
    {
        return SelectedIc is "NT51950" or "NT51951"
            ? "TP paste range: 0x0A000-0x36FFF (len 0x2D000); 0x37000-0x37FFF (len 0x1000) is preserved customer information."
            : "Address ranges come from the built-in Standard Merge profile.";
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "DP",
            "tp-input" => "TP",
            "ld-input" => "LD",
            _ => addressSpaceId,
        };
    }

    private bool CanRunStandardMerge()
    {
        IReadOnlyList<string> requiredAddressSpaces = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return IsNormalMergeModeSelected && requiredAddressSpaces.Count > 0 && requiredAddressSpaces.All(addressSpace =>
            MergeSlotForAddressSpace(addressSpace) is { HasFile: true });
    }

    private async Task RunStandardMergeAsync(bool build)
    {
        await RunStandardMergeAsync(build, outputPath: null);
    }

    private async Task RunStandardMergeAsync(bool build, string? outputPath)
    {
        string previewToken = CreateStandardMergePreviewToken();
        if (build && !HasCurrentStandardMergePreview())
        {
            BlockStandardMergeBuildUntilPreview();
            return;
        }

        try
        {
            WorkbenchRunResult result = await UiCompositionRunner.RunStandardMergeAsync(
                SelectedIc,
                CreateStandardMergeSlotPaths(),
                build,
                CancellationToken.None,
                outputPath);
            ApplyRunResult(result, build);
            CompleteStandardMergeRun(build, result.Succeeded, previewToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            CompleteStandardMergeRun(build, false, previewToken);
            string action = build ? "Build" : "Preview";
            LastRunResult = new UiRunResultViewModel(
                $"{action} failed",
                exception.Message,
                "No output",
                succeeded: false);
            OnPropertyChanged(nameof(LastRunResult));
            LoadRunErrorReport(
                action,
                UiCompositionRunner.GetStandardMergeProfileId(SelectedIc) ?? "standard-merge",
                SelectedIc,
                SelectedNumber,
                exception.Message,
                CreateStandardMergeSlotPaths());
        }
    }

    private Dictionary<string, string> CreateStandardMergeSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        AddPath(paths, "dp-input", _mergeDpSlot);
        AddPath(paths, "tp-input", _mergeTpSlot);
        AddPath(paths, "ld-input", _mergeLdSlot);
        return paths;
    }

    private static void AddPath(
        Dictionary<string, string> paths,
        string addressSpaceId,
        FirmwareSlotViewModel slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.FilePath))
        {
            paths[addressSpaceId] = slot.FilePath;
        }
    }

    private void ApplyRunResult(WorkbenchRunResult result, bool build)
    {
        string action = build ? "Build" : "Preview";
        var report = ReportReviewViewModel.FromJson(result.ReportJson, $"{action.ToLowerInvariant()} report");
        string detail = result.Succeeded
            ? $"{result.ProfileId} / {result.OutputSize} bytes / {result.OutputSha256[..Math.Min(12, result.OutputSha256.Length)]}"
            : report.Issues.Count == 0 ? result.Status : report.Issues[0].Detail;
        LastRunResult = new UiRunResultViewModel(
            result.Succeeded ? $"{action} succeeded" : $"{action} blocked",
            detail,
            result.Succeeded ? result.CommittedOutputId ?? result.OutputFileName : "No output",
            result.Succeeded);
        OnPropertyChanged(nameof(LastRunResult));

        LoadedReport = report;
        LoadedReportJson = result.ReportJson;
        SetReportToast($"{action} report generated");
        NotifyReportChanged();
        RefreshSettingsState();
    }

    private FirmwareSlotViewModel? MergeSlotForAddressSpace(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => _mergeDpSlot,
            "tp-input" => _mergeTpSlot,
            "ld-input" => _mergeLdSlot,
            _ => null,
        };
    }
}
