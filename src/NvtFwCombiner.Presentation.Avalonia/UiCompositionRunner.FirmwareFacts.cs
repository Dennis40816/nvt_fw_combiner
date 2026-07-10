using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets compact firmware facts decoded from a selected BIN file.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        string icId,
        string path,
        bool includeInvalid = false)
    {
        WorkbenchFirmwareConfigMetadata? metadata = WorkbenchCompositionService.TryReadFirmwareConfigMetadata(
            icId,
            path);
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeInvalid))
        {
            return [];
        }

        string firmwareVersion = metadata.IsFirmwareVersionBarValid
            ? FormattableString.Invariant($"0x{metadata.FirmwareVersion:X2}.0x{metadata.FirmwareSubVersion:X2}")
            : FormattableString.Invariant($"0x{metadata.FirmwareVersion:X2}.0x{metadata.FirmwareSubVersion:X2} (bar 0x{metadata.FirmwareVersionBar:X2} mismatch)");
        List<FirmwareSlotFactViewModel> facts =
        [
            new("Common FW", metadata.CommonFwVersion),
            new("FW", firmwareVersion, !metadata.IsFirmwareVersionBarValid),
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
        ];
        if (!string.IsNullOrWhiteSpace(metadata.PostbuildCategory))
        {
            facts.Add(new FirmwareSlotFactViewModel("Refresh", metadata.PostbuildCategory));
        }

        return facts;
    }

    /// <summary>Gets compact DP version facts decoded using gen_flash standard-merge version rules.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetDpFirmwareSlotFacts(
        string icId,
        string path,
        string? tpPath = null)
    {
        WorkbenchDpVersionMetadata? legacyMetadata = WorkbenchCompositionService.TryReadDpVersionMetadata(
            icId,
            path);
        WorkbenchCmiDpCodeMetadata? cmiMetadata = WorkbenchCompositionService.TryReadCmiDpCodeMetadata(
            icId,
            path,
            tpPath);
        if (legacyMetadata is null && cmiMetadata is null)
        {
            return [new FirmwareSlotFactViewModel("DP", "Pending", true)];
        }

        List<FirmwareSlotFactViewModel> facts =
        [
            legacyMetadata is not null
                ? new FirmwareSlotFactViewModel("DP", legacyMetadata.DisplayVersion)
                : new FirmwareSlotFactViewModel(
                    "DP",
                    FormattableString.Invariant($"D{cmiMetadata!.MajorVersionByte:X2}.{cmiMetadata.MinorVersionNibble:X1}")),
        ];
        if (cmiMetadata is not null && legacyMetadata is not null)
        {
            facts.Add(new FirmwareSlotFactViewModel(
                "CMI DP",
                FormattableString.Invariant($"D{cmiMetadata.MajorVersionByte:X2}.{cmiMetadata.MinorVersionNibble:X1}")));
        }

        if (cmiMetadata is not null && !string.IsNullOrWhiteSpace(cmiMetadata.JiraBadge))
        {
            facts.Add(new FirmwareSlotFactViewModel("Jira", cmiMetadata.JiraBadge));
        }

        if (cmiMetadata is { HasPayloadLengthWarning: true })
        {
            facts.Add(new FirmwareSlotFactViewModel(
                "DP size",
                FormattableString.Invariant(
                    $"0x{cmiMetadata.PayloadLength:X} (expected {FormatPayloadLengths(cmiMetadata.ExpectedPayloadLengths)})"),
                true));
        }

        if (cmiMetadata is not { HasPayloadLengthWarning: true } &&
            File.Exists(path) &&
            WorkbenchCompositionService.TryGetStandardMergeDpInputLengthPolicy(icId, out WorkbenchStandardMergeDpInputLengthPolicy policy))
        {
            long payloadLength = new FileInfo(path).Length;
            if (!policy.ExpectedInputLengths.Contains(payloadLength))
            {
                facts.Add(new FirmwareSlotFactViewModel(
                    "DP size",
                    FormattableString.Invariant(
                        $"0x{payloadLength:X} (expected {FormatExpectedPayloadLengths(policy.ExpectedInputLengths)}; required through 0x{policy.RequiredLength:X})"),
                    true));
            }
        }

        return facts;
    }

    private static string FormatPayloadLengths(IReadOnlyList<int> payloadLengths)
    {
        return string.Join(" / ", payloadLengths.Select(length => FormattableString.Invariant($"0x{length:X}")));
    }

    private static string FormatExpectedPayloadLengths(IReadOnlyList<long> payloadLengths)
    {
        return string.Join(" / ", payloadLengths.Select(length => FormattableString.Invariant($"0x{length:X}")));
    }

    /// <summary>Creates the catalog-backed FlashCode output file name suggestion.</summary>
    public static string CreateFlashCodeOutputFileName(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates)
    {
        return WorkbenchCompositionService.CreateFlashCodeOutputFileName(icId, candidates).FileName;
    }
}
