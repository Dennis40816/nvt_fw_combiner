using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.InputInspection;

internal sealed partial class FirmwareArtifactClassificationResolver
{
    internal bool IsCurrent(CtrlRamBaseInspection inspection)
    {
        return IsCurrent(inspection.ResolutionToken);
    }

    public bool IsCurrent(ResolutionToken resolutionToken)
    {
        return _catalog.TryGetCurrentSnapshot()?.ResolutionToken == resolutionToken;
    }

    internal CtrlRamBaseInspection ResolveCtrlRamBase(string icId, ResolvedCapability? exactCapability,
        ReadOnlyMemory<byte> candidate, CtrlRamAuthoringDraftState? draft, ICtrlRamAuthoringAdapter adapter)
    {
        CanonicalCapabilityCatalogSnapshot? publication = _catalog.TryGetCurrentSnapshot();
        FileStamp referenceStamp = FileStamp.FromBytes(candidate.Span);
        string ic = IcIdentifier.Normalize(icId);
        if (publication is null || (exactCapability is not null && !IsCurrentCapability(publication, ic, exactCapability)))
        {
            return new(CtrlRamBaseKind.Unknown, draft, [],
                [new(AuthoringSessionIssueCodes.StaleInspection, "Reference classification requires the current catalog publication.")],
                publication?.ResolutionToken ?? default, referenceStamp);
        }

        if (adapter.ResolveAbReferenceRoute(ic, IcNumberSelectionTokens.SingleChip).Succeeded ||
            adapter.ResolveAbReferenceRoute(ic, IcNumberSelectionTokens.Cascade).Succeeded ||
            adapter.ResolveAbReferenceRoute(ic, IcNumberSelectionTokens.CascadeTwoToEight).Succeeded)
        {
            List<(CompiledComposition Layout, ResolvedCapability Capability)> layouts =
                CompileAbLayoutsForReference(ic, candidate);
            var assessments = new List<AbReferenceCandidateAssessment>(layouts.Count);
            foreach ((CompiledComposition layout, ResolvedCapability layoutCapability) in layouts)
            {
                if (!IsCurrentCapability(publication, ic, layoutCapability) ||
                    layout.Plan.OutputInitialization.Capacity != candidate.Length)
                {
                    continue;
                }
                AbReferenceCandidateAssessment? assessment = InspectAbCandidate(
                    ic, publication, layout, candidate, adapter);
                if (assessment is not null)
                {
                    assessments.Add(assessment);
                }
            }

            // AB-TWO-NVT-1112-01: a compiled AB layout is AB evidence only through its trusted
            // structure or exactly one complete NVT marker in each canonical bank. Bank issues are
            // reported on the AB result but never decide the kind.
            AbReferenceCandidateAssessment[] trusted =
                [.. assessments.Where(static assessment => assessment.HasTrustedStructure)];
            AbReferenceCandidateAssessment[] recognized = trusted.Length != 0
                ? trusted
                : [.. assessments.Where(static assessment => assessment.HasOneNvtMarkerPerBank)];
            if (recognized.Length > 0)
            {
                return !IsCurrentSnapshot(publication)
                    ? new(CtrlRamBaseKind.Unknown, draft, [],
                        [new(AuthoringSessionIssueCodes.StaleInspection,
                            "The catalog changed during Reference classification.")],
                        publication.ResolutionToken, referenceStamp)
                    : recognized.Length == 1
                    ? new(CtrlRamBaseKind.AbFlash, draft as AbCtrlRamDraftState ?? new AbCtrlRamDraftState(),
                        recognized[0].Facts, recognized[0].Issues, publication.ResolutionToken, referenceStamp)
                    : new(CtrlRamBaseKind.AbFlash, draft as AbCtrlRamDraftState ?? new AbCtrlRamDraftState(), [],
                        [new("input.bank-reference.ambiguous", "More than one AB bank layout matches this Reference with equal evidence.",
                            CompositionSlotIds.ReplaceBase)], publication.ResolutionToken, referenceStamp);
            }
        }

        (CompiledFirmwareArtifactClassification? classified, ResolvedCapability? exactStandard,
            IReadOnlyList<ResolvedCapability>? consensusStandards) =
            ResolveWithExactStandardCapability(ic, exactCapability, candidate.Span);
        CtrlRamBaseKind kind = classified?.Kind switch
        {
            CompiledFirmwareArtifactKind.TpFirmware => CtrlRamBaseKind.StandardTp,
            CompiledFirmwareArtifactKind.FlashCode => CtrlRamBaseKind.StandardFlash,
            CompiledFirmwareArtifactKind.Unknown or null => CtrlRamBaseKind.Unknown,
            _ => throw new InvalidOperationException("Unknown compiled firmware artifact kind."),
        };
        byte? standardEventBufferFormat = null;
        if (kind is CtrlRamBaseKind.StandardTp or CtrlRamBaseKind.StandardFlash &&
            IsCurrentSnapshot(publication) &&
            FirmwareConfigMetadataReader.TryReadBackup(candidate.Span,
                ResolveStandardNvtEndFlag(exactStandard, consensusStandards),
                out FirmwareConfigMetadata standardConfig, out _) && standardConfig.IsFirmwareVersionBarValid)
        {
            standardEventBufferFormat = exactStandard?.MetadataPlan.ResolutionToken == publication.ResolutionToken
                ? FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(
                    exactStandard.MetadataPlan, candidate, standardConfig.StructureStart)?.EventBufferFormatVersion
                : consensusStandards is not null
                    ? ReadConsensusEventBufferFormat(consensusStandards, publication.ResolutionToken,
                        candidate, standardConfig.StructureStart)
                    : null;
        }
        return new(kind, kind == CtrlRamBaseKind.Unknown ? draft : draft as CtrlRamFirmwareVersionDraftState, [],
            !IsCurrentSnapshot(publication)
                ? [new(AuthoringSessionIssueCodes.StaleInspection, "The catalog changed during Reference classification.")]
                : kind == CtrlRamBaseKind.Unknown
                    ? [new("input.reference.unrecognized", "The captured Base is not an unambiguous Standard or trusted AB Reference.", CompositionSlotIds.ReplaceBase)]
                    : [],
            publication.ResolutionToken, referenceStamp, standardEventBufferFormat);
    }

    private List<(CompiledComposition Layout, ResolvedCapability Capability)> CompileAbLayoutsForReference(
        string icId, ReadOnlyMemory<byte> reference)
    {
        var candidates = new List<(CompiledComposition Layout, ResolvedCapability Capability)>();
        TopologySelection?[] selections =
        [
            null,
            .. _compiler.GetAbMergeTopologyChoices(icId).Select(static choice => choice.Selection),
        ];
        foreach (TopologySelection? selection in selections)
        {
            if (!_compiler.TryCompileAbMergeCapability(icId, selection, ["dp-ab-input"],
                    out _, out ResolvedCapability? published, out _) || published is null ||
                !_compiler.TryCompilePublishedDynamicCapability(published.Identity, reference.Length,
                    [new FirmwareArtifactPayload("dp-ab-input", reference.Span)], ["dp-ab-input"],
                    out CompiledComposition? candidate, out ResolvedCapability? captured, out _, selection) ||
                candidate is null || captured is null ||
                candidate.Plan.OutputInitialization.Capacity != reference.Length)
            {
                continue;
            }
            candidates.Add((candidate, captured));
        }
        return candidates;
    }

    private AbReferenceCandidateAssessment? InspectAbCandidate(string icId,
        CanonicalCapabilityCatalogSnapshot publication, CompiledComposition layout,
        ReadOnlyMemory<byte> reference, ICtrlRamAuthoringAdapter adapter)
    {
        FirmwareRegion[] banks = [.. layout.V2Details.Provenance.ResolvedMap.ImageMap.Regions
            .Where(static region => region.RegionId is "a-bank" or "b-bank")
            .OrderBy(static region => region.Range.Start)];
        if (banks.Length != 2 || banks.Any(bank => bank.Range.EndExclusive > reference.Length) ||
            banks[0].Range.Length != banks[1].Range.Length)
        {
            return null;
        }
        bool hasStandard = _compiler.TryCompileStandardMerge(icId, banks[0].Range.Length,
            out CompiledComposition? standard, out ResolvedCapability? standardCapability, out _) &&
            standardCapability is not null && IsCurrentCapability(publication, icId, standardCapability) &&
            ReferenceEquals(standardCapability.CompiledComposition, standard) &&
            standardCapability.MetadataPlan.ResolutionToken == publication.ResolutionToken;
        int plausibleBanks = hasStandard ? banks.Count(bank => CompiledFirmwareArtifactClassifier.Classify(standard!,
            reference.Span.Slice(checked((int)bank.Range.Start), checked((int)bank.Range.Length))).Kind ==
                CompiledFirmwareArtifactKind.FlashCode) : 0;
        // NVT-END-FLAG-1113-01: the AB layout declares each bank's NVT end flag in bank-local coordinates; only that
        // position is bank evidence and markers anywhere else in the bank never count. A migration-inventory layout
        // keeps the existing compatibility read; a failed declaration gives no marker evidence.
        FirmwareNvtEndFlagResolution bankEndFlag = layout.V2Details.Provenance.ResolvedMap.NvtEndFlagResolution;
        var facts = new List<CtrlRamBaseBankInspection>(2);
        var issues = new List<CompositionIssue>();
        bool oneNvtMarkerPerBank = true;
        foreach (FirmwareRegion bank in banks)
        {
            ReadOnlyMemory<byte> bytes = reference.Slice(checked((int)bank.Range.Start), checked((int)bank.Range.Length));
            bool readable = FirmwareConfigMetadataReader.TryReadBackup(bytes.Span, bankEndFlag,
                out FirmwareConfigMetadata config, out int markers);
            oneNvtMarkerPerBank &= markers == 1;
            bool valid = readable && config.IsFirmwareVersionBarValid && config.ChipNumber > 0;
            CompositionIssue[] bankIssues = valid ? [] :
                [!readable
                    ? new(markers > 1 ? "input.bank-reference.metadata-ambiguous" : "input.bank-reference.metadata-unreadable",
                        $"{bank.RegionId}: FWConfig/IC count unreadable; expected one valid Backup, marker count={markers}.", CompositionSlotIds.ReplaceBase)
                    : !config.IsFirmwareVersionBarValid
                        ? new("input.bank-reference.version-bar", $"{bank.RegionId}: FWConfig version/bar mismatch.", CompositionSlotIds.ReplaceBase)
                        : new("input.bank-reference.count-zero", $"{bank.RegionId}: IC count Read 0; a positive count is required.", CompositionSlotIds.ReplaceBase)];
            FirmwareConfigMetadataSnapshot? snapshot = valid ? new(config.StructureStart, config.CommonFwVersion,
                config.FirmwareVersion, config.FirmwareVersionBar, config.IsFirmwareVersionBarValid, config.FirmwareSubVersion,
                config.ChipNumber, config.ProjectId, null, config.Hardware) : null;
            facts.Add(new(bank.RegionId, bank.Range, snapshot,
                valid ? new(CompiledInputVersionKind.TpReferenceFirmwareConfig, config.FirmwareVersion,
                    config.FirmwareSubVersion) : null,
                CompiledInputArtifactObservationService.DecodeDpRegion(layout,
                    bank.RegionId == "a-bank" ? CompiledInputVersionKind.DpA : CompiledInputVersionKind.DpB,
                    bank.RegionId == "a-bank" ? "a-cmi-dp-version" : "b-cmi-dp-version", reference),
                eventBufferFormatVersion: valid
                    ? ReadBankEventBufferFormat(icId, publication.ResolutionToken,
                        hasStandard ? standard : null, hasStandard ? standardCapability : null, bytes, config)
                    : null, bankIssues));
            issues.AddRange(bankIssues);
        }
        if (facts.All(static bank => bank.FirmwareConfig is not null) &&
            facts[0].FirmwareConfig!.ChipNumber != facts[1].FirmwareConfig!.ChipNumber)
        {
            issues.Add(new("input.bank-reference.count", $"AB IC count mismatch: a-bank Read {facts[0].FirmwareConfig!.ChipNumber}, b-bank Read {facts[1].FirmwareConfig!.ChipNumber}.", CompositionSlotIds.ReplaceBase));
        }
        AbReferenceValidation validation = adapter.ValidateAbReference(layout, reference);
        issues.AddRange(validation.Issues);
        if (hasStandard && plausibleBanks != banks.Length)
        {
            issues.Add(new("input.bank-reference.content",
                "AB bank contents do not satisfy the declared Flash plausibility checks.",
                CompositionSlotIds.ReplaceBase));
        }
        return new([.. facts], [.. issues], validation.HasTrustedAbStructure, oneNvtMarkerPerBank);
    }

    /// <summary>
    /// F-1: the declaration of the exact Standard layout, or the one shared by every consensus Standard layout. No
    /// candidate, disagreeing candidates or a failed declaration give <see cref="FirmwareNvtEndFlagResolution.Unresolved"/>,
    /// so the event-buffer read is skipped instead of searching the whole image.
    /// </summary>
    internal static FirmwareNvtEndFlagResolution ResolveStandardNvtEndFlag(
        ResolvedCapability? exactStandard, IReadOnlyList<ResolvedCapability>? consensusStandards)
    {
        return FirmwareNvtEndFlagResolution.Common(exactStandard is not null
            ? [exactStandard.CompiledComposition.V2Details.Provenance.ResolvedMap.NvtEndFlagResolution]
            : consensusStandards is { Count: > 0 }
                ? [.. consensusStandards.Select(static capability =>
                    capability.CompiledComposition.V2Details.Provenance.ResolvedMap.NvtEndFlagResolution)]
                : []);
    }

    private sealed record AbReferenceCandidateAssessment(
        IReadOnlyList<CtrlRamBaseBankInspection> Facts,
        IReadOnlyList<CompositionIssue> Issues,
        bool HasTrustedStructure,
        bool HasOneNvtMarkerPerBank);

    private static byte? ReadConsensusEventBufferFormat(
        IReadOnlyList<ResolvedCapability> candidates, ResolutionToken resolutionToken,
        ReadOnlyMemory<byte> candidate, long structureStart)
    {
        var observations = new List<CanonicalEventBufferFieldObservation?>(candidates.Count);
        foreach (ResolvedCapability capability in candidates)
        {
            if (capability.ResolutionToken != resolutionToken ||
                capability.MetadataPlan.ResolutionToken != resolutionToken)
            {
                return null;
            }

            observations.Add(FirmwareConfigGeneralParametersProjector.ReadObservation(
                capability.MetadataPlan, candidate, structureStart)?.EventBuffer);
        }

        return SelectCommonEventBufferFormat(observations);
    }

    /// <inheritdoc />
    public byte? ReadCommonEventBufferFormatForTp(
        string icId,
        ResolutionToken capturedPublication,
        ReadOnlyMemory<byte> acceptedTpBytes,
        long expectedStructureStart)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedStructureStart);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot? publication = _catalog.TryGetCurrentSnapshot();
        if (publication is null || publication.ResolutionToken != capturedPublication)
        {
            return null;
        }

        StandardCandidate[]? candidates = ResolveCurrentCompositions(publication, normalizedIcId);
        if (candidates is null || candidates.Length == 0 ||
            candidates.Any(static candidate => candidate.Capability is null))
        {
            return null;
        }

        ResolvedCapability[] capabilities =
            [.. candidates.Select(static candidate => candidate.Capability!)];
        byte? observed = ReadConsensusEventBufferFormat(capabilities, capturedPublication,
            acceptedTpBytes, expectedStructureStart);
        return IsCurrentSnapshot(publication) ? observed : null;
    }

    internal static byte? SelectCommonEventBufferFormat(
        IReadOnlyList<CanonicalEventBufferFieldObservation?> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        CanonicalEventBufferFieldObservation? first = observations.Count == 0 ? null : observations[0];
        return first is not null && observations.All(observed => observed == first)
            ? first.Value
            : null;
    }

    private byte? ReadBankEventBufferFormat(string icId, ResolutionToken resolutionToken,
        CompiledComposition? standard, ResolvedCapability? standardCapability,
        ReadOnlyMemory<byte> bankBytes, FirmwareConfigMetadata validatedConfig)
    {
        // An accepted Standard compilation owns its exact metadata. Without one, the
        // existing full-image query supplies display authority, never execution support.
        ResolvedMetadataPlan? plan = standardCapability?.MetadataPlan ??
            _catalog.ResolveFullImageMetadataPlan(icId, bankBytes.Length).MetadataPlan;
        if (plan is null || plan.ResolutionToken != resolutionToken) { return null; }
        if (standard is not null)
        {
            MetadataPlanEntry[] matches = [.. plan.Entries.Select(static item => item.Definition)
                .Where(static entry => entry.StructureDefinition.Definition.DefinitionId ==
                    FirmwareConfigGeneralParametersContract.StructureId).Take(2)];
            if (matches.Length != 1) { return null; }
            MetadataPlanEntry entry = matches[0];
            FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap map = standard.V2Details.Provenance.ResolvedMap;
            if (bankBytes.Length != map.CapacityBytes ||
                entry.ImageMap.MapId != map.ImageMap.MapId ||
                entry.MemberId != standard.V2Details.Provenance.Context.MemberId ||
                entry.FamilyDefinition.FamilyContentHash != standard.V2Details.Provenance.Context.FamilyContentHash ||
                entry.ResolvedMap.ResolutionFingerprint != map.ResolutionFingerprint)
            {
                return null;
            }
        }
        return FirmwareConfigGeneralParametersProjector.ReadGeneralParameters(
            plan, bankBytes, validatedConfig.StructureStart)?.EventBufferFormatVersion;
    }
}
