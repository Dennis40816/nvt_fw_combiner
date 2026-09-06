namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string ExitTitle => SelectLanguage("Exit NVT FW Combiner?", "離開 NVT FW Combiner？");
    public string ExitDetail => SelectLanguage(
        "Files are selected. Exiting closes this session and stops any active work. Unsaved Hex Editor changes will be lost. Source files will not be deleted.",
        "目前已有選取的檔案。離開會結束本次操作並停止進行中的工作；Hex Editor 尚未儲存的修改將遺失，不會刪除來源檔案。");
    public string ExitConfirmLabel => SelectLanguage("Exit", "離開");
    public string LeaveEditorTitle => SelectLanguage("Leave Hex Editor?", "離開 Hex Editor？");
    public string LeaveEditorDetail => SelectLanguage(
        "The opened file and edits will remain available when you return. This does not save changes to a file.",
        "已開啟的檔案與修改會保留，返回後可繼續操作；切頁不會將修改儲存到檔案。");
    public string LeaveEditorConfirmLabel => SelectLanguage("Continue", "繼續");
}
