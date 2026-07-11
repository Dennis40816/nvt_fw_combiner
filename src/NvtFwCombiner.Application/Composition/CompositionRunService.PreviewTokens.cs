using System.Globalization;
using System.Text;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static string CalculatePreviewToken(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyList<InputArtifactSummary> inputSummaries)
    {
        var builder = new StringBuilder();
        AppendTokenField(builder, "profile.id", request.Profile.ProfileId);
        AppendTokenField(builder, "profile.version", request.Profile.ProfileVersion);
        AppendTokenField(builder, "profile.ic", request.Profile.IcId);
        AppendTokenField(builder, "profile.mode", request.Profile.ModeId);
        AppendTokenField(builder, "profile.experience", request.Profile.ExperienceId);
        AppendTokenField(builder, "profile.kind", request.Profile.CompositionKind.ToString());
        AppendTokenField(builder, "profile.ic-number-mode", request.Profile.IcNumberInputMode?.ToString() ?? string.Empty);
        AppendTokenField(builder, "run.ic-number", request.IcNumberSelection?.ToStableToken() ?? string.Empty);
        AppendTokenField(builder, "output.name", request.OutputFileName);
        AppendPlanFingerprint(builder, request.Plan);
        foreach (InputArtifactSummary input in inputSummaries.OrderBy(item => item.AddressSpaceId, StringComparer.Ordinal))
        {
            AppendTokenField(builder, "input.address-space", input.AddressSpaceId);
            AppendTokenField(builder, "input.artifact", input.ArtifactId);
            AppendTokenField(builder, "input.size", input.Size.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "input.sha256", input.Sha256);
        }

        AppendTokenField(builder, "execution.status", execution.Status.ToString());
        AppendTokenField(builder, "execution.output.sha256", ToSha256Hex(execution.OutputBytes.Span));
        return ToSha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static void AppendPlanFingerprint(StringBuilder builder, CompositionPlan plan)
    {
        AppendTokenField(builder, "plan.output-space", plan.OutputSpaceId);
        foreach (ImageInitialization initialization in plan.Initializations)
        {
            AppendTokenField(builder, "plan.init.kind", initialization.Kind.ToString());
            AppendTokenField(builder, "plan.init.target", initialization.TargetSpaceId);
            AppendTokenField(builder, "plan.init.reference", initialization.ReferenceSpaceId ?? string.Empty);
            AppendTokenField(builder, "plan.init.capacity", initialization.Capacity.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "plan.init.fill", initialization.FillByte.ToString(CultureInfo.InvariantCulture));
        }

        foreach (AddressSpace addressSpace in plan.AddressSpaces)
        {
            AppendTokenField(builder, "plan.space.id", addressSpace.AddressSpaceId);
            AppendTokenField(builder, "plan.space.length", addressSpace.Length.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "plan.space.mutability", addressSpace.Mutability.ToString());
            AppendTokenField(
                builder,
                "plan.space.input-padding",
                addressSpace.InputPaddingByte?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendTokenField(builder, "plan.space.input-oversize-policy", addressSpace.InputOversizePolicy.ToString());
            AppendTokenField(
                builder,
                "plan.space.allowed-input-lengths",
                string.Join(",", addressSpace.AllowedInputLengths.Select(length => length.ToString(CultureInfo.InvariantCulture))));
            AppendTokenField(
                builder,
                "plan.space.expected-input-lengths",
                string.Join(",", addressSpace.ExpectedInputLengths.Select(length => length.ToString(CultureInfo.InvariantCulture))));
        }

        foreach (CompositionOperation operation in plan.OrderedOperations)
        {
            AppendTokenField(builder, "plan.operation.id", operation.OperationId);
            AppendTokenField(builder, "plan.operation.sequence", operation.Sequence.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "plan.operation.kind", operation.Kind.ToString());
            AppendTokenField(builder, "plan.operation.source-space", operation.SourceSpaceId ?? string.Empty);
            AppendTokenField(builder, "plan.operation.source-range", FormatRange(operation.SourceRange));
            AppendTokenField(builder, "plan.operation.target-space", operation.TargetSpaceId);
            AppendTokenField(builder, "plan.operation.target-range", FormatRange(operation.TargetRange));
            AppendTokenField(builder, "plan.operation.overlap", operation.OverlapPolicy.ToString());
            AppendTokenField(builder, "plan.operation.fill-byte", operation.FillByte?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendTokenField(builder, "plan.operation.patch-bytes", ToHex(operation.PatchBytes.Span));
            AppendProcessorFingerprint(builder, operation);
            AppendTokenField(builder, "plan.operation.reason", operation.Reason);
            AppendTokenField(builder, "plan.operation.provenance.kind", operation.Provenance.Kind);
            AppendTokenField(builder, "plan.operation.provenance.source-id", operation.Provenance.SourceId ?? string.Empty);
            AppendTokenField(builder, "plan.operation.provenance.source-version", operation.Provenance.SourceVersion ?? string.Empty);
        }
    }

    private static void AppendProcessorFingerprint(StringBuilder builder, CompositionOperation operation)
    {
        if (operation.ExternalProcessorInvocation is not { } invocation)
        {
            AppendTokenField(builder, "plan.operation.processor.id", string.Empty);
            AppendTokenField(builder, "plan.operation.processor.tool-binding", string.Empty);
            return;
        }

        AppendTokenField(builder, "plan.operation.processor.id", invocation.ProcessorId);
        AppendTokenField(builder, "plan.operation.processor.tool-binding", invocation.ToolBindingId);
        foreach (ByteRange range in invocation.AllowedReadRanges)
        {
            AppendTokenField(builder, "plan.operation.processor.read-range", FormatRange(range));
        }

        foreach (ByteRange range in invocation.AllowedWriteRanges)
        {
            AppendTokenField(builder, "plan.operation.processor.write-range", FormatRange(range));
        }

        foreach (ExternalProcessorWriteRangeSection section in invocation.AllowedWriteRangeSections)
        {
            AppendTokenField(builder, "plan.operation.processor.write-section.id", section.SectionId);
            AppendTokenField(builder, "plan.operation.processor.write-section.range", FormatRange(section.Range));
            AppendTokenField(builder, "plan.operation.processor.write-section.source-range", FormatRange(section.SourceRange));
        }

        foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
        {
            AppendTokenField(builder, "plan.operation.processor.staged-source.source-space", binding.SourceSpaceId);
            AppendTokenField(builder, "plan.operation.processor.staged-source.source-range", FormatRange(binding.SourceRange));
            AppendTokenField(builder, "plan.operation.processor.staged-source.firmware-range", FormatRange(binding.FirmwareRange));
        }
    }

    private static void AppendTokenField(StringBuilder builder, string fieldName, string value)
    {
        _ = builder
            .Append(fieldName.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(fieldName)
            .Append('=')
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('\n');
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FormatRange(ByteRange? range)
    {
        return range is { } value
            ? FormattableString.Invariant($"{value.Start}:{value.Length}")
            : string.Empty;
    }

}
