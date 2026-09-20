using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Exact read-only family view retained even when its metadata plan is explicitly empty.</summary>
public sealed class CanonicalFullImageMetadataContext
{
    /// <summary>Retains canonical references and both trusted source hashes without execution authority.</summary>
    public CanonicalFullImageMetadataContext(
        FirmwareFamilyResolutionDefinition family,
        FirmwareFullImageMetadataView view,
        string memberId,
        string trustedBundleSha256)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        if (family.FullImageMetadataViews is null ||
            !family.FullImageMetadataViews.Any(candidate => ReferenceEquals(candidate, view)) ||
            !view.MemberIds.Contains(memberId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Full-image metadata requires an exact canonical family view and member.", nameof(view));
        }

        Family = family;
        View = view;
        MemberId = memberId;
        SourceIdentity = MetadataPlanSourceIdentity.ForFullImageView(
            family.FamilyId, family.FamilyVersion, family.FamilyContentHash,
            view.ViewId, trustedBundleSha256);
    }

    /// <summary>Canonical family which owns the view.</summary>
    public FirmwareFamilyResolutionDefinition Family { get; }

    /// <summary>Exact canonical map, member set and selected bindings.</summary>
    public FirmwareFullImageMetadataView View { get; }

    /// <summary>Exact selected family member.</summary>
    public string MemberId { get; }

    /// <summary>Family/view identity with trusted family and bundle hashes.</summary>
    public MetadataPlanSourceIdentity SourceIdentity { get; }
}
