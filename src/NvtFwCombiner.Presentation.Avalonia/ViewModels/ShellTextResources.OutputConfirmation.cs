using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string OutputConfirmationTargetLabel => SelectLanguage("Target", "目標");
    public string OutputConfirmationModeLabel => SelectLanguage("Mode / Output format", "模式 / 輸出格式");
    public string OutputConfirmationSizeLabel => SelectLanguage("Flash output size", "Flash 輸出大小");
    public string OutputConfirmationInputsLabel => SelectLanguage("Input sources", "輸入來源");
    public string OutputConfirmationLooseHint => SelectLanguage("Save the Flash BIN directly. Source files are not copied.", "直接儲存 Flash BIN，不複製來源檔案。");
    public string OutputConfirmationGeneratedHint => SelectLanguage("Dummy DP will be generated; it is not an input file.", "將產生 Dummy DP；它不屬於輸入檔案。");
    public string OutputConfirmationWarning => SelectLanguage("Input warnings require your attention. Expand input sources for details.", "輸入含有警告，請展開輸入來源確認詳情。");
    internal string FormatExpectedInputSizeLabel(string bindingId)
    {
        string role = bindingId == CompositionAddressSpaceIds.DpAbInput ? "DP" : GetOutputInputLabel(bindingId);
        return SelectLanguage($"Expected {role} size", $"預期 {role} 大小");
    }
    public string OutputConfirmationEventLabel => SelectLanguage("Event Buffer Version", "Event Buffer 版本");

    internal string FormatOutputBundleContents(int count)
    {
        return SelectLanguage($"Flash BIN + {count} source files", $"Flash BIN + {count} 個來源檔案");
    }

    internal static string GetOutputInputLabel(string bindingId)
    {
        return bindingId switch
        {
            CompositionAddressSpaceIds.DpAbInput => "DP AB Code",
            CompositionAddressSpaceIds.TpAInput => "TP A",
            CompositionAddressSpaceIds.TpBInput => "TP B",
            _ => GetInputArtifactRoleLabel(bindingId),
        };
    }

    internal string FormatOutputTopology(TopologyRequirement? requirement)
    {
        return requirement?.Kind switch
        {
            TopologyRequirementKind.SingleChip => SelectLanguage("Single", "單 IC"),
            TopologyRequirementKind.Cascade => SelectLanguage("Cascade", "串接"),
            TopologyRequirementKind.ExactCount => $"{requirement.ExactChipCount} IC",
            TopologyRequirementKind.None => string.Empty,
            _ => string.Empty,
        };
    }

    internal string FormatOutputInputWarning(CompositionOutputInputSummary input)
    {
        return input.InspectionLifecycle != AuthoringSlotLifecycle.Warning ? string.Empty :
            GetIgnoredTrailingInputDescription(input.Inspection) ??
            GetInputIssueHelp(input.InspectionIssueCode ?? string.Empty, "warning", input.Inspection?.DiagnosticEvidence)?.Detail ??
            (input.InspectionIssueCode == InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown ? AbUnknownVersionWarning :
                SelectLanguage($"Review input before Build ({input.InspectionIssueCode}).", $"Build 前請確認輸入（{input.InspectionIssueCode}）。"));
    }

    internal string FormatOutputNumber(IcNumberSelection selection)
    {
        return selection.Mode == IcNumberInputMode.SingleSelector ? SelectLanguage("Single", "單 IC") :
            string.Join(" / ", selection.Parts.Select(part => part switch
            {
                IcNumberSelectionTokens.Cascade => SelectLanguage("Cascade", "串接"),
                IcNumberSelectionTokens.CascadeTwoToEight => SelectLanguage("Cascade (2–8 IC)", "串接（2–8 IC）"),
                _ => selection.Mode == IcNumberInputMode.NumericSelector ? $"{part} IC" : part,
            }));
    }
}
