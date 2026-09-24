using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>A map choice only; it grants no artifact, session or execution admission.</summary>
internal sealed record AbFormatMapSelection(string MapId, string FormatId, string DisplayName);

/// <summary>One map choice or immutable selection issues.</summary>
internal sealed class AbFormatMapResolutionResult(
    AbFormatMapSelection? selection, IEnumerable<CompositionIssue> issues)
{
    internal AbFormatMapSelection? Selection { get; } = selection;
    internal IReadOnlyList<CompositionIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
}

/// <summary>Shared format/map selection over canonical declarations and caller-validated observations.</summary>
internal static class AbFormatMapResolver
{
    /// <summary>
    /// The caller admits configuration against this family and validates both primary artifacts and
    /// topology from the same capture before calling. Observed counts are not selector defaults.
    /// The caller retains provenance and terminal admission; this resolver owns no mutable state.
    /// </summary>
    internal static AbFormatMapResolutionResult Resolve(
        FirmwareFamilyResolutionDefinition family,
        string memberId,
        EventBufferFormatConfiguration configuration,
        byte rawA,
        byte rawB,
        TopologySelection? selectedTopology,
        int? observedCountA,
        int? observedCountB)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(configuration);
        FirmwareAbFormatPolicy policy = family.AbFormatPolicy ??
            throw new ArgumentException("The caller must provide a declared AB format policy.", nameof(family));

        EventBufferFormatEntry? formatA = configuration.Match(policy.ScopeId, rawA);
        EventBufferFormatEntry? formatB = configuration.Match(policy.ScopeId, rawB);
        string formatId = formatA?.UniqueId ?? policy.CommonFormatId;
        if (formatId != (formatB?.UniqueId ?? policy.CommonFormatId))
        {
            return Blocked("AB_FORMAT_MISMATCH", "TPA and TPB resolve to different Event Buffer Formats.");
        }

        FirmwareImageMap[] variants = [.. policy.Variants
            .Where(variant => variant.MemberId == memberId && variant.FormatId == formatId)
            .Select(variant => family.ImageMaps.Single(map => map.MapId == variant.MapId))];
        FirmwareImageMap[] baselines = [.. variants.Where(map =>
            map.Applicability.TopologyRequirement.Kind != TopologyRequirementKind.ExactCount &&
            map.Applicability.TopologyRequirement.Matches(selectedTopology))];
        if (baselines.Length != 1)
        {
            return Blocked("AB_FORMAT_MAP_UNAVAILABLE", "No unique AB baseline matches the selected format and topology.");
        }

        FirmwareImageMap[] exact = [.. variants.Where(map =>
            map.Applicability.TopologyRequirement.Kind == TopologyRequirementKind.ExactCount &&
            observedCountA is not null && observedCountB is not null &&
            map.Applicability.TopologyRequirement.ExactChipCount == observedCountA &&
            map.Applicability.TopologyRequirement.ExactChipCount == observedCountB)];
        if (exact.Length > 1)
        {
            return Blocked("AB_FORMAT_MAP_UNAVAILABLE", "More than one exact-count AB map matches these TP inputs.");
        }

        FirmwareImageMap selected = exact.Length == 1 ? exact[0] : baselines[0];
        return new(new(selected.MapId, formatId, formatA?.DisplayName ?? policy.CommonDisplayName), []);
    }

    private static AbFormatMapResolutionResult Blocked(string code, string message)
    {
        return new(null, [new(code, message)]);
    }
}
