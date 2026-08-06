using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private const string RuntimeReferenceProfileShapeInvalid = "profile.v2.runtime-reference-replace.profile-shape-invalid";
    private const string RuntimeReferenceBindingInvalid = "profile.v2.runtime-reference-replace.binding-invalid";
    private const string RuntimeReferenceMappingInvalid = "profile.v2.runtime-reference-replace.mapping-invalid";
    private const string RuntimeReferenceSourceOutOfBounds = "profile.v2.runtime-reference-replace.source-out-of-bounds";
    private const string RuntimeReferenceTargetOutOfBounds = "profile.v2.runtime-reference-replace.target-out-of-bounds";
    private const string RuntimeReferenceCtrlRamTargetInvalid = "profile.v2.runtime-reference-replace.ctrlram-target-invalid";
    private const string RuntimeReferenceFirmwareVersionEditInvalid = "profile.v2.runtime-reference-replace.firmware-version-edit-invalid";
    private const string RuntimeReferenceProcessorRequired = "profile.v2.runtime-reference-replace.processor-required";
    private const string RuntimeReferenceProcessorOrderInvalid = "profile.v2.runtime-reference-replace.processor-order-invalid";

    /// <summary>Atomically admits and lowers one exact catalog-owned runtime reference Replace request.</summary>
    internal static bool TryCompileRuntimeReferenceReplaceAdmitted(
        TrustedProfileBundleCatalog catalog,
        TrustedCompositionProfileCatalogEntry profileEntry,
        FirmwareMapResolutionInputs resolutionInputs,
        V2RuntimeReferenceReplaceCompileRequest request,
        out V2CompositionPlanCompileResult? compilation,
        out IReadOnlyList<CompositionIssue> issues)
    {
        compilation = null;
        if (!V2CompositionPreparationService.TryPrepare(
                catalog,
                profileEntry,
                resolutionInputs,
                out FirmwareMapResolutionResult? mapResolution,
                out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
                out issues))
        {
            return false;
        }

        compilation = CompileRuntimeReferenceReplaceAdmittedCore(
            catalog.BundleIdentity,
            profileEntry,
            mapResolution!.ResolvedMap!,
            capabilityAdmissions,
            request);
        return true;
    }

    /// <summary>Lowers one admitted map-bound runtime reference Replace request through the shared plan algebra.</summary>
    private static V2CompositionPlanCompileResult CompileRuntimeReferenceReplaceAdmittedCore(
        ProfileBundleIdentity bundleIdentity,
        TrustedCompositionProfileCatalogEntry profileEntry,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(bundleIdentity);
        ArgumentNullException.ThrowIfNull(profileEntry);
        ArgumentNullException.ThrowIfNull(resolvedMap);
        ArgumentNullException.ThrowIfNull(capabilityAdmissions);
        ArgumentNullException.ThrowIfNull(request);
        CompositionProfileDefinition profile = profileEntry.Profile;
        var issues = new List<CompositionIssue>();
        if (!IsRuntimeReferenceReplaceProfile(profile))
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(
                    RuntimeReferenceProfileShapeInvalid,
                    "The admitted profile is not a closed map-bound runtime reference-replace shape.")]);
        }

        RuntimeReferenceReplaceProfileShape shape = AssertRuntimeReferenceReplaceProfileShape(profile);
        Dictionary<string, V2RuntimeReferenceReplaceInputBinding> bindings =
            ValidateRuntimeReferenceReplaceBindings(shape, resolvedMap, request, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        bool truncateCtrlRamSources =
            StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.CtrlRamReplace) &&
            shape.SourceSlot.Normalization is CompiledTruncateCtrlRamInputNormalization;
        var spaces = bindings.Values.ToDictionary(
            static binding => binding.BindingId,
            binding => new AddressSpace(
                binding.BindingId,
                binding.ExactLengthBytes,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy:
                    truncateCtrlRamSources && StringComparer.Ordinal.Equals(binding.SlotId, shape.SourceSlot.SlotId)
                        ? InputOversizePolicy.TruncateWithWarning
                        : InputOversizePolicy.Reject),
            StringComparer.Ordinal);
        spaces.Add(
            shape.Output.SpaceId,
            new AddressSpace(
                shape.Output.SpaceId,
                resolvedMap.CapacityBytes,
                AddressSpaceMutability.Mutable));
        Dictionary<string, ResolvedView> views = LowerViews(profile, resolvedMap, spaces, issues);
        LoweredRegionAccess regionAccess = LowerRegionAccess(profile, resolvedMap, views, issues);
        bool touchesTp = ValidateRuntimeReferenceReplaceMappings(
            shape,
            resolvedMap,
            request,
            bindings,
            regionAccess,
            issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] mappingOperations =
        [
            .. request.Mappings.Select(static mapping => CompositionOperation.ReplaceRange(
                mapping.MappingId,
                mapping.Sequence,
                mapping.SourceBindingId,
                mapping.SourceRange,
                mapping.TargetSpaceId,
                mapping.TargetRange,
                mapping.OverlapPolicy,
                mapping.Reason,
                mapping.Provenance)),
        ];
        RuntimeFirmwareVersionEditLowering firmwareVersionEdit = LowerRuntimeFirmwareVersionEdit(
            profile, shape, resolvedMap, request.FirmwareVersionEdit, regionAccess, issues);
        ValidateOperationOverlaps([.. firmwareVersionEdit.Operations, .. mappingOperations], issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] declaredProcessorOperations = LowerOperations(
            profile,
            resolvedMap,
            spaces,
            views,
            regionAccess,
            issues,
            useProcessorWriteAuthority: true);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        ValidatePostbuildPolicy(
            resolvedMap,
            request.PostbuildPolicy,
            bindings,
            mappingOperations,
            declaredProcessorOperations,
            issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] processorOperations = touchesTp
            ? NarrowRuntimeReferenceProcessorAuthority(
                resolvedMap,
                mappingOperations,
                firmwareVersionEdit.PostbuildWriteRanges,
                request.PostbuildPolicy,
                request.PostbuildWriteRangeSections,
                declaredProcessorOperations)
            : [];
        CompositionOperation[] operations = [.. firmwareVersionEdit.Operations, .. mappingOperations, .. processorOperations];

        V2RuntimeReferenceReplaceInputBinding referenceBinding = bindings.Values.Single(binding =>
            StringComparer.Ordinal.Equals(binding.SlotId, shape.ReferenceSlot.SlotId));
        var plan = new CompositionPlan(
            [ImageInitialization.Reference(shape.Output.SpaceId, referenceBinding.BindingId, resolvedMap.CapacityBytes)],
            shape.Output.SpaceId,
            spaces.Values,
            operations);
        IReadOnlyList<CompiledValidationRequirement> versionValidations = request.FirmwareVersionEdit is { } edit
            ? [CompiledValidationRequirements.FirmwareConfigBackupVersion(
                "verify-nvt-fwconfig-backup-version", edit.InvalidOutputIssueCode,
                edit.MismatchOutputIssueCode, edit.FirmwareVersion, edit.FirmwareSubVersion)]
            : [];
        IReadOnlyList<CompiledValidationRequirement> placementValidations =
            request.PostbuildPolicy is { } placement
                ?
                [
                    CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority(
                        "verify-nvt-fwconfig-backup-authority",
                        placement.InvalidPlacementIssueCode,
                        placement.InactiveMutationIssueCode,
                        referenceBinding.BindingId,
                        placement.ResolvedProcessorAuthority,
                        placement.FirmwareConfigBackupLength),
                    CompiledValidationRequirements.FirmwareConfigBackupExpectedAddress(
                        "verify-nvt-fwconfig-backup-expected-address",
                        placement.UnexpectedPlacementIssueCode,
                        placement.ExpectedFirmwareConfigBackupStart),
                ]
                : [];
        IReadOnlyList<CompiledValidationRequirement> inputValidations =
            request.PostbuildPolicy is
            {
                SourceAddressSpaceId: { } sourceAddressSpaceId,
                UniformSourceIssueCode: { } uniformSourceIssueCode,
            } sourcePolicy
                ?
                [
                    CompiledValidationRequirements.RejectUniformInputRanges(
                        "verify-dynamic-diffdlm-active-source-records",
                        CompiledValidationSeverity.Error,
                        uniformSourceIssueCode,
                        sourceAddressSpaceId,
                        sourcePolicy.RequiredNonuniformSourceRanges),
                ]
                : [];
        string[] processorWriteViewIds = shape.ProcessorOperation is null
            ? []
            :
            [
                .. profile.ProcessorStages
                    .OfType<LegacyCombinerProfileProcessorStage>()
                    .Single(stage => StringComparer.Ordinal.Equals(
                        stage.ProcessorStageId,
                        shape.ProcessorOperation.ProcessorStageId))
                    .AllowedWriteViewIds,
            ];
        return Succeed(
            profile,
            bundleIdentity,
            profileEntry.EntryIdentity,
            new RuntimeReferenceReplaceV2CompilationContext(
                resolvedMap,
                ((RuntimeReferenceReplaceProfileCompilationContext)profile.CompilationContext)
                    .AllowsConditionalProcessor,
                processorWriteViewIds),
            plan,
            profile.InputSlots.Select(slot => MapInputSlot(slot, resolvedMap)),
            bindings.Values.Select(binding => new CompiledInputSpaceBinding(
                binding.BindingId,
                binding.SlotId,
                StringComparer.Ordinal.Equals(binding.SlotId, shape.ReferenceSlot.SlotId)
                    ? CompiledInputInstancePolicy.Singleton
                    : CompiledInputInstancePolicy.PerBinding)),
            regionAccess.Contract,
            CompiledIcNumberPolicies.From(profile.IcNumberInputMode),
            capabilityAdmissions,
            additionalValidationRequirements:
            [
                .. inputValidations,
                .. versionValidations,
                .. placementValidations,
            ]);
    }

    private static RuntimeFirmwareVersionEditLowering LowerRuntimeFirmwareVersionEdit(
        CompositionProfileDefinition profile,
        RuntimeReferenceReplaceProfileShape shape,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        V2RuntimeReferenceReplaceFirmwareVersionEdit? edit,
        LoweredRegionAccess regionAccess, List<CompositionIssue> issues)
    {
        if (edit is null)
        {
            return RuntimeFirmwareVersionEditLowering.Empty;
        }

        if (!StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.CtrlRamReplace) ||
            shape.ProcessorOperation is null ||
            edit.SourceFirmwareVersionAndBarRange.Length != 2 ||
            edit.SourceFirmwareSubVersionRange.Length != 1 ||
            !TryResolveGoverningRegionChain(edit.SourceFirmwareVersionAndBarRange, regionAccess.RegionsById, out FirmwareRegion[] versionChain) ||
            !TryResolveGoverningRegionChain(edit.SourceFirmwareSubVersionRange, regionAccess.RegionsById, out FirmwareRegion[] subVersionChain) ||
            versionChain[^1] is not { Owner: FirmwareRegionOwner.Tp, Kind: FirmwareRegionKind.FirmwareConfig } sourceRegion ||
            subVersionChain[^1] is not { Owner: FirmwareRegionOwner.Tp, Kind: FirmwareRegionKind.FirmwareConfig } subVersionRegion ||
            !StringComparer.Ordinal.Equals(sourceRegion.RegionId, subVersionRegion.RegionId) ||
            !TryResolveFirmwareVersionBackupWrites(
                resolvedMap,
                shape.ReferenceSlot.SlotId,
                sourceRegion,
                edit,
                out ByteRange[] postbuildWriteRanges))
        {
            issues.Add(new CompositionIssue(RuntimeReferenceFirmwareVersionEditInvalid,
                "CtrlRAM TP-version edits must bind exact source fields in one canonical firmware-config region.", "firmware-version"));
            return RuntimeFirmwareVersionEditLowering.Empty;
        }

        CompositionOperation Patch(string id, int sequence, ByteRange range, byte[] bytes)
        {
            return CompositionOperation.PatchScalar(id, sequence, shape.Output.SpaceId, range, bytes, OverlapPolicy.Reject,
                "Apply the owner-confirmed TP FW version before postbuild.");
        }

        return new RuntimeFirmwareVersionEditLowering(
            [
                Patch("patch-fw-version-and-bar", 10, edit.SourceFirmwareVersionAndBarRange,
                    [edit.FirmwareVersion, unchecked((byte)~edit.FirmwareVersion)]),
                Patch("patch-fw-sub-version", 20, edit.SourceFirmwareSubVersionRange, [edit.FirmwareSubVersion]),
            ],
            postbuildWriteRanges);
    }

    private static bool TryResolveFirmwareVersionBackupWrites(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        string referenceSlotId,
        FirmwareRegion sourceRegion,
        V2RuntimeReferenceReplaceFirmwareVersionEdit edit,
        out ByteRange[] postbuildWriteRanges)
    {
        postbuildWriteRanges = [];
        if (!sourceRegion.Range.Contains(edit.SourceFirmwareVersionAndBarRange) ||
            !sourceRegion.Range.Contains(edit.SourceFirmwareSubVersionRange))
        {
            return false;
        }

        long versionOffset = checked(edit.SourceFirmwareVersionAndBarRange.Start - sourceRegion.Range.Start);
        long subVersionOffset = checked(edit.SourceFirmwareSubVersionRange.Start - sourceRegion.Range.Start);
        ByteRange[] candidates =
        [
            .. resolvedMap.ResolvedMetadataStructures
                .Where(structure => StringComparer.Ordinal.Equals(
                    structure.DecodedStructure.ArtifactBindingId,
                    referenceSlotId))
                .SelectMany(structure =>
                {
                    ByteRange envelope = structure.LocatorOutcome.ResolvedRange.Range;
                    ByteRange version = new(checked(envelope.Start + versionOffset), edit.SourceFirmwareVersionAndBarRange.Length);
                    ByteRange subVersion = new(checked(envelope.Start + subVersionOffset), edit.SourceFirmwareSubVersionRange.Length);
                    return envelope.Contains(version) && envelope.Contains(subVersion)
                        ? [version, subVersion]
                        : Array.Empty<ByteRange>();
                }),
        ];
        if (candidates.Length != 2)
        {
            return false;
        }

        postbuildWriteRanges = candidates;
        return true;
    }


    private static bool IsRuntimeReferenceReplaceProfile(CompositionProfileDefinition profile)
    {
        bool isGeneralReplace = StringComparer.Ordinal.Equals(
            profile.Experience.ExperienceId,
            ExperienceIds.GeneralReplace);
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            profile.Experience.ExperienceId,
            ExperienceIds.CtrlRamReplace);
        return profile.CompilationContext is RuntimeReferenceReplaceProfileCompilationContext &&
            profile.CompositionKind == CompositionKind.Replace &&
            ((isGeneralReplace &&
              profile.Experience.LayoutPolicy == LayoutPolicy.UserDefined &&
              profile.Experience.InputPolicy == InputPolicy.Extensible) ||
             (isCtrlRamReplace &&
              profile.Experience.LayoutPolicy == LayoutPolicy.Fixed &&
              profile.Experience.InputPolicy == InputPolicy.Fixed)) &&
            profile.MetadataBindings.Count == 0 &&
            profile.RegionAccessRules.Count != 0 &&
            profile.Validations.Count == 0 &&
            ((!isCtrlRamReplace && profile.ProcessorStages.Count == 0) ||
             profile.CompilationContext is RuntimeReferenceReplaceProfileCompilationContext
             {
                 AllowsConditionalProcessor: true,
             });
    }

    internal static bool TryGetRuntimeReferenceReplaceReferenceSlotId(
        CompositionProfileDefinition profile,
        out string referenceSlotId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        referenceSlotId = string.Empty;
        if (!IsRuntimeReferenceReplaceProfile(profile))
        {
            return false;
        }

        try
        {
            referenceSlotId = AssertRuntimeReferenceReplaceProfileShape(profile).ReferenceSlot.SlotId;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static RuntimeReferenceReplaceProfileShape AssertRuntimeReferenceReplaceProfileShape(
        CompositionProfileDefinition profile)
    {
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            profile.Experience.ExperienceId,
            ExperienceIds.CtrlRamReplace);
        CompiledInputArtifactClass expectedSourceClass = isCtrlRamReplace
            ? CompiledInputArtifactClass.CtrlRamReplacement
            : CompiledInputArtifactClass.Auxiliary;
        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        CloneProfileInitializer clone = output.Capacity is RuntimeRequestProfileCapacity &&
            output.Initializer is CloneProfileInitializer initializer
            ? initializer
            : throw new InvalidOperationException("Validated runtime reference-replace profile has an invalid output space.");
        CompositionInputSlotDefinition reference = profile.InputSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        CompositionInputSlotDefinition source = profile.InputSlots.Single(slot =>
            !StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        InputArtifactProfileSpace referenceSpace = profile.Spaces.OfType<InputArtifactProfileSpace>().Single(space =>
            StringComparer.Ordinal.Equals(space.SlotId, reference.SlotId));
        InputArtifactProfileSpace sourceSpace = profile.Spaces.OfType<InputArtifactProfileSpace>().Single(space =>
            StringComparer.Ordinal.Equals(space.SlotId, source.SlotId));
        bool sourceNormalizationIsValid = isCtrlRamReplace
            ? source.Normalization is CompiledTruncateCtrlRamInputNormalization
            : source.Normalization is CompiledNoInputNormalization;
        return reference is not
        {
            Required: true,
            ArtifactClass: CompiledInputArtifactClass.ReferenceImage,
            Cardinality: CompiledInputSlotCardinality.ExactlyOne,
            LengthRequirement: ResolvedMapCapacityInputLengthDefinition,
            Normalization: CompiledNoInputNormalization,
        } ||
            source is not
            {
                Required: true,
                Cardinality: CompiledInputSlotCardinality.OneOrMore,
                LengthRequirement: CompiledBoundedInputLengthRequirement { MinimumBytes: 1, MaximumBytes: int.MaxValue },
            } ||
            source.ArtifactClass != expectedSourceClass ||
            !sourceNormalizationIsValid ||
            referenceSpace.InstancePolicy != CompiledInputInstancePolicy.Singleton ||
            sourceSpace.InstancePolicy != CompiledInputInstancePolicy.PerBinding
            ? throw new InvalidOperationException("Validated runtime reference-replace profile has an invalid input contract.")
            : new RuntimeReferenceReplaceProfileShape(
                reference,
                source,
                output,
                profile.Operations.OfType<RunProcessorProfileOperation>().SingleOrDefault());
    }

    private static Dictionary<string, V2RuntimeReferenceReplaceInputBinding> ValidateRuntimeReferenceReplaceBindings(
        RuntimeReferenceReplaceProfileShape shape,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        V2RuntimeReferenceReplaceCompileRequest request,
        List<CompositionIssue> issues)
    {
        var bindings = new Dictionary<string, V2RuntimeReferenceReplaceInputBinding>(StringComparer.Ordinal);
        int referenceCount = 0;
        int sourceCount = 0;
        foreach (V2RuntimeReferenceReplaceInputBinding? binding in request.Bindings)
        {
            bool isReference = binding is not null && StringComparer.Ordinal.Equals(binding.SlotId, shape.ReferenceSlot.SlotId);
            bool isSource = binding is not null && StringComparer.Ordinal.Equals(binding.SlotId, shape.SourceSlot.SlotId);
            bool valid = binding is not null &&
                !string.IsNullOrWhiteSpace(binding.BindingId) &&
                !StringComparer.Ordinal.Equals(binding.BindingId, shape.Output.SpaceId) &&
                (isReference || isSource) &&
                binding.ExactLengthBytes > 0 &&
                (isReference
                    ? binding.ExactLengthBytes == resolvedMap.CapacityBytes
                    : binding.ExactLengthBytes <= int.MaxValue) &&
                bindings.TryAdd(binding.BindingId, binding);
            if (!valid)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceBindingInvalid,
                    "Runtime reference-replace bindings must be unique declared reference or source instances with valid exact lengths."));
                continue;
            }

            referenceCount += isReference ? 1 : 0;
            sourceCount += isSource ? 1 : 0;
        }

        if (referenceCount != 1 || sourceCount == 0)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceBindingInvalid,
                "Runtime reference-replace compilation requires exactly one map-capacity reference binding and one or more experience-owned source bindings."));
        }

        return bindings;
    }

    private static bool ValidateRuntimeReferenceReplaceMappings(
        RuntimeReferenceReplaceProfileShape shape,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        V2RuntimeReferenceReplaceCompileRequest request,
        Dictionary<string, V2RuntimeReferenceReplaceInputBinding> bindings,
        LoweredRegionAccess regionAccess,
        List<CompositionIssue> issues)
    {
        bool touchesTp = false;
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            resolvedMap.ModeId,
            ExperienceIds.CtrlRamReplace);

        var mappingIds = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<int>();
        var referencedSourceBindingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExplicitMapping? mapping in request.Mappings)
        {
            if (mapping is null ||
                string.IsNullOrWhiteSpace(mapping.MappingId) ||
                !mappingIds.Add(mapping.MappingId) ||
                !sequences.Add(mapping.Sequence) ||
                mapping.OperationKind != ExplicitMappingOperationKind.ReplaceRange ||
                mapping.OverlapPolicy != OverlapPolicy.Reject ||
                !StringComparer.Ordinal.Equals(mapping.TargetSpaceId, shape.Output.SpaceId) ||
                mapping.TargetRegionId is not null ||
                mapping.Alignment != 1 ||
                mapping.SourceRange.Length != mapping.TargetRange.Length)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceMappingInvalid,
                    "Runtime reference-replace mappings must be uniquely ordered unaligned ReplaceRange writes to the output without caller-owned region authority.",
                    mapping?.MappingId));
                continue;
            }

            if (!bindings.TryGetValue(mapping.SourceBindingId, out V2RuntimeReferenceReplaceInputBinding? source) ||
                !StringComparer.Ordinal.Equals(source.SlotId, shape.SourceSlot.SlotId))
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceMappingInvalid,
                    "Runtime reference-replace mappings must read one declared source binding, never the reference image.",
                    mapping.MappingId));
                continue;
            }

            _ = referencedSourceBindingIds.Add(source.BindingId);
            if (mapping.SourceRange.EndExclusive > source.ExactLengthBytes)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceSourceOutOfBounds,
                    "Runtime reference-replace mapping source range escapes its concrete immutable binding.",
                    mapping.MappingId));
            }

            if (mapping.TargetRange.EndExclusive > resolvedMap.CapacityBytes)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceTargetOutOfBounds,
                    "Runtime reference-replace mapping target range escapes the resolved physical image map.",
                    mapping.MappingId));
                continue;
            }

            if (!TryResolveGoverningRegionChain(
                    mapping.TargetRange,
                    regionAccess.RegionsById,
                    out FirmwareRegion[] governingRegionChain))
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceTargetOutOfBounds,
                    "Runtime reference-replace mapping target range is not contained by one canonical physical region chain.",
                    mapping.MappingId));
                continue;
            }

            FirmwareRegion governingRegion = governingRegionChain[^1];
            if (isCtrlRamReplace &&
                (governingRegion.Owner != FirmwareRegionOwner.Tp ||
                 governingRegion.Kind != FirmwareRegionKind.CtrlRam))
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceCtrlRamTargetInvalid,
                    "CtrlRAM Replace mappings must target one canonical TP-owned CtrlRAM region.",
                    mapping.MappingId));
                continue;
            }

            touchesTp |= resolvedMap.ImageMap.Regions.Any(region =>
                region.Owner == FirmwareRegionOwner.Tp &&
                region.Range.Overlaps(mapping.TargetRange));

            _ = TryAuthorizeTargetWrite(
                mapping.MappingId,
                "runtime-request-target",
                new ResolvedView(
                    shape.Output.SpaceId,
                    mapping.TargetRange,
                    governingRegionChain,
                    IsSourceOnly: false),
                regionAccess,
                issues);
        }

        int sourceBindingCount = bindings.Values.Count(binding =>
            StringComparer.Ordinal.Equals(binding.SlotId, shape.SourceSlot.SlotId));
        if (request.Mappings.Count == 0 || referencedSourceBindingIds.Count != sourceBindingCount)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceMappingInvalid,
                "Runtime reference-replace compilation requires mappings for every concrete auxiliary source binding.",
                "mappings"));
        }

        if (touchesTp && shape.ProcessorOperation is null)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceProcessorRequired,
                "A runtime reference Replace mapping touches a TP-owned canonical region, but the selected profile has no approved Legacy Combiner refresh stage.",
                "mappings"));
        }
        else if (touchesTp && request.Mappings.Any(mapping =>
                     mapping is not null && mapping.Sequence >= shape.ProcessorOperation!.Sequence))
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceProcessorOrderInvalid,
                "Every runtime reference Replace mapping must run before the profile-owned Legacy Combiner refresh stage.",
                shape.ProcessorOperation!.OperationId));
        }

        return touchesTp;
    }

    private sealed record RuntimeReferenceReplaceProfileShape(
        CompositionInputSlotDefinition ReferenceSlot,
        CompositionInputSlotDefinition SourceSlot,
        MutableCompositionProfileSpace Output,
        RunProcessorProfileOperation? ProcessorOperation);

    private sealed record RuntimeFirmwareVersionEditLowering(
        CompositionOperation[] Operations,
        ByteRange[] PostbuildWriteRanges)
    {
        internal static RuntimeFirmwareVersionEditLowering Empty { get; } = new([], []);
    }
}
