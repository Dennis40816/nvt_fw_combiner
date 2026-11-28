// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string MessageCenterTitle { get; private init; } = string.Empty;

    public string MessageCenterTooltip { get; private init; } = string.Empty;

    public string RunReportsLabel { get; private init; } = string.Empty;

    public string SystemInformationLabel { get; private init; } = string.Empty;

    public string RefreshDiagnosticsLabel { get; private init; } = string.Empty;

    public string NoActiveDiagnosticsLabel { get; private init; } = string.Empty;

    public string OpenReportHistoryLabel { get; private init; } = string.Empty;

    public string DiagnosticsExportedLabel { get; private init; } = string.Empty;

    public string DiagnosticsExportFailedLabel { get; private init; } = string.Empty;

    public string RefreshingDiagnosticsLabel { get; private init; } = string.Empty;

    public string SystemActivityTitle { get; private init; } = string.Empty;

    public string SystemActivitySubtitle { get; private init; } = string.Empty;

    public string ImportantActivityLabel { get; private init; } = string.Empty;

    public string WarningActivityLabel { get; private init; } = string.Empty;

    public string ErrorActivityLabel { get; private init; } = string.Empty;

    public string ShowDebugActivityLabel { get; private init; } = string.Empty;

    public string HideDebugActivityLabel { get; private init; } = string.Empty;

    public string SessionActivityNotice { get; private init; } = string.Empty;

    public string NoActivityLabel { get; private init; } = string.Empty;

    public string ExportActivityLabel { get; private init; } = string.Empty;

    public string FormatMessageCenterAccessibleName(int activeCount)
    {
        return activeCount == 0
            ? MessageCenterTitle
            : SelectLanguage(
                $"{MessageCenterTitle}, {activeCount} active diagnostics",
                $"{MessageCenterTitle}，{activeCount} 個目前診斷");
    }

    public string FormatSystemDiagnosticAnnouncement(int activeCount)
    {
        return activeCount == 0
            ? NoActiveDiagnosticsLabel
            : SelectLanguage(
                $"{activeCount} active system diagnostics.",
                $"目前有 {activeCount} 個系統診斷。");
    }

    public string GetCatalogStateLabel(CanonicalSupportMatrixCatalogState state)
    {
        return state switch
        {
            CanonicalSupportMatrixCatalogState.Loading =>
                SelectLanguage("Loading", "載入中"),
            CanonicalSupportMatrixCatalogState.Current =>
                SelectLanguage("Current", "目前版本"),
            CanonicalSupportMatrixCatalogState.LastKnownGood =>
                SelectLanguage("Last-known-good", "最後已知正常版本"),
            CanonicalSupportMatrixCatalogState.ColdStartBlocked =>
                SelectLanguage("Unavailable", "無法使用"),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
    }

    public string FormatExternalEnvironmentSummary(ExternalProcessorEnvironmentStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        string state = status.State switch
        {
            ExternalProcessorEnvironmentState.NotLoaded => SelectLanguage("Not loaded", "尚未載入"),
            ExternalProcessorEnvironmentState.Loading => SelectLanguage("Loading", "載入中"),
            ExternalProcessorEnvironmentState.Current => SelectLanguage("Current", "目前版本"),
            ExternalProcessorEnvironmentState.LastKnownGood =>
                SelectLanguage("Last-known-good", "最後已知正常版本"),
            ExternalProcessorEnvironmentState.Unavailable => SelectLanguage("Unavailable", "無法使用"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status.State, null),
        };
        return SelectLanguage(
            $"{state} · {status.ManifestCount} manifests · generation {status.PublicationGeneration}",
            $"{state} · {status.ManifestCount} 個 manifest · 第 {status.PublicationGeneration} 代");
    }

    public string GetSystemDiagnosticCategory(SystemDiagnosticCategory category)
    {
        return category switch
        {
            SystemDiagnosticCategory.CapabilityCatalog =>
                SelectLanguage("Capability catalog", "Capability 目錄"),
            SystemDiagnosticCategory.ExternalProcessorEnvironment =>
                SelectLanguage("External tools", "外部工具"),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }

    public string GetSystemDiagnosticMessage(ActionableSystemDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return diagnostic.Code switch
        {
            SystemDiagnosticCodes.CapabilityCatalogUnavailable => SelectLanguage(
                "The capability catalog is unavailable, blocking Build; select Refresh under System activity in Message Center.",
                "功能目錄無法使用而阻擋 Build，請在訊息中心的「系統活動」按「重新整理」。"),
            SystemDiagnosticCodes.PrebuiltCatalogUnused => SelectLanguage(diagnostic.Message, "未使用預建 Profile 目錄；目前使用 JSON admission。"),
            SystemDiagnosticCodes.CapabilityCatalogLastKnownGood => SelectLanguage(diagnostic.Message, "Capability 目錄重新載入失敗；目前仍使用最後已知正常版本。"),
            SystemDiagnosticCodes.ExternalProcessorEnvironmentUnavailable => SelectLanguage(diagnostic.Message, "外部工具環境無法使用。"),
            SystemDiagnosticCodes.ExternalProcessorEnvironmentLastKnownGood => SelectLanguage(diagnostic.Message, "外部工具重新整理失敗；目前仍使用最後已知正常環境。"),
            _ => diagnostic.Message,
        };
    }

    public string GetSystemDiagnosticAction(ActionableSystemDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return diagnostic.Code switch
        {
            SystemDiagnosticCodes.CapabilityCatalogUnavailable => string.Empty,
            SystemDiagnosticCodes.PrebuiltCatalogUnused => SelectLanguage(diagnostic.Action, "請修復或重新安裝套件，然後重新啟動應用程式。"),
            SystemDiagnosticCodes.CapabilityCatalogLastKnownGood => SelectLanguage(diagnostic.Action, "請檢查目錄來源後重新載入。"),
            SystemDiagnosticCodes.ExternalProcessorEnvironmentUnavailable or
            SystemDiagnosticCodes.ExternalProcessorEnvironmentLastKnownGood => SelectLanguage(diagnostic.Action, "請檢查外部工具 manifest 後重新整理。"),
            _ => diagnostic.Action,
        };
    }

    public string FormatCapabilityActionBlocker(CapabilityActionBlocker blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        string? message = blocker.Code switch
        {
            "AB_FORMAT_CONFIGURATION_INVALID" =>
                SelectLanguage("Event Buffer Format settings are invalid or unreadable; review and save them in Settings.", "Event Buffer Format 設定無效或無法讀取，請到「設定」檢查並儲存。"),
            CapabilityActionReadinessIssueCodes.AuthoringUnavailable =>
                SelectLanguage("The selected workflow is unavailable; choose another workflow.", "所選工作流程目前無法使用，請選擇其他工作流程。"),
            CapabilityActionReadinessIssueCodes.ExecutionNotAdmitted =>
                SelectLanguage("The selected workflow cannot run; review the Build status or choose another workflow.", "所選工作流程無法執行，請查看 Build 狀態或選擇其他工作流程。"),
            CapabilityActionReadinessIssueCodes.InputBlocked =>
                SelectLanguage("An input is blocking Build; review the input cards and their required actions.", "有輸入阻擋 Build，請查看輸入卡及其處理指示。"),
            CapabilityActionReadinessIssueCodes.InputPending =>
                SelectLanguage("Build prerequisites are not ready; check the workflow selection and required inputs.", "Build 所需條件尚未就緒，請確認工作流程選擇及必要輸入。"),
            CapabilityActionReadinessIssueCodes.PostbuildStageAuthorityMissing =>
                SelectLanguage("General Replace cannot prepare the required postbuild step; review the selected IC, base BIN, and mappings.", "General Replace 無法準備必要的後處理步驟，請檢查所選 IC、基底 BIN 及對應。"),
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked =>
                SelectLanguage("A required external tool is not ready; select Refresh under System activity in Message Center.", "必要的外部工具尚未就緒，請在訊息中心的「系統活動」按「重新整理」。"),
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale =>
                SelectLanguage("External tool status is not current; select Refresh under System activity in Message Center.", "外部工具狀態尚未更新，請在訊息中心的「系統活動」按「重新整理」。"),
            _ => null,
        };
        string action = blocker.NextAction switch
        {
            CapabilityReadinessNextAction.SelectAvailableRoute => SelectLanguage(
                "Select an available route.",
                "請選擇可用路由。"),
            CapabilityReadinessNextAction.LoadRequiredInput => SelectLanguage(
                "Load the required input.",
                "請載入必要輸入。"),
            CapabilityReadinessNextAction.CorrectInput => SelectLanguage(
                "Correct or replace the input.",
                "請修正或更換輸入。"),
            CapabilityReadinessNextAction.ReviewConfiguration => SelectLanguage(
                "Open Settings > Config > Event Buffer Format, correct and save the settings, or reload the repaired file.",
                "請開啟設定 > Config > Event Buffer Format，修正並儲存設定，或重新載入已修復的檔案。"),
            CapabilityReadinessNextAction.ReviewCompilation => SelectLanguage(
                "Review the compiled route.",
                "請檢查編譯後的路由。"),
            CapabilityReadinessNextAction.RefreshRuntimeDependencies => SelectLanguage(
                "Refresh runtime dependencies.",
                "請重新整理 runtime dependencies。"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(blocker),
                blocker.NextAction,
                null),
        };
        return message ?? $"{blocker.Message} {action}";
    }
}

#pragma warning restore CS1591
