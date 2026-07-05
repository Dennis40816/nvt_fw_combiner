namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string? _approvedStandardMergePreviewToken;
    private string? _approvedReplacePreviewToken;

    private bool HasCurrentStandardMergePreview()
    {
        return string.Equals(
            _approvedStandardMergePreviewToken,
            CreateStandardMergePreviewToken(),
            StringComparison.Ordinal);
    }

    private bool HasCurrentReplacePreview()
    {
        return string.Equals(
            _approvedReplacePreviewToken,
            CreateReplacePreviewToken(),
            StringComparison.Ordinal);
    }

    private string CreateStandardMergePreviewToken()
    {
        return string.Join(
            "\n",
            "standard-merge",
            SelectedIc,
            SelectedNumber,
            SelectedMergeMode,
            SlotToken(_mergeDpSlot),
            SlotToken(_mergeTpSlot),
            SlotToken(_mergeLdSlot));
    }

    private string CreateReplacePreviewToken()
    {
        List<string> parts =
        [
            "replace",
            SelectedIc,
            SelectedNumber,
            SelectedReplaceMode,
        ];

        parts.AddRange(ReplaceSlots
            .OrderBy(slot => slot.SlotId, StringComparer.Ordinal)
            .Select(SlotToken));

        if (!ReplaceSlots.Contains(ReplaceBaseSlot))
        {
            parts.Add(SlotToken(ReplaceBaseSlot));
        }

        parts.AddRange(GeneralReplaceMappings
            .OrderBy(mapping => mapping.MappingId, StringComparer.Ordinal)
            .Select(mapping =>
                $"map|{mapping.MappingId}|{mapping.StartAddress}|{mapping.EndAddress}|{FileToken(mapping.FilePath)}"));

        return string.Join("\n", parts);
    }

    private static string SlotToken(FirmwareSlotViewModel slot)
    {
        return $"slot|{slot.SlotId}|optional:{slot.IsOptional}|{FileToken(slot.FilePath)}";
    }

    private static string FileToken(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "empty";
        }

        string fullPath = GetStablePath(path);
        try
        {
            var file = new FileInfo(fullPath);
            return file.Exists
                ? $"{fullPath}|{file.Length}|{file.LastWriteTimeUtc.Ticks}"
                : $"{fullPath}|missing";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return $"{fullPath}|unreadable";
        }
    }

    private static string GetStablePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private void CompleteStandardMergeRun(bool build, bool succeeded, string previewToken)
    {
        if (!build)
        {
            _approvedStandardMergePreviewToken = succeeded ? previewToken : null;
        }

        RefreshCommandState();
    }

    private void CompleteReplaceRun(bool build, bool succeeded, string previewToken)
    {
        if (!build)
        {
            _approvedReplacePreviewToken = succeeded ? previewToken : null;
        }

        RefreshCommandState();
    }

    private void BlockStandardMergeBuildUntilPreview()
    {
        const string message = "Run a valid Standard Merge Preview after the latest IC/mode/file change before Build.";
        LastRunResult = new UiRunResultViewModel("Build blocked", message, "No output", succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
        LoadRunErrorReport(
            "Build",
            UiCompositionRunner.GetStandardMergeProfileId(SelectedIc) ?? "standard-merge",
            SelectedIc,
            SelectedNumber,
            message,
            CreateStandardMergeSlotPaths(),
            issueCode: "ui.build.preview-required");
        RefreshCommandState();
    }

    private void BlockReplaceBuildUntilPreview()
    {
        string message = $"Run a valid {SelectedReplaceMode} Replace Preview after the latest IC/mode/file change before Build.";
        LastRunResult = new UiRunResultViewModel("Build blocked", message, "No output", succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
        LoadRunErrorReport(
            "Build",
            $"{SelectedIc.ToLowerInvariant()}-{SelectedReplaceMode.ToLowerInvariant()}-replace",
            SelectedIc,
            SelectedNumber,
            message,
            CreateReplaceSlotPaths(),
            compositionKind: "Replace",
            modeId: $"{SelectedReplaceMode.ToLowerInvariant()}-replace",
            experienceId: $"{SelectedReplaceMode.ToLowerInvariant()}-replace",
            issueCode: "ui.build.preview-required");
        RefreshCommandState();
    }
}
