using Avalonia.Platform.Storage;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static class FirmwareFilePickerDialogs
{
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

    public static async Task<string?> PickReplacedFirmwareOutputPathAsync(
        IStorageProvider storageProvider,
        string suggestedFileName)
    {
        IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save replaced firmware BIN",
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
