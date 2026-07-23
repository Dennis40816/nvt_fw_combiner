using System.Globalization;
using System.Text;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static string CalculatePreviewToken(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string outputFileName,
        OutputNamingSummary? outputNaming)
    {
        var builder = new StringBuilder();
        AppendTokenField(builder, "compiled.fingerprint", request.CompiledComposition.CompilationFingerprint);
        AppendTokenField(builder, "run.ic-number", request.IcNumberSelection?.ToStableToken() ?? string.Empty);
        AppendTokenField(builder, "output.name", outputFileName);
        if (outputNaming is not null)
        {
            AppendTokenField(builder, "output.renderer", outputNaming.RendererKind);
            AppendTokenField(builder, "output.template", outputNaming.Template);
            AppendTokenField(builder, "output.automatic-name", outputNaming.AutomaticFileName);
            AppendTokenField(builder, "output.explicit-override", outputNaming.IsExplicitOverride ? "1" : "0");
            AppendTokenField(builder, "output.date-source", outputNaming.DateSource);
            foreach (OutputNamingTokenSummary token in outputNaming.Tokens.OrderBy(static token => token.TokenId, StringComparer.Ordinal))
            {
                AppendTokenField(builder, "output.token.id", token.TokenId);
                AppendTokenField(builder, "output.token.value", token.Value);
                AppendTokenField(builder, "output.token.known", token.IsKnown ? "1" : "0");
                AppendTokenField(builder, "output.token.source", token.SourceAddressSpaceId ?? string.Empty);
                AppendTokenField(builder, "output.token.snapshot", token.AcceptedSnapshotSha256 ?? string.Empty);
                AppendTokenField(builder, "output.token.parser", token.ParserId);
            }
        }
        foreach (InputArtifactSummary input in inputSummaries.OrderBy(item => item.AddressSpaceId, StringComparer.Ordinal))
        {
            AppendTokenField(builder, "input.address-space", input.AddressSpaceId);
            AppendTokenField(builder, "input.artifact", input.ArtifactId);
            AppendTokenField(builder, "input.original-file-name", input.OriginalFileName ?? string.Empty);
            AppendTokenField(builder, "input.size", input.Size.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "input.sha256", input.Sha256);
        }

        AppendTokenField(builder, "execution.status", execution.Status.ToString());
        AppendTokenField(builder, "execution.output.sha256", ToSha256Hex(execution.OutputBytes.Span));
        return ToSha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
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

}
