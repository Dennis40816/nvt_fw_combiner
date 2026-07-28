using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>
/// Creates the single typed diagnostic used when the canonical TP FWConfig <c>Chip_Num</c>
/// field is zero. The caller declares whether the selected workflow actually depends on
/// the exact IC Count; the firmware fact and operator wording remain shared.
/// </summary>
public static class FirmwareConfigChipCountDiagnostics
{
    /// <summary>Non-blocking issue code for a zero IC Count that is not consumed by the route.</summary>
    public const string ZeroWarningIssueCode = "firmware-config.chip-count-zero";

    /// <summary>Blocking issue code for a zero IC Count consumed by topology-dependent planning.</summary>
    public const string RequiredIssueCode = "firmware-config.chip-count-required";

    /// <summary>Creates no issue for a positive value, otherwise a warning or blocker from one policy.</summary>
    public static CompositionIssue? CreateZeroIssue(
        FirmwareConfigMetadata metadata,
        FirmwareConfigChipCountRequirement requirement,
        string operationId,
        string? dependencyReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (!Enum.IsDefined(requirement))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requirement),
                requirement,
                "Unknown FWConfig IC Count requirement.");
        }

        if (metadata.ChipNumber != 0)
        {
            return null;
        }

        if (requirement == FirmwareConfigChipCountRequirement.WarningIfZero)
        {
            return new CompositionIssue(
                ZeroWarningIssueCode,
                "TP FW IC Count is 0. Confirm that FWConfig Chip_Num at offset 0x17 is configured correctly. This workflow does not depend on IC Count, so Build may continue.",
                operationId,
                CompositionIssueSeverity.Warning);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyReason);
        return new CompositionIssue(
            RequiredIssueCode,
            $"IC Count Required: FWConfig Chip_Num at offset 0x17 is 0. {dependencyReason} Set Chip_Num correctly before Build.",
            operationId);
    }
}

/// <summary>
/// Resolved workflow dependency on FWConfig <c>Chip_Num</c>. This is a route fact,
/// never an IC-id switch inside the validator.
/// </summary>
public enum FirmwareConfigChipCountRequirement
{
    /// <summary>The route does not consume IC Count; zero is visible but non-blocking.</summary>
    WarningIfZero,

    /// <summary>The route uses IC Count to resolve topology, ranges, or placement; zero blocks.</summary>
    RequiredPositive,
}
