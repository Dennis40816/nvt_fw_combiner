using System.Text;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void AppendRuntimeReferenceCompilationContext(
        StringBuilder builder,
        RuntimeReferenceReplaceV2CompilationContext context,
        bool capabilityBound)
    {
        AppendInteger(builder, "compilation-context", 2);
        if (context.AllowsConditionalProcessor)
        {
            AppendInteger(
                builder,
                "compilation-context.conditional-processor",
                1);
        }

        if (!capabilityBound)
        {
            return;
        }

        AppendStringList(
            builder,
            "compilation-context.processor-write-view",
            context.ProcessorWriteViewIds);
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

        AppendList(builder, $"{prefix}.write-section", invocation.AllowedWriteRangeSections, AppendWriteRangeSection);

        if (invocation.OutputAssertions.Count > 0)
        {
            AppendList(builder, $"{prefix}.output-assertion", invocation.OutputAssertions, AppendOutputAssertion);
        }

        AppendList(builder, $"{prefix}.staged-source", invocation.StagedSourceBindings, AppendStagedSource);
        AppendList(builder, $"{prefix}.staged-artifact", invocation.StagedArtifactBindings, AppendStagedArtifact);
    }

    private static void AppendWriteRangeSection(
        StringBuilder builder,
        string prefix,
        ExternalProcessorWriteRangeSection section)
    {
        AppendField(builder, $"{prefix}.id", section.SectionId);
        AppendRange(builder, $"{prefix}.range", section.Range);
        AppendRange(builder, $"{prefix}.source-range", section.SourceRange);
    }

    private static void AppendOutputAssertion(
        StringBuilder builder,
        string prefix,
        ExternalProcessorOutputAssertion assertion)
    {
        AppendRange(builder, $"{prefix}.range", assertion.Range);
        AppendField(builder, $"{prefix}.expected", Convert.ToHexString(assertion.ExpectedBytes.Span).ToLowerInvariant());
    }

    private static void AppendStagedSource(
        StringBuilder builder,
        string prefix,
        ExternalProcessorStagedSourceBinding binding)
    {
        AppendField(builder, $"{prefix}.source-space", binding.SourceSpaceId);
        AppendRange(builder, $"{prefix}.source-range", binding.SourceRange);
        AppendRange(builder, $"{prefix}.firmware-range", binding.FirmwareRange);
    }

    private static void AppendStagedArtifact(
        StringBuilder builder,
        string prefix,
        ExternalProcessorStagedArtifactBinding binding)
    {
        AppendField(builder, $"{prefix}.id", binding.ArtifactId);
        AppendField(builder, $"{prefix}.source-space", binding.SourceSpaceId);
        AppendRange(builder, $"{prefix}.source-range", binding.SourceRange);
    }
}
