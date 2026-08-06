using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

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
        ProfileBundleIdentity? bundleIdentity,
        TrustedCompositionProfileCatalogEntry? selection,
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
            bundleIdentity,
            selection,
            mapResolution,
            _capabilityAdmissions,
            _issues);
        Status = status;
        BundleIdentity = bundleIdentity;
        Selection = selection;
        ProfileEntry = status == V2CompositionPreparationStatus.Admitted ? selection : null;
        MapResolution = mapResolution;
        CapabilityAdmissions = Array.AsReadOnly(_capabilityAdmissions);
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Closed pre-plan status; no status grants runtime execution authority.</summary>
    internal V2CompositionPreparationStatus Status { get; }

    /// <summary>Exact trusted bundle retained once its catalog accepted the selected entry.</summary>
    internal ProfileBundleIdentity? BundleIdentity { get; }

    /// <summary>Exact selection retained once catalog identity was accepted; otherwise null.</summary>
    internal TrustedCompositionProfileCatalogEntry? Selection { get; }

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
            bundleIdentity: null,
            selection: null,
            mapResolution: null,
            capabilityAdmissions: [],
            [new CompositionIssue(SelectionStale, "The selected trusted profile no longer belongs to this catalog.")]);
    }

    internal static V2CompositionPreparationResult MapIsPending(
        ProfileBundleIdentity bundleIdentity,
        TrustedCompositionProfileCatalogEntry selection,
        FirmwareMapResolutionResult mapResolution)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.MapPending,
            bundleIdentity,
            selection,
            mapResolution,
            capabilityAdmissions: [],
            []);
    }

    internal static V2CompositionPreparationResult MapWasRejected(
        ProfileBundleIdentity bundleIdentity,
        TrustedCompositionProfileCatalogEntry selection,
        FirmwareMapResolutionResult mapResolution)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.MapRejected,
            bundleIdentity,
            selection,
            mapResolution,
            capabilityAdmissions: [],
            []);
    }

    internal static V2CompositionPreparationResult AdmissionWasRejected(
        ProfileBundleIdentity bundleIdentity,
        TrustedCompositionProfileCatalogEntry selection,
        FirmwareMapResolutionResult mapResolution,
        IEnumerable<CompositionIssue> issues)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.AdmissionRejected,
            bundleIdentity,
            selection,
            mapResolution,
            capabilityAdmissions: [],
            issues);
    }

    internal static V2CompositionPreparationResult Admitted(
        ProfileBundleIdentity bundleIdentity,
        TrustedCompositionProfileCatalogEntry selection,
        FirmwareMapResolutionResult mapResolution,
        IEnumerable<CompiledCapabilityAdmission> capabilityAdmissions)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.Admitted,
            bundleIdentity,
            selection,
            mapResolution,
            capabilityAdmissions,
            []);
    }

    private static void ValidatePayload(
        V2CompositionPreparationStatus status,
        ProfileBundleIdentity? bundleIdentity,
        TrustedCompositionProfileCatalogEntry? selection,
        FirmwareMapResolutionResult? mapResolution,
        CompiledCapabilityAdmission[] capabilityAdmissions,
        CompositionIssue[] issues)
    {
        bool capabilitiesMatchProfile = selection is not null &&
            selection.Profile.MapBinding.RequiredCapabilityIds.SequenceEqual(
                capabilityAdmissions.Select(static admission => admission.RequiredCapabilityId),
                StringComparer.Ordinal);
        bool valid = status switch
        {
            V2CompositionPreparationStatus.SelectionRejected =>
                bundleIdentity is null && selection is null && mapResolution is null &&
                capabilityAdmissions.Length == 0 && issues.Length == 1,
            V2CompositionPreparationStatus.MapPending =>
                bundleIdentity is not null && selection is not null &&
                mapResolution?.Status == FirmwareMapResolutionStatus.Pending &&
                capabilityAdmissions.Length == 0 && issues.Length == 0,
            V2CompositionPreparationStatus.MapRejected =>
                bundleIdentity is not null && selection is not null &&
                mapResolution?.Status == FirmwareMapResolutionStatus.Rejected &&
                capabilityAdmissions.Length == 0 && issues.Length == 0,
            V2CompositionPreparationStatus.AdmissionRejected =>
                bundleIdentity is not null && selection is not null &&
                mapResolution?.Status == FirmwareMapResolutionStatus.Unique &&
                capabilityAdmissions.Length == 0 && issues.Length != 0,
            V2CompositionPreparationStatus.Admitted =>
                bundleIdentity is not null && selection is not null &&
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
        TrustedCompositionProfileCatalogEntry selectedProfile,
        FirmwareMapResolutionInputs resolutionInputs)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selectedProfile);
        ArgumentNullException.ThrowIfNull(resolutionInputs);
        if (!catalog.OwnsProfile(selectedProfile))
        {
            return V2CompositionPreparationResult.SelectionWasRejected();
        }

        var profileMapIds = selectedProfile.Profile.MapBinding.MapIds.ToHashSet(StringComparer.Ordinal);
        var deferredInspectionStructureIds = selectedProfile.Profile.MetadataBindings
            .Select(static binding => binding.StructureId)
            .ToHashSet(StringComparer.Ordinal);
        var requiredMetadataStructureIds =
            selectedProfile.Profile.MapBinding.RequiredMetadataStructureIds
                .Where(structureId => !deferredInspectionStructureIds.Contains(structureId))
                .ToHashSet(StringComparer.Ordinal);
        FirmwareMapResolutionResult mapResolution = selectedProfile.Family.Family.ResolveMapWithinForProfile(
            resolutionInputs,
            profileMapIds,
            requiredMetadataStructureIds);
        switch (mapResolution.Status)
        {
            case FirmwareMapResolutionStatus.Pending:
                return V2CompositionPreparationResult.MapIsPending(
                    catalog.BundleIdentity,
                    selectedProfile,
                    mapResolution);
            case FirmwareMapResolutionStatus.Rejected:
                return V2CompositionPreparationResult.MapWasRejected(
                    catalog.BundleIdentity,
                    selectedProfile,
                    mapResolution);
            case FirmwareMapResolutionStatus.Unique:
                IReadOnlyList<CompositionIssue> admissionIssues = CompositionProfileMapAdmissionValidator.Validate(
                    selectedProfile.Profile,
                    selectedProfile.Family.Family,
                    mapResolution.ResolvedMap!,
                    out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions);
                return admissionIssues.Count == 0
                    ? V2CompositionPreparationResult.Admitted(
                        catalog.BundleIdentity,
                        selectedProfile,
                        mapResolution,
                        capabilityAdmissions)
                    : V2CompositionPreparationResult.AdmissionWasRejected(
                        catalog.BundleIdentity,
                        selectedProfile,
                        mapResolution,
                        admissionIssues);
            default:
                throw new InvalidOperationException("Unknown firmware map resolution status.");
        }
    }
}
