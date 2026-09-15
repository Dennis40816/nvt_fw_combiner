using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Unadmitted AB declarations from one profile; not a compiled contract or execution authority.</summary>
public sealed class CanonicalAbAuthoringDefinition
{
    /// <summary>Snapshots declarations. Shape validation does not establish trust or publication authority.</summary>
    public CanonicalAbAuthoringDefinition(string profileId, string profileVersion, string bundleContentHash,
        FirmwareFamilyResolutionDefinition family, IEnumerable<CompiledAuthoringInputBinding> inputBindings,
        CompiledAuthoringInputBinding? tpAInputBinding, CompiledAuthoringInputBinding? tpBInputBinding,
        IEnumerable<string> selectionGroupMemberSlotIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(bundleContentHash);
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(inputBindings);
        ArgumentNullException.ThrowIfNull(selectionGroupMemberSlotIds);
        CompiledAuthoringInputBinding[] bindings = [.. inputBindings];
        string[] members = [.. selectionGroupMemberSlotIds];
        if (!CapabilityRouteIdentity.IsSha256(bundleContentHash) || bindings.Length == 0 ||
            bindings.Any(static binding => binding is null || string.IsNullOrWhiteSpace(binding.SlotId) ||
                string.IsNullOrWhiteSpace(binding.AddressSpaceId) || binding.RequiredEndExclusive is not null ||
                binding.ExpectedOuterLengths is not null) ||
            bindings.Select(static binding => binding.SlotId).Distinct(StringComparer.Ordinal).Count() != bindings.Length ||
            bindings.Select(static binding => binding.AddressSpaceId).Distinct(StringComparer.Ordinal).Count() != bindings.Length ||
            members.Any(member => string.IsNullOrWhiteSpace(member) || !bindings.Any(binding => binding.SlotId == member)) ||
            members.Distinct(StringComparer.Ordinal).Count() != members.Length)
        {
            throw new ArgumentException("AB declarations require unique unresolved input bindings and valid group membership.");
        }

        bool validPrimary = family.AbFormatPolicy is null
            ? tpAInputBinding is null && tpBInputBinding is null
            : tpAInputBinding is not null && tpBInputBinding is not null &&
                !ReferenceEquals(tpAInputBinding, tpBInputBinding) &&
                bindings.Any(binding => ReferenceEquals(binding, tpAInputBinding)) &&
                bindings.Any(binding => ReferenceEquals(binding, tpBInputBinding));
        if (!validPrimary)
        {
            throw new ArgumentException("A declared AB format policy requires two distinct primary references from this input binding list.");
        }

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        BundleContentHash = bundleContentHash;
        Family = family;
        InputBindings = Array.AsReadOnly(bindings);
        TpAInputBinding = tpAInputBinding;
        TpBInputBinding = tpBInputBinding;
        Array.Sort(members, StringComparer.Ordinal);
        SelectionGroupMemberSlotIds = Array.AsReadOnly(members);
    }

    /// <summary>Actual source profile identity.</summary>
    public string ProfileId { get; }
    /// <summary>Actual source profile version.</summary>
    public string ProfileVersion { get; }
    /// <summary>Actual source bundle identity, checked against current publication by the compiler.</summary>
    public string BundleContentHash { get; }
    /// <summary>Canonical family reference bound by that source profile.</summary>
    public FirmwareFamilyResolutionDefinition Family { get; }
    /// <summary>Input membership only; map-dependent geometry is unresolved.</summary>
    public IReadOnlyList<CompiledAuthoringInputBinding> InputBindings { get; }
    /// <summary>TP-A primary's profile input binding; null only when no format policy exists.</summary>
    public CompiledAuthoringInputBinding? TpAInputBinding { get; }
    /// <summary>TP-B primary's profile input binding; null only when no format policy exists.</summary>
    public CompiledAuthoringInputBinding? TpBInputBinding { get; }
    /// <summary>Profile selection-group membership, not inferred from slot names or roles.</summary>
    public IReadOnlyList<string> SelectionGroupMemberSlotIds { get; }

    internal bool Matches(ResolvedCapabilityRoute route)
    {
        CanonicalCapabilityCompilationContract contract = route.CompilationContract;
        return ProfileId == contract.ProfileId && ProfileVersion == contract.ProfileVersion &&
            BundleContentHash == contract.TrustedDefinitionSha256 &&
            SelectionGroupMemberSlotIds.SequenceEqual(contract.SemanticBindingIds, StringComparer.Ordinal) &&
            contract.AllowedMapVariantIds.All(mapId => Family.ImageMaps.Any(map => map.MapId == mapId &&
                map.Applicability.MemberIds.Contains(route.Identity.IcId, StringComparer.Ordinal))) &&
            (Family.AbFormatPolicy is null || Family.AbFormatPolicy.Variants.Any(variant => variant.MemberId == route.Identity.IcId));
    }
}
