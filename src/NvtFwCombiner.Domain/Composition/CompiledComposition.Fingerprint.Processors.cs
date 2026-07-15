using System.Text;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
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

        if (invocation.OutputAssertions.Count > 0)
        {
            AppendInteger(builder, $"{prefix}.output-assertion.count", invocation.OutputAssertions.Count);
            for (int index = 0; index < invocation.OutputAssertions.Count; index++)
            {
                ExternalProcessorOutputAssertion assertion = invocation.OutputAssertions[index];
                string assertionPrefix = FormattableString.Invariant($"{prefix}.output-assertion.{index}");
                AppendRange(builder, $"{assertionPrefix}.range", assertion.Range);
                AppendField(builder, $"{assertionPrefix}.expected", Convert.ToHexString(assertion.ExpectedBytes.Span).ToLowerInvariant());
            }
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
}
