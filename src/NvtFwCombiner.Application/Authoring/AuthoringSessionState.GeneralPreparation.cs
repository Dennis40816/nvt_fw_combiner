namespace NvtFwCombiner.Application.Authoring;

public sealed partial class AuthoringSessionState
{
    private GeneralPreparationLease? _generalPreparationLease;

    /// <summary>One request's identity and the accepted snapshot it began from.</summary>
    internal sealed class GeneralPreparationLease
    {
        internal GeneralPreparationLease(ActiveSessionSnapshot? expectedSnapshot)
        {
            ExpectedSnapshot = expectedSnapshot;
        }

        internal ActiveSessionSnapshot? ExpectedSnapshot { get; }
    }

    private AuthoringSessionState(AuthoringSessionState accepted)
    {
        WorkflowId = accepted.WorkflowId;
        _transitionLock = accepted._transitionLock;
        _catalog = accepted._catalog;
        _current = accepted._current;
    }

    /// <summary>Renews request ownership before any progress callback or file capture.</summary>
    internal (GeneralPreparationLease Lease, AuthoringSessionState Candidate) BeginGeneralPreparation()
    {
        lock (_transitionLock)
        {
            _generalPreparationLease = new GeneralPreparationLease(_current);
            // Reuse the same transitions and lock on a call-local candidate. Only
            // adoption writes the accepted state, preserving its original revisions.
            return (_generalPreparationLease, new AuthoringSessionState(this));
        }
    }

    /// <summary>Releases a terminated request without revoking a newer preparation.</summary>
    internal void EndGeneralPreparation(GeneralPreparationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_transitionLock)
        {
            if (ReferenceEquals(lease, _generalPreparationLease))
            {
                _generalPreparationLease = null;
            }
        }
    }

    /// <summary>Rejects cancellation or supersession without touching accepted state.</summary>
    internal AuthoringSessionIssue? CheckGeneralPreparation(
        GeneralPreparationLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_transitionLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReferenceEquals(lease, _generalPreparationLease)
                ? null
                : new AuthoringSessionIssue(AuthoringSessionIssueCodes.StaleInspection,
                    "The General preparation was superseded by a newer request.", WorkflowId);
        }
    }

    /// <summary>Adopts a completed candidate once, only while its request and starting snapshot are current.</summary>
    internal AuthoringSessionTransitionResult TryAdoptGeneralPreparation(
        GeneralPreparationLease lease, AuthoringSessionState candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_transitionLock)
        {
            AuthoringSessionIssue? issue = CheckGeneralPreparation(lease, cancellationToken);
            if (issue is not null)
            {
                return new AuthoringSessionTransitionResult(_current, issue);
            }
            if (!ReferenceEquals(_current, lease.ExpectedSnapshot))
            {
                return Failure(AuthoringSessionIssueCodes.StaleInspection,
                    "The accepted session changed during General preparation.", WorkflowId);
            }

            _generalPreparationLease = null;
            _catalog = candidate._catalog;
            Volatile.Write(ref _current, candidate._current);
            return new AuthoringSessionTransitionResult(_current, null);
        }
    }
}
