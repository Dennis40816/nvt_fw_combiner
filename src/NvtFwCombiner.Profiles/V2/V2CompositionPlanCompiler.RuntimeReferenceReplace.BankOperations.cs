using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static CompositionOperation RebindBankOperation(
        CompositionOperation operation,
        V2RuntimeReferenceBankReplaceBinding bank,
        int sequence)
    {
        string sourceReference = bank.LocalComposition.Plan.OutputInitialization.ReferenceSpaceId!;
        string Bind(string space)
        {
            return StringComparer.Ordinal.Equals(space, sourceReference) ? bank.Reference.ArtifactId
                : StringComparer.Ordinal.Equals(space, bank.LocalComposition.Plan.OutputSpaceId) ? bank.WorkspaceId : space;
        }
        string id = $"{bank.BankInstanceId}/{operation.OperationId}";
        const OverlapPolicy overlap = OverlapPolicy.ReplaceExisting;
        return operation.Kind switch
        {
            CompositionOperationKind.ReplaceRange => CompositionOperation.ReplaceRange(
                id, sequence, Bind(operation.SourceSpaceId!), operation.SourceRange!.Value,
                bank.WorkspaceId, operation.TargetRange, overlap, operation.Reason, operation.Provenance),
            CompositionOperationKind.PatchScalar => CompositionOperation.PatchScalar(
                id, sequence, bank.WorkspaceId, operation.TargetRange, operation.PatchBytes.ToArray(),
                overlap, operation.Reason, operation.Provenance),
            CompositionOperationKind.RunExternalProcessor => CompositionOperation.RunExternalProcessor(
                id, sequence, bank.WorkspaceId, operation.TargetRange,
                RebindProcessor(operation.ExternalProcessorInvocation!), overlap, operation.Reason, operation.Provenance),
            CompositionOperationKind.CopyRange or CompositionOperationKind.FillRange or
                CompositionOperationKind.TransformScalar => throw new ArgumentException("Unsupported bank-local operation."),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        ExternalProcessorInvocation RebindProcessor(ExternalProcessorInvocation processor)
        {
            return new ExternalProcessorInvocation(
                processor.ProcessorId, processor.ToolBindingId, processor.AllowedReadRanges, processor.AllowedWriteRanges,
                processor.StagedSourceBindings.Select(binding => new ExternalProcessorStagedSourceBinding(
                    Bind(binding.SourceSpaceId), binding.SourceRange, binding.FirmwareRange)),
                processor.AllowedWriteRangeSections,
                processor.StagedArtifactBindings.Select(binding => new ExternalProcessorStagedArtifactBinding(
                    binding.ArtifactId, Bind(binding.SourceSpaceId), binding.SourceRange)),
                processor.OutputAssertions, processor.ProtocolPlan);
        }
    }

    private static CompositionOperation BankRelocation(
        CompositionOperation canonical,
        V2RuntimeReferenceBankReplaceBinding bank,
        int sequence,
        ulong expected,
        bool restore)
    {
        ScalarTransform transform = canonical.ScalarTransform!;
        ScalarTransformAddendSource authority = transform.AddendSource;
        return CompositionOperation.TransformScalar(
            $"{bank.BankInstanceId}/{(restore ? "restore" : "normalize")}/{canonical.OperationId}", sequence,
            bank.WorkspaceId, canonical.SourceRange!.Value, bank.WorkspaceId, canonical.TargetRange,
            new ScalarTransform(transform.Width, transform.ByteOrder, restore ? transform.Addend : -transform.Addend,
                expected, transform.OverflowPolicy, restore ? authority : ScalarTransformAddendSource.RegionInstanceDelta(
                    authority.TargetRegionInstanceId!, authority.SourceRegionInstanceId!)),
            OverlapPolicy.ReplaceExisting, restore ? "Restore canonical B addresses after local postbuild."
                : "Normalize B addresses to the verified A Header addresses before postbuild.", canonical.Provenance);
    }

    private static CompositionOperation PartialHeaderRelocation(
        V2RuntimeReferenceBankReplaceBinding bank, int sequence, long field, long delta, ulong expected,
        ScalarTransformAddendSource addendSource)
    {
        var range = new ByteRange(field, sizeof(uint));
        return CompositionOperation.TransformScalar(
            $"{bank.BankInstanceId}/normalize/header-0x{field:X}", sequence,
            bank.WorkspaceId, range, bank.WorkspaceId, range,
            new ScalarTransform(ScalarTransformWidth.FourBytes, ScalarTransformByteOrder.LittleEndian,
                -delta, expected, ScalarTransformOverflowPolicy.Reject, addendSource),
            OverlapPolicy.ReplaceExisting,
            "Normalize the verified B Header address before the existing local CtrlRAM postbuild.");
    }

    private static CompositionOperation PartialAbFinalization(CompositionOperation canonical,
        string output, int sequence)
    {
        ExternalProcessorInvocation source = canonical.ExternalProcessorInvocation!;
        var invocation = new ExternalProcessorInvocation(source.ProcessorId, source.ToolBindingId,
            source.AllowedReadRanges, source.AllowedWriteRanges,
            source.StagedSourceBindings.Select(binding => new ExternalProcessorStagedSourceBinding(
                output, binding.SourceRange, binding.FirmwareRange)), source.AllowedWriteRangeSections,
            source.StagedArtifactBindings.Select(binding => new ExternalProcessorStagedArtifactBinding(
                binding.ArtifactId, output, binding.SourceRange)), source.OutputAssertions, source.ProtocolPlan);
        return CompositionOperation.RunExternalProcessor("ab-replace/b-finalize", sequence, output,
            canonical.TargetRange, invocation, OverlapPolicy.ReplaceExisting,
            "Use the trusted AB stage once to relocate B ILM/DLM and recalculate its Header CRC.",
            canonical.Provenance);
    }
}
