using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    private System.Collections.ObjectModel.ReadOnlyCollection<FirmwareFullImageMetadataView>? SnapshotFullImageMetadataViews(
        IEnumerable<FirmwareFullImageMetadataView>? views)
    {
        if (views is null)
        {
            return null;
        }

        FirmwareFullImageMetadataView[] snapshot = ImmutableReferenceSnapshot.CreateUnique(
            views, static view => view.ViewId,
            "Full-image metadata views cannot contain null.",
            "Full-image metadata view ids must be unique.", StringComparer.Ordinal);
        var selections = new HashSet<(string MapId, string MemberId)>();
        foreach (FirmwareFullImageMetadataView view in snapshot)
        {
            DomainInvariant.Reject(!_imageMaps.Any(map => ReferenceEquals(map, view.ImageMap)),
                "Full-image metadata views must reference an exact canonical family map.", nameof(views));
            foreach (string memberId in view.MemberIds)
            {
                DomainInvariant.Reject(!selections.Add((view.ImageMap.MapId, memberId)),
                    "Full-image metadata views cannot overlap for the same map and member.", nameof(views));
            }

            var structures = view.MetadataBindings.Select(static binding => binding.Structure.StructureId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (FirmwareFullImageMetadataBinding binding in view.MetadataBindings)
            {
                // ValidateCandidate has already checked the canonical graph and its fields/ranges.
                // Closure of every direct edge guarantees transitive containment without another resolver.
                DomainInvariant.Reject(binding.Structure.Locator is FirmwareMetadataFieldSelectedLocator selected &&
                    !structures.Contains(selected.PrerequisiteStructureId),
                    "Full-image metadata bindings must include every canonical prerequisite.", nameof(views));
            }
        }

        Array.Sort(snapshot, static (left, right) => StringComparer.Ordinal.Compare(left.ViewId, right.ViewId));
        return Array.AsReadOnly(snapshot);
    }
}
