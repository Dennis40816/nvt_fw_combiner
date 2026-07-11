using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private const string LegacyFingerprintFormat = "nfc.compiled-composition.legacy.v1";

    private static string CalculateCompilationFingerprint(CompiledComposition composition)
    {
        var builder = new StringBuilder();
        AppendField(builder, "format", LegacyFingerprintFormat);
        AppendField(builder, "authority.kind", "legacy-profile");
        AppendField(
            builder,
            "authority.model-version",
            ((LegacyProfileCompilationAuthority)composition.Authority).ModelVersion);
        AppendField(builder, "profile.id", composition.ProfileId);
        AppendField(builder, "profile.version", composition.ProfileVersion);
        AppendField(builder, "profile.ic", composition.IcId);
        AppendField(builder, "profile.mode", composition.ModeId);
        AppendField(builder, "profile.experience", composition.ExperienceId);
        AppendEnum(builder, "profile.composition-kind", composition.CompositionKind);
        AppendField(builder, "output.default-file-name", composition.DefaultOutputFileName);
        AppendEnum(builder, "run-policy.ic-number", composition.IcNumberPolicy);
        AppendEnum(builder, "eligibility", composition.Eligibility);
        AppendPlan(builder, composition.Plan);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendPlan(StringBuilder builder, CompositionPlan plan)
    {
        AppendField(builder, "plan.output-space", plan.OutputSpaceId);

        ImageInitialization[] initializations = [.. plan.Initializations.OrderBy(item => item.TargetSpaceId, StringComparer.Ordinal)];
        AppendInteger(builder, "plan.initializer.count", initializations.Length);
        for (int index = 0; index < initializations.Length; index++)
        {
            ImageInitialization initialization = initializations[index];
            string prefix = FormattableString.Invariant($"plan.initializer.{index}");
            AppendEnum(builder, $"{prefix}.kind", initialization.Kind);
            AppendField(builder, $"{prefix}.target-space", initialization.TargetSpaceId);
            AppendField(builder, $"{prefix}.reference-space", initialization.ReferenceSpaceId ?? string.Empty);
            AppendInteger(builder, $"{prefix}.capacity", initialization.Capacity);
            AppendInteger(builder, $"{prefix}.fill-byte", initialization.FillByte);
        }

        AddressSpace[] addressSpaces = [.. plan.AddressSpaces.OrderBy(item => item.AddressSpaceId, StringComparer.Ordinal)];
        AppendInteger(builder, "plan.address-space.count", addressSpaces.Length);
        for (int index = 0; index < addressSpaces.Length; index++)
        {
            AddressSpace addressSpace = addressSpaces[index];
            string prefix = FormattableString.Invariant($"plan.address-space.{index}");
            AppendField(builder, $"{prefix}.id", addressSpace.AddressSpaceId);
            AppendInteger(builder, $"{prefix}.length", addressSpace.Length);
            AppendEnum(builder, $"{prefix}.mutability", addressSpace.Mutability);
            AppendField(
                builder,
                $"{prefix}.input-padding-byte",
                addressSpace.InputPaddingByte?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendEnum(builder, $"{prefix}.input-oversize-policy", addressSpace.InputOversizePolicy);
            AppendIntegerList(builder, $"{prefix}.allowed-input-length", addressSpace.AllowedInputLengths);
            AppendIntegerList(builder, $"{prefix}.expected-input-length", addressSpace.ExpectedInputLengths);
        }

        AppendInteger(builder, "plan.operation.count", plan.OrderedOperations.Count);
        for (int index = 0; index < plan.OrderedOperations.Count; index++)
        {
            AppendOperation(builder, plan.OrderedOperations[index], index);
        }
    }

    private static void AppendOperation(
        StringBuilder builder,
        CompositionOperation operation,
        int index)
    {
        string prefix = FormattableString.Invariant($"plan.operation.{index}");
        AppendField(builder, $"{prefix}.id", operation.OperationId);
        AppendInteger(builder, $"{prefix}.sequence", operation.Sequence);
        AppendEnum(builder, $"{prefix}.kind", operation.Kind);
        AppendField(builder, $"{prefix}.source-space", operation.SourceSpaceId ?? string.Empty);
        AppendRange(builder, $"{prefix}.source-range", operation.SourceRange);
        AppendField(builder, $"{prefix}.target-space", operation.TargetSpaceId);
        AppendRange(builder, $"{prefix}.target-range", operation.TargetRange);
        AppendEnum(builder, $"{prefix}.overlap-policy", operation.OverlapPolicy);
        AppendField(
            builder,
            $"{prefix}.fill-byte",
            operation.FillByte?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        AppendField(builder, $"{prefix}.patch-bytes", Convert.ToHexString(operation.PatchBytes.Span).ToLowerInvariant());
        AppendField(builder, $"{prefix}.reason", operation.Reason);
        AppendField(builder, $"{prefix}.provenance.kind", operation.Provenance.Kind);
        AppendField(builder, $"{prefix}.provenance.source-id", operation.Provenance.SourceId ?? string.Empty);
        AppendField(builder, $"{prefix}.provenance.source-version", operation.Provenance.SourceVersion ?? string.Empty);
        AppendProcessor(builder, prefix, operation.ExternalProcessorInvocation);
    }

    private static void AppendProcessor(
        StringBuilder builder,
        string operationPrefix,
        ExternalProcessorInvocation? invocation)
    {
        string prefix = $"{operationPrefix}.processor";
        AppendInteger(builder, $"{prefix}.present", invocation is null ? 0 : 1);
        if (invocation is null)
        {
            return;
        }

        AppendField(builder, $"{prefix}.id", invocation.ProcessorId);
        AppendField(builder, $"{prefix}.tool-binding", invocation.ToolBindingId);
        AppendRangeList(builder, $"{prefix}.read-range", invocation.AllowedReadRanges);
        AppendRangeList(builder, $"{prefix}.write-range", invocation.AllowedWriteRanges);

        AppendInteger(builder, $"{prefix}.write-section.count", invocation.AllowedWriteRangeSections.Count);
        for (int index = 0; index < invocation.AllowedWriteRangeSections.Count; index++)
        {
            ExternalProcessorWriteRangeSection section = invocation.AllowedWriteRangeSections[index];
            string sectionPrefix = FormattableString.Invariant($"{prefix}.write-section.{index}");
            AppendField(builder, $"{sectionPrefix}.id", section.SectionId);
            AppendRange(builder, $"{sectionPrefix}.range", section.Range);
            AppendRange(builder, $"{sectionPrefix}.source-range", section.SourceRange);
        }

        AppendInteger(builder, $"{prefix}.staged-source.count", invocation.StagedSourceBindings.Count);
        for (int index = 0; index < invocation.StagedSourceBindings.Count; index++)
        {
            ExternalProcessorStagedSourceBinding binding = invocation.StagedSourceBindings[index];
            string bindingPrefix = FormattableString.Invariant($"{prefix}.staged-source.{index}");
            AppendField(builder, $"{bindingPrefix}.source-space", binding.SourceSpaceId);
            AppendRange(builder, $"{bindingPrefix}.source-range", binding.SourceRange);
            AppendRange(builder, $"{bindingPrefix}.firmware-range", binding.FirmwareRange);
        }
    }

    private static void AppendRangeList(
        StringBuilder builder,
        string fieldPrefix,
        IReadOnlyList<ByteRange> ranges)
    {
        AppendInteger(builder, $"{fieldPrefix}.count", ranges.Count);
        for (int index = 0; index < ranges.Count; index++)
        {
            AppendRange(builder, FormattableString.Invariant($"{fieldPrefix}.{index}"), ranges[index]);
        }
    }

    private static void AppendIntegerList(
        StringBuilder builder,
        string fieldPrefix,
        IReadOnlyList<long> values)
    {
        AppendInteger(builder, $"{fieldPrefix}.count", values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            AppendInteger(builder, FormattableString.Invariant($"{fieldPrefix}.{index}"), values[index]);
        }
    }

    private static void AppendRange(StringBuilder builder, string fieldName, ByteRange? range)
    {
        AppendField(
            builder,
            fieldName,
            range is { } value
                ? FormattableString.Invariant($"{value.Start}:{value.Length}")
                : string.Empty);
    }

    private static void AppendEnum<TEnum>(StringBuilder builder, string fieldName, TEnum value)
        where TEnum : struct, Enum
    {
        AppendInteger(builder, fieldName, Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    private static void AppendInteger(StringBuilder builder, string fieldName, long value)
    {
        AppendField(builder, fieldName, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendField(StringBuilder builder, string fieldName, string value)
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
}
