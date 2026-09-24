using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string OutputDeliveryEditAdditionalNameLabel => SelectLanguage("Edit A FlashCode filename", "編輯 A FlashCode 檔名");
    public string OutputDeliveryDoneAdditionalNameLabel => SelectLanguage("Finish editing A FlashCode filename", "完成 A FlashCode 檔名編輯");
    public string OutputConfirmationTargetLabel => SelectLanguage("Target", "目標");
    public string OutputConfirmationModeLabel => SelectLanguage("Mode", "模式");
    public string OutputConfirmationFlashMapLabel => SelectLanguage("Flash map", "Flash 配置");
    public string OutputConfirmationSizeLabel => SelectLanguage("Flash output size", "Flash 輸出大小");
    public string OutputConfirmationInputsLabel => SelectLanguage("Input sources", "輸入來源");
    public string OutputConfirmationLooseHint => SelectLanguage("Save the Flash BIN directly. Source files are not copied.", "直接儲存 Flash BIN，不複製來源檔案。");
    public string OutputConfirmationGeneratedHint => SelectLanguage("Dummy DP will be generated; it is not an input file.", "將產生 Dummy DP；它不屬於輸入檔案。");
    internal string FormatOutputWarningsTitle(int count)
    {
        return SelectLanguage($"Build warnings · {count}", $"Build 警告 · {count}");
    }
    internal string FormatOutputWarningsReady(int count)
    {
        return SelectLanguage($"{count} warning{(count == 1 ? "" : "s")} · Can continue Build", $"{count} 則警告 · 可繼續 Build");
    }
    internal string FormatExpectedInputSizeLabel(string bindingId)
    {
        string role = bindingId == CompositionAddressSpaceIds.DpAbInput ? "DP" : GetOutputInputLabel(bindingId);
        return SelectLanguage($"Expected {role} size", $"預期 {role} 大小");
    }
    public string OutputConfirmationEventLabel => SelectLanguage("Event Buffer Format", "Event Buffer 格式");

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
        return string.Join(Environment.NewLine, FormatOutputInputWarnings(input));
    }

    internal IReadOnlyList<string> FormatOutputInputWarnings(CompositionOutputInputSummary input)
    {
        var warnings = new List<string>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        if (input.Inspection is { Severity: CompiledInputArtifactInspectionSeverity.Warning } inspection)
        {
            _ = codes.Add(inspection.IssueCode);
            warnings.Add(GetIgnoredTrailingInputDescription(inspection) ?? FormatWarning(inspection.IssueCode));
        }
        if (input.InspectionLifecycle == AuthoringSlotLifecycle.Warning)
        {
            string code = input.InspectionIssueCode ?? string.Empty;
            if (codes.Add(code)) { warnings.Add(FormatWarning(code)); }
        }
        foreach (CompiledInputArtifactInspectionAdvisory advisory in input.InspectionAdvisories)
        {
            if (codes.Add(advisory.IssueCode)) { warnings.Add(FormatWarning(advisory.IssueCode)); }
        }
        return warnings;

        string FormatWarning(string code)
        {
            return GetInputIssueHelp(code, "warning", input.Inspection?.DiagnosticEvidence)?.Detail ??
                (code == InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown ? AbUnknownVersionWarning :
                    SelectLanguage($"Review input before Build ({code}).", $"Build 前請確認輸入（{code}）。"));
        }
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
