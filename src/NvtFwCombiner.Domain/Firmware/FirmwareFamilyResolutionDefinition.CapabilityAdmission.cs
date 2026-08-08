using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    /// <summary>Admits the profile-required capability evidence applicable to one resolved canonical map.</summary>
    internal IReadOnlyList<CompositionIssue> AdmitRequiredCapabilities(
        CompositionProfileMapBinding profileBinding,
        ResolvedFirmwareImageMap resolvedMap,
        out IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> admissions)
    {
        ArgumentNullException.ThrowIfNull(profileBinding);
        ArgumentNullException.ThrowIfNull(resolvedMap);

        List<FirmwareMapFactBinding<FirmwareCapabilityFact>> admitted = [];
        List<CompositionIssue> issues = [];
        foreach (string capabilityId in profileBinding.RequiredCapabilityIds)
        {
            (FirmwareMapFactBinding<FirmwareCapabilityFact> Binding, FirmwareApplicabilityResult Applicability)[] candidates =
            [
                .. CapabilityBindings
                    .Where(candidate =>
                        StringComparer.Ordinal.Equals(candidate.EffectiveKey.MemberId, resolvedMap.MemberId) &&
                        StringComparer.Ordinal.Equals(candidate.EffectiveKey.MapId, resolvedMap.ImageMap.MapId) &&
                        StringComparer.Ordinal.Equals(candidate.Value.CapabilityId, capabilityId))
                    .Select(candidate => (candidate, candidate.Applicability.Evaluate(resolvedMap))),
            ];
            FirmwareMapFactBinding<FirmwareCapabilityFact>[] applicable =
            [
                .. candidates
                    .Where(static candidate => candidate.Applicability == FirmwareApplicabilityResult.Match)
                    .Select(static candidate => candidate.Binding),
            ];
            if (applicable is [FirmwareMapFactBinding<FirmwareCapabilityFact> capability])
            {
                switch (capability.Value.State)
                {
                    case FirmwareCapabilityState.ConfirmedPresent:
                        admitted.Add(capability);
                        continue;
                    case FirmwareCapabilityState.ConfirmedAbsent:
                        AddIssue("profile.v2.map.required-capability-absent", "is confirmed absent");
                        continue;
                    case FirmwareCapabilityState.Unknown:
                        AddIssue("profile.v2.map.required-capability-unknown", "has unknown evidence state");
                        continue;
                    default:
                        throw new InvalidOperationException("Unknown firmware capability state.");
                }
            }

            if (applicable.Length > 1)
            {
                AddIssue("profile.v2.map.required-capability-ambiguous", "has multiple applicable evidence bindings");
            }
            else if (candidates.Any(static candidate => candidate.Applicability == FirmwareApplicabilityResult.Pending))
            {
                AddIssue(
                    "profile.v2.map.required-capability-applicability-unavailable",
                    "cannot be evaluated from the resolved-map selection");
            }
            else
            {
                AddIssue("profile.v2.map.required-capability-missing", "has no applicable evidence binding");
            }

            void AddIssue(string code, string outcome)
            {
                issues.Add(new CompositionIssue(
                    code,
                    $"Required capability '{capabilityId}' {outcome}."));
            }
        }

        issues.Sort(static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            return code != 0 ? code : StringComparer.Ordinal.Compare(left.Message, right.Message);
        });
        admissions = issues.Count == 0 ? admitted.AsReadOnly() : [];
        return issues.AsReadOnly();
    }
}
