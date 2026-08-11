using Avalonia.Platform.Storage;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static class FirmwareFilePickerDialogs
{
    public static async Task<string?> PickFirmwareBinOpenFileAsync(
        IStorageProvider storageProvider,
        string title)
    {
        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = CreateFirmwareBinChoices(),
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public static async Task<string?> PickMergedFirmwareOutputPathAsync(
        IStorageProvider storageProvider,
        string suggestedFileName)
    {
        IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save merged firmware BIN",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = CreateFirmwareBinChoices(),
        });

        return file?.TryGetLocalPath();
    }

    public static Task<string?> PickAbAFlashCodeOutputPathAsync(
        IStorageProvider storageProvider,
        string suggestedFileName)
    {
        return PickFirmwareBinOutputPathAsync(
            storageProvider,
            "Save A FlashCode BIN",
            suggestedFileName);
    }

    public static async Task<string?> PickReplacedFirmwareOutputPathAsync(
        IStorageProvider storageProvider,
        string suggestedFileName)
    {
        return await PickFirmwareBinOutputPathAsync(
            storageProvider,
            "Save replaced firmware BIN",
            suggestedFileName);
    }

    public static async Task<string?> PickEditedFirmwareOutputPathAsync(
        IStorageProvider storageProvider,
        string suggestedFileName)
    {
        return await PickFirmwareBinOutputPathAsync(
            storageProvider,
            "Save edited firmware BIN as",
            suggestedFileName);
    }

    private static async Task<string?> PickFirmwareBinOutputPathAsync(
        IStorageProvider storageProvider,
        string title,
        string suggestedFileName)
    {
        IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = CreateFirmwareBinChoices(),
        });

        return file?.TryGetLocalPath();
    }

    public static Task<IStorageFile?> PickRunReportSaveFileAsync(
        IStorageProvider storageProvider,
        string suggestedFileName)
    {
        return storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save run report JSON",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Run report JSON")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
                FilePickerFileTypes.All,
            ],
        });
    }

    public static Task<IStorageFile?> PickDiagnosticsSaveFileAsync(
        IStorageProvider storageProvider,
        string suggestedFileName)
    {
        return storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export system diagnostics",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("System diagnostics JSON")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
                FilePickerFileTypes.All,
            ],
        });
    }

    private static IReadOnlyList<FilePickerFileType> CreateFirmwareBinChoices()
    {
        return
        [
            new FilePickerFileType("Firmware BIN")
            {
                Patterns = ["*.bin"],
                MimeTypes = ["application/octet-stream"],
            },
            FilePickerFileTypes.All,
        ];
    }
}
