using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using System.Text.RegularExpressions;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Single built-in firmware inspection owner over one canonical runtime publication.</summary>
internal sealed partial class BuiltInFirmwareInspection : IFirmwareInspection
{
    private const int FirmwareIcHintHeaderProbeLength = 256 * 1024;
    private readonly IFirmwareMetadataPlanAuthorityResolver _metadataPlanAuthority;
    private readonly ICompositionCapabilityExperience _projection;
    private readonly ICompiledInputSlotInspector<FirmwareInspectionStatusBatch>
        _standardMergeAuthoring;
    private readonly ICompiledInputSlotInspector<AbMergeInspectionBatch>
        _abMergeAuthoring;
    private readonly ICompiledInputSlotInspector<FirmwareInspectionStatusBatch>
        _dpReplaceAuthoring;
    private readonly ICompiledInputSlotInspector<FirmwareInspectionStatusBatch>
        _ctrlRamAuthoring;
    private readonly ISelectedFileContentInspector _contentInspector;
    private readonly IFirmwareArtifactClassificationResolver _artifactClassification;

    internal BuiltInFirmwareInspection(
        IFirmwareMetadataPlanAuthorityResolver metadataPlanAuthority,
        ICompositionCapabilityExperience projection,
        ICompiledInputSlotInspector<FirmwareInspectionStatusBatch> standardMergeAuthoring,
        ICompiledInputSlotInspector<AbMergeInspectionBatch> abMergeAuthoring,
        ICompiledInputSlotInspector<FirmwareInspectionStatusBatch> dpReplaceAuthoring,
        ICompiledInputSlotInspector<FirmwareInspectionStatusBatch> ctrlRamAuthoring,
        IFirmwareArtifactClassificationResolver artifactClassification,
        ISelectedFileContentInspector? contentInspector = null)
    {
        _metadataPlanAuthority = metadataPlanAuthority ??
            throw new ArgumentNullException(nameof(metadataPlanAuthority));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _standardMergeAuthoring = standardMergeAuthoring ??
            throw new ArgumentNullException(nameof(standardMergeAuthoring));
        _abMergeAuthoring = abMergeAuthoring ??
            throw new ArgumentNullException(nameof(abMergeAuthoring));
        _dpReplaceAuthoring = dpReplaceAuthoring ??
            throw new ArgumentNullException(nameof(dpReplaceAuthoring));
        _ctrlRamAuthoring = ctrlRamAuthoring ??
            throw new ArgumentNullException(nameof(ctrlRamAuthoring));
        _artifactClassification = artifactClassification ??
            throw new ArgumentNullException(nameof(artifactClassification));
        _contentInspector = contentInspector ?? new FileContentSnapshotInspector();
    }

    public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string numberToken,
        FirmwareConfigMetadataSnapshot? baseFirmware)
    {
        return ProjectCtrlRamInspectionDisplay(
            this,
            icId,
            numberToken,
            baseFirmware);
    }

    /// <summary>Reads every distinct selected path once and returns named immutable projections.</summary>
    internal static IReadOnlyList<FirmwareInspectionSnapshotResult> InspectFirmwareBatch(
        BuiltInFirmwareInspection inspection,
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs)
    {
        return InspectFirmwareBatch(
            inspection,
            icId,
            inputs,
            TryReadFirmwareImage);
    }

    internal static IReadOnlyList<FirmwareInspectionSnapshotResult> InspectFirmwareBatch(
        BuiltInFirmwareInspection inspection,
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        Func<string, byte[]?> readFirmwareImage,
        FirmwareInspectionDispatch dispatch = FirmwareInspectionDispatch.TypedApplicable)
    {
        ValidateInspectionInputs(icId, inputs);
        ArgumentNullException.ThrowIfNull(readFirmwareImage);

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

        bool inspectAll = dispatch == FirmwareInspectionDispatch.AllStrategiesBaseline;
        FirmwareInspectionStatusBatch dpInputBatch = inspectAll ||
            inputs.Any(static input => input.DpReplaceAddressSpaceId is not null)
                ? inspection._dpReplaceAuthoring.InspectInputSlots(icId, inputs, ReadOnce)
                : FirmwareInspectionStatusBatch.Empty;
        FirmwareInspectionStatusBatch standardMergeInputBatch = inspectAll ||
            inputs.Any(static input => input.StandardMergeAddressSpaceId is not null)
                ? inspection._standardMergeAuthoring.InspectInputSlots(icId, inputs, ReadOnce)
                : FirmwareInspectionStatusBatch.Empty;
        FirmwareInspectionStatusBatch ctrlRamInputBatch = inspectAll ||
            inputs.Any(static input => input.CtrlRamReplaceAddressSpaceId is not null)
                ? inspection._ctrlRamAuthoring.InspectInputSlots(icId, inputs, ReadOnce)
                : FirmwareInspectionStatusBatch.Empty;
        AbMergeInspectionBatch abMergeInputBatch = inspectAll ||
            inputs.Any(static input => input.AbMergeAddressSpaceId is not null)
                ? inspection._abMergeAuthoring.InspectInputSlots(icId, inputs, ReadOnce)
                : AbMergeInspectionBatch.Empty;
        List<FirmwareInspectionSnapshotResult> results = [];
        foreach (FirmwareInspectionSnapshotInput input in inputs)
        {
            byte[]? primaryImage = ReadOnce(input.Path);
            FirmwareMetadataPlanAuthority metadataAuthority = primaryImage is null
                ? FirmwareMetadataPlanAuthority.NotApplicable
                : inspection._metadataPlanAuthority.Resolve(
                    icId,
                    input,
                    primaryImage.LongLength,
                    dpInputBatch,
                    standardMergeInputBatch,
                    ctrlRamInputBatch);
            FirmwareInspectionSnapshot snapshot = InspectFirmware(
                inspection,
                icId,
                input.Path,
                input.TpPath,
                input.CtrlRamRequest,
                ReadOnce,
                input.ExactCapability,
                input.StandardMergeAddressSpaceId,
                metadataAuthority);
            if (!string.IsNullOrWhiteSpace(input.AbMergeAddressSpaceId))
            {
                snapshot = snapshot with
                {
                    AbMergeFacts = abMergeInputBatch.Facts[input.InspectionId],
                    InputSlotStatus = abMergeInputBatch.Statuses[input.InspectionId],
                    InputSlotCatalog = abMergeInputBatch.Catalog,
                };
            }

            if (dpInputBatch.Statuses.TryGetValue(
                    input.InspectionId,
                    out AuthoringInputSlotStatus? status))
            {
                snapshot = snapshot with
                {
                    InputSlotStatus = status,
                    InputSlotCatalog = dpInputBatch.Catalog,
                };
            }

            if (standardMergeInputBatch.Statuses.TryGetValue(
                    input.InspectionId,
                    out AuthoringInputSlotStatus? standardMergeStatus))
            {
                snapshot = snapshot with
                {
                    InputSlotStatus = standardMergeStatus,
                    InputSlotCatalog = standardMergeInputBatch.Catalog,
                };
            }

            if (input.CtrlRamReplaceAddressSpaceId is not null)
            {
                _ = ctrlRamInputBatch.Statuses.TryGetValue(
                    input.InspectionId,
                    out AuthoringInputSlotStatus? ctrlRamStatus);
                snapshot = snapshot with
                {
                    InputSlotStatus = ctrlRamStatus,
                    InputSlotCatalog = ctrlRamInputBatch.Catalog,
                    AuthoringCompilationIssues = input.CtrlRamReplaceAddressSpaceId == CompositionAddressSpaceIds.ReferenceBase ? ctrlRamInputBatch.Issues : [],
                    CtrlRamBaseDiscoveryReadiness =
                        ctrlRamInputBatch.CtrlRamBaseDiscovery is { } discovery &&
                        StringComparer.Ordinal.Equals(discovery.InspectionId, input.InspectionId)
                            ? discovery.Readiness
                            : CtrlRamBaseDiscoveryReadiness.NotApplicable,
                };
            }

            results.Add(new FirmwareInspectionSnapshotResult(input.InspectionId, snapshot));
        }

        return results;
    }

    private static void ValidateInspectionInputs(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(inputs);
        var inspectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FirmwareInspectionSnapshotInput? input in inputs)
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
    }

    internal static FirmwareInspectionSnapshot InspectFirmware(
        BuiltInFirmwareInspection inspection,
        string icId,
        string path,
        string? tpPath,
        CtrlRamInspectionRequest? ctrlRamRequest,
        Func<string, byte[]?> readFirmwareImage,
        ResolvedCapability? exactCapability = null,
        string? standardMergeAddressSpaceId = null,
        FirmwareMetadataPlanAuthority? metadataAuthority = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(readFirmwareImage);

        Match fileNameMatch = FirmwareIcHintMarker().Match(Path.GetFileNameWithoutExtension(path));
        string? detectedIcId = fileNameMatch.Success
            ? $"NT{fileNameMatch.Groups["ic"].Value}"
            : null;
        byte[]? image = readFirmwareImage(path);
        if (image is null)
        {
            return new FirmwareInspectionSnapshot(detectedIcId, null, null, null, null, null);
        }

        metadataAuthority ??= inspection._metadataPlanAuthority.Resolve(
            icId,
            new FirmwareInspectionSnapshotInput(
                "direct",
                path,
                tpPath,
                CtrlRamRequest: ctrlRamRequest,
                StandardMergeAddressSpaceId: standardMergeAddressSpaceId,
                ExactCapability: exactCapability),
            image.LongLength,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);

        byte[]? tpImage = string.IsNullOrWhiteSpace(tpPath)
            ? null
            : string.Equals(path, tpPath, StringComparison.Ordinal)
                ? image
                : readFirmwareImage(tpPath);
        FirmwareConfigMetadata? firmwareConfig =
            TryReadFirmwareConfigMetadataFromImage(
                inspection._projection,
                icId,
                image,
                out FirmwareConfigMetadata metadata)
                    ? metadata
                    : null;
        CompiledFirmwareArtifactClassification? artifactClassification =
            inspection._artifactClassification.Resolve(icId, exactCapability, image);
        BaseFirmwareArtifactKind artifactKind = artifactClassification?.Kind switch
        {
            CompiledFirmwareArtifactKind.TpFirmware => BaseFirmwareArtifactKind.TpFirmware,
            CompiledFirmwareArtifactKind.FlashCode => BaseFirmwareArtifactKind.FlashCode,
            CompiledFirmwareArtifactKind.Unknown or null => BaseFirmwareArtifactKind.Unknown,
            _ => throw new InvalidOperationException("Unknown compiled firmware artifact kind."),
        };
        bool shouldProjectDpMetadata = artifactClassification?.IsDpMetadataApplicable != false;
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            inspection._projection,
            icId,
            firmwareConfig,
            out LegacyCombinerPostbuildProfile? resolvedProfile)
                ? resolvedProfile
                : null;
        CtrlRamInspectionDisplay? ctrlRamDisplay = ctrlRamRequest is { } request
            ? BuiltInCtrlRamAuthoringAdapter.CreateDisplay(
                icId,
                request.NumberToken,
                postbuildProfile,
                hasReadableBase: true)
            : null;
        (DpVersionMetadata? Version,
            CmiDpCodeMetadata? Cmi,
            FirmwareMetadataPrerequisite? Prerequisite)
            dpMetadata = shouldProjectDpMetadata
                ? ReadDpMetadata(
                    image,
                    string.Equals(path, tpPath, StringComparison.Ordinal) ? null : tpImage,
                    standardMergeAddressSpaceId,
                    metadataAuthority)
                : (null, null, null);
        return new FirmwareInspectionSnapshot(
            detectedIcId ?? DetectFirmwareIcHintFromHeader(image),
            ReadFirmwareConfigMetadata(firmwareConfig, postbuildProfile),
            dpMetadata.Version,
            dpMetadata.Cmi,
            ReadFirmwareContextSuggestion(inspection._projection, icId, firmwareConfig),
            ctrlRamDisplay,
            artifactKind)
        {
            ArtifactClassification = artifactClassification,
            FileStamp = FileStamp.FromBytes(image),
            DpMetadataPrerequisite = dpMetadata.Prerequisite,
        };
    }

    /// <summary>Reprojects CtrlRAM display state from an existing immutable firmware inspection.</summary>
    internal static CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        BuiltInFirmwareInspection inspection,
        string icId,
        string number,
        FirmwareConfigMetadataSnapshot? firmwareConfig)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            inspection._projection,
            icId,
            firmwareConfig?.CommonFwVersion,
            out LegacyCombinerPostbuildProfile? resolvedProfile)
                ? resolvedProfile
                : null;
        return BuiltInCtrlRamAuthoringAdapter.CreateDisplay(
            icId,
            number,
            postbuildProfile,
            hasReadableBase: true);
    }

    internal static FirmwareConfigMetadataSnapshot? ReadFirmwareConfigMetadata(
        ICompositionCapabilityExperience projection,
        string icId,
        ReadOnlySpan<byte> image)
    {
        if (!TryReadFirmwareConfigMetadataFromImage(
                projection,
                icId,
                image,
                out FirmwareConfigMetadata firmwareConfig))
        {
            return null;
        }

        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            projection,
            icId,
            firmwareConfig,
            out LegacyCombinerPostbuildProfile? resolvedProfile)
                ? resolvedProfile
                : null;
        return ReadFirmwareConfigMetadata(firmwareConfig, postbuildProfile);
    }

    private static FirmwareConfigMetadataSnapshot? ReadFirmwareConfigMetadata(
        FirmwareConfigMetadata? metadata,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return metadata is { } firmwareConfig
            ? new FirmwareConfigMetadataSnapshot(
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

    private static FirmwareContextSuggestion? ReadFirmwareContextSuggestion(
        ICompositionCapabilityExperience projection,
        string icId,
        FirmwareConfigMetadata? metadata)
    {
        return metadata is { ChipNumber: not 0 } firmwareConfig &&
            TryResolveNumberTokenForFirmwareConfig(
                projection,
                icId,
                firmwareConfig,
                out string? numberToken)
                ? new FirmwareContextSuggestion(
                    icId,
                    numberToken!,
                    firmwareConfig.ChipNumber,
                    firmwareConfig.CommonFwVersion,
                    firmwareConfig.ProjectId)
                : null;
    }

    private static bool TryReadFirmwareConfigMetadataFromImage(
        ICompositionCapabilityExperience projection,
        string icId,
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        return projection.IsKnownIcId(icId) &&
            FirmwareConfigMetadataReader.TryReadBackup(image, out metadata);
    }

    private static bool TryResolvePostbuildProfileForDisplay(
        ICompositionCapabilityExperience projection,
        string icId,
        FirmwareConfigMetadata? metadata,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return TryResolvePostbuildProfileForDisplay(
            projection,
            icId,
            metadata?.CommonFwVersion,
            out postbuildProfile);
    }

    private static bool TryResolvePostbuildProfileForDisplay(
        ICompositionCapabilityExperience projection,
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        if (!projection.IsKnownIcId(icId))
        {
            postbuildProfile = null;
            return false;
        }

        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles =
            BuiltInPostbuildProfileCatalog.GetProfiles(
                IcIdentifier.Normalize(icId));
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
            BuiltInPostbuildProfileCatalog.TrySelectProfileForCommonFwVersion(
                IcIdentifier.Normalize(icId),
                commonFwVersion,
                out postbuildProfile,
                out _);
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

internal enum FirmwareInspectionDispatch
{
    TypedApplicable,
    AllStrategiesBaseline,
}
