using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>
/// Immutable metadata references supplied by one captured full-image artifact.
/// Canonical artifact bindings retain their identities; this declaration never remaps addresses.
/// </summary>
public sealed class FirmwareFullImageMetadataView
{
    internal FirmwareFullImageMetadataView(
        string viewId,
        FirmwareImageMap imageMap,
        IEnumerable<string> memberIds,
        IEnumerable<FirmwareFullImageMetadataBinding> metadataBindings,
        IEnumerable<string> evidenceRefs)
    {
        ViewId = RequiredValue.NotBlank(viewId);
        ImageMap = RequiredValue.NotNull(imageMap);
        MemberIds = Array.AsReadOnly(SnapshotIds(memberIds, nameof(memberIds)));
        EvidenceRefs = Array.AsReadOnly(SnapshotIds(evidenceRefs, nameof(evidenceRefs)));
        FirmwareFullImageMetadataBinding[] bindings = ImmutableReferenceSnapshot.CreateUnique(
            metadataBindings,
            static binding => binding.BindingId,
            "Full-image metadata bindings cannot contain null.",
            "Full-image metadata binding ids must be unique.",
            StringComparer.Ordinal);
        DomainInvariant.Reject(
            bindings.Select(static binding => binding.Structure.StructureId).Distinct(StringComparer.Ordinal).Count() != bindings.Length,
            "Full-image metadata structures must be unique.", nameof(metadataBindings));
        foreach (string memberId in MemberIds)
        {
            DomainInvariant.Reject(!imageMap.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal),
                "Full-image metadata members must belong to the exact map.", nameof(memberIds));
            foreach (FirmwareFullImageMetadataBinding binding in bindings)
            {
                DomainInvariant.Reject(imageMap.MetadataSetBindings.Count(candidate =>
                    StringComparer.Ordinal.Equals(candidate.EffectiveKey.MemberId, memberId) &&
                    candidate.Value.Structures.Any(structure => ReferenceEquals(structure, binding.Structure))) != 1,
                    "Full-image metadata must reference one canonical structure binding for each member.", nameof(metadataBindings));
            }
        }

        Array.Sort(bindings, static (left, right) => StringComparer.Ordinal.Compare(left.BindingId, right.BindingId));
        MetadataBindings = Array.AsReadOnly(bindings);
    }

    /// <summary>Stable view identity.</summary>
    public string ViewId { get; }

    /// <summary>Exact canonical image map, never a reconstructed equivalent.</summary>
    public FirmwareImageMap ImageMap { get; }

    /// <summary>Exact members to which this declaration applies.</summary>
    public IReadOnlyList<string> MemberIds { get; }

    /// <summary>Canonical metadata targets; an empty list is an explicitly empty view.</summary>
    public IReadOnlyList<FirmwareFullImageMetadataBinding> MetadataBindings { get; }

    /// <summary>Evidence supporting this declaration.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

    internal static string[] SnapshotIds(IEnumerable<string> values, string parameterName)
    {
        return ImmutableStringSnapshot.Create(values, parameterName,
            "At least one identifier is required.", "Identifiers cannot be blank.", "Identifiers must be unique.");
    }
}

/// <summary>Read-only targets in one exact canonical structure supplied by the captured full image.</summary>
public sealed class FirmwareFullImageMetadataBinding
{
    internal FirmwareFullImageMetadataBinding(
        string bindingId,
        FirmwareMetadataStructure structure,
        IEnumerable<FirmwareMetadataReferenceTarget> targetReferences,
        IEnumerable<string> evidenceRefs)
    {
        BindingId = RequiredValue.NotBlank(bindingId);
        Structure = RequiredValue.NotNull(structure);
        FirmwareMetadataReferenceTarget[] targets = ImmutableReferenceSnapshot.Create(
            targetReferences, "Full-image metadata targets cannot contain null.");
        DomainInvariant.Reject(targets.Length == 0 || targets.Distinct().Count() != targets.Length,
            "Full-image metadata targets must be nonempty and unique.", nameof(targetReferences));
        DomainInvariant.Reject(targets.Any(target => !structure.Definition.ContainsReferenceTarget(target)),
            "Full-image metadata targets must belong to the canonical definition.", nameof(targetReferences));
        Array.Sort(targets, static (left, right) =>
        {
            int kind = left.Kind.CompareTo(right.Kind);
            return kind != 0 ? kind : StringComparer.Ordinal.Compare(left.TargetId, right.TargetId);
        });
        TargetReferences = Array.AsReadOnly(targets);
        EvidenceRefs = Array.AsReadOnly(FirmwareFullImageMetadataView.SnapshotIds(evidenceRefs, nameof(evidenceRefs)));
    }

    /// <summary>Stable binding identity within a view.</summary>
    public string BindingId { get; }

    /// <summary>Exact canonical located structure, including its unchanged artifact identity.</summary>
    public FirmwareMetadataStructure Structure { get; }

    /// <summary>Checked references into the structure's canonical definition.</summary>
    public IReadOnlyList<FirmwareMetadataReferenceTarget> TargetReferences { get; }

    /// <summary>Evidence supporting the selected targets.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }
}
