using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
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
    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly CanonicalCapabilityExperience _projection;
    private readonly StandardMergeAuthoringExperience _standardMergeAuthoring;
    private readonly AbMergeAuthoringExperience _abMergeAuthoring;
    private readonly DpReplaceAuthoringExperience _dpReplaceAuthoring;
    private readonly CtrlRamAuthoringExperience _ctrlRamAuthoring;
    private readonly ISelectedFileContentInspector _contentInspector;

    internal BuiltInFirmwareInspection(
        ICanonicalCapabilityQuery catalog,
        CanonicalCapabilityExperience projection,
        StandardMergeAuthoringExperience standardMergeAuthoring,
        AbMergeAuthoringExperience abMergeAuthoring,
        DpReplaceAuthoringExperience dpReplaceAuthoring,
        CtrlRamAuthoringExperience ctrlRamAuthoring,
        ISelectedFileContentInspector? contentInspector = null)
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
            FirmwareInspectionSnapshot snapshot = InspectFirmware(
                inspection,
                icId,
                input.Path,
                input.TpPath,
                input.CtrlRamRequest,
                ReadOnce,
                input.ExactCapability,
                input.StandardMergeAddressSpaceId);
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
        string? standardMergeAddressSpaceId = null)
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
            ClassifyBaseFirmwareArtifact(inspection, icId, exactCapability, image);
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
                    inspection,
                    icId,
                    image,
                    string.Equals(path, tpPath, StringComparison.Ordinal) ? null : tpImage,
                    standardMergeAddressSpaceId)
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

    private static (
        DpVersionMetadata? Version,
        CmiDpCodeMetadata? Cmi,
        FirmwareMetadataPrerequisite? Prerequisite)
        ReadDpMetadata(
            BuiltInFirmwareInspection inspection,
            string icId,
            byte[] image,
            byte[]? tpImage,
            string? standardMergeAddressSpaceId)
    {
        if (TryReadCanonicalDpcmi(
                inspection,
                icId,
                image,
                tpImage,
                standardMergeAddressSpaceId,
                out DpcmiMetadataFacts? dpcmi,
                out FirmwareMetadataPrerequisite? prerequisite))
        {
            return dpcmi is null
                ? (null, null, prerequisite)
                : (
                    new DpVersionMetadata(dpcmi.VersionToken),
                    new CmiDpCodeMetadata(
                        dpcmi.MajorVersion,
                        dpcmi.MinorVersion,
                        dpcmi.JiraNumber,
                        checked((int)dpcmi.ResolvedRange.Start)),
                    null);
        }

        return (null, null, null);
    }

    private static bool TryReadCanonicalDpcmi(
        BuiltInFirmwareInspection inspection,
        string icId,
        byte[] image,
        byte[]? tpImage,
        string? standardMergeAddressSpaceId,
        out DpcmiMetadataFacts? facts,
        out FirmwareMetadataPrerequisite? prerequisite)
    {
        facts = null;
        prerequisite = null;
        string normalizedIcId = IcIdentifier.Normalize(icId);
        bool isStandardMergeDpInput = StringComparer.Ordinal.Equals(
            standardMergeAddressSpaceId,
            CompositionAddressSpaceIds.DpInput);
        CapabilityResolutionResult resolution;
        if (tpImage is null && !isStandardMergeDpInput)
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
        if (!declaresDpcmi)
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
                .Where(spaceId =>
                    !isStandardMergeDpInput ||
                    tpImage is not null ||
                    !StringComparer.Ordinal.Equals(
                        spaceId,
                        CompositionAddressSpaceIds.TpInput))
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
        else
        {
            prerequisite = snapshot.Results
                .Single(result => StringComparer.Ordinal.Equals(
                    result.PlanEntry.Definition.StructureDefinition.Definition.DefinitionId,
                    DpcmiMetadataContract.StructureId))
                .Resolution?
                .Prerequisite;
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
        BuiltInFirmwareInspection inspection,
        string icId,
        ResolvedCapability? exactCapability,
        ReadOnlySpan<byte> image)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot snapshot = inspection._catalog.GetCurrentSnapshot();
        IEnumerable<ResolvedCapability> capabilities = exactCapability is null
            ? snapshot.Capabilities
            : snapshot.Capabilities.Append(exactCapability);
        CompiledComposition[] compositions =
        [
            .. capabilities
                .Where(capability =>
                    capability.ResolutionToken == snapshot.ResolutionToken &&
                    StringComparer.Ordinal.Equals(capability.Identity.IcId, normalizedIcId) &&
                    StringComparer.Ordinal.Equals(
                        capability.Identity.WorkflowId,
                        ExperienceIds.StandardMerge))
                .Select(static capability => capability.CompiledComposition)
                .DistinctBy(static composition => composition.CompilationFingerprint),
        ];
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
