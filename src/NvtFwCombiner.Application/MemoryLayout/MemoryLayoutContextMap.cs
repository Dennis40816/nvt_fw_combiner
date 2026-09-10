using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>Exact trusted Standard counterpart for read-only CtrlRAM location context, not Report or execution authority.</summary>
public sealed class MemoryLayoutContextMap
{
    private const string BindingPrefix = "memory-layout-context-";

    /// <summary>Retains an already-materialized map and its exact trusted source identity.</summary>
    public MemoryLayoutContextMap(string icId, string profileId, string profileVersion,
        string trustedDefinitionSha256, FirmwareImageMap map)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(trustedDefinitionSha256);
        ArgumentNullException.ThrowIfNull(map);
        if (!CapabilityRouteIdentity.IsSha256(trustedDefinitionSha256) ||
            !map.Applicability.MemberIds.Contains(icId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Memory context requires a trusted hash and a map applicable to the exact IC.");
        }
        IcId = icId;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        TrustedDefinitionSha256 = trustedDefinitionSha256;
        Map = map;
        SemanticBindingIds = Array.AsReadOnly<string>(
        [
            $"{BindingPrefix}ic:{icId}",
            $"{BindingPrefix}profile:{profileId}@{profileVersion}",
            $"{BindingPrefix}bundle:{trustedDefinitionSha256}",
            $"{BindingPrefix}map:{map.MapId}",
            $"{BindingPrefix}space:{map.AddressSpaceId}",
        ]);
    }

    /// <summary>Exact registered member, including an explicitly declared family alias.</summary>
    public string IcId { get; }
    /// <summary>Source Standard profile identity.</summary>
    public string ProfileId { get; }
    /// <summary>Source Standard profile version.</summary>
    public string ProfileVersion { get; }
    /// <summary>Trusted source bundle identity.</summary>
    public string TrustedDefinitionSha256 { get; }
    /// <summary>Canonical immutable map; never a second geometry definition.</summary>
    public FirmwareImageMap Map { get; }
    /// <summary>Exact source bindings included in the published dynamic capability fingerprint.</summary>
    public IReadOnlyList<string> SemanticBindingIds { get; }

    internal static void ValidateBinding(CapabilityRouteIdentity identity,
        CanonicalCapabilityCompilationContract contract, MemoryLayoutContextMap? context)
    {
        string[] actual = [.. contract.SemanticBindingIds.Where(static value =>
            value.StartsWith(BindingPrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        IEnumerable<string> expected = context?.SemanticBindingIds ?? [];
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            (context is not null && (identity.WorkflowId != ExperienceIds.CtrlRamReplace || identity.IcId != context.IcId)))
        {
            throw new ArgumentException("Memory context must match the exact CtrlRAM capability and fingerprint bindings.");
        }
    }
}
