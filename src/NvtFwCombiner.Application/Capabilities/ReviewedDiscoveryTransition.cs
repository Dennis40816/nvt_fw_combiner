using System.Collections.ObjectModel;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>One reviewed exact route admitted from a pre-compilation discovery binding.</summary>
public sealed record ReviewedDiscoveryExactMember(
    string RouteId,
    string CapabilityFingerprint);

/// <summary>
/// Canonical-publication proof for the closed exact-route set that one
/// pre-compilation discovery binding may resolve after its prerequisite is read.
/// </summary>
public sealed class ReviewedDiscoveryTransition
{
    private readonly ReviewedDiscoveryExactMember[] _allowedExactMembers;

    internal ReviewedDiscoveryTransition(
        ResolutionToken resolutionToken,
        string workflowId,
        string icId,
        string icCountVariant,
        ReviewedDiscoveryExactMember discoveryMember,
        string prerequisiteSlotId,
        IEnumerable<ReviewedDiscoveryExactMember> allowedExactMembers)
    {
        ResolutionToken = resolutionToken;
        WorkflowId = workflowId;
        IcId = icId;
        IcCountVariant = icCountVariant;
        DiscoveryMember = discoveryMember;
        PrerequisiteSlotId = prerequisiteSlotId;
        _allowedExactMembers =
        [
            .. allowedExactMembers
                .Distinct()
                .OrderBy(static member => member.RouteId, StringComparer.Ordinal)
                .ThenBy(static member => member.CapabilityFingerprint, StringComparer.Ordinal),
        ];
        AllowedExactMembers = Array.AsReadOnly(_allowedExactMembers);
    }

    /// <summary>Canonical publication shared by discovery and every allowed exact member.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Workflow owning the reviewed transition.</summary>
    public string WorkflowId { get; }

    /// <summary>IC axis fixed by the reviewed transition.</summary>
    public string IcId { get; }

    /// <summary>IC Count axis fixed by the reviewed transition.</summary>
    public string IcCountVariant { get; }

    /// <summary>Reviewed route and capability used before exact compilation.</summary>
    public ReviewedDiscoveryExactMember DiscoveryMember { get; }

    /// <summary>Compiler-owned input that resolves the exact member.</summary>
    public string PrerequisiteSlotId { get; }

    /// <summary>Closed reviewed exact-route set published by the canonical catalog.</summary>
    public ReadOnlyCollection<ReviewedDiscoveryExactMember> AllowedExactMembers { get; }

    internal bool Allows(string routeId, string capabilityFingerprint)
    {
        return _allowedExactMembers.Contains(new ReviewedDiscoveryExactMember(
            routeId,
            capabilityFingerprint));
    }

    internal bool Matches(ReviewedDiscoveryTransition? other)
    {
        return other is not null &&
            ResolutionToken == other.ResolutionToken &&
            StringComparer.Ordinal.Equals(WorkflowId, other.WorkflowId) &&
            StringComparer.Ordinal.Equals(IcId, other.IcId) &&
            StringComparer.Ordinal.Equals(IcCountVariant, other.IcCountVariant) &&
            Equals(DiscoveryMember, other.DiscoveryMember) &&
            StringComparer.Ordinal.Equals(PrerequisiteSlotId, other.PrerequisiteSlotId) &&
            _allowedExactMembers.SequenceEqual(other._allowedExactMembers);
    }
}
