using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>
/// Stable source references used by the semantic FirmwareConfig projection.
/// The complete 36-field physical definition remains solely in the canonical profile data.
/// </summary>
internal static class FirmwareConfigGeneralParametersContract
{
    /// <summary>Canonical all-IC metadata structure identity.</summary>
    public const string StructureId = "firmware-config-general-parameters";

    public const string TpFirmwareVersion = "tp-firmware-version";
    public const string TpFirmwareVersionComplement = "tp-firmware-version-complement";
    public const string TpFirmwareVersionComplementRelation = "firmware-version-complement";
    public const string SensorCountX = "sensor-count-x";
    public const string SensorCountY = "sensor-count-y";
    public const string DisplayResolutionX = "display-resolution-x";
    public const string DisplayResolutionY = "display-resolution-y";
    public const string MaximumOperableFingers = "maximum-operable-fingers";
    public const string ReportIrqType = "report-irq-type";
    public const string TpFirmwareSubVersion = "tp-firmware-subversion";
    public const string TpResolutionX = "tp-resolution-x";
    public const string TpResolutionY = "tp-resolution-y";
    public const string ObservedIcCount = "observed-ic-count";
    public const string OutermostIcMasterEnable = "outermost-ic-master-enable";
    public const string CommonFirmwareMajorVersion = "common-firmware-major-version";
    public const string CommonFirmwareMinorVersion = "common-firmware-minor-version";
    public const string CommonFirmwareAdditionalVersion = "common-firmware-additional-version";
    public const string Pid = "pid";
}

/// <summary>Semantic projection selected from the complete common General Parameters prefix.</summary>
public sealed record FirmwareConfigGeneralParametersFacts(
    byte TpFirmwareVersion,
    byte TpFirmwareVersionComplement,
    bool IsTpFirmwareVersionComplementValid,
    byte SensorCountX,
    byte SensorCountY,
    ushort DisplayResolutionX,
    ushort DisplayResolutionY,
    byte MaximumOperableFingers,
    byte ReportIrqType,
    byte TpFirmwareSubVersion,
    ushort TpResolutionX,
    ushort TpResolutionY,
    byte ObservedIcCount,
    byte OutermostIcMasterEnable,
    byte CommonFirmwareMajorVersion,
    byte CommonFirmwareMinorVersion,
    byte CommonFirmwareAdditionalVersion,
    ushort Pid)
{
    /// <summary>Whether FirmwareConfig requests the outermost IC as Master.</summary>
    public bool UseOutermostIcAsMaster => OutermostIcMasterEnable != 0;

    /// <summary>Three-byte common firmware version.</summary>
    public string CommonFirmwareVersion =>
        FormattableString.Invariant(
            $"{CommonFirmwareMajorVersion}.{CommonFirmwareMinorVersion}.{CommonFirmwareAdditionalVersion}");
}

/// <summary>One report-safe canonical FirmwareConfig inspection diagnostic.</summary>
public sealed record FirmwareConfigInspectionDiagnostic(string Code, string Message);

/// <summary>Projects canonical FirmwareConfig facts without selecting IC, IC Count, family, or route.</summary>
public static class FirmwareConfigGeneralParametersProjector
{
    private const string MarkerCountMismatchCode = "firmware-config.marker-count-mismatch";

    /// <summary>Projects exactly one successful canonical General Parameters inspection.</summary>
    public static bool TryProject(
        MetadataInspectionSnapshot snapshot,
        out FirmwareConfigGeneralParametersFacts facts)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        facts = null!;
        MetadataInspectionResult? result = FindSingle(snapshot);
        if (result is not { State: MetadataInspectionState.Value } ||
            result.Resolution?.Resolved?.DecodedStructure is not { } decoded)
        {
            return false;
        }

        var reader = new DecodedFactReader(decoded.Facts);
        if (!reader.TryReadByte(FirmwareConfigGeneralParametersContract.TpFirmwareVersion, out byte firmwareVersion) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplement, out byte firmwareVersionBar) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.SensorCountX, out byte algorithmSensorCountX) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.SensorCountY, out byte algorithmSensorCountY) ||
            !reader.TryReadUInt16(FirmwareConfigGeneralParametersContract.DisplayResolutionX, out ushort displayResolutionX) ||
            !reader.TryReadUInt16(FirmwareConfigGeneralParametersContract.DisplayResolutionY, out ushort displayResolutionY) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.MaximumOperableFingers, out byte maximumFingerCount) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.ReportIrqType, out byte reportInterruptType) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.TpFirmwareSubVersion, out byte firmwareSubVersion) ||
            !reader.TryReadUInt16(FirmwareConfigGeneralParametersContract.TpResolutionX, out ushort touchPanelResolutionX) ||
            !reader.TryReadUInt16(FirmwareConfigGeneralParametersContract.TpResolutionY, out ushort touchPanelResolutionY) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.ObservedIcCount, out byte observedIcCount) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.OutermostIcMasterEnable, out byte outermostIcMasterEnable) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.CommonFirmwareMajorVersion, out byte commonFirmwareMajorVersion) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.CommonFirmwareMinorVersion, out byte commonFirmwareMinorVersion) ||
            !reader.TryReadByte(FirmwareConfigGeneralParametersContract.CommonFirmwareAdditionalVersion, out byte commonFirmwareAdditionalVersion) ||
            !reader.TryReadUInt16(FirmwareConfigGeneralParametersContract.Pid, out ushort novatekProjectId))
        {
            return false;
        }

        FirmwareDecodedMetadataRelation? complementRelation =
            decoded.Relations.SingleOrDefault(relation =>
            StringComparer.Ordinal.Equals(
                relation.RelationId,
                FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplementRelation));
        if (complementRelation is not
            {
                Kind: FirmwareMetadataFieldRelationKind.BitwiseComplement,
            } ||
            !StringComparer.Ordinal.Equals(
                complementRelation.SourceFieldId,
                FirmwareConfigGeneralParametersContract.TpFirmwareVersion) ||
            !StringComparer.Ordinal.Equals(
                complementRelation.RelatedFieldId,
                FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplement))
        {
            return false;
        }

        facts = new FirmwareConfigGeneralParametersFacts(
            firmwareVersion,
            firmwareVersionBar,
            complementRelation.IsSatisfied,
            algorithmSensorCountX,
            algorithmSensorCountY,
            displayResolutionX,
            displayResolutionY,
            maximumFingerCount,
            reportInterruptType,
            firmwareSubVersion,
            touchPanelResolutionX,
            touchPanelResolutionY,
            observedIcCount,
            outermostIcMasterEnable,
            commonFirmwareMajorVersion,
            commonFirmwareMinorVersion,
            commonFirmwareAdditionalVersion,
            novatekProjectId);
        return true;
    }

    /// <summary>Creates the exact owner-approved marker-count diagnostic when cardinality rejects.</summary>
    public static bool TryCreateDiagnostic(
        MetadataInspectionSnapshot snapshot,
        out FirmwareConfigInspectionDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        diagnostic = null!;
        MetadataInspectionResult? result = FindSingle(snapshot);
        if (result?.Resolution is not
            {
                Failure: FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch,
                ObservedMarkerMatchCount: { } count,
            })
        {
            return false;
        }

        diagnostic = new FirmwareConfigInspectionDiagnostic(
            MarkerCountMismatchCode,
            FormattableString.Invariant(
                $"Expected exactly one NVT marker (00 4E 56 54), but found {count}."));
        return true;
    }

    private static MetadataInspectionResult? FindSingle(MetadataInspectionSnapshot snapshot)
    {
        MetadataInspectionResult[] matches =
        [
            .. snapshot.Results.Where(result =>
                StringComparer.Ordinal.Equals(
                    result.PlanEntry.Definition.StructureDefinition.StructureId,
                    FirmwareConfigGeneralParametersContract.StructureId)),
        ];
        return matches.Length == 1 ? matches[0] : null;
    }

    private sealed class DecodedFactReader
    {
        private readonly Dictionary<string, FirmwareMetadataValue> _values;

        internal DecodedFactReader(IEnumerable<FirmwareDecodedMetadataFact> facts)
        {
            _values = facts.ToDictionary(
                static fact => fact.FieldId,
                static fact => fact.Value,
                StringComparer.Ordinal);
        }

        internal bool TryReadByte(string fieldId, out byte value)
        {
            value = 0;
            if (!_values.TryGetValue(fieldId, out FirmwareMetadataValue? metadata) ||
                metadata.UnsignedIntegerValue is not { } unsigned ||
                unsigned > byte.MaxValue)
            {
                return false;
            }

            value = (byte)unsigned;
            return true;
        }

        internal bool TryReadUInt16(string fieldId, out ushort value)
        {
            value = 0;
            if (!_values.TryGetValue(fieldId, out FirmwareMetadataValue? metadata) ||
                metadata.UnsignedIntegerValue is not { } unsigned ||
                unsigned > ushort.MaxValue)
            {
                return false;
            }

            value = (ushort)unsigned;
            return true;
        }
    }
}
