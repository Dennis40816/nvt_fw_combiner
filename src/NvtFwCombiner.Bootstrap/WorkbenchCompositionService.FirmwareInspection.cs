using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;
using System.Text.RegularExpressions;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const int FirmwareIcHintHeaderProbeLength = 256 * 1024;

    /// <summary>Reads each selected image once and projects all shell firmware metadata from that snapshot.</summary>
    public static WorkbenchFirmwareInspection InspectFirmware(
        string icId,
        string path,
        string? tpPath = null,
        WorkbenchCtrlRamInspectionRequest? ctrlRamRequest = null)
    {
        return InspectFirmware(icId, path, tpPath, ctrlRamRequest, TryReadFirmwareImage);
    }

    /// <summary>Reads every distinct selected path once and returns named immutable projections.</summary>
    public static IReadOnlyList<WorkbenchFirmwareInspectionResult> InspectFirmwareBatch(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs)
    {
        return InspectFirmwareBatch(icId, inputs, TryReadFirmwareImage);
    }

    internal static IReadOnlyList<WorkbenchFirmwareInspectionResult> InspectFirmwareBatch(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(readFirmwareImage);

        var inspectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkbenchFirmwareInspectionInput? input in inputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentException.ThrowIfNullOrWhiteSpace(input.InspectionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(input.Path);
            if (!inspectionIds.Add(input.InspectionId))
            {
                throw new ArgumentException(
                    $"Duplicate firmware inspection id '{input.InspectionId}'.",
                    nameof(inputs));
            }
        }

        var images = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        byte[]? ReadOnce(string path)
        {
            if (!images.TryGetValue(path, out byte[]? image))
            {
                image = readFirmwareImage(path);
                images.Add(path, image);
            }

            return image;
        }

        List<WorkbenchFirmwareInspectionResult> results = [];
        foreach (WorkbenchFirmwareInspectionInput input in inputs)
        {
            WorkbenchFirmwareInspection inspection = InspectFirmware(
                icId,
                input.Path,
                input.TpPath,
                input.CtrlRamRequest,
                ReadOnce);
            if (!string.IsNullOrWhiteSpace(input.AbMergeAddressSpaceId))
            {
                inspection = inspection with
                {
                    AbMergeInput = InspectAbMergeInput(
                        icId,
                        input.AbMergeAddressSpaceId,
                        ReadOnce(input.Path),
                        AbMergeWorkbenchCompositionService.ResolveTopologySelection(
                            input.AbMergeTopologyToken)),
                };
            }

            results.Add(new WorkbenchFirmwareInspectionResult(input.InspectionId, inspection));
        }

        return results;
    }

    internal static WorkbenchFirmwareInspection InspectFirmware(
        string icId,
        string path,
        string? tpPath,
        WorkbenchCtrlRamInspectionRequest? ctrlRamRequest,
        Func<string, byte[]?> readFirmwareImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(readFirmwareImage);

        string? detectedIcId = DetectFirmwareIcHintFromFileName(path);
        byte[]? image = readFirmwareImage(path);
        if (image is null)
        {
            return new WorkbenchFirmwareInspection(detectedIcId, null, null, null, null, null);
        }

        byte[]? tpImage = string.IsNullOrWhiteSpace(tpPath)
            ? null
            : string.Equals(path, tpPath, StringComparison.Ordinal)
                ? image
                : readFirmwareImage(tpPath);
        FirmwareConfigMetadata? firmwareConfig = ReadFirmwareConfigMetadataValue(icId, image);
        FirmwareConfigMetadata? tpFirmwareConfig = string.Equals(path, tpPath, StringComparison.Ordinal)
            ? firmwareConfig
            : ReadFirmwareConfigMetadataValue(icId, tpImage);
        FirmwareConfigMetadata? cmiFirmwareConfig = tpFirmwareConfig ??
            (string.IsNullOrWhiteSpace(tpPath) ? firmwareConfig : null);
        WorkbenchBaseFirmwareArtifactKind artifactKind = ClassifyBaseFirmwareArtifact(icId, image);
        bool shouldProjectDpMetadata = artifactKind != WorkbenchBaseFirmwareArtifactKind.TpFirmware;
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            firmwareConfig,
            out LegacyCombinerPostbuildProfile? resolvedProfile)
                ? resolvedProfile
                : null;
        WorkbenchCtrlRamInspectionDisplay? ctrlRamDisplay = ctrlRamRequest is { } request
            ? CreateCtrlRamInspectionDisplay(icId, request.NumberToken, postbuildProfile)
            : null;
        (WorkbenchDpVersionMetadata? Version, WorkbenchCmiDpCodeMetadata? Cmi)
            dpMetadata = shouldProjectDpMetadata
                ? ReadDpMetadata(icId, image, cmiFirmwareConfig?.ChipNumber)
                : (null, null);
        return new WorkbenchFirmwareInspection(
            detectedIcId ?? DetectFirmwareIcHintFromHeader(image),
            ReadFirmwareConfigMetadata(firmwareConfig, postbuildProfile),
            dpMetadata.Version,
            dpMetadata.Cmi,
            ReadFirmwareContextSuggestion(icId, firmwareConfig),
            ctrlRamDisplay,
            artifactKind);
    }

    /// <summary>Reprojects CtrlRAM display state from an existing immutable firmware inspection.</summary>
    public static WorkbenchCtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string number,
        WorkbenchFirmwareConfigMetadata? firmwareConfig)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            firmwareConfig?.CommonFwVersion,
            out LegacyCombinerPostbuildProfile? resolvedProfile)
                ? resolvedProfile
                : null;
        return CreateCtrlRamInspectionDisplay(icId, number, postbuildProfile);
    }

    private static WorkbenchDpVersionMetadata? ReadDpVersionMetadata(string icId, ReadOnlySpan<byte> image)
    {
        return ReadDpMetadata(icId, image, firmwareConfigChipNumber: null).Version;
    }

    private static WorkbenchCmiDpCodeMetadata? ReadCmiDpCodeMetadata(
        string icId,
        ReadOnlySpan<byte> image,
        byte? firmwareConfigChipNumber)
    {
        return ReadDpMetadata(icId, image, firmwareConfigChipNumber).Cmi;
    }

    private static (
        WorkbenchDpVersionMetadata? Version,
        WorkbenchCmiDpCodeMetadata? Cmi)
        ReadDpMetadata(
            string icId,
            ReadOnlySpan<byte> image,
            byte? firmwareConfigChipNumber)
    {
        if (TryReadCanonicalDpcmi(icId, image, out DpcmiMetadataFacts? dpcmi))
        {
            return dpcmi is null
                ? (null, null)
                : (
                    new WorkbenchDpVersionMetadata(dpcmi.VersionToken),
                    new WorkbenchCmiDpCodeMetadata(
                        dpcmi.MajorVersion,
                        dpcmi.MinorVersion,
                        dpcmi.JiraNumber,
                        checked((int)dpcmi.ResolvedRange.Start)));
        }

        WorkbenchDpVersionMetadata? version =
            GenFlashVersionCatalog.TryReadDpVersion(
                icId,
                image,
                out GenFlashDpVersionMetadata versionMetadata)
                ? new WorkbenchDpVersionMetadata(versionMetadata.VersionToken)
                : null;
        WorkbenchCmiDpCodeMetadata? cmi =
            GenFlashVersionCatalog.TryReadCmiDpCode(
                icId,
                image,
                firmwareConfigChipNumber,
                out CmiDpCodeMetadata cmiMetadata)
                ? new WorkbenchCmiDpCodeMetadata(
                    cmiMetadata.MajorVersionByte,
                    cmiMetadata.MinorVersionNibble,
                    cmiMetadata.JiraNumber,
                    cmiMetadata.Register16Offset)
                : null;
        return (version, cmi);
    }

    private static bool TryReadCanonicalDpcmi(
        string icId,
        ReadOnlySpan<byte> image,
        out DpcmiMetadataFacts? facts)
    {
        facts = null;
        CapabilityResolutionResult resolution = ResolveCanonicalDpReplaceCapability(
            IcSupportCatalog.NormalizeIcId(icId));
        ResolvedMetadataPlan? plan = resolution.Capability?.MetadataPlan;
        bool declaresDpcmi = plan?.Entries.Any(entry =>
            StringComparer.Ordinal.Equals(
                entry.Definition.StructureDefinition.StructureId,
                DpcmiMetadataContract.StructureId)) == true;
        if (!declaresDpcmi)
        {
            return false;
        }

        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            plan!,
            [new FirmwareArtifactPayload(CompositionAddressSpaceIds.DpReplacement, image)]);
        if (DpcmiMetadataProjector.TryProject(snapshot, out DpcmiMetadataFacts projected))
        {
            facts = projected;
        }

        // A declared canonical DPCMI route owns both success and failure. Never
        // fall back to a second physical-offset interpretation for that route.
        return true;
    }

    private static WorkbenchFirmwareConfigMetadata? ReadFirmwareConfigMetadata(
        string icId,
        ReadOnlySpan<byte> image)
    {
        if (!TryReadFirmwareConfigMetadataFromImage(icId, image, out FirmwareConfigMetadata firmwareConfig))
        {
            return null;
        }

        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            firmwareConfig,
            out LegacyCombinerPostbuildProfile? resolvedProfile)
                ? resolvedProfile
                : null;
        return ReadFirmwareConfigMetadata(firmwareConfig, postbuildProfile);
    }

    private static WorkbenchFirmwareConfigMetadata? ReadFirmwareConfigMetadata(
        FirmwareConfigMetadata? metadata,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return metadata is { } firmwareConfig
            ? new WorkbenchFirmwareConfigMetadata(
                firmwareConfig.StructureStart,
                firmwareConfig.CommonFwVersion,
                firmwareConfig.FirmwareVersion,
                firmwareConfig.FirmwareVersionBar,
                firmwareConfig.IsFirmwareVersionBarValid,
                firmwareConfig.FirmwareSubVersion,
                firmwareConfig.ChipNumber,
                firmwareConfig.ProjectId,
                postbuildProfile?.DisplayCategory,
                firmwareConfig.Hardware)
            : null;
    }

    private static WorkbenchFirmwareContextSuggestion? ReadFirmwareContextSuggestion(
        string icId,
        ReadOnlySpan<byte> image)
    {
        FirmwareConfigMetadata? firmwareConfig = TryReadFirmwareConfigMetadataFromImage(
            icId,
            image,
            out FirmwareConfigMetadata metadata)
                ? metadata
                : null;
        return ReadFirmwareContextSuggestion(icId, firmwareConfig);
    }

    private static WorkbenchFirmwareContextSuggestion? ReadFirmwareContextSuggestion(
        string icId,
        FirmwareConfigMetadata? metadata)
    {
        return metadata is { ChipNumber: not 0 } firmwareConfig &&
            TryResolveNumberTokenForFirmwareConfig(icId, firmwareConfig, out string? numberToken)
                ? new WorkbenchFirmwareContextSuggestion(
                    icId,
                    numberToken!,
                    firmwareConfig.ChipNumber,
                    firmwareConfig.CommonFwVersion,
                    firmwareConfig.ProjectId)
                : null;
    }

    private static WorkbenchBaseFirmwareArtifactKind ClassifyBaseFirmwareArtifact(
        string icId,
        ReadOnlySpan<byte> image)
    {
        if (!BuiltInTpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? resolvedFlashMap) ||
            resolvedFlashMap is not { } flashMap)
        {
            return WorkbenchBaseFirmwareArtifactKind.Unknown;
        }

        if (IsNt51950Family(icId) && image.Length == flashMap.TpPrefixLength)
        {
            // Only NT51950/NT51951 TP FW has the owner-declared standalone 0x37000 shape.
            // Other ICs always classify from their declared DP regions.
            return WorkbenchBaseFirmwareArtifactKind.TpFirmware;
        }

        if (image.Length < flashMap.TpPrefixLength ||
            !flashMap.FullFlashCapacities.Contains(image.Length))
        {
            return WorkbenchBaseFirmwareArtifactKind.Unknown;
        }

        bool hasDeclaredDpRegion = false;
        bool hasPresentDpRegion = false;
        foreach (TpFlashMapRegion region in flashMap.Regions)
        {
            if (region.Kind != TpFlashMapRegionKind.Dp)
            {
                continue;
            }

            hasDeclaredDpRegion = true;
            if (region.Range.Start < image.Length && region.Range.EndExclusive > image.Length)
            {
                return WorkbenchBaseFirmwareArtifactKind.Unknown;
            }

            if (region.Range.EndExclusive > image.Length)
            {
                continue;
            }

            hasPresentDpRegion = true;
            ReadOnlySpan<byte> dpBytes = image.Slice(
                checked((int)region.Range.Start),
                checked((int)region.Range.Length));
            if (!IsErasedOrCleared(dpBytes))
            {
                return WorkbenchBaseFirmwareArtifactKind.FlashCode;
            }
        }

        return hasDeclaredDpRegion && hasPresentDpRegion
            ? WorkbenchBaseFirmwareArtifactKind.TpFirmware
            : WorkbenchBaseFirmwareArtifactKind.Unknown;
    }

    private static bool IsErasedOrCleared(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || (bytes[0] is not 0x00 and not 0xFF))
        {
            return false;
        }

        byte expected = bytes[0];
        foreach (byte value in bytes)
        {
            if (value != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNt51950Family(string icId)
    {
        return string.Equals(icId, "NT51950", StringComparison.Ordinal) ||
            string.Equals(icId, "NT51951", StringComparison.Ordinal);
    }

    private static FirmwareConfigMetadata? ReadFirmwareConfigMetadataValue(
        string icId,
        byte[]? image)
    {
        return image is not null && TryReadFirmwareConfigMetadataFromImage(
                icId,
                image,
                out FirmwareConfigMetadata metadata)
                ? metadata
                : null;
    }

    private static bool TryReadFirmwareConfigMetadataFromImage(
        string icId,
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        return IcSupportCatalog.TryFind(icId, out _) &&
            FirmwareConfigMetadataReader.TryReadBackup(image, out metadata);
    }

    private static bool TryResolvePostbuildProfileForDisplay(
        string icId,
        FirmwareConfigMetadata? metadata,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return TryResolvePostbuildProfileForDisplay(
            icId,
            metadata?.CommonFwVersion,
            out postbuildProfile);
    }

    private static bool TryResolvePostbuildProfileForDisplay(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetPostbuildProfiles(icId);
        postbuildProfile = null;
        if (profiles.Count == 0)
        {
            return false;
        }

        if (profiles.Count == 1)
        {
            postbuildProfile = profiles[0];
            return true;
        }

        return !string.IsNullOrWhiteSpace(commonFwVersion) &&
            TrySelectPostbuildProfileByCommonFwVersion(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out _);
    }

    private static WorkbenchCtrlRamInspectionDisplay CreateCtrlRamInspectionDisplay(
        string icId,
        string number,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return new WorkbenchCtrlRamInspectionDisplay(
            number,
            CreateCtrlRamRegions(icId, number, postbuildProfile),
            CreateCtrlRamReplaceInputSlots(icId, number, postbuildProfile, hasReadableBase: true),
            CreateReplaceMemoryDisplay(
                icId,
                number,
                WorkbenchReplaceModes.CtrlRam,
                dpBaseLength: null,
                postbuildProfile: postbuildProfile));
    }

    private static string? DetectFirmwareIcHintFromFileName(string path)
    {
        Match fileNameMatch = FirmwareIcHintMarker().Match(Path.GetFileNameWithoutExtension(path));
        return fileNameMatch.Success ? $"NT{fileNameMatch.Groups["ic"].Value}" : null;
    }

    private static string? DetectFirmwareIcHintFromHeader(ReadOnlySpan<byte> image)
    {
        int length = Math.Min(image.Length, FirmwareIcHintHeaderProbeLength);
        ReadOnlySpan<byte> probe = image[..length];
        for (int index = 0; index <= probe.Length - 5; index++)
        {
            if (probe[index] != (byte)'5' ||
                probe[index + 1] != (byte)'1' ||
                probe[index + 2] != (byte)'9' ||
                !IsAsciiDigit(probe[index + 3]) ||
                !IsAsciiDigit(probe[index + 4]))
            {
                continue;
            }

            if (index > 0 && IsAsciiDigit(probe[index - 1]))
            {
                continue;
            }

            int endExclusive = index + 5;
            if (endExclusive < probe.Length && IsAsciiDigit(probe[endExclusive]))
            {
                continue;
            }

            return $"NT519{(char)probe[index + 3]}{(char)probe[index + 4]}";
        }

        return null;
    }

    private static bool IsAsciiDigit(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9';
    }

    [GeneratedRegex(@"(?<!\d)(?:NT)?(?<ic>519\d{2})(?:TT)?(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirmwareIcHintMarker();
}
