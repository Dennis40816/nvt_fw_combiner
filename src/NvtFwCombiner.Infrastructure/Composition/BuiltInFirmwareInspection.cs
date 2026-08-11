using System.Collections.Concurrent;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using System.Text.RegularExpressions;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Single built-in firmware inspection owner over one canonical runtime publication.</summary>
internal sealed partial class BuiltInFirmwareInspection : IFirmwareInspection
{
    private const int FirmwareIcHintHeaderProbeLength = 256 * 1024;
    private static readonly ConcurrentDictionary<string, Lazy<CompiledComposition[]>>
        s_artifactClassificationCompositions = new(StringComparer.Ordinal);

    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly CanonicalCapabilityExperience _projection;
    private readonly StandardMergeAuthoringExperience _standardMergeAuthoring;
    private readonly AbMergeAuthoringExperience _abMergeAuthoring;
    private readonly DpReplaceAuthoringExperience _dpReplaceAuthoring;
    private readonly CtrlRamAuthoringExperience _ctrlRamAuthoring;

    internal BuiltInFirmwareInspection(
        ICanonicalCapabilityQuery catalog,
        CanonicalCapabilityExperience projection,
        StandardMergeAuthoringExperience standardMergeAuthoring,
        AbMergeAuthoringExperience abMergeAuthoring,
        DpReplaceAuthoringExperience dpReplaceAuthoring,
        CtrlRamAuthoringExperience ctrlRamAuthoring)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _standardMergeAuthoring = standardMergeAuthoring ??
            throw new ArgumentNullException(nameof(standardMergeAuthoring));
        _abMergeAuthoring = abMergeAuthoring ??
            throw new ArgumentNullException(nameof(abMergeAuthoring));
        _dpReplaceAuthoring = dpReplaceAuthoring ??
            throw new ArgumentNullException(nameof(dpReplaceAuthoring));
        _ctrlRamAuthoring = ctrlRamAuthoring ??
            throw new ArgumentNullException(nameof(ctrlRamAuthoring));
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
        Func<string, byte[]?> readFirmwareImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(readFirmwareImage);

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

        FirmwareInspectionStatusBatch dpInputBatch =
            inspection._dpReplaceAuthoring.InspectInputSlots(
                icId,
                inputs,
                ReadOnce);
        FirmwareInspectionStatusBatch standardMergeInputBatch =
            inspection._standardMergeAuthoring.InspectInputSlots(
                icId,
                inputs,
                ReadOnce);
        FirmwareInspectionStatusBatch ctrlRamInputBatch =
            inspection._ctrlRamAuthoring.InspectInputSlots(icId, inputs, ReadOnce);
        AbMergeInspectionBatch abMergeInputBatch =
            inspection._abMergeAuthoring.InspectInputSlots(
                icId,
                inputs,
                ReadOnce);
        List<FirmwareInspectionSnapshotResult> results = [];
        foreach (FirmwareInspectionSnapshotInput input in inputs)
        {
            FirmwareInspectionSnapshot snapshot = InspectFirmware(
                inspection,
                icId,
                input.Path,
                input.TpPath,
                input.CtrlRamRequest,
                ReadOnce);
            if (!string.IsNullOrWhiteSpace(input.AbMergeAddressSpaceId))
            {
                snapshot = snapshot with
                {
                    AbMergeFacts = abMergeInputBatch.Facts[input.InspectionId],
                    InputSlotStatus = abMergeInputBatch.Statuses[input.InspectionId],
                    InputSlotCatalog = abMergeInputBatch.Catalog,
                };
            }

            if (dpInputBatch.Statuses.TryGetValue(input.InspectionId, out AuthoringInputSlotStatus? status))
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

            if (ctrlRamInputBatch.Statuses.TryGetValue(
                    input.InspectionId,
                    out AuthoringInputSlotStatus? ctrlRamStatus))
            {
                snapshot = snapshot with
                {
                    InputSlotStatus = ctrlRamStatus,
                    InputSlotCatalog = ctrlRamInputBatch.Catalog,
                };
            }

            results.Add(new FirmwareInspectionSnapshotResult(input.InspectionId, snapshot));
        }

        return results;
    }

    internal static FirmwareInspectionSnapshot InspectFirmware(
        BuiltInFirmwareInspection inspection,
        string icId,
        string path,
        string? tpPath,
        CtrlRamInspectionRequest? ctrlRamRequest,
        Func<string, byte[]?> readFirmwareImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(readFirmwareImage);

        string? detectedIcId = DetectFirmwareIcHintFromFileName(path);
        byte[]? image = readFirmwareImage(path);
        if (image is null)
        {
            return new FirmwareInspectionSnapshot(detectedIcId, null, null, null, null, null);
        }

        byte[]? tpImage = string.IsNullOrWhiteSpace(tpPath)
            ? null
            : string.Equals(path, tpPath, StringComparison.Ordinal)
                ? image
                : readFirmwareImage(tpPath);
        FirmwareConfigMetadata? firmwareConfig =
            ReadFirmwareConfigMetadataValue(inspection._projection, icId, image);
        CompiledFirmwareArtifactClassification? artifactClassification =
            ClassifyBaseFirmwareArtifact(icId, image);
        BaseFirmwareArtifactKind artifactKind = artifactClassification?.Kind switch
        {
            CompiledFirmwareArtifactKind.TpFirmware => BaseFirmwareArtifactKind.TpFirmware,
            CompiledFirmwareArtifactKind.FlashCode => BaseFirmwareArtifactKind.FlashCode,
            CompiledFirmwareArtifactKind.Unknown or null => BaseFirmwareArtifactKind.Unknown,
            _ => throw new InvalidOperationException("Unknown compiled firmware artifact kind."),
        };
        bool shouldProjectDpMetadata = artifactKind != BaseFirmwareArtifactKind.TpFirmware;
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            inspection._projection,
            icId,
            firmwareConfig,
            out LegacyCombinerPostbuildProfile? resolvedProfile)
                ? resolvedProfile
                : null;
        CtrlRamInspectionDisplay? ctrlRamDisplay = ctrlRamRequest is { } request
            ? CreateCtrlRamInspectionDisplay(icId, request.NumberToken, postbuildProfile)
            : null;
        (DpVersionMetadata? Version, CmiDpCodeMetadata? Cmi)
            dpMetadata = shouldProjectDpMetadata
                ? ReadDpMetadata(
                    inspection,
                    icId,
                    image,
                    string.Equals(path, tpPath, StringComparison.Ordinal) ? null : tpImage)
                : (null, null);
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
        return CreateCtrlRamInspectionDisplay(icId, number, postbuildProfile);
    }

    private static (
        DpVersionMetadata? Version,
        CmiDpCodeMetadata? Cmi)
        ReadDpMetadata(
            BuiltInFirmwareInspection inspection,
            string icId,
            byte[] image,
            byte[]? tpImage)
    {
        if (TryReadCanonicalDpcmi(
                inspection,
                icId,
                image,
                tpImage,
                out DpcmiMetadataFacts? dpcmi))
        {
            return dpcmi is null
                ? (null, null)
                : (
                    new DpVersionMetadata(dpcmi.VersionToken),
                    new CmiDpCodeMetadata(
                        dpcmi.MajorVersion,
                        dpcmi.MinorVersion,
                        dpcmi.JiraNumber,
                        checked((int)dpcmi.ResolvedRange.Start)));
        }

        return (null, null);
    }

    private static bool TryReadCanonicalDpcmi(
        BuiltInFirmwareInspection inspection,
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
            resolution = inspection._catalog.ResolveUniqueRoute(
                normalizedIcId,
                ExperienceIds.DpReplace,
                "1-ic");
            if (StringComparer.Ordinal.Equals(
                    resolution.Issue?.Code,
                    CapabilityCatalogIssueCodes.RouteAmbiguous))
            {
                resolution = inspection._catalog.ResolveUniqueRoute(
                    normalizedIcId,
                    ExperienceIds.DpReplace,
                    "1-ic",
                    image.LongLength);
            }
        }
        else
        {
            resolution = inspection._catalog.ResolveUniqueRoute(
                normalizedIcId,
                ExperienceIds.StandardMerge,
                "selector-free",
                image.LongLength);
        }

        ResolvedMetadataPlan? plan = resolution.Capability?.MetadataPlan;
        bool declaresDpcmi = DeclaresDpcmi(plan);
        if (!declaresDpcmi && tpImage is not null)
        {
            CapabilityResolutionResult dpResolution =
                inspection._catalog.ResolveUniqueRoute(
                    normalizedIcId,
                    ExperienceIds.DpReplace,
                    "1-ic");
            if (StringComparer.Ordinal.Equals(
                    dpResolution.Issue?.Code,
                    CapabilityCatalogIssueCodes.RouteAmbiguous))
            {
                dpResolution = inspection._catalog.ResolveUniqueRoute(
                    normalizedIcId,
                    ExperienceIds.DpReplace,
                    "1-ic",
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

    internal static FirmwareConfigMetadataSnapshot? ReadFirmwareConfigMetadata(
        CanonicalCapabilityExperience projection,
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
        CanonicalCapabilityExperience projection,
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
        CanonicalCapabilityExperience projection,
        string icId,
        byte[]? image)
    {
        return image is not null && TryReadFirmwareConfigMetadataFromImage(
                projection,
                icId,
                image,
                out FirmwareConfigMetadata metadata)
                ? metadata
                : null;
    }

    private static bool TryReadFirmwareConfigMetadataFromImage(
        CanonicalCapabilityExperience projection,
        string icId,
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        return projection.IsKnownIcId(icId) &&
            FirmwareConfigMetadataReader.TryReadBackup(image, out metadata);
    }

    private static bool TryResolvePostbuildProfileForDisplay(
        CanonicalCapabilityExperience projection,
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
        CanonicalCapabilityExperience projection,
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

    private static CtrlRamInspectionDisplay CreateCtrlRamInspectionDisplay(
        string icId,
        string number,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return BuiltInCtrlRamAuthoringAdapter.CreateDisplay(
            icId,
            number,
            postbuildProfile,
            hasReadableBase: true);
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
