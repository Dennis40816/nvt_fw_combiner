using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Resolves run-dependent capability admission for one catalog-owned profile and map.</summary>
internal static class CompositionProfileMapAdmissionValidator
{
    private const string RequiredCapabilityMissing = "profile.v2.map.required-capability-missing";
    private const string RequiredCapabilityAbsent = "profile.v2.map.required-capability-absent";
    private const string RequiredCapabilityUnknown = "profile.v2.map.required-capability-unknown";
    private const string RequiredCapabilityApplicabilityUnavailable = "profile.v2.map.required-capability-applicability-unavailable";
    private const string RequiredCapabilityAmbiguous = "profile.v2.map.required-capability-ambiguous";

    /// <summary>
    /// Resolves the required capabilities whose applicability depends on the selected map.
    /// Static family, map, region, and metadata facts were already admitted atomically by
    /// <see cref="TrustedProfileBundleCatalogFactory"/>.
    /// </summary>
    internal static IReadOnlyList<CompositionIssue> Validate(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(resolvedMap);

        var issues = new List<CompositionIssue>();
        CompiledCapabilityAdmission[] capabilities = ResolveRequiredCapabilities(
            profile.MapBinding,
            family,
            resolvedMap,
            issues);
        issues.Sort(static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            return code != 0
                ? code
                : StringComparer.Ordinal.Compare(left.Message, right.Message);
        });

        capabilityAdmissions = issues.Count == 0
            ? Array.AsReadOnly(capabilities)
            : [];
        return issues.AsReadOnly();
    }

    private static CompiledCapabilityAdmission[] ResolveRequiredCapabilities(
        CompositionProfileMapBinding binding,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues)
    {
        var admitted = new List<CompiledCapabilityAdmission>();
        foreach (string requiredCapabilityId in binding.RequiredCapabilityIds)
        {
            FirmwareMapFactBinding<FirmwareCapabilityFact>[] candidates =
            [
                .. family.CapabilityBindings.Where(candidate =>
                    StringComparer.Ordinal.Equals(candidate.EffectiveKey.MemberId, resolvedMap.MemberId) &&
                    StringComparer.Ordinal.Equals(candidate.EffectiveKey.MapId, resolvedMap.ImageMap.MapId) &&
                    StringComparer.Ordinal.Equals(candidate.Value.CapabilityId, requiredCapabilityId)),
            ];
            FirmwareMapFactBinding<FirmwareCapabilityFact>[] applicable =
            [
                .. candidates.Where(candidate =>
                    candidate.Applicability.Evaluate(resolvedMap) == FirmwareApplicabilityResult.Match),
            ];
            if (applicable.Length > 1)
            {
                issues.Add(new CompositionIssue(
                    RequiredCapabilityAmbiguous,
                    $"Required capability '{requiredCapabilityId}' has multiple applicable evidence bindings."));
                continue;
            }

            if (applicable.Length == 1)
            {
                FirmwareMapFactBinding<FirmwareCapabilityFact> capability = applicable[0];
                switch (capability.Value.State)
                {
                    case FirmwareCapabilityState.ConfirmedPresent:
                        admitted.Add(new CompiledCapabilityAdmission(requiredCapabilityId, capability));
                        break;
                    case FirmwareCapabilityState.ConfirmedAbsent:
                        issues.Add(new CompositionIssue(
                            RequiredCapabilityAbsent,
                            $"Required capability '{requiredCapabilityId}' is confirmed absent."));
                        break;
                    case FirmwareCapabilityState.Unknown:
                        issues.Add(new CompositionIssue(
                            RequiredCapabilityUnknown,
                            $"Required capability '{requiredCapabilityId}' has unknown evidence state."));
                        break;
                    default:
                        throw new InvalidOperationException("Unknown firmware capability state.");
                }

                continue;
            }

            if (candidates.Any(candidate =>
                candidate.Applicability.Evaluate(resolvedMap) == FirmwareApplicabilityResult.Pending))
            {
                issues.Add(new CompositionIssue(
                    RequiredCapabilityApplicabilityUnavailable,
                    $"Required capability '{requiredCapabilityId}' cannot be evaluated from the resolved-map selection."));
                continue;
            }

            issues.Add(new CompositionIssue(
                RequiredCapabilityMissing,
                $"Required capability '{requiredCapabilityId}' has no applicable evidence binding."));
        }

        return [.. admitted];
    }
}
