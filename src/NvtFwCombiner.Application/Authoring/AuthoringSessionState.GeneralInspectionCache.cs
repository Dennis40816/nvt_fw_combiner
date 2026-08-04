namespace NvtFwCombiner.Application.Authoring;

public sealed partial class AuthoringSessionState
{
    /// <summary>Retains pre-binding content identity without claiming terminal health.</summary>
    public AuthoringPublicationResult TryCacheGeneralSelectedFileInspection(
        AuthoringPublicationLease lease,
        GeneralSelectedFileInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(inspection);
        lock (_transitionLock)
        {
            if (_current is null || lease.Kind != AuthoringDerivedResultKind.Inspection ||
                !LeaseMatches(lease, _current, _publicationIdentity) ||
                lease.AuthoringRevision != inspection.AuthoringRevision ||
                !lease.Slots.Any(slot =>
                    StringComparer.Ordinal.Equals(slot.DefinitionId, inspection.DefinitionId) &&
                    StringComparer.Ordinal.Equals(slot.SelectedPath, inspection.SelectedPathHint)))
            {
                return PublicationFailure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected-file content cache belongs to older authoring state.");
            }

            _generalInspectionCache[inspection.DefinitionId] =
                new CachedGeneralSelectedFileInspection(
                    _current.ResolutionToken,
                    _current.CapabilityFingerprint,
                    inspection);
            return new AuthoringPublicationResult(true, null);
        }
    }

    /// <summary>Returns reusable content only for the current capability, token, definition, and path.</summary>
    public bool TryGetCachedGeneralSelectedFileInspection(
        string definitionId,
        string selectedPath,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out GeneralSelectedFileInspection? inspection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        lock (_transitionLock)
        {
            _ = _generalInspectionCache.TryGetValue(
                definitionId,
                out CachedGeneralSelectedFileInspection? cached);
            bool current = _current is not null && cached is not null &&
                cached.ResolutionToken == _current.ResolutionToken &&
                StringComparer.Ordinal.Equals(
                    cached.CapabilityFingerprint, _current.CapabilityFingerprint) &&
                StringComparer.Ordinal.Equals(
                    cached.Inspection.SelectedPathHint, selectedPath);
            inspection = current ? cached!.Inspection : null;
            return current;
        }
    }
}
