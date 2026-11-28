using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Read-only localized explanation; it never decides severity or Build availability.</summary>
internal sealed record IssueCardViewModel(
    string Caption,
    string Subject,
    string Summary,
    string Impact,
    string Action,
    bool IsError,
    string DiagnosticCode = "",
    bool IsBuildStatus = false,
    int AdditionalBlockerCount = 0,
    string AdditionalBlockerText = "")
{
    public static IssueCardViewModel FromBuildAvailability(
        CapabilityActionAvailability availability, IEnumerable<FirmwareSlotViewModel> slots,
        ShellTextResources text, bool globalBlocked, string globalBlockerText)
    {
        CapabilityActionBlocker? blocker = availability.PrimaryBlocker;
        // Application owns the order and count; Presentation enriches only the exact inspected slot.
        IssueCardViewModel? input = globalBlocked ? null : FindInputCard(blocker, slots);
        int additional = globalBlocked ? 0 : Math.Max(0, availability.Blockers.Count - 1);
        IssueCardViewModel? nextInput = additional > 0 ? FindInputCard(availability.Blockers[1], slots) : null;
        string next = additional == 0 ? string.Empty : AdditionalBlocker(availability.Blockers[1], nextInput, text);
        return new IssueCardViewModel(text.BuildIssueCaption, text.BuildBlockedTitle,
            input?.Summary ?? (globalBlocked ? globalBlockerText : blocker is null ? string.Empty : text.FormatCapabilityActionBlocker(blocker)), string.Empty, input is null ? (globalBlocked || blocker is null ? string.Empty : BlockerDetail(blocker, text))
                : input.IsShortInput ? NamedMinimum(input) : input.Action,
            IsError: true, DiagnosticCode: input?.DiagnosticCode ?? (globalBlocked ? string.Empty : blocker?.Code ?? string.Empty),
            IsBuildStatus: true, AdditionalBlockerCount: additional,
            AdditionalBlockerText: additional > 0 ? text.FormatAdditionalBuildBlockers(additional, next) : string.Empty);
    }

    private static string AdditionalBlocker(CapabilityActionBlocker blocker, IssueCardViewModel? input, ShellTextResources text)
    {
        if (input is not null)
        {
            return $"{input.Summary} ({(input.IsShortInput ? NamedMinimum(input) : input.Subject)})";
        }
        string sentence = text.FormatCapabilityActionBlocker(blocker);
        string detail = BlockerDetail(blocker, text);
        return detail.Length == 0 ? sentence : $"{sentence} ({detail})";
    }

    private bool IsShortInput => DiagnosticCode == "input.source-view.incomplete" && IsError;

    private static string NamedMinimum(IssueCardViewModel input)
    {
        return $"{input.Subject} · {input.Action}";
    }

    // The approved sentence replaces the message, so keep the facts only these blockers carry.
    private static string BlockerDetail(CapabilityActionBlocker blocker, ShellTextResources text)
    {
        bool specific = blocker.Code is CapabilityActionReadinessIssueCodes.InputBlocked or
            CapabilityActionReadinessIssueCodes.PostbuildStageAuthorityMissing or
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked;
        return !specific ? string.Empty
            : text.Language == ShellLanguage.ChineseTraditional ? blocker.SubjectId : blocker.Message;
    }

    private static IssueCardViewModel? FindInputCard(CapabilityActionBlocker? blocker, IEnumerable<FirmwareSlotViewModel> slots)
    {
        return blocker?.Code == CapabilityActionReadinessIssueCodes.InputBlocked
            ? slots.FirstOrDefault(slot => slot.InspectedSlotId == blocker.SubjectId && slot.BlocksBuild)?.IssueCard
            : null;
    }

    public bool HasImpact => !string.IsNullOrWhiteSpace(Impact);
    public bool HasAction => !string.IsNullOrWhiteSpace(Action);
    public bool HasDiagnosticCode => !string.IsNullOrWhiteSpace(DiagnosticCode);
    public bool HasAdditionalBlockers => AdditionalBlockerCount > 0;
    public string AdditionalBlockerBadge => $"+{AdditionalBlockerCount}";

    public string AutomationText => string.Join(" ", new[] { Caption, Subject, Impact, Summary, Action, AdditionalBlockerText, DiagnosticCode }
        .Where(static value => !string.IsNullOrWhiteSpace(value)));

    public string IconPath => IsError
        ? "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01"
        : "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01";

    public string ImpactIconPath => IsError
        ? "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M7 12H17"
        : "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 11V17 M12 7H12.01";
}
