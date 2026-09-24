using System.Buffers.Binary;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    /// <summary>Prepares the trusted Perfect-family bank route without granting product execution eligibility.</summary>
    internal static V2RuntimeReferenceBankReplacePlan PrepareAbRuntimeReferenceReplace(
        CompiledComposition abLayout,
        FirmwareArtifactPayload reference,
        BankReferenceReplaceDefinition definition,
        int topologyCount,
        TrustedProfileBundleCatalog localCatalog,
        IReadOnlyList<V2RuntimeReferenceBankReplaceRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(abLayout);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(localCatalog);
        ArgumentNullException.ThrowIfNull(requests);
        if (definition.FinalizationKind == BankReferenceFinalizationKind.RunAbHeaderProcessor)
        {
            return PreparePartialAbRuntimeReferenceReplace(abLayout, reference, definition,
                topologyCount, localCatalog, requests);
        }
        RequireBankShape(requests.Count is >= 1 and <= 2, "One or two selected banks are required.");
        (FirmwareImageMap map, FirmwareRegionInstance a, FirmwareRegionInstance b, long capacity, long delta,
            CompositionOperation[] relocation) = ValidateAbReferenceLayout(abLayout, reference, definition);
        var banks = new List<V2RuntimeReferenceBankReplaceBinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        V2ExplicitMappingInputBinding[]? sharedSources = null;
        foreach (V2RuntimeReferenceBankReplaceRequest request in requests.OrderBy(static request => request.BankInstanceId, StringComparer.Ordinal))
        {
            RequireBankShape(request.BankInstanceId is "a-bank" or "b-bank" && seen.Add(request.BankInstanceId),
                "Selected banks must be unique canonical A/B instances.");
            V2ExplicitMappingInputBinding[] references = [.. request.Replace.Bindings.Where(static binding => binding.SlotId == CompositionAddressSpaceIds.ReferenceBase)];
            RequireBankShape(references.Length == 1 && references[0].BindingId == reference.ArtifactId &&
                references[0].ExactLengthBytes == capacity, "Bank-local Reference binding must identify the captured AB Reference and local capacity.");
            V2ExplicitMappingInputBinding[] sources = [.. request.Replace.Bindings.Where(static binding => binding.SlotId != CompositionAddressSpaceIds.ReferenceBase)
                .OrderBy(static binding => binding.BindingId, StringComparer.Ordinal)];
            RequireBankShape(sharedSources is null || sources.Select(static item => (item.BindingId, item.SlotId, item.ExactLengthBytes))
                .SequenceEqual(sharedSources.Select(static item => (item.BindingId, item.SlotId, item.ExactLengthBytes))),
                "Selected banks must share the same source bindings.");
            sharedSources = sources;
            long start = request.BankInstanceId == a.InstanceId ? a.BaseOffset : b.BaseOffset;
            var localReference = new FirmwareArtifactPayload(reference.ArtifactId,
                reference.Bytes.Slice(checked((int)start), checked((int)capacity)));
            CompiledComposition local = CompileBankLocalReference(definition, topologyCount,
                localCatalog, localReference, request);
            if (banks.Count == 0)
            {
                ValidateBankNativeInputs(map, local.V2Details.Provenance.ResolvedMap.ImageMap,
                    reference, relocation.Single(static operation => operation.OperationId == "relocate-tpb-diff").TargetRange,
                    capacity, delta, topologyCount);
            }

            RequireBankShape(local.Plan.OutputInitialization.Kind == ImageInitializationKind.Reference &&
                local.Plan.OutputInitialization.Capacity == capacity && local.Plan.Initializations.Count == 1 &&
                local.Plan.OrderedOperations.Count(static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor) == 1,
                "Bank-local compilation must retain the existing single Replace/postbuild contract.");
            foreach (CompiledValidationRequirement validation in local.V2Details.Provenance.ValidationRequirements)
            {
                RequireBankShape(validation is CompiledUniformInputRangeValidation or CompiledFirmwareConfigBackupVersionValidation or
                    CompiledFirmwareConfigBackupPlacementAuthorityValidation or CompiledFirmwareConfigBackupExpectedAddressValidation,
                    "Unsupported bank-local validation obligation.");
            }

            banks.Add(new V2RuntimeReferenceBankReplaceBinding(request.BankInstanceId, $"ab-replace/{request.BankInstanceId}/work",
                new ByteRange(start, capacity), new FirmwareArtifactPayload($"ab-replace/{request.BankInstanceId}/reference", localReference.Bytes), local));
        }

        const string output = "ab-replace/output";
        var spaces = new Dictionary<string, AddressSpace>(StringComparer.Ordinal)
        {
            [reference.ArtifactId] = new(reference.ArtifactId, reference.LengthBytes, AddressSpaceMutability.Immutable),
            [output] = new(output, reference.LengthBytes, AddressSpaceMutability.Mutable),
        };
        var initializations = new List<ImageInitialization> { ImageInitialization.Reference(output, reference.ArtifactId, reference.LengthBytes) };
        var operations = new List<CompositionOperation>();
        foreach (V2RuntimeReferenceBankReplaceBinding bank in banks)
        {
            spaces.Add(bank.Reference.ArtifactId, new AddressSpace(bank.Reference.ArtifactId, capacity, AddressSpaceMutability.Immutable));
            spaces.Add(bank.WorkspaceId, new AddressSpace(bank.WorkspaceId, capacity, AddressSpaceMutability.Mutable));
            initializations.Add(ImageInitialization.Blank(bank.WorkspaceId, capacity, 0));
            foreach (AddressSpace space in bank.LocalComposition.Plan.AddressSpaces.Where(space =>
                         space.Mutability == AddressSpaceMutability.Immutable && space.AddressSpaceId != reference.ArtifactId))
            {
                RequireBankShape(!space.AddressSpaceId.StartsWith("ab-replace/", StringComparison.Ordinal), "Input binding collides with a private bank workspace.");
                _ = spaces.TryAdd(space.AddressSpaceId, space);
            }

            var wholeBank = new ByteRange(0, capacity);
            operations.Add(CompositionOperation.CopyRange($"{bank.BankInstanceId}/seed", operations.Count,
                bank.Reference.ArtifactId, wholeBank, bank.WorkspaceId, wholeBank, OverlapPolicy.Reject, "Clone only this bank's immutable Reference bytes."));
            if (bank.BankInstanceId == b.InstanceId)
            {
                foreach (CompositionOperation transform in relocation)
                {
                    operations.Add(BankRelocation(transform, bank, operations.Count,
                        ReadBankAddress(reference.Bytes, delta + transform.TargetRange.Start), restore: false));
                }
            }
        }

        foreach (V2RuntimeReferenceBankReplaceBinding bank in banks)
        {
            foreach (CompositionOperation operation in bank.LocalComposition.Plan.OrderedOperations)
            {
                operations.Add(RebindBankOperation(operation, bank, operations.Count));
            }

            if (bank.BankInstanceId == b.InstanceId)
            {
                foreach (CompositionOperation transform in relocation)
                {
                    operations.Add(BankRelocation(transform, bank, operations.Count,
                        ReadBankAddress(reference.Bytes, transform.TargetRange.Start), restore: true));
                }
            }

            operations.Add(CompositionOperation.CopyRange($"{bank.BankInstanceId}/publish", operations.Count,
                bank.WorkspaceId, new ByteRange(0, capacity), output, bank.OutputRange, OverlapPolicy.Reject,
                "Copy the selected processed bank into the immutable AB Reference clone."));
        }

        var issues = new List<CompositionIssue>();
        ValidateOperationOverlaps(operations, issues);
        RequireBankShape(issues.Count == 0, string.Join("; ", issues.Select(static issue => issue.Message)));
        return new V2RuntimeReferenceBankReplacePlan(abLayout, reference, definition,
            new CompositionPlan(initializations, output, spaces.Values, operations), banks);
    }

    /// <summary>Read-only structural verification shared by detection and execution; no replacement plan is created.</summary>
    internal static void ValidateAbReference(CompiledComposition abLayout, FirmwareArtifactPayload reference,
        BankReferenceReplaceDefinition definition, FirmwareImageMap localMap)
    {
        if (definition.FinalizationKind == BankReferenceFinalizationKind.RunAbHeaderProcessor)
        {
            _ = ValidatePartialAbReferenceLayout(abLayout, reference, definition, localMap);
            return;
        }
        (FirmwareImageMap map, _, _, long capacity, long delta, CompositionOperation[] relocation) =
            ValidateAbReferenceLayout(abLayout, reference, definition);
        ValidateBankNativeInputs(map, localMap, reference,
            relocation.Single(static operation => operation.OperationId == "relocate-tpb-diff").TargetRange,
            capacity, delta, ReadAbNativeCount(abLayout, reference));
    }

    private static V2RuntimeReferenceBankReplacePlan PreparePartialAbRuntimeReferenceReplace(
        CompiledComposition abLayout, FirmwareArtifactPayload reference, BankReferenceReplaceDefinition definition,
        int topologyCount, TrustedProfileBundleCatalog localCatalog,
        IReadOnlyList<V2RuntimeReferenceBankReplaceRequest> requests)
    {
        RequireBankShape(requests.Count is >= 1 and <= 2, "One or two selected banks are required.");
        TrustedCompositionProfileCatalogEntry localProfile = localCatalog.SelectProfile(
            definition.Local.ProfileId, definition.Local.ProfileVersion, out _) ??
            throw new ArgumentException("Partial AB local profile is no longer trusted.");
        IReadOnlyList<FirmwareImageMap> localMaps = localCatalog.GetMapVariants(
            definition.Local.ProfileId, definition.Local.ProfileVersion, definition.Local.MemberId,
            localProfile.Profile.Header.ExperienceId, out _, out IReadOnlyList<CompositionIssue> mapIssues);
        RequireBankShape(mapIssues.Count == 0, string.Join("; ", mapIssues.Select(static issue => issue.Message)));
        FirmwareImageMap localMap = localMaps.Single(map => map.MapId == definition.Local.MapId);
        (FirmwareImageMap map, FirmwareRegion a, FirmwareRegion b, long delta,
            CompositionOperation diff, CompositionOperation finalizer, ByteRange[] headerAddressFields,
            ByteRange processorView) =
            ValidatePartialAbReferenceLayout(abLayout, reference, definition, localMap);
        int nativeCount = ReadPartialAbNativeCount(abLayout, reference, definition.Local,
            localProfile.Family.Family);
        RequireBankShape(nativeCount == topologyCount,
            $"AB native IC count Read {nativeCount}; selected topology is {topologyCount}.");

        var banks = new List<V2RuntimeReferenceBankReplaceBinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        V2ExplicitMappingInputBinding[]? sharedSources = null;
        foreach (V2RuntimeReferenceBankReplaceRequest request in requests.OrderBy(static item => item.BankInstanceId, StringComparer.Ordinal))
        {
            RequireBankShape(request.BankInstanceId is "a-bank" or "b-bank" && seen.Add(request.BankInstanceId),
                "Selected banks must be unique canonical A/B instances.");
            V2ExplicitMappingInputBinding[] references = [.. request.Replace.Bindings.Where(static binding =>
                binding.SlotId == CompositionAddressSpaceIds.ReferenceBase)];
            RequireBankShape(references.Length == 1 && references[0].BindingId == reference.ArtifactId &&
                references[0].ExactLengthBytes == definition.Local.CapacityBytes,
                "Bank-local Reference binding must identify the captured AB Reference and its local prefix.");
            V2ExplicitMappingInputBinding[] sources = [.. request.Replace.Bindings.Where(static binding =>
                binding.SlotId != CompositionAddressSpaceIds.ReferenceBase).OrderBy(static binding => binding.BindingId,
                StringComparer.Ordinal)];
            RequireBankShape(sharedSources is null || sources.Select(static item =>
                    (item.BindingId, item.SlotId, item.ExactLengthBytes)).SequenceEqual(sharedSources.Select(static item =>
                    (item.BindingId, item.SlotId, item.ExactLengthBytes))),
                "Selected banks must share the same source bindings.");
            sharedSources = sources;
            long bankStart = request.BankInstanceId == "a-bank" ? a.Range.Start : b.Range.Start;
            long localStart = checked(bankStart + definition.LocalBankRange.Start);
            var localReference = new FirmwareArtifactPayload(reference.ArtifactId,
                reference.Bytes.Slice(checked((int)localStart), checked((int)definition.LocalBankRange.Length)));
            CompiledComposition local = CompileBankLocalReference(definition, topologyCount,
                localCatalog, localReference, request);
            RequireBankShape(local.Plan.OutputInitialization.Kind == ImageInitializationKind.Reference &&
                local.Plan.OutputInitialization.Capacity == definition.Local.CapacityBytes &&
                local.Plan.Initializations.Count == 1 &&
                local.Plan.OrderedOperations.Count(static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor) == 1,
                "Partial bank-local compilation must retain the existing single Replace/postbuild contract.");
            RequireBankShape(local.Plan.OrderedOperations.Single(static operation =>
                    operation.Kind == CompositionOperationKind.RunExternalProcessor).TargetRange == processorView,
                "Partial bank-local processor staging view changed from its trusted map.");
            foreach (CompiledValidationRequirement validation in local.V2Details.Provenance.ValidationRequirements)
            {
                RequireBankShape(validation is CompiledUniformInputRangeValidation or
                    CompiledFirmwareConfigBackupVersionValidation or
                    CompiledFirmwareConfigBackupPlacementAuthorityValidation or
                    CompiledFirmwareConfigBackupExpectedAddressValidation,
                    "Unsupported bank-local validation obligation.");
            }
            banks.Add(new V2RuntimeReferenceBankReplaceBinding(request.BankInstanceId,
                $"ab-replace/{request.BankInstanceId}/work",
                new ByteRange(localStart, definition.LocalBankRange.Length),
                new FirmwareArtifactPayload($"ab-replace/{request.BankInstanceId}/reference", localReference.Bytes), local));
        }

        const string output = "ab-replace/output";
        var spaces = new Dictionary<string, AddressSpace>(StringComparer.Ordinal)
        {
            [reference.ArtifactId] = new(reference.ArtifactId, reference.LengthBytes, AddressSpaceMutability.Immutable),
            [output] = new(output, reference.LengthBytes, AddressSpaceMutability.Mutable),
        };
        var initializations = new List<ImageInitialization> { ImageInitialization.Reference(output, reference.ArtifactId,
            reference.LengthBytes) };
        var operations = new List<CompositionOperation>();
        foreach (V2RuntimeReferenceBankReplaceBinding bank in banks)
        {
            spaces.Add(bank.Reference.ArtifactId, new AddressSpace(bank.Reference.ArtifactId,
                definition.Local.CapacityBytes, AddressSpaceMutability.Immutable));
            spaces.Add(bank.WorkspaceId, new AddressSpace(bank.WorkspaceId,
                definition.Local.CapacityBytes, AddressSpaceMutability.Mutable));
            initializations.Add(ImageInitialization.Blank(bank.WorkspaceId, definition.Local.CapacityBytes, 0));
            foreach (AddressSpace space in bank.LocalComposition.Plan.AddressSpaces.Where(space =>
                         space.Mutability == AddressSpaceMutability.Immutable && space.AddressSpaceId != reference.ArtifactId))
            {
                RequireBankShape(!space.AddressSpaceId.StartsWith("ab-replace/", StringComparison.Ordinal),
                    "Input binding collides with a private bank workspace.");
                _ = spaces.TryAdd(space.AddressSpaceId, space);
            }
            var localRange = new ByteRange(0, definition.Local.CapacityBytes);
            operations.Add(CompositionOperation.CopyRange($"{bank.BankInstanceId}/seed", operations.Count,
                bank.Reference.ArtifactId, localRange, bank.WorkspaceId, localRange,
                OverlapPolicy.Reject, "Clone only this bank's immutable local Reference slice."));
            if (bank.BankInstanceId == "b-bank")
            {
                foreach (ByteRange field in headerAddressFields)
                {
                    ScalarTransformAddendSource canonicalSource = diff.ScalarTransform!.AddendSource;
                    operations.Add(PartialHeaderRelocation(bank, operations.Count, field.Start, delta,
                        ReadBankAddress(reference.Bytes, checked(delta + field.Start)),
                        ScalarTransformAddendSource.RegionInstanceDelta(
                            canonicalSource.TargetRegionInstanceId!, canonicalSource.SourceRegionInstanceId!)));
                }
                operations.Add(BankRelocation(diff, bank, operations.Count,
                    ReadBankAddress(reference.Bytes, checked(delta + diff.TargetRange.Start)), restore: false));
            }
        }
        foreach (V2RuntimeReferenceBankReplaceBinding bank in banks)
        {
            foreach (CompositionOperation operation in bank.LocalComposition.Plan.OrderedOperations)
            {
                operations.Add(RebindBankOperation(operation, bank, operations.Count));
            }
            if (bank.BankInstanceId == "b-bank")
            {
                operations.Add(BankRelocation(diff, bank, operations.Count,
                    ReadBankAddress(reference.Bytes, diff.TargetRange.Start), restore: true));
            }
            operations.Add(CompositionOperation.CopyRange($"{bank.BankInstanceId}/publish", operations.Count,
                bank.WorkspaceId, new ByteRange(0, definition.Local.CapacityBytes), output, bank.OutputRange,
                OverlapPolicy.Reject, "Publish only the selected local slice; retain the rest of each AB bank."));
        }
        if (banks.Any(static bank => bank.BankInstanceId == "b-bank"))
        {
            operations.Add(PartialAbFinalization(finalizer, output, operations.Count));
        }
        var issues = new List<CompositionIssue>();
        ValidateOperationOverlaps(operations, issues);
        RequireBankShape(issues.Count == 0, string.Join("; ", issues.Select(static issue => issue.Message)));
        return new V2RuntimeReferenceBankReplacePlan(abLayout, reference, definition,
            new CompositionPlan(initializations, output, spaces.Values, operations), banks);
    }

    private static CompiledComposition CompileBankLocalReference(BankReferenceReplaceDefinition definition,
        int topologyCount, TrustedProfileBundleCatalog localCatalog, FirmwareArtifactPayload localReference,
        V2RuntimeReferenceBankReplaceRequest request)
    {
        TrustedCompositionProfileCatalogEntry localProfile = localCatalog.SelectProfile(
            definition.Local.ProfileId, definition.Local.ProfileVersion, out _) ??
            throw new ArgumentException("AB local profile is no longer trusted.");
        V2CompositionPlanCompileResult compiled = localCatalog.CompileRuntimeReferenceReplace(
            definition.Local.ProfileId, definition.Local.ProfileVersion, definition.Local.MemberId,
            localProfile.Profile.Header.ExperienceId,
            new TopologySelection(topologyCount, topologyCount == 1 ? "single" : "cascade_2to8",
                TopologySelectionSource.Requested, "number-selector"),
            [localReference], request.Replace);
        RequireBankShape(compiled.IsCompiled, string.Join("; ", compiled.Issues.Select(static issue => issue.Message)));
        return compiled.CompiledComposition!;
    }

    /// <summary>Reads the native count from both complete canonical bank views before choosing a local shape.</summary>
    internal static int ReadAbNativeCount(CompiledComposition abLayout, FirmwareArtifactPayload reference)
    {
        ArgumentNullException.ThrowIfNull(abLayout);
        ArgumentNullException.ThrowIfNull(reference);
        FirmwareImageMap map = abLayout.V2Details.Provenance.ResolvedMap.ImageMap;
        FirmwareRegionInstance[] banks = [.. map.RegionSets.SelectMany(static set => set.RegionInstances)
            .Where(static instance => instance.InstanceId is "a-bank" or "b-bank")];
        RequireBankShape(banks.Length == 2 && reference.LengthBytes == map.CapacityBytes,
            "AB native count requires two complete canonical bank views.");
        long tpStart = map.Regions.Single(static region => region.RegionId == "tpa-code").Range.Start;
        byte a = reference.Bytes[checked((int)(tpStart + 0x2B))];
        byte b = reference.Bytes[checked((int)(banks.Single(static bank => bank.InstanceId == "b-bank").BaseOffset + tpStart + 0x2B))];
        RequireBankShape(a is >= 1 and <= 8 && a == b,
            $"AB native IC count mismatch: A Read {a}, B Read {b}; expected equal counts in 1..8.");
        return a;
    }

    /// <summary>Resolve both Partial-family counts using the existing local profile's typed NVT Backup locator.</summary>
    internal static int ReadPartialAbNativeCount(CompiledComposition abLayout, FirmwareArtifactPayload reference,
        BankReferenceDefinitionSource local, FirmwareFamilyResolutionDefinition localFamily)
    {
        ArgumentNullException.ThrowIfNull(abLayout);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(localFamily);
        FirmwareImageMap map = abLayout.V2Details.Provenance.ResolvedMap.ImageMap;
        FirmwareRegion a = map.Regions.Single(static region => region.RegionId == "a-bank");
        FirmwareRegion b = map.Regions.Single(static region => region.RegionId == "b-bank");
        RequireBankShape(reference.LengthBytes == map.CapacityBytes &&
            a.Range.Length == b.Range.Length && a.Range.Start == 0 &&
            b.Range.Start == a.Range.Length && b.Range.EndExclusive == map.CapacityBytes &&
            local.CapacityBytes <= a.Range.Length, "Partial AB Reference has no exact complete bank/local geometry.");
        int countA = TrustedProfileBundleCatalog.ReadPartialBankNativeCount(localFamily, local,
            reference.Bytes.Slice(checked((int)a.Range.Start), checked((int)local.CapacityBytes)));
        int countB = TrustedProfileBundleCatalog.ReadPartialBankNativeCount(localFamily, local,
            reference.Bytes.Slice(checked((int)b.Range.Start), checked((int)local.CapacityBytes)));
        RequireBankShape(countA == countB,
            $"AB native IC count mismatch: A Read {countA}, B Read {countB}.");
        return countA;
    }

    private static (FirmwareImageMap Map, FirmwareRegion A, FirmwareRegion B, long Delta,
        CompositionOperation Diff, CompositionOperation Finalizer, ByteRange[] HeaderAddressFields,
        ByteRange ProcessorView) ValidatePartialAbReferenceLayout(
        CompiledComposition abLayout, FirmwareArtifactPayload reference,
        BankReferenceReplaceDefinition definition, FirmwareImageMap localMap)
    {
        V2CompiledCompositionDetails details = abLayout.V2Details;
        RequireExactAbLayout(details, definition.Layout);
        FirmwareImageMap map = details.Provenance.ResolvedMap.ImageMap;
        FirmwareRegion a = map.Regions.Single(static region => region.RegionId == "a-bank");
        FirmwareRegion b = map.Regions.Single(static region => region.RegionId == "b-bank");
        long delta = b.Range.Start;
        RequireBankShape(reference.LengthBytes == map.CapacityBytes && a.Range.Start == 0 &&
            a.Range.Length == definition.BankCapacityBytes && b.Range.Start == a.Range.Length &&
            b.Range.Length == a.Range.Length && b.Range.EndExclusive == reference.LengthBytes &&
            definition.LocalBankRange.Start == 0 &&
            definition.LocalBankRange.Length == localMap.CapacityBytes &&
            localMap.MapId == definition.Local.MapId,
            "Partial AB bank geometry or local Reference slice changed from its trusted definition.");
        CompositionOperation diff = abLayout.Plan.OrderedOperations.Single(static operation =>
            operation.Kind == CompositionOperationKind.TransformScalar);
        ScalarTransform transform = diff.ScalarTransform!;
        RequireBankShape(diff.TargetRange == new ByteRange(0xA120, sizeof(uint)) &&
            diff.SourceRange == diff.TargetRange && diff.SourceSpaceId == diff.TargetSpaceId &&
            transform.Width == ScalarTransformWidth.FourBytes &&
            transform.ByteOrder == ScalarTransformByteOrder.LittleEndian && transform.Addend == delta &&
            transform.AddendSource.SourceRegionInstanceId == "a-tp-placement" &&
            transform.AddendSource.TargetRegionInstanceId == "b-tp-placement",
            "Partial AB DIFF relocation differs from the trusted AB profile.");
        CompositionOperation finalizer = abLayout.Plan.OrderedOperations.Single(static operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor);
        ExternalProcessorInvocation invocation = finalizer.ExternalProcessorInvocation!;
        RequireBankShape(invocation.ToolBindingId == "legacy-combiner-1.13.0" &&
            invocation.ProcessorId == (delta == 0x40000 ? "nfc-nt51950-ab-merge-combiner-v1" :
                "nfc-nt51951-ab-merge-combiner-v1") &&
            finalizer.TargetRange == new ByteRange(0, reference.LengthBytes) &&
            invocation.AllowedWriteRanges.SequenceEqual(
            [
                new ByteRange(checked(delta + 0xA100), sizeof(uint)),
                new ByteRange(checked(delta + 0xA110), sizeof(uint)),
                new ByteRange(checked(delta + 0xA130), sizeof(uint)),
            ]) &&
            invocation.StagedArtifactBindings.Select(static binding =>
                (binding.ArtifactId, binding.SourceRange)).SequenceEqual(
            [
                ("a-bank", new ByteRange(0, delta)),
                ("b-bank", new ByteRange(delta, delta)),
            ]), "Partial AB finalizer lost its exact three-word B Header authority.");
        ByteRange[] headerAddressFields = [.. invocation.AllowedWriteRanges.Take(2)
            .Select(range => new ByteRange(checked(range.Start - delta), range.Length))];
        RequireBankShape(localMap.Regions.Any(static region => region.RegionId == "fw-config-source" &&
                region.Range == new ByteRange(0x22200, 0x780)) &&
            localMap.Regions.Any(static region => region.RegionId == "fw-config-backup" &&
                region.Range == new ByteRange(0x36000, 0x780)),
            "Partial AB local FWConfig source/Backup authority changed.");
        ByteRange processorView = localMap.Regions.Single(static region => region.RegionId == "flash-image").Range;
        RequireBankShape(processorView.Start == 0 && processorView.EndExclusive <= definition.LocalBankRange.Length,
            "Partial AB processor staging view exceeds its local Reference slice.");
        foreach (long field in headerAddressFields.Select(static range => range.Start).Append(diff.TargetRange.Start))
        {
            uint expected = ReadBankAddress(reference.Bytes, field);
            uint actual = ReadBankAddress(reference.Bytes, checked(delta + field));
            RequireBankShape(actual >= delta && actual - delta == expected,
                $"AB address mismatch: field=0x{field:X}, B=0x{actual:X}, recovered={(actual >= delta ? $"0x{actual - delta:X}" : "underflow")}, expected A=0x{expected:X}.");
        }
        foreach (long bankBase in new[] { 0L, delta })
        {
            RequireBankShape(ReadBankAddress(reference.Bytes, checked(bankBase + 0xA038)) == 0x22200,
                $"AB FWConfig source mismatch: bank=0x{bankBase:X}; expected 0x22200.");
            foreach (ByteRange field in headerAddressFields)
            {
                uint start = ReadBankAddress(reference.Bytes, checked(bankBase + field.Start));
                uint sizeCode = ReadBankAddress(reference.Bytes, checked(bankBase + field.Start + 8));
                long localStart = checked(start - bankBase);
                RequireBankShape(localStart >= processorView.Start && checked(localStart + sizeCode + 1L) <=
                    processorView.EndExclusive,
                    $"AB section exceeds the native processor staging view: bank=0x{bankBase:X}, field=0x{field.Start:X}.");
                if (field == headerAddressFields[1])
                {
                    ValidateDlmOverlayDescriptor(localStart, processorView.EndExclusive, bankBase);
                }
            }
        }
        return (map, a, b, delta, diff, finalizer, headerAddressFields, processorView);
    }

    private static void RequireExactAbLayout(V2CompiledCompositionDetails details,
        BankReferenceDefinitionSource source)
    {
        RequireBankShape(details.ProfileId == source.ProfileId && details.ProfileVersion == source.ProfileVersion &&
            details.Provenance.Context is ResolvedMapV2CompilationContext &&
            details.Provenance.Context.MemberId == source.MemberId &&
            details.Provenance.Bundle.ContentHash == source.Bundle.ContentHash &&
            details.Provenance.ProfileEntry.ContentHash == source.Entry.ContentHash &&
            details.Provenance.Context.FamilyContentHash == source.FamilyHash &&
            details.Provenance.ResolvedMap.ImageMap.MapId == source.MapId &&
            details.ExperienceId == ExperienceIds.AbMerge,
            "Only the selected exact trusted AB layout is admitted by this preparation.");
    }

    private static (FirmwareImageMap Map, FirmwareRegionInstance A, FirmwareRegionInstance B, long Capacity,
        long Delta, CompositionOperation[] Relocation) ValidateAbReferenceLayout(
        CompiledComposition abLayout, FirmwareArtifactPayload reference, BankReferenceReplaceDefinition definition)
    {
        V2CompiledCompositionDetails details = abLayout.V2Details;
        BankReferenceDefinitionSource source = definition.Layout;
        RequireExactAbLayout(details, source);
        FirmwareImageMap map = details.Provenance.ResolvedMap.ImageMap;
        RequireBankShape(reference.LengthBytes == map.CapacityBytes,
            "Reference length and selected banks must match the canonical AB layout.");
        FirmwareRegionInstance[] instances = [.. map.RegionSets.SelectMany(static set => set.RegionInstances)];
        FirmwareRegionInstance a = instances.Single(static instance => instance.InstanceId == "a-bank");
        FirmwareRegionInstance b = instances.Single(static instance => instance.InstanceId == "b-bank");
        long delta = checked(b.BaseOffset - a.BaseOffset);
        long capacity = a.Template.Capacity;
        RequireBankShape(a.BaseOffset == 0 && delta == capacity && b.Template.Capacity == capacity &&
            checked(b.BaseOffset + capacity) == reference.LengthBytes, "AB bank geometry is not a contiguous equal-sized pair.");
        CompositionOperation[] relocation = [.. abLayout.Plan.OrderedOperations.Where(static operation =>
            operation.Kind == CompositionOperationKind.TransformScalar)];
        RequireBankShape(relocation.Length == 3 && !abLayout.Plan.OrderedOperations.Any(static operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor), "AB relocation requires a different finalization contract.");
        foreach (CompositionOperation operation in relocation)
        {
            ScalarTransform transform = operation.ScalarTransform!;
            RequireBankShape(transform.Width == ScalarTransformWidth.FourBytes &&
                transform.ByteOrder == ScalarTransformByteOrder.LittleEndian && transform.Addend == delta &&
                transform.AddendSource.SourceRegionInstanceId == a.InstanceId &&
                transform.AddendSource.TargetRegionInstanceId == b.InstanceId &&
                operation.SourceSpaceId == operation.TargetSpaceId && operation.SourceRange == operation.TargetRange &&
                operation.TargetRange.EndExclusive <= capacity, "Unexpected canonical AB scalar relocation.");
        }

        // All address checks precede local compilation and every external operation, including A-only.
        ValidateBankAddresses(map, reference, relocation, capacity, delta);
        return (map, a, b, capacity, delta, relocation);
    }

    private static void ValidateBankAddresses(FirmwareImageMap map, FirmwareArtifactPayload reference,
        IReadOnlyList<CompositionOperation> relocation, long capacity, long delta)
    {
        FirmwareMetadataStructure header = map.MetadataSetBindings.SelectMany(static binding => binding.Value.Structures)
            .Single(static structure => structure.ArtifactBindingId == "tp-a-input" &&
                structure.Definition.TypedDefinition is FirmwareTpFlashHeaderDefinition);
        RequireBankShape(header.Locator is FirmwareAbsoluteRangeLocator, "AB Header requires its canonical absolute locator.");
        long headerStart = ((FirmwareAbsoluteRangeLocator)header.Locator).Range.Range.Start;
        var typed = (FirmwareTpFlashHeaderDefinition)header.Definition.TypedDefinition!;
        foreach (CompositionOperation operation in relocation)
        {
            FirmwareMetadataField addressField = header.Fields.Single(field => headerStart + field.Range.Start == operation.TargetRange.Start);
            FirmwareTpFlashHeaderFieldSemantics semantic = typed.FieldSemantics.Single(field => field.FieldId == addressField.FieldId);
            RequireBankShape(semantic.Role == TpFlashHeaderFieldRole.TpBinStartAddress,
                "Relocation must target a canonical TP BIN address field.");
            ulong expected = ReadBankAddress(reference.Bytes, operation.TargetRange.Start);
            ulong actual = ReadBankAddress(reference.Bytes, checked(delta + operation.TargetRange.Start));
            string recovered = actual >= (ulong)delta ? $"0x{actual - (ulong)delta:X}" : "underflow";
            RequireBankShape(expected < (ulong)capacity && actual >= (ulong)delta && actual - (ulong)delta == expected,
                $"AB address mismatch: {addressField.FieldId}, B=0x{actual:X}, bank offset=0x{delta:X}, recovered={recovered}, expected A=0x{expected:X}.");
            FirmwareTpFlashHeaderFieldSemantics sizeSemantic = typed.FieldSemantics.Single(field =>
                field.Subject == semantic.Subject && field.Role == TpFlashHeaderFieldRole.Size);
            FirmwareMetadataField size = header.Fields.Single(field => field.FieldId == sizeSemantic.FieldId);
            foreach (long bankBase in new[] { 0L, delta })
            {
                ReadOnlySpan<byte> fieldBytes = reference.Bytes.Slice(checked((int)(bankBase + headerStart + size.Range.Start)), size.WidthBytes);
                ulong sizeCode = size.WidthBytes switch
                {
                    2 => BinaryPrimitives.ReadUInt16LittleEndian(fieldBytes),
                    4 => BinaryPrimitives.ReadUInt32LittleEndian(fieldBytes),
                    _ => throw new ArgumentException("Unsupported TP section-size width."),
                };
                // DIFF has its own count-dependent envelope, checked before calling the native processor below.
                RequireBankShape(semantic.Subject == TpFlashHeaderFieldSubject.DlmDifference || expected + sizeCode + 1 <= (ulong)capacity,
                    $"AB section out of bounds: {semantic.Subject}, bank=0x{bankBase:X}, start=0x{expected:X}, size code=0x{sizeCode:X}.");
                if (semantic.Subject == TpFlashHeaderFieldSubject.Dlm)
                {
                    ValidateDlmOverlayDescriptor(checked((long)expected), capacity, bankBase);
                }
            }
        }
    }

    private static void ValidateDlmOverlayDescriptor(long localStart, long processorViewEnd, long bankBase)
    {
        RequireBankShape(checked(localStart + 16) <= processorViewEnd,
            $"AB DLM overlay descriptor exceeds the native processor staging view: bank=0x{bankBase:X}.");
    }

    private static uint ReadBankAddress(ReadOnlySpan<byte> bytes, long offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(checked((int)offset), sizeof(uint)));
    }

    private static void ValidateBankNativeInputs(FirmwareImageMap abMap, FirmwareImageMap localMap,
        FirmwareArtifactPayload reference, ByteRange diffAddressField, long capacity, long delta, int topologyCount)
    {
        long tpStart = abMap.Regions.Single(static region => region.RegionId == "tpa-code").Range.Start;
        ByteRange source = localMap.Regions.Single(static region => region.RegionId == "fw-config-source").Range;
        FirmwareRegion? fixedBackup = localMap.Regions.SingleOrDefault(static region => region.RegionId == "fw-config-backup");
        // Closed 1.13 compatibility preconditions, not a generic metadata resolver.
        // Recovered Combiner.c:1395-1423,1495,1515-1526 read the count/pointer and copy a full 4KiB page.
        // The two reader offsets below are relative to the trusted canonical TP region.
        // A new family/profile/tool contract requires a separately admitted implementation.
        const long pageLength = 0x1000;
        RequireBankShape(topologyCount is >= 1 and <= 8 && checked(source.Start + pageLength) <= capacity,
            "Canonical FWConfig source envelope or selected topology is invalid.");
        ByteRange? crcEnvelope = localMap.Regions.SingleOrDefault(static region =>
            region.RegionId == "header-dlm-crc-7128-7144")?.Range;
        RequireBankShape(topologyCount == 1 ||
            (crcEnvelope is not null && crcEnvelope.Value.Start == 0x7128 &&
             crcEnvelope.Value.Length >= checked((topologyCount - 1) * sizeof(uint))),
            "Canonical Cascade DIFF CRC envelope is invalid.");
        ByteRange? dynamicDiff = localMap.Regions.SingleOrDefault(static region => region.RegionId == "diff-ctrlram")?.Range;
        ByteRange? dynamicTail = localMap.Regions.SingleOrDefault(static region =>
            region.RegionId == "dynamic-diffdlm-layout-authority-tail")?.Range;
        foreach (long bankBase in new[] { 0L, delta })
        {
            byte count = reference.Bytes[checked((int)(bankBase + tpStart + 0x2B))];
            RequireBankShape(count == topologyCount,
                $"AB native IC count mismatch: bank=0x{bankBase:X}, read={count}, expected={topologyCount}.");
            uint pointer = ReadBankAddress(reference.Bytes, checked(bankBase + tpStart + 0x38));
            RequireBankShape(pointer == source.Start && checked(pointer + pageLength) <= capacity,
                $"AB FWConfig source mismatch: bank=0x{bankBase:X}, read=0x{pointer:X}, expected=0x{source.Start:X}, length=0x{pageLength:X}.");
            long diffStart = checked(ReadBankAddress(reference.Bytes, bankBase + diffAddressField.Start) - bankBase);
            ushort diffSizeCode = BinaryPrimitives.ReadUInt16LittleEndian(reference.Bytes.Slice(
                checked((int)(bankBase + 0x7120)), sizeof(ushort)));
            long diffStride = checked((long)diffSizeCode + 1);
            long diffEnd = diffStart + (diffStride * (count - 1));
            RequireBankShape(diffStart >= 0 && diffEnd <= capacity &&
                (count == 1 || (diffStride == 0x1400 && dynamicDiff is not null &&
                    diffStart == dynamicDiff.Value.Start && diffEnd <= dynamicDiff.Value.EndExclusive)),
                $"AB DIFF envelope mismatch: bank=0x{bankBase:X}, start=0x{diffStart:X}, stride=0x{diffStride:X}, count={count}.");
            long destination = checked(((diffEnd / pageLength) + 1) * pageLength);
            long destinationEnd = checked(destination + pageLength);
            bool allowed = count == 1
                ? fixedBackup is not null && fixedBackup.Range.Start == destination && fixedBackup.Range.Length == pageLength
                : dynamicDiff is not null && dynamicTail is not null &&
                  destination >= dynamicDiff.Value.Start && destinationEnd <= dynamicTail.Value.EndExclusive &&
                  dynamicDiff.Value.EndExclusive == dynamicTail.Value.Start;
            RequireBankShape(allowed && destinationEnd <= capacity,
                $"AB Backup destination mismatch: bank=0x{bankBase:X}, derived=0x{destination:X}, length=0x{pageLength:X}.");
        }
    }

    private static void RequireBankShape(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}
