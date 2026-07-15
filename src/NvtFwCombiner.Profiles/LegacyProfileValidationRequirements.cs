using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Closed legacy-profile bridge for final-output validations already admitted by the runtime.</summary>
internal static class LegacyProfileValidationRequirements
{
    internal static CompiledFirmwareConfigBackupVersionValidation FirmwareConfigBackupVersion(
        byte firmwareVersion,
        byte firmwareSubVersion)
    {
        return CompiledValidationRequirements.FirmwareConfigBackupVersion(
            "verify-nvt-fwconfig-backup-version",
            "replace.ctrlram.fw-version-output-invalid",
            "replace.ctrlram.fw-version-output-mismatch",
            firmwareVersion,
            firmwareSubVersion);
    }
}
