using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Atomic trusted selection and immutable map-resolution input for a future V2 compiler.</summary>
internal sealed class V2CompositionPreparationRequest
{
    internal V2CompositionPreparationRequest(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        FirmwareMapResolutionInputs resolutionInputs)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(resolutionInputs);
        Selection = selection;
        ResolutionInputs = resolutionInputs;
    }

    /// <summary>Exact catalog-minted profile selection.</summary>
    internal TrustedProfileBundleCatalog.ProfileSelection Selection { get; }

    /// <summary>Immutable Domain-owned map resolution selections and artifact snapshots.</summary>
    internal FirmwareMapResolutionInputs ResolutionInputs { get; }

}

/// <summary>Closed non-executable preparation outcome before V2 composition-plan lowering exists.</summary>
internal enum V2CompositionPreparationStatus
{
    SelectionRejected,
    MapPending,
    MapRejected,
    AdmissionRejected,
    Admitted,
}

/// <summary>Pairs exact trusted profile selection with existing map resolution and admission outcomes.</summary>
internal sealed class V2CompositionPreparationResult
{
    private const string SelectionStale = "profile.v2.selection.stale";
    private readonly CompiledCapabilityAdmission[] _capabilityAdmissions;
    private readonly CompositionIssue[] _issues;

    private V2CompositionPreparationResult(
        V2CompositionPreparationStatus status,
        TrustedProfileBundleCatalog.ProfileSelection? selection,
        TrustedCompositionProfileCatalogEntry? profileEntry,
        FirmwareMapResolutionResult? mapResolution,
        IEnumerable<CompiledCapabilityAdmission> capabilityAdmissions,
        IEnumerable<CompositionIssue> issues)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown V2 composition preparation status.");
        }

        _capabilityAdmissions = ImmutableReferenceSnapshot.Create(
            capabilityAdmissions,
            "Preparation capability admissions cannot contain null.");
        if (_capabilityAdmissions.Select(static admission => admission.RequiredCapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != _capabilityAdmissions.Length)
        {
            throw new ArgumentException(
                "Preparation capability admissions must be unique by required capability id.",
                nameof(capabilityAdmissions));
        }

        Array.Sort(_capabilityAdmissions, static (left, right) =>
            StringComparer.Ordinal.Compare(left.RequiredCapabilityId, right.RequiredCapabilityId));
        _issues = ImmutableReferenceSnapshot.Create(issues, "Preparation issues cannot contain null.");

        Array.Sort(_issues, static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            if (code != 0)
            {
                return code;
            }

            int message = StringComparer.Ordinal.Compare(left.Message, right.Message);
            return message != 0
                ? message
                : StringComparer.Ordinal.Compare(left.OperationId, right.OperationId);
        });
        ValidatePayload(
            status,
            selection,
            profileEntry,
            mapResolution,
            _capabilityAdmissions,
            _issues);
        Status = status;
        Selection = selection;
        ProfileEntry = profileEntry;
        MapResolution = mapResolution;
        CapabilityAdmissions = Array.AsReadOnly(_capabilityAdmissions);
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Closed pre-plan status; no status grants runtime execution authority.</summary>
    internal V2CompositionPreparationStatus Status { get; }

    /// <summary>Exact selection retained once catalog identity was accepted; otherwise null.</summary>
    internal TrustedProfileBundleCatalog.ProfileSelection? Selection { get; }

    /// <summary>Exact selected normalized profile and family retained only after map admission.</summary>
    internal TrustedCompositionProfileCatalogEntry? ProfileEntry { get; }

    /// <summary>Unchanged Domain resolver outcome after selection acceptance; otherwise null.</summary>
    internal FirmwareMapResolutionResult? MapResolution { get; }

    /// <summary>Domain-owned confirmed-present capability admissions retained after map admission.</summary>
    internal IReadOnlyList<CompiledCapabilityAdmission> CapabilityAdmissions { get; }

    /// <summary>Selection or admission errors; map pending/rejection remains in the typed resolver result.</summary>
    internal IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>True only when exact selection, map resolution, and admission all succeeded.</summary>
    internal bool IsAdmitted => Status == V2CompositionPreparationStatus.Admitted;

    internal static V2CompositionPreparationResult SelectionWasRejected()
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.SelectionRejected,
            selection: null,
            profileEntry: null,
            mapResolution: null,
            capabilityAdmissions: [],
            [new CompositionIssue(SelectionStale, "The selected trusted profile no longer belongs to this catalog.")]);
    }

    internal static V2CompositionPreparationResult MapIsPending(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        FirmwareMapResolutionResult mapResolution)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.MapPending,
            selection,
            profileEntry: null,
            mapResolution,
            capabilityAdmissions: [],
            []);
    }

    internal static V2CompositionPreparationResult MapWasRejected(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        FirmwareMapResolutionResult mapResolution)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.MapRejected,
            selection,
            profileEntry: null,
            mapResolution,
            capabilityAdmissions: [],
            []);
    }

    internal static V2CompositionPreparationResult AdmissionWasRejected(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        FirmwareMapResolutionResult mapResolution,
        IEnumerable<CompositionIssue> issues)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.AdmissionRejected,
            selection,
            profileEntry: null,
            mapResolution,
            capabilityAdmissions: [],
            issues);
    }

    internal static V2CompositionPreparationResult Admitted(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        TrustedCompositionProfileCatalogEntry profileEntry,
        FirmwareMapResolutionResult mapResolution,
        IEnumerable<CompiledCapabilityAdmission> capabilityAdmissions)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.Admitted,
            selection,
            profileEntry,
            mapResolution,
            capabilityAdmissions,
            []);
    }

    private static void ValidatePayload(
        V2CompositionPreparationStatus status,
        TrustedProfileBundleCatalog.ProfileSelection? selection,
        TrustedCompositionProfileCatalogEntry? profileEntry,
        FirmwareMapResolutionResult? mapResolution,
        CompiledCapabilityAdmission[] capabilityAdmissions,
        CompositionIssue[] issues)
    {
        bool selectionMatchesEntry = selection is not null && profileEntry is not null &&
            StringComparer.Ordinal.Equals(
                selection.ProfileEntryIdentity.EntryId,
                profileEntry.Identity.EntryId) &&
            StringComparer.Ordinal.Equals(
                selection.ProfileEntryIdentity.ContentHash,
                profileEntry.Identity.ContentHash) &&
            StringComparer.Ordinal.Equals(selection.ProfileId, profileEntry.Profile.ProfileId) &&
            StringComparer.Ordinal.Equals(selection.ProfileVersion, profileEntry.Profile.ProfileVersion);
        bool capabilitiesMatchProfile = profileEntry is not null &&
            profileEntry.Profile.MapBinding.RequiredCapabilityIds.SequenceEqual(
                capabilityAdmissions.Select(static admission => admission.RequiredCapabilityId),
                StringComparer.Ordinal);
        bool valid = status switch
        {
            V2CompositionPreparationStatus.SelectionRejected =>
                selection is null && profileEntry is null && mapResolution is null &&
                capabilityAdmissions.Length == 0 && issues.Length == 1,
            V2CompositionPreparationStatus.MapPending =>
                selection is not null && profileEntry is null &&
                mapResolution?.Status == FirmwareMapResolutionStatus.Pending &&
                capabilityAdmissions.Length == 0 && issues.Length == 0,
            V2CompositionPreparationStatus.MapRejected =>
                selection is not null && profileEntry is null &&
                mapResolution?.Status == FirmwareMapResolutionStatus.Rejected &&
                capabilityAdmissions.Length == 0 && issues.Length == 0,
            V2CompositionPreparationStatus.AdmissionRejected =>
                selection is not null && profileEntry is null &&
                mapResolution?.Status == FirmwareMapResolutionStatus.Unique &&
                capabilityAdmissions.Length == 0 && issues.Length != 0,
            V2CompositionPreparationStatus.Admitted =>
                selectionMatchesEntry &&
                mapResolution is { Status: FirmwareMapResolutionStatus.Unique, ResolvedMap: not null } &&
                capabilitiesMatchProfile && issues.Length == 0,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("V2 composition preparation result payload is inconsistent.");
        }
    }
}

/// <summary>Profiles-owned coordinator for exact trusted selection, canonical map resolution, and map admission.</summary>
internal static class V2CompositionPreparationService
{
    /// <summary>Prepares one non-executable V2 compiler context without creating a plan or compiled composition.</summary>
    internal static V2CompositionPreparationResult Prepare(
        TrustedProfileBundleCatalog catalog,
        V2CompositionPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        if (!catalog.TryResolveSelection(request.Selection, out TrustedCompositionProfileCatalogEntry? profile))
        {
            return V2CompositionPreparationResult.SelectionWasRejected();
        }

        var profileMapIds = profile.Profile.MapBinding.MapIds.ToHashSet(StringComparer.Ordinal);
        var deferredInspectionStructureIds = profile.Profile.MetadataBindings
            .Select(static binding => binding.StructureId)
            .ToHashSet(StringComparer.Ordinal);
        var requiredMetadataStructureIds =
            profile.Profile.MapBinding.RequiredMetadataStructureIds
                .Where(structureId => !deferredInspectionStructureIds.Contains(structureId))
                .ToHashSet(StringComparer.Ordinal);
        FirmwareMapResolutionResult mapResolution = profile.Family.Family.ResolveMapWithinForProfile(
            request.ResolutionInputs,
            profileMapIds,
            requiredMetadataStructureIds);
        switch (mapResolution.Status)
        {
            case FirmwareMapResolutionStatus.Pending:
                return V2CompositionPreparationResult.MapIsPending(
                    request.Selection,
                    mapResolution);
            case FirmwareMapResolutionStatus.Rejected:
                return V2CompositionPreparationResult.MapWasRejected(
                    request.Selection,
                    mapResolution);
            case FirmwareMapResolutionStatus.Unique:
                IReadOnlyList<CompositionIssue> admissionIssues = CompositionProfileMapAdmissionValidator.Validate(
                    profile.Profile,
                    profile.Family.Family,
                    mapResolution.ResolvedMap!,
                    out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions);
                return admissionIssues.Count == 0
                    ? V2CompositionPreparationResult.Admitted(
                        request.Selection,
                        profile,
                        mapResolution,
                        capabilityAdmissions)
                    : V2CompositionPreparationResult.AdmissionWasRejected(
                        request.Selection,
                        mapResolution,
                        admissionIssues);
            default:
                throw new InvalidOperationException("Unknown firmware map resolution status.");
        }
    }
}
