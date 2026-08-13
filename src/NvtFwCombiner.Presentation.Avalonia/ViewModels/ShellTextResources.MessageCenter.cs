// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string MessageCenterTitle { get; private init; } = string.Empty;

    public string MessageCenterTooltip { get; private init; } = string.Empty;

    public string RunReportsLabel { get; private init; } = string.Empty;

    public string SystemInformationLabel { get; private init; } = string.Empty;

    public string RefreshDiagnosticsLabel { get; private init; } = string.Empty;

    public string ExportDiagnosticsLabel { get; private init; } = string.Empty;

    public string NoActiveDiagnosticsLabel { get; private init; } = string.Empty;

    public string ActiveDiagnosticsLabel { get; private init; } = string.Empty;

    public string ApplicationVersionLabel { get; private init; } = string.Empty;

    public string RuntimeLabel { get; private init; } = string.Empty;

    public string CatalogLabel { get; private init; } = string.Empty;

    public string OpenReportHistoryLabel { get; private init; } = string.Empty;

    public string DiagnosticsExportedLabel { get; private init; } = string.Empty;

    public string DiagnosticsExportFailedLabel { get; private init; } = string.Empty;

    public string RefreshingDiagnosticsLabel { get; private init; } = string.Empty;

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

    public string GetSystemDiagnosticCategory(SystemDiagnosticCategory category)
    {
        return category switch
        {
            SystemDiagnosticCategory.CapabilityCatalog =>
                SelectLanguage("Capability catalog", "Capability 目錄"),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }

    public string GetSystemDiagnosticMessage(ActionableSystemDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return diagnostic.Code switch
        {
            SystemDiagnosticCodes.CapabilityCatalogUnavailable => SelectLanguage(
                diagnostic.Message,
                "Capability 目錄無法使用，因此已停用 Build。"),
            SystemDiagnosticCodes.CapabilityCatalogLastKnownGood => SelectLanguage(
                diagnostic.Message,
                "Capability 目錄重新載入失敗；目前仍使用最後已知正常版本。"),
            _ => diagnostic.Message,
        };
    }

    public string GetSystemDiagnosticAction(ActionableSystemDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return diagnostic.Code switch
        {
            SystemDiagnosticCodes.CapabilityCatalogUnavailable => SelectLanguage(
                diagnostic.Action,
                "請修正目錄來源後重新載入。"),
            SystemDiagnosticCodes.CapabilityCatalogLastKnownGood => SelectLanguage(
                diagnostic.Action,
                "請檢查目錄來源後重新載入。"),
            _ => diagnostic.Action,
        };
    }

    public string FormatCapabilityActionBlocker(CapabilityActionBlocker blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        string message = blocker.Code switch
        {
            CapabilityActionReadinessIssueCodes.AuthoringUnavailable => SelectLanguage(
                blocker.Message,
                $"路由 {blocker.SubjectId} 不允許 authoring。"),
            CapabilityActionReadinessIssueCodes.ExecutionNotAdmitted => SelectLanguage(
                blocker.Message,
                $"編譯後的路由 {blocker.SubjectId} 尚未允許執行。"),
            CapabilityActionReadinessIssueCodes.PostbuildStageAuthorityMissing => SelectLanguage(
                blocker.Message,
                $"{blocker.SubjectId} 缺少必要的 postbuild stage 權限。"),
            CapabilityActionReadinessIssueCodes.InputPending => SelectLanguage(
                blocker.Message,
                $"必要輸入 {blocker.SubjectId} 尚未選擇。"),
            CapabilityActionReadinessIssueCodes.InputBlocked => SelectLanguage(
                blocker.Message,
                $"輸入 {blocker.SubjectId} 未通過檢查。"),
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale => SelectLanguage(
                blocker.Message,
                "目前的 runtime dependency 狀態尚未重新整理。"),
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked => SelectLanguage(
                blocker.Message,
                $"必要的 runtime dependency {blocker.SubjectId} 無法使用。"),
            _ => blocker.Message,
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
        return $"{message} {action}";
    }
}

#pragma warning restore CS1591
