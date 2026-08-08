using System.Collections.Concurrent;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;
using System.Text.RegularExpressions;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Migration-only firmware inspection compatibility surface pending focused Application adoption.</summary>
public static partial class FirmwareInspectionAdapter
{
    private const int FirmwareIcHintHeaderProbeLength = 256 * 1024;
    private static readonly ConcurrentDictionary<string, Lazy<CompiledComposition[]>>
        s_artifactClassificationCompositions = new(StringComparer.Ordinal);

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

        WorkbenchCompiledAuthoringInspectionBatch dpInputBatch =
            CanonicalAuthoringAdapter.InspectDpReplaceInputSlots(
                icId,
                inputs,
                ReadOnce);
        WorkbenchCompiledAuthoringInspectionBatch standardMergeInputBatch =
            CanonicalAuthoringAdapter.InspectStandardMergeInputSlots(
                icId,
                inputs,
                ReadOnce);
        WorkbenchCompiledAuthoringInspectionBatch ctrlRamInputBatch =
            CompositionAuthoringSessionAdapter.InspectCtrlRamReplaceInputSlots(icId, inputs, ReadOnce);
        WorkbenchAbMergeInspectionBatch abMergeInputBatch =
            CanonicalAuthoringAdapter.InspectAbMergeInputSlots(
                icId,
                inputs,
                ReadOnce);
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
                    AbMergeFacts = abMergeInputBatch.Facts[input.InspectionId],
                    InputSlotStatus = abMergeInputBatch.Statuses[input.InspectionId],
                    InputSlotCatalog = abMergeInputBatch.Catalog,
                };
            }

            if (dpInputBatch.Statuses.TryGetValue(input.InspectionId, out AuthoringInputSlotStatus? status))
            {
                inspection = inspection with
                {
                    InputSlotStatus = status,
                    InputSlotCatalog = dpInputBatch.Catalog,
                };
            }

            if (standardMergeInputBatch.Statuses.TryGetValue(
                    input.InspectionId,
                    out AuthoringInputSlotStatus? standardMergeStatus))
            {
                inspection = inspection with
                {
                    InputSlotStatus = standardMergeStatus,
                    InputSlotCatalog = standardMergeInputBatch.Catalog,
                };
            }

            if (ctrlRamInputBatch.Statuses.TryGetValue(
                    input.InspectionId,
                    out AuthoringInputSlotStatus? ctrlRamStatus))
            {
                inspection = inspection with
                {
                    InputSlotStatus = ctrlRamStatus,
                    InputSlotCatalog = ctrlRamInputBatch.Catalog,
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
        CompiledFirmwareArtifactClassification? artifactClassification =
            ClassifyBaseFirmwareArtifact(icId, image);
        WorkbenchBaseFirmwareArtifactKind artifactKind = artifactClassification?.Kind switch
        {
            CompiledFirmwareArtifactKind.TpFirmware => WorkbenchBaseFirmwareArtifactKind.TpFirmware,
            CompiledFirmwareArtifactKind.FlashCode => WorkbenchBaseFirmwareArtifactKind.FlashCode,
            CompiledFirmwareArtifactKind.Unknown or null => WorkbenchBaseFirmwareArtifactKind.Unknown,
            _ => throw new InvalidOperationException("Unknown compiled firmware artifact kind."),
        };
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
                ? ReadDpMetadata(
                    icId,
                    image,
                    string.Equals(path, tpPath, StringComparison.Ordinal) ? null : tpImage)
                : (null, null);
        return new WorkbenchFirmwareInspection(
            detectedIcId ?? DetectFirmwareIcHintFromHeader(image),
            ReadFirmwareConfigMetadata(firmwareConfig, postbuildProfile),
            dpMetadata.Version,
            dpMetadata.Cmi,
            ReadFirmwareContextSuggestion(icId, firmwareConfig),
            ctrlRamDisplay,
            artifactKind)
        {
            ArtifactClassification = artifactClassification,
            FileStamp = FileStamp.FromBytes(image),
        };
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

    private static (
        WorkbenchDpVersionMetadata? Version,
        WorkbenchCmiDpCodeMetadata? Cmi)
        ReadDpMetadata(
            string icId,
            byte[] image,
            byte[]? tpImage)
    {
        if (TryReadCanonicalDpcmi(
                icId,
                image,
                tpImage,
                out DpcmiMetadataFacts? dpcmi))
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

        return (null, null);
    }

    private static bool TryReadCanonicalDpcmi(
        string icId,
        byte[] image,
        byte[]? tpImage,
        out DpcmiMetadataFacts? facts)
    {
        facts = null;
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CapabilityResolutionResult resolution;
        if (tpImage is null)
        {
            resolution = CanonicalCapabilityResolution
                .ResolveCanonicalDpReplaceCapability(normalizedIcId);
            if (StringComparer.Ordinal.Equals(
                    resolution.Issue?.Code,
                    CapabilityCatalogIssueCodes.RouteAmbiguous))
            {
                resolution = CanonicalCapabilityResolution.ResolveCanonicalDpReplaceCapability(
                    normalizedIcId,
                    image.LongLength);
            }
        }
        else
        {
            resolution = CanonicalCapabilityResolution.ResolveCanonicalStandardMergeCapability(
                normalizedIcId,
                image.LongLength);
        }

        ResolvedMetadataPlan? plan = resolution.Capability?.MetadataPlan;
        bool declaresDpcmi = DeclaresDpcmi(plan);
        if (!declaresDpcmi && tpImage is not null)
        {
            CapabilityResolutionResult dpResolution =
                CanonicalCapabilityResolution.ResolveCanonicalDpReplaceCapability(
                    normalizedIcId);
            if (StringComparer.Ordinal.Equals(
                    dpResolution.Issue?.Code,
                    CapabilityCatalogIssueCodes.RouteAmbiguous))
            {
                dpResolution = CanonicalCapabilityResolution.ResolveCanonicalDpReplaceCapability(
                    normalizedIcId,
                    image.LongLength);
            }

            plan = dpResolution.Capability?.MetadataPlan;
            declaresDpcmi = DeclaresDpcmi(plan);
        }

        if (!declaresDpcmi)
        {
            return false;
        }

        if (image.Length == 0)
        {
            return true;
        }

        FirmwareArtifactPayload[] artifacts =
        [
            .. plan!.Entries
                .Select(static entry => entry.Definition.SpaceId)
                .Distinct(StringComparer.Ordinal)
                .Select(spaceId => new FirmwareArtifactPayload(
                    spaceId,
                    StringComparer.Ordinal.Equals(
                            spaceId,
                            CompositionAddressSpaceIds.TpInput) &&
                        tpImage is not null
                            ? tpImage
                            : image)),
        ];
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            plan,
            artifacts);
        if (DpcmiMetadataProjector.TryProject(snapshot, out DpcmiMetadataFacts projected))
        {
            facts = projected;
        }

        // A declared canonical DPCMI route owns both success and failure. Never
        // fall back to a second physical-offset interpretation for that route.
        return true;
    }

    private static bool DeclaresDpcmi(ResolvedMetadataPlan? plan)
    {
        return plan?.Entries.Any(entry =>
            StringComparer.Ordinal.Equals(
                entry.Definition.StructureDefinition.Definition.DefinitionId,
                DpcmiMetadataContract.StructureId)) == true;
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

    private static CompiledFirmwareArtifactClassification? ClassifyBaseFirmwareArtifact(
        string icId,
        ReadOnlySpan<byte> image)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CompiledComposition[] compositions =
            s_artifactClassificationCompositions.GetOrAdd(
                normalizedIcId,
                static key => new Lazy<CompiledComposition[]>(
                    () => CompileArtifactClassificationCompositions(key),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if (compositions.Length == 0)
        {
            return null;
        }

        CompiledFirmwareArtifactClassification? best = null;
        foreach (CompiledComposition composition in compositions)
        {
            CompiledFirmwareArtifactClassification classification =
                CompiledFirmwareArtifactClassifier.Classify(composition, image);
            if (classification.Kind == CompiledFirmwareArtifactKind.FlashCode)
            {
                return classification;
            }

            if (best is null ||
                (best.Kind == CompiledFirmwareArtifactKind.Unknown &&
                 classification.Kind == CompiledFirmwareArtifactKind.TpFirmware))
            {
                best = classification;
            }
        }

        return best;
    }

    private static CompiledComposition[] CompileArtifactClassificationCompositions(
        string icId)
    {
        if (!BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration))
        {
            return [];
        }

        IReadOnlyList<long> capacities = registration.GetMapCapacities(
            out IReadOnlyList<CompositionIssue> capacityIssues);
        if (capacityIssues.Count != 0)
        {
            return [];
        }

        var compositions = new List<CompiledComposition>(capacities.Count);
        foreach (long capacity in capacities)
        {
            registration.TryCompile(
                capacity,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> compilationIssues);
            if (composition is not null && compilationIssues.Count == 0)
            {
                compositions.Add(composition);
            }
        }

        return [.. compositions];
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
        return CanonicalCapabilityProjection.IsKnownIcId(icId) &&
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
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles =
            CanonicalCapabilityProjection.GetPostbuildProfiles(icId);
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
            CanonicalCapabilityProjection.TrySelectPostbuildProfileByCommonFwVersion(
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
            CanonicalAuthoringAdapter.CreateCtrlRamRegions(icId, number, postbuildProfile),
            CompositionMemoryProjection.CreateCtrlRamReplaceInputSlots(icId, number, postbuildProfile, hasReadableBase: true),
            CompositionMemoryProjection.CreateReplaceMemoryDisplay(
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
