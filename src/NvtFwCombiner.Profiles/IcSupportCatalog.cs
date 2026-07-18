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

/// <summary>Golden-evidence state; this is independent from product support approval.</summary>
public enum IcWorkflowEvidenceStatus
{
    /// <summary>The workflow has direct or owner-approved fact-scoped golden parity.</summary>
    GoldenVerified,

    /// <summary>The workflow is available, but its current golden/owner gate remains open.</summary>
    EvidenceGated,

    /// <summary>The workflow has no approved executable/safety contract for this IC.</summary>
    NotAvailable,
}

/// <summary>Owner-defined relationship of one IC to a canonical IC family member.</summary>
public enum IcFamilyRelationship
{
    /// <summary>The IC has no declared cross-IC family relationship.</summary>
    Standalone,

    /// <summary>The IC is the canonical member used to name a family.</summary>
    Canonical,

    /// <summary>All facts in the declared family scope are reusable.</summary>
    PerfectAlias,

    /// <summary>Only the explicitly documented subset of facts is reusable.</summary>
    PartialAlias,
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
        IReadOnlyList<string>? goldenVerifiedWorkflowIds = null,
        string? familyId = null,
        string? familySourceIcId = null,
        IcFamilyRelationship familyRelationship = IcFamilyRelationship.Standalone,
        string? familyScope = null,
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

        if (distinctWorkflowIds.Contains(IcWorkflowIds.DpReplace, StringComparer.Ordinal) &&
            !distinctWorkflowIds.Contains(IcWorkflowIds.StandardMerge, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "DP Replace requires the same IC to expose its canonical Standard Merge map.",
                nameof(workflowIds));
        }

        string[] distinctGoldenVerifiedWorkflowIds =
        [
            .. (goldenVerifiedWorkflowIds ?? []).Distinct(StringComparer.Ordinal),
        ];
        if (distinctGoldenVerifiedWorkflowIds.Any(workflowId =>
                !distinctWorkflowIds.Contains(workflowId, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "Golden-verified workflow ids must also be exposed by the IC entry.",
                nameof(goldenVerifiedWorkflowIds));
        }

        bool declaresFamily = !string.IsNullOrWhiteSpace(familyId);
        if (declaresFamily != (familyRelationship != IcFamilyRelationship.Standalone) ||
            declaresFamily != !string.IsNullOrWhiteSpace(familySourceIcId))
        {
            throw new ArgumentException(
                "Family id, source IC, and non-standalone relationship must be declared together.",
                nameof(familyId));
        }

        IcId = IcSupportCatalog.NormalizeIcId(icId);
        WorkflowIds = distinctWorkflowIds;
        StandardMergeSourceIcId = string.IsNullOrWhiteSpace(standardMergeSourceIcId)
            ? null
            : IcSupportCatalog.NormalizeIcId(standardMergeSourceIcId);
        CtrlRamPostbuildSourceIcId = string.IsNullOrWhiteSpace(ctrlRamPostbuildSourceIcId)
            ? null
            : IcSupportCatalog.NormalizeIcId(ctrlRamPostbuildSourceIcId);
        GoldenVerifiedWorkflowIds = distinctGoldenVerifiedWorkflowIds;
        FamilyId = familyId;
        FamilySourceIcId = string.IsNullOrWhiteSpace(familySourceIcId)
            ? null
            : IcSupportCatalog.NormalizeIcId(familySourceIcId);
        FamilyRelationship = familyRelationship;
        FamilyScope = familyScope;
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

    /// <summary>Workflows with direct or owner-approved fact-scoped golden parity.</summary>
    public IReadOnlyList<string> GoldenVerifiedWorkflowIds { get; }

    /// <summary>Owner-defined IC family id, or null for a standalone IC.</summary>
    public string? FamilyId { get; }

    /// <summary>Canonical IC whose facts define this family scope.</summary>
    public string? FamilySourceIcId { get; }

    /// <summary>Whether the IC is canonical, a perfect alias, a partial alias, or standalone.</summary>
    public IcFamilyRelationship FamilyRelationship { get; }

    /// <summary>Human-readable boundary of facts reusable through the family relation.</summary>
    public string? FamilyScope { get; }

    /// <summary>Short human-readable evidence note.</summary>
    public string? Notes { get; }

    /// <summary>Returns true when this IC exposes the selected workflow.</summary>
    public bool SupportsWorkflow(string workflowId)
    {
        return WorkflowIds.Contains(workflowId, StringComparer.Ordinal);
    }

    /// <summary>Gets golden evidence readiness without turning a missing golden into a feature ban.</summary>
    public IcWorkflowEvidenceStatus GetWorkflowEvidenceStatus(string workflowId)
    {
        return !SupportsWorkflow(workflowId)
            ? IcWorkflowEvidenceStatus.NotAvailable
            : GoldenVerifiedWorkflowIds.Contains(workflowId, StringComparer.Ordinal)
                ? IcWorkflowEvidenceStatus.GoldenVerified
                : IcWorkflowEvidenceStatus.EvidenceGated;
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
        Entry(
            "NT51917",
            standardMergeSourceIcId: "NT51927",
            ctrlRamPostbuildSourceIcId: "NT51927",
            familyId: "nt51927-family",
            familySourceIcId: "NT51927",
            familyRelationship: IcFamilyRelationship.PerfectAlias,
            familyScope: "Perfect alias of NT51927 across the owner-declared IC family facts."),
        Entry(
            "NT51919",
            standardMergeSourceIcId: "NT51929",
            ctrlRamPostbuildSourceIcId: "NT51929",
            familyId: "nt51929-nt51932-family",
            familySourceIcId: "NT51929",
            familyRelationship: IcFamilyRelationship.PerfectAlias,
            familyScope: "Perfect alias in the NT51919/NT51929/NT51932 family."),
        Entry("NT51920"),
        Entry("NT51923"),
        Entry("NT51926", notes: "CtrlRAM postbuild category is selected by Common FW version."),
        Entry(
            "NT51927",
            familyId: "nt51927-family",
            familySourceIcId: "NT51927",
            familyRelationship: IcFamilyRelationship.Canonical,
            familyScope: "Canonical member for NT51917 and NT51928 non-NB family facts."),
        Entry(
            "NT51928",
            ctrlRamPostbuildSourceIcId: "NT51927",
            familyId: "nt51927-family",
            familySourceIcId: "NT51927",
            familyRelationship: IcFamilyRelationship.PartialAlias,
            familyScope: "Replace follows NT51927; LDC differs and NT51928 NB is excluded.",
            notes: "NT51928 NB is not covered."),
        Entry(
            "NT51929",
            ctrlRamPostbuildSourceIcId: "NT51932",
            familyId: "nt51929-nt51932-family",
            familySourceIcId: "NT51929",
            familyRelationship: IcFamilyRelationship.Canonical,
            familyScope: "Canonical single-product member for NT51919/NT51929/NT51932 family facts."),
        Entry(
            "NT51930",
            workflowIds: [.. DefaultWorkflowIds, IcWorkflowIds.DpReplace],
            notes: "DP Replace reuses the canonical 256 KiB Standard Merge map and remains Evidence open pending direct golden parity; CtrlRAM Postbuild category is selected by Common FW version."),
        Entry(
            "NT51931",
            workflowIds: [IcWorkflowIds.StandardMerge, IcWorkflowIds.GeneralMerge],
            notes: "Replace is Not available until the exact V2 route is materialized. Registered Combiner 1.13.0 NT51931BASED_NORMAL_MODE matches the reviewed 1.2.0.4 NT51930-based control byte-for-byte; the shared output differs from the owner expected only in 108 classified header/header-copy CRC bytes."),
        Entry(
            "NT51932",
            familyId: "nt51929-nt51932-family",
            familySourceIcId: "NT51929",
            familyRelationship: IcFamilyRelationship.PerfectAlias,
            familyScope: "Perfect family alias; NT51932 owns the cascade product path."),
        Entry(
            "NT51950",
            workflowIds: [.. DefaultWorkflowIds, IcWorkflowIds.DpReplace],
            goldenVerifiedWorkflowIds: [IcWorkflowIds.StandardMerge, IcWorkflowIds.DpReplace]),
        Entry(
            "NT51951",
            workflowIds: [.. DefaultWorkflowIds, IcWorkflowIds.DpReplace],
            standardMergeSourceIcId: "NT51950",
            ctrlRamPostbuildSourceIcId: "NT51950",
            goldenVerifiedWorkflowIds: [IcWorkflowIds.StandardMerge, IcWorkflowIds.DpReplace]),
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
        IReadOnlyList<string>? goldenVerifiedWorkflowIds = null,
        string? familyId = null,
        string? familySourceIcId = null,
        IcFamilyRelationship familyRelationship = IcFamilyRelationship.Standalone,
        string? familyScope = null,
        string? notes = null)
    {
        return new IcSupportEntry(
            icId,
            workflowIds ?? DefaultWorkflowIds,
            standardMergeSourceIcId,
            ctrlRamPostbuildSourceIcId,
            goldenVerifiedWorkflowIds ?? [IcWorkflowIds.StandardMerge],
            familyId,
            familySourceIcId,
            familyRelationship,
            familyScope,
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
