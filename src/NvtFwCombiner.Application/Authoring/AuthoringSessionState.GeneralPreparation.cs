namespace NvtFwCombiner.Application.Authoring;

public sealed partial class AuthoringSessionState
{
    private object? _generalPreparationLease;

    private AuthoringSessionState(AuthoringSessionState accepted)
    {
        WorkflowId = accepted.WorkflowId;
        _transitionLock = accepted._transitionLock;
        _catalog = accepted._catalog;
        _current = accepted._current;
    }

    /// <summary>Renews request ownership before any progress callback or file capture.</summary>
    internal (object Lease, AuthoringSessionState Candidate) BeginGeneralPreparation()
    {
        lock (_transitionLock)
        {
            _generalPreparationLease = new object();
            // Reuse the same transitions and lock on a call-local candidate. Only
            // adoption writes the accepted state, preserving its original revisions.
            return (_generalPreparationLease, new AuthoringSessionState(this));
        }
    }

    /// <summary>Rejects cancellation or supersession without touching accepted state.</summary>
    internal AuthoringSessionIssue? CheckGeneralPreparation(
        object lease, CancellationToken cancellationToken)
    {
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
        object lease, ActiveSessionSnapshot? expected, AuthoringSessionState candidate,
        CancellationToken cancellationToken)
    {
        lock (_transitionLock)
        {
            AuthoringSessionIssue? issue = CheckGeneralPreparation(lease, cancellationToken);
            if (issue is not null)
            {
                return new AuthoringSessionTransitionResult(_current, issue);
            }
            if (!ReferenceEquals(_current, expected))
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
