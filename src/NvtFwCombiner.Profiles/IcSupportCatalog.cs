using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Supported workflow identifiers used by IC onboarding policy.</summary>
public static class IcWorkflowIds
{
    /// <summary>Profile-backed normal Standard Merge.</summary>
    public const string StandardMerge = ExperienceIds.StandardMerge;

    /// <summary>DP Replace workflow.</summary>
    public const string DpReplace = ExperienceIds.DpReplace;

    /// <summary>CtrlRAM Replace workflow.</summary>
    public const string CtrlRamReplace = ExperienceIds.CtrlRamReplace;

    /// <summary>General Merge workbench workflow.</summary>
    public const string GeneralMerge = ExperienceIds.GeneralMerge;

    /// <summary>General Replace workbench workflow.</summary>
    public const string GeneralReplace = ExperienceIds.GeneralReplace;

    /// <summary>All workflow ids that may be declared by the IC support catalog.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        StandardMerge,
        DpReplace,
        CtrlRamReplace,
        GeneralMerge,
        GeneralReplace,
    ];

    /// <summary>Returns true when a workflow id is owned by the IC support catalog contract.</summary>
    public static bool IsKnown(string workflowId)
    {
        return All.Contains(workflowId, StringComparer.Ordinal);
    }
}

/// <summary>One IC onboarding entry that defines workflow exposure and alias/family facts.</summary>
public sealed class IcSupportEntry
{
    /// <summary>Creates an IC onboarding entry.</summary>
    public IcSupportEntry(
        string icId,
        IReadOnlyList<string> workflowIds,
        string? standardMergeSourceIcId = null,
        string? ctrlRamPostbuildSourceIcId = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(workflowIds);

        string[] distinctWorkflowIds =
        [
            .. workflowIds.Distinct(StringComparer.Ordinal),
        ];
        if (distinctWorkflowIds.Length == 0)
        {
            throw new ArgumentException("At least one supported workflow id is required.", nameof(workflowIds));
        }

        foreach (string workflowId in distinctWorkflowIds)
        {
            if (!IcWorkflowIds.IsKnown(workflowId))
            {
                throw new ArgumentException($"Unknown IC workflow id '{workflowId}'.", nameof(workflowIds));
            }
        }

        IcId = IcSupportCatalog.NormalizeIcId(icId);
        WorkflowIds = distinctWorkflowIds;
        StandardMergeSourceIcId = string.IsNullOrWhiteSpace(standardMergeSourceIcId)
            ? null
            : IcSupportCatalog.NormalizeIcId(standardMergeSourceIcId);
        CtrlRamPostbuildSourceIcId = string.IsNullOrWhiteSpace(ctrlRamPostbuildSourceIcId)
            ? null
            : IcSupportCatalog.NormalizeIcId(ctrlRamPostbuildSourceIcId);
        Notes = notes;
    }

    /// <summary>NT-prefixed IC id.</summary>
    public string IcId { get; }

    /// <summary>Workflow ids that may be surfaced for this IC.</summary>
    public IReadOnlyList<string> WorkflowIds { get; }

    /// <summary>Reference IC when Standard Merge is an owner-approved alias.</summary>
    public string? StandardMergeSourceIcId { get; }

    /// <summary>Reference IC when CtrlRAM postbuild is an owner-approved alias.</summary>
    public string? CtrlRamPostbuildSourceIcId { get; }

    /// <summary>Short human-readable evidence note.</summary>
    public string? Notes { get; }

    /// <summary>Returns true when this IC exposes the selected workflow.</summary>
    public bool SupportsWorkflow(string workflowId)
    {
        return WorkflowIds.Contains(workflowId, StringComparer.Ordinal);
    }

}

/// <summary>Single C# entry point for IC workflow support and alias facts.</summary>
public static class IcSupportCatalog
{
    private static readonly string[] DefaultWorkflowIds =
    [
        IcWorkflowIds.StandardMerge,
        IcWorkflowIds.CtrlRamReplace,
        IcWorkflowIds.GeneralMerge,
        IcWorkflowIds.GeneralReplace,
    ];

    private static readonly IcSupportEntry[] Entries =
    [
        Entry("NT51917", standardMergeSourceIcId: "NT51927", ctrlRamPostbuildSourceIcId: "NT51927"),
        Entry("NT51919", standardMergeSourceIcId: "NT51929", ctrlRamPostbuildSourceIcId: "NT51929"),
        Entry("NT51920"),
        Entry("NT51923"),
        Entry("NT51926", notes: "CtrlRAM postbuild category is selected by Common FW version."),
        Entry("NT51927"),
        Entry("NT51928", ctrlRamPostbuildSourceIcId: "NT51927", notes: "NT51928 NB is not covered."),
        Entry("NT51929", ctrlRamPostbuildSourceIcId: "NT51932"),
        Entry("NT51930", notes: "CtrlRAM postbuild category is selected by Common FW version."),
        Entry(
            "NT51931",
            workflowIds: [IcWorkflowIds.StandardMerge, IcWorkflowIds.GeneralMerge],
            notes: "Replace is Not Supported: the official NT51930BASED_NORMAL_MODE command crashes Combiner 1.13, while the alternate NT51931BASED_NORMAL_MODE path has unexplained 108-byte drift."),
        Entry("NT51932"),
        Entry(
            "NT51950",
            workflowIds: [.. DefaultWorkflowIds, IcWorkflowIds.DpReplace]),
        Entry(
            "NT51951",
            workflowIds: [.. DefaultWorkflowIds, IcWorkflowIds.DpReplace],
            standardMergeSourceIcId: "NT51950",
            ctrlRamPostbuildSourceIcId: "NT51950"),
    ];

    private static readonly Dictionary<string, IcSupportEntry> EntriesByIc = Entries
        .ToDictionary(entry => entry.IcId, StringComparer.Ordinal);

    /// <summary>All current workbench IC entries in stable display order.</summary>
    public static IReadOnlyList<IcSupportEntry> All => Entries;

    /// <summary>Initial IC selected by shell/workbench surfaces.</summary>
    public static string DefaultIcId => "NT51950";

    /// <summary>All selectable workbench IC ids.</summary>
    public static IReadOnlyList<string> IcIds => [.. Entries.Select(entry => entry.IcId)];

    /// <summary>Returns true when the catalog contains an entry for the selected IC.</summary>
    public static bool TryFind(string icId, out IcSupportEntry? entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return EntriesByIc.TryGetValue(NormalizeIcId(icId), out entry);
    }

    /// <summary>Returns true when an IC exposes the selected workflow.</summary>
    public static bool SupportsWorkflow(string icId, string workflowId)
    {
        return TryFind(icId, out IcSupportEntry? entry) &&
            entry is not null &&
            entry.SupportsWorkflow(workflowId);
    }

    private static IcSupportEntry Entry(
        string icId,
        IReadOnlyList<string>? workflowIds = null,
        string? standardMergeSourceIcId = null,
        string? ctrlRamPostbuildSourceIcId = null,
        string? notes = null)
    {
        return new IcSupportEntry(
            icId,
            workflowIds ?? DefaultWorkflowIds,
            standardMergeSourceIcId,
            ctrlRamPostbuildSourceIcId,
            notes);
    }

    /// <summary>Normalizes a required IC identifier to its canonical NT-prefixed form.</summary>
    public static string NormalizeIcId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
    }
}
