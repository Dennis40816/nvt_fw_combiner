#pragma warning disable CS1591

using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    private string GetCtrlRamInputTitle(
        string declaredTitle,
        ReplaceRegionGroup regionGroup,
        CtrlRamInputDescriptionFacts? facts)
    {
        return facts is null
            ? declaredTitle
            : FormatCtrlRamInputTitle(
                facts.TitleStem,
                regionGroup,
                facts.IsShared,
                facts.RequiresDiffNfMerge);
    }

    private string FormatCtrlRamInputTitle(
        string titleStem,
        ReplaceRegionGroup regionGroup,
        bool isShared,
        bool isDiffNfMergeOutput)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleStem);
        string groupSuffix = (regionGroup, isShared) switch
        {
            (ReplaceRegionGroup.Common, true) => SelectLanguage(" (Shared)", "（共用）"),
            (ReplaceRegionGroup.Master, _) => SelectLanguage(" (Master)", "（主控）"),
            (ReplaceRegionGroup.SlaveRight, _) => SelectLanguage(" (Slave R)", "（右側從屬）"),
            (ReplaceRegionGroup.SlaveLeft, _) => SelectLanguage(" (Slave L)", "（左側從屬）"),
            (ReplaceRegionGroup.Cascade or
                ReplaceRegionGroup.Common or
                ReplaceRegionGroup.Base or
                ReplaceRegionGroup.Other, _) => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(regionGroup), regionGroup, null),
        };
        string title = $"{titleStem}{groupSuffix}";
        string diffNfMergeSuffix = SelectLanguage(
            " (DiffNFMerge output)",
            "（DiffNFMerge 輸出）");
        return isDiffNfMergeOutput
            ? $"{title}{diffNfMergeSuffix}"
            : title;
    }

    private string GetCtrlRamInputDescription(
        string declaredDescription,
        CtrlRamInputDescriptionFacts? facts)
    {
        if (Language != ShellLanguage.ChineseTraditional || facts is null)
        {
            return declaredDescription;
        }

        string sections = string.Join("；", facts.Sections.Select(section =>
            $"{FormatCtrlRamInputTitle(
                section.TitleStem,
                section.RegionGroup,
                isShared: false,
                isDiffNfMergeOutput: false)}" +
            $"：上限 {section.MaximumLength} B → 0x{section.TargetStart:X}"));
        string description = $"{facts.SourceFileName} · {sections}";
        return facts.RequiresDiffNfMerge
            ? $"{description} · 串接模式需要預先由 DiffNFMerge 產生的 NF_Ctrlram.bin；目前未整合產生流程。"
            : description;
    }

    public string GetReplaceRegionGroupTitle(ReplaceRegionGroup group)
    {
        return group switch
        {
            ReplaceRegionGroup.Cascade => SelectLanguage("Cascade", "串接"),
            ReplaceRegionGroup.Common => SelectLanguage("Common", "共用"),
            ReplaceRegionGroup.Master => SelectLanguage("Master", "主 IC"),
            ReplaceRegionGroup.SlaveRight => SelectLanguage("Slave R", "右從 IC"),
            ReplaceRegionGroup.SlaveLeft => SelectLanguage("Slave L", "左從 IC"),
            ReplaceRegionGroup.Base => SelectLanguage(
                "Base firmware (FlashCode / TP FW)", "基底韌體 (FlashCode / TP FW)"),
            ReplaceRegionGroup.Other => SelectLanguage("Other", "其他"),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };
    }

    public string FormatReplaceSlotGroupSummary(ReplaceRegionGroup group, int count)
    {
        return group switch
        {
            ReplaceRegionGroup.Cascade => SelectLanguage(
                $"{count} cascade-only areas.", $"{count} 個僅限串接模式的區域。"),
            ReplaceRegionGroup.Base => SelectLanguage(
                "Original firmware used as the starting point.", "作為起點的原始韌體。"),
            ReplaceRegionGroup.Common or ReplaceRegionGroup.Master or
            ReplaceRegionGroup.SlaveRight or ReplaceRegionGroup.SlaveLeft or
            ReplaceRegionGroup.Other => SelectLanguage(
                $"{count} replaceable areas. Add files only for areas you want to change.",
                $"{count} 個可取代區域；只需為要變更的區域加入檔案。"),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };
    }

    public string FormatReplaceCoverageGroupSummary(ReplaceRegionGroup group, int count)
    {
        return group switch
        {
            ReplaceRegionGroup.Cascade => SelectLanguage(
                $"{count} cascade-only areas that can be replaced.",
                $"{count} 個可取代的串接專用區域。"),
            ReplaceRegionGroup.Base => SelectLanguage(
                $"{count} areas retained from the base firmware BIN.",
                $"{count} 個從基底韌體 BIN 保留的區域。"),
            ReplaceRegionGroup.Common or ReplaceRegionGroup.Master or
            ReplaceRegionGroup.SlaveRight or ReplaceRegionGroup.SlaveLeft or
            ReplaceRegionGroup.Other => SelectLanguage(
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

    public string FormatCoverageSelectionSummary(bool isBase, int selected, int total)
    {
        return isBase
            ? SelectLanguage("Kept from base firmware.", "從基底韌體保留。")
            : SelectLanguage($"{selected} selected / {total} areas.",
                $"已選 {selected} / 共 {total} 個區域。");
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
            _ => issue.Message,
        };
    }
}

#pragma warning restore CS1591
