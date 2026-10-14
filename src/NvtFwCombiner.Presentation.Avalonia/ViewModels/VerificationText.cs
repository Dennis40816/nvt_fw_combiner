using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Verification wording shared by the Merge and Replace panels, the support matrix and the report review.
/// It lives outside <see cref="ShellTextResources"/> so the label owner stays one small type.
/// </summary>
internal static class VerificationText
{
    /// <summary>Label of the verification-status field in the report header.</summary>
    public static string FieldLabel(ShellTextResources text)
    {
        return text.SupportMatrixLegendTitle;
    }

    /// <summary>Short badge text for a route whose output has no independent Golden evidence.</summary>
    public static string Unverified(ShellTextResources text)
    {
        return Pick(text, "Unverified", "未驗證");
    }

    /// <summary>Plain-language sentence shown next to the output of an unverified route.</summary>
    public static string OutputSentence(ShellTextResources text)
    {
        return Pick(text, "This output has no independent Golden result.", "這個輸出沒有獨立的 Golden 結果。");
    }

    /// <summary>Whether the exact selected route is unverified.</summary>
    public static bool IsUnverified(CapabilityWorkflowReadiness? readiness)
    {
        return readiness is { HasExactRoute: true, IsUnverified: true };
    }

    /// <summary>Output notice for the selected route; empty when the route is verified or not exact.</summary>
    public static string OutputNotice(ShellTextResources text, CapabilityWorkflowReadiness? readiness)
    {
        return IsUnverified(readiness) ? OutputSentence(text) : string.Empty;
    }

    /// <summary>Verification label and header summary for a loaded run report.</summary>
    public static (string StatusLabel, string Summary) ForReport(ShellLanguage language, CapabilityRunSummary? capability)
    {
        ShellTextResources text = ShellTextResources.For(language);
        string label = capability is null ? Pick(text, "Not recorded", "未記錄")
            : CapabilityWorkflowReadinessProjector.IsUnverified(capability.Evidence)
                ? text.SupportMatrixEvidenceValue(capability.Evidence)
                : Pick(
                    text,
                    $"{text.SupportMatrixReviewedEvidenceLabel} ({text.SupportMatrixEvidenceValue(capability.Evidence)})",
                    $"{text.SupportMatrixReviewedEvidenceLabel}（{text.SupportMatrixEvidenceValue(capability.Evidence)}）");
        return (label, $"{FieldLabel(text)}{Pick(text, ": ", "：")}{label}");
    }

    private static string Pick(ShellTextResources text, string english, string traditionalChinese)
    {
        return text.Language == ShellLanguage.ChineseTraditional ? traditionalChinese : english;
    }
}
