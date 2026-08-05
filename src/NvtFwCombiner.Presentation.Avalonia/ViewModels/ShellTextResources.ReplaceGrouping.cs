#pragma warning disable CS1591

using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
    public string GetReplaceRegionGroupTitle(WorkbenchReplaceRegionGroup group)
    {
        return group switch
        {
            WorkbenchReplaceRegionGroup.Cascade => SelectLanguage("Cascade", "串接"),
            WorkbenchReplaceRegionGroup.Common => SelectLanguage("Common", "共用"),
            WorkbenchReplaceRegionGroup.Master => SelectLanguage("Master", "主 IC"),
            WorkbenchReplaceRegionGroup.SlaveRight => SelectLanguage("Slave R", "右從 IC"),
            WorkbenchReplaceRegionGroup.SlaveLeft => SelectLanguage("Slave L", "左從 IC"),
            WorkbenchReplaceRegionGroup.Base => SelectLanguage(
                "Base firmware (FlashCode / TP FW)", "基底韌體 (FlashCode / TP FW)"),
            WorkbenchReplaceRegionGroup.Other => SelectLanguage("Other", "其他"),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };
    }

    public string FormatReplaceSlotGroupSummary(WorkbenchReplaceRegionGroup group, int count)
    {
        return group switch
        {
            WorkbenchReplaceRegionGroup.Cascade => SelectLanguage(
                $"{count} cascade-only areas.", $"{count} 個僅限串接模式的區域。"),
            WorkbenchReplaceRegionGroup.Base => SelectLanguage(
                "Original firmware used as the starting point.", "作為起點的原始韌體。"),
            WorkbenchReplaceRegionGroup.Common or WorkbenchReplaceRegionGroup.Master or
            WorkbenchReplaceRegionGroup.SlaveRight or WorkbenchReplaceRegionGroup.SlaveLeft or
            WorkbenchReplaceRegionGroup.Other => SelectLanguage(
                $"{count} replaceable areas. Add files only for areas you want to change.",
                $"{count} 個可取代區域；只需為要變更的區域加入檔案。"),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };
    }

    public string FormatReplaceCoverageGroupSummary(WorkbenchReplaceRegionGroup group, int count)
    {
        return group switch
        {
            WorkbenchReplaceRegionGroup.Cascade => SelectLanguage(
                $"{count} cascade-only areas that can be replaced.",
                $"{count} 個可取代的串接專用區域。"),
            WorkbenchReplaceRegionGroup.Base => SelectLanguage(
                $"{count} areas retained from the base firmware BIN.",
                $"{count} 個從基底韌體 BIN 保留的區域。"),
            WorkbenchReplaceRegionGroup.Common or WorkbenchReplaceRegionGroup.Master or
            WorkbenchReplaceRegionGroup.SlaveRight or WorkbenchReplaceRegionGroup.SlaveLeft or
            WorkbenchReplaceRegionGroup.Other => SelectLanguage(
                $"{count} areas that can be replaced for this IC group.",
                $"此 IC 群組有 {count} 個可取代區域。"),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };
    }

    public string FormatAreaSelectionSummary(int selected, int total)
    {
        return selected == 0
            ? SelectLanguage($"{total} areas. None selected.", $"{total} 個區域，尚未選擇。")
            : SelectLanguage($"{selected} selected / {total} areas.",
                $"已選 {selected} / 共 {total} 個區域。");
    }

    public string FormatCoverageChangeSummary(bool isBase, int changed, int total)
    {
        return isBase
            ? SelectLanguage("Kept from base firmware.", "從基底韌體保留。")
            : SelectLanguage($"{changed} selected / {total} areas.",
                $"已選 {changed} / 共 {total} 個區域。");
    }

    public string GetGeneralSelectedFileInspectionIssue(GeneralSelectedFileInspectionIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return issue.Code switch
        {
            GeneralSelectedFileInspectionIssueCodes.InspectionFailed => SelectLanguage(
                "The selected BIN could not be inspected.", "無法檢查所選 BIN。"),
            GeneralSelectedFileInspectionIssueCodes.SnapshotRequired => SelectLanguage(
                "Reload or rebind the selected BIN before continuing.",
                "繼續前請重新載入或重新綁定所選 BIN。"),
            GeneralSelectedFileInspectionIssueCodes.ObservedLengthChanged => SelectLanguage(
                "The selected BIN length changed; inspect it again.",
                "所選 BIN 的長度已變更，請重新檢查。"),
            _ => issue.Message,
        };
    }
}

#pragma warning restore CS1591
