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
    private readonly CompositionIssue[] _issues;

    private V2CompositionPreparationResult(
        V2CompositionPreparationStatus status,
        TrustedProfileBundleCatalog.ProfileSelection? selection,
        FirmwareMapResolutionResult? mapResolution,
        CompositionProfileMapAdmission? admission,
        IEnumerable<CompositionIssue> issues)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown V2 composition preparation status.");
        }

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
        ValidatePayload(status, selection, mapResolution, admission, _issues);
        Status = status;
        Selection = selection;
        MapResolution = mapResolution;
        Admission = admission;
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Closed pre-plan status; no status grants runtime execution authority.</summary>
    internal V2CompositionPreparationStatus Status { get; }

    /// <summary>Exact selection retained once catalog identity was accepted; otherwise null.</summary>
    internal TrustedProfileBundleCatalog.ProfileSelection? Selection { get; }

    /// <summary>Unchanged Domain resolver outcome after selection acceptance; otherwise null.</summary>
    internal FirmwareMapResolutionResult? MapResolution { get; }

    /// <summary>Existing admitted physical/profile context only for an admitted preparation.</summary>
    internal CompositionProfileMapAdmission? Admission { get; }

    /// <summary>Selection or admission errors; map pending/rejection remains in the typed resolver result.</summary>
    internal IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>True only when exact selection, map resolution, and admission all succeeded.</summary>
    internal bool IsAdmitted => Status == V2CompositionPreparationStatus.Admitted;

    internal static V2CompositionPreparationResult SelectionWasRejected()
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.SelectionRejected,
            selection: null,
            mapResolution: null,
            admission: null,
            [new CompositionIssue(SelectionStale, "The selected trusted profile no longer belongs to this catalog.")]);
    }

    internal static V2CompositionPreparationResult MapIsPending(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        FirmwareMapResolutionResult mapResolution)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.MapPending,
            selection,
            mapResolution,
            admission: null,
            []);
    }

    internal static V2CompositionPreparationResult MapWasRejected(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        FirmwareMapResolutionResult mapResolution)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.MapRejected,
            selection,
            mapResolution,
            admission: null,
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
            mapResolution,
            admission: null,
            issues);
    }

    internal static V2CompositionPreparationResult Admitted(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        FirmwareMapResolutionResult mapResolution,
        CompositionProfileMapAdmission admission)
    {
        return new V2CompositionPreparationResult(
            V2CompositionPreparationStatus.Admitted,
            selection,
            mapResolution,
            admission,
            []);
    }

    private static void ValidatePayload(
        V2CompositionPreparationStatus status,
        TrustedProfileBundleCatalog.ProfileSelection? selection,
        FirmwareMapResolutionResult? mapResolution,
        CompositionProfileMapAdmission? admission,
        CompositionIssue[] issues)
    {
        bool valid = status switch
        {
            V2CompositionPreparationStatus.SelectionRejected =>
                selection is null && mapResolution is null && admission is null && issues.Length == 1,
            V2CompositionPreparationStatus.MapPending =>
                selection is not null && mapResolution?.Status == FirmwareMapResolutionStatus.Pending && admission is null && issues.Length == 0,
            V2CompositionPreparationStatus.MapRejected =>
                selection is not null && mapResolution?.Status == FirmwareMapResolutionStatus.Rejected && admission is null && issues.Length == 0,
            V2CompositionPreparationStatus.AdmissionRejected =>
                selection is not null && mapResolution?.Status == FirmwareMapResolutionStatus.Unique && admission is null && issues.Length != 0,
            V2CompositionPreparationStatus.Admitted =>
                selection is not null &&
                mapResolution is { Status: FirmwareMapResolutionStatus.Unique, ResolvedMap: { } resolvedMap } &&
                admission is not null &&
                ReferenceEquals(resolvedMap, admission.ResolvedMap) &&
                issues.Length == 0,
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

        FirmwareMapResolutionResult mapResolution = profile.Family.Family.ResolveMap(request.ResolutionInputs);
        switch (mapResolution.Status)
        {
            case FirmwareMapResolutionStatus.Pending:
                return V2CompositionPreparationResult.MapIsPending(request.Selection, mapResolution);
            case FirmwareMapResolutionStatus.Rejected:
                return V2CompositionPreparationResult.MapWasRejected(request.Selection, mapResolution);
            case FirmwareMapResolutionStatus.Unique:
                CompositionProfileMapAdmissionResult admission = CompositionProfileMapAdmissionValidator.Validate(
                    profile.Profile,
                    profile.Family.Family,
                    mapResolution.ResolvedMap!);
                return admission.IsAdmitted
                    ? V2CompositionPreparationResult.Admitted(
                        request.Selection,
                        mapResolution,
                        admission.Admission!)
                    : V2CompositionPreparationResult.AdmissionWasRejected(
                        request.Selection,
                        mapResolution,
                        admission.Issues);
            default:
                throw new InvalidOperationException("Unknown firmware map resolution status.");
        }
    }
}
