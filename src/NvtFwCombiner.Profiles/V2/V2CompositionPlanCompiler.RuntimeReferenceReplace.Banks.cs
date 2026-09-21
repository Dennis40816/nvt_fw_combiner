using System.Buffers.Binary;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    /// <summary>Prepares the closed 929 single-IC route without granting product execution eligibility.</summary>
    internal static V2RuntimeReferenceBankReplacePlan PrepareAbRuntimeReferenceReplace(
        CompiledComposition abLayout,
        FirmwareArtifactPayload reference,
        TrustedProfileBundleCatalog localCatalog,
        IReadOnlyList<V2RuntimeReferenceBankReplaceRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(abLayout);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(localCatalog);
        ArgumentNullException.ThrowIfNull(requests);
        V2CompiledCompositionDetails details = abLayout.V2Details;
        RequireBankShape(details.ProfileId == "nt51929-ab-merge" && details.ProfileVersion == "0.4.0" &&
            details.Provenance.Context is ResolvedMapV2CompilationContext &&
            details.Provenance.Context.MemberId == "NT51929" && details.ExperienceId == ExperienceIds.AbMerge,
            "Only the trusted NT51929 AB layout is admitted by this preparation.");
        FirmwareImageMap map = details.Provenance.ResolvedMap.ImageMap;
        RequireBankShape(reference.LengthBytes == map.CapacityBytes && requests.Count is >= 1 and <= 2,
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
        var banks = new List<V2RuntimeReferenceBankReplaceBinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        V2ExplicitMappingInputBinding[]? sharedSources = null;
        foreach (V2RuntimeReferenceBankReplaceRequest request in requests.OrderBy(static request => request.BankInstanceId, StringComparer.Ordinal))
        {
            RequireBankShape(request.BankInstanceId is "a-bank" or "b-bank" && seen.Add(request.BankInstanceId),
                "Selected banks must be unique canonical A/B instances.");
            V2ExplicitMappingInputBinding[] references = [.. request.Replace.Bindings.Where(static binding => binding.SlotId == "reference-base")];
            RequireBankShape(references.Length == 1 && references[0].BindingId == reference.ArtifactId &&
                references[0].ExactLengthBytes == capacity, "Bank-local Reference binding must identify the captured AB Reference and local capacity.");
            V2ExplicitMappingInputBinding[] sources = [.. request.Replace.Bindings.Where(static binding => binding.SlotId != "reference-base")
                .OrderBy(static binding => binding.BindingId, StringComparer.Ordinal)];
            RequireBankShape(sharedSources is null || sources.Select(static item => (item.BindingId, item.SlotId, item.ExactLengthBytes))
                .SequenceEqual(sharedSources.Select(static item => (item.BindingId, item.SlotId, item.ExactLengthBytes))),
                "Selected banks must share the same source bindings.");
            sharedSources = sources;
            long start = request.BankInstanceId == a.InstanceId ? a.BaseOffset : b.BaseOffset;
            var localReference = new FirmwareArtifactPayload(reference.ArtifactId,
                reference.Bytes.Slice(checked((int)start), checked((int)capacity)));
            V2CompositionPlanCompileResult compiled = localCatalog.CompileRuntimeReferenceReplace(
                "nt51929-ctrlram-replace-fw200-single", "0.3.0", "NT51929", ExperienceIds.CtrlRamReplace,
                new TopologySelection(1, "single", TopologySelectionSource.Requested, "number-selector"),
                [localReference], request.Replace);
            RequireBankShape(compiled.IsCompiled, string.Join("; ", compiled.Issues.Select(static issue => issue.Message)));
            CompiledComposition local = compiled.CompiledComposition!;
            if (banks.Count == 0)
            {
                ValidateSingleBankNativeInputs(map, local.V2Details.Provenance.ResolvedMap.ImageMap,
                    reference, relocation.Single(static operation => operation.OperationId == "relocate-tpb-diff").TargetRange,
                    capacity, delta);
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
        return new V2RuntimeReferenceBankReplacePlan(abLayout, reference,
            new CompositionPlan(initializations, output, spaces.Values, operations), banks);
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
                // The admitted single-IC legacy route has no DIFF CRC records; its start still controls Backup placement.
                RequireBankShape(semantic.Subject == TpFlashHeaderFieldSubject.DlmDifference || expected + sizeCode + 1 <= (ulong)capacity,
                    $"AB section out of bounds: {semantic.Subject}, bank=0x{bankBase:X}, start=0x{expected:X}, size code=0x{sizeCode:X}.");
            }
        }
    }

    private static uint ReadBankAddress(ReadOnlySpan<byte> bytes, long offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(checked((int)offset), sizeof(uint)));
    }

    private static void ValidateSingleBankNativeInputs(FirmwareImageMap abMap, FirmwareImageMap localMap,
        FirmwareArtifactPayload reference, ByteRange diffAddressField, long capacity, long delta)
    {
        long tpStart = abMap.Regions.Single(static region => region.RegionId == "tpa-code").Range.Start;
        ByteRange source = localMap.Regions.Single(static region => region.RegionId == "fw-config-source").Range;
        ByteRange backup = localMap.Regions.Single(static region => region.RegionId == "fw-config-backup").Range;
        // Closed 1.13 compatibility preconditions, not a generic metadata resolver.
        // Recovered Combiner.c:1395-1423,1495,1515-1526 read the count/pointer and copy a full 4KiB page.
        // The two reader offsets below are relative to the trusted canonical TP region.
        // A new family/profile/tool contract requires a separately admitted implementation.
        const long pageLength = 0x1000;
        RequireBankShape(backup.Length == pageLength && checked(source.Start + pageLength) <= capacity &&
            backup.EndExclusive <= capacity, "Canonical Single FWConfig copy envelope is invalid.");
        foreach (long bankBase in new[] { 0L, delta })
        {
            byte count = reference.Bytes[checked((int)(bankBase + tpStart + 0x2B))];
            RequireBankShape(count == 1, $"AB Single native IC count mismatch: bank=0x{bankBase:X}, read={count}, expected=1.");
            uint pointer = ReadBankAddress(reference.Bytes, checked(bankBase + tpStart + 0x38));
            RequireBankShape(pointer == source.Start && checked(pointer + pageLength) <= capacity,
                $"AB FWConfig source mismatch: bank=0x{bankBase:X}, read=0x{pointer:X}, expected=0x{source.Start:X}, length=0x{pageLength:X}.");
            long diffStart = checked(ReadBankAddress(reference.Bytes, bankBase + diffAddressField.Start) - bankBase);
            long destination = checked(((diffStart / pageLength) + 1) * pageLength);
            RequireBankShape(destination == backup.Start && checked(destination + pageLength) <= capacity,
                $"AB Backup destination mismatch: bank=0x{bankBase:X}, derived=0x{destination:X}, expected=0x{backup.Start:X}, length=0x{pageLength:X}.");
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
