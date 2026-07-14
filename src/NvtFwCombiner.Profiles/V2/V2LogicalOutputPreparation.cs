using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed logical-output preparation status with no physical-map resolution state.</summary>
internal enum V2LogicalOutputPreparationStatus
{
    SelectionRejected,
    AdmissionRejected,
    Admitted,
}

/// <summary>Exact trusted profile and family admission for one logical General Merge member.</summary>
internal sealed class V2LogicalOutputAdmission
{
    internal V2LogicalOutputAdmission(
        TrustedCompositionProfileCatalogEntry profileEntry,
        string memberId)
    {
        ArgumentNullException.ThrowIfNull(profileEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        if (profileEntry.Profile.CompilationContext is not LogicalOutputProfileCompilationContext binding ||
            !binding.MemberIds.Contains(memberId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Logical-output admission requires a selected allowed member.", nameof(memberId));
        }

        ProfileEntry = profileEntry;
        MemberId = memberId;
    }

    internal TrustedCompositionProfileCatalogEntry ProfileEntry { get; }

    internal string MemberId { get; }
}

/// <summary>Atomic trusted logical-output preparation result before plan lowering.</summary>
internal sealed class V2LogicalOutputPreparationResult
{
    private readonly CompositionIssue[] _issues;

    private V2LogicalOutputPreparationResult(
        V2LogicalOutputPreparationStatus status,
        TrustedProfileBundleCatalog.ProfileSelection? selection,
        V2LogicalOutputAdmission? admission,
        IEnumerable<CompositionIssue> issues)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown logical-output preparation status.");
        }

        ArgumentNullException.ThrowIfNull(issues);
        _issues = [.. issues];
        if (_issues.Any(static issue => issue is null))
        {
            throw new ArgumentException("Logical-output preparation issues cannot contain null.", nameof(issues));
        }

        Array.Sort(_issues, static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            return code != 0
                ? code
                : StringComparer.Ordinal.Compare(left.Message, right.Message);
        });
        bool valid = status switch
        {
            V2LogicalOutputPreparationStatus.SelectionRejected => selection is null && admission is null && _issues.Length == 1,
            V2LogicalOutputPreparationStatus.AdmissionRejected => selection is not null && admission is null && _issues.Length != 0,
            V2LogicalOutputPreparationStatus.Admitted => selection is not null && admission is not null && _issues.Length == 0,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("Logical-output preparation payload is inconsistent.");
        }

        Status = status;
        Selection = selection;
        Admission = admission;
        Issues = Array.AsReadOnly(_issues);
    }

    internal V2LogicalOutputPreparationStatus Status { get; }

    internal TrustedProfileBundleCatalog.ProfileSelection? Selection { get; }

    internal V2LogicalOutputAdmission? Admission { get; }

    internal IReadOnlyList<CompositionIssue> Issues { get; }

    internal bool IsAdmitted => Status == V2LogicalOutputPreparationStatus.Admitted;

    internal static V2LogicalOutputPreparationResult SelectionWasRejected()
    {
        return new V2LogicalOutputPreparationResult(
            V2LogicalOutputPreparationStatus.SelectionRejected,
            selection: null,
            admission: null,
            [new CompositionIssue("profile.v2.logical.selection-stale", "The selected trusted logical-output profile no longer belongs to this catalog.")]);
    }

    internal static V2LogicalOutputPreparationResult AdmissionWasRejected(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        IEnumerable<CompositionIssue> issues)
    {
        return new V2LogicalOutputPreparationResult(
            V2LogicalOutputPreparationStatus.AdmissionRejected,
            selection,
            admission: null,
            issues);
    }

    internal static V2LogicalOutputPreparationResult Admitted(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        V2LogicalOutputAdmission admission)
    {
        return new V2LogicalOutputPreparationResult(
            V2LogicalOutputPreparationStatus.Admitted,
            selection,
            admission,
            []);
    }
}

/// <summary>Profiles-owned trusted admission for one map-independent General Merge selection.</summary>
internal static class V2LogicalOutputPreparationService
{
    internal static V2LogicalOutputPreparationResult Prepare(
        TrustedProfileBundleCatalog catalog,
        TrustedProfileBundleCatalog.ProfileSelection selection,
        string memberId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selection);
        return catalog.TryResolveSelection(selection, out TrustedCompositionProfileCatalogEntry? profileEntry)
            ? PrepareResolvedProfile(profileEntry, selection, memberId)
            : V2LogicalOutputPreparationResult.SelectionWasRejected();
    }

    private static V2LogicalOutputPreparationResult PrepareResolvedProfile(
        TrustedCompositionProfileCatalogEntry profileEntry,
        TrustedProfileBundleCatalog.ProfileSelection selection,
        string memberId)
    {
        return profileEntry.Profile.CompilationContext switch
        {
            not LogicalOutputProfileCompilationContext => RejectProfileShape(selection),
            LogicalOutputProfileCompilationContext when profileEntry.Profile.CompositionKind != CompositionKind.Merge ||
                                                       !StringComparer.Ordinal.Equals(
                                                           profileEntry.Profile.Experience.ExperienceId,
                                                           ExperienceIds.GeneralMerge) => RejectProfileShape(selection),
            LogicalOutputProfileCompilationContext binding when string.IsNullOrWhiteSpace(memberId) ||
                                                               !binding.MemberIds.Contains(memberId, StringComparer.Ordinal) =>
                RejectMember(selection),
            _ => V2LogicalOutputPreparationResult.Admitted(selection, new V2LogicalOutputAdmission(profileEntry, memberId)),
        };
    }

    private static V2LogicalOutputPreparationResult RejectProfileShape(
        TrustedProfileBundleCatalog.ProfileSelection selection)
    {
        return V2LogicalOutputPreparationResult.AdmissionWasRejected(
            selection,
            [new CompositionIssue(
                "profile.v2.logical.profile-shape-invalid",
                "The selected trusted V2 profile is not a logical-output General Merge declaration.")]);
    }

    private static V2LogicalOutputPreparationResult RejectMember(
        TrustedProfileBundleCatalog.ProfileSelection selection)
    {
        return V2LogicalOutputPreparationResult.AdmissionWasRejected(
            selection,
            [new CompositionIssue(
                "profile.v2.logical.member-not-admitted",
                "The requested member is not admitted by the selected logical-output profile.")]);
    }
}
