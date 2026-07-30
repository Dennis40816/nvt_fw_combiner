using System.Globalization;
using System.Text;
using NvtFwCombiner.Application.Authoring;
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
        AppendGeneralAdmission(builder, request.GeneralAdmission);
        if (outputNaming is not null)
        {
            AppendTokenField(builder, "output.renderer", outputNaming.RendererKind);
            AppendTokenField(builder, "output.template", outputNaming.Template);
            AppendTokenField(builder, "output.automatic-name", outputNaming.AutomaticFileName);
            AppendTokenField(builder, "output.explicit-override", outputNaming.IsExplicitOverride ? "1" : "0");
            AppendTokenField(builder, "output.date-source", outputNaming.DateSource);
            AppendOutputNamingAdmission(builder, outputNaming.Admission);
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

    private static void AppendOutputNamingAdmission(
        StringBuilder builder,
        OutputNamingAdmissionSummary? admission)
    {
        AppendTokenField(
            builder,
            "output.admission.present",
            admission is null ? "0" : "1");
        if (admission is null)
        {
            return;
        }

        AppendTokenField(builder, "output.admission.route", admission.RouteId);
        AppendTokenField(
            builder,
            "output.admission.fingerprint",
            admission.CapabilityFingerprint);
        AppendTokenField(
            builder,
            "output.admission.resolution",
            admission.ResolutionToken);
        AppendTokenField(
            builder,
            "output.admission.authoring-revision",
            admission.AuthoringRevision.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendGeneralAdmission(
        StringBuilder builder,
        GeneralAuthoringAdmissionSummary? admission)
    {
        AppendTokenField(
            builder,
            "general.admission.present",
            admission is null ? "0" : "1");
        if (admission is null)
        {
            return;
        }

        AppendTokenField(
            builder,
            "general.admission.parent",
            admission.TrustedParentId);
        AppendTokenField(
            builder,
            "general.admission.saved-rule",
            admission.SavedRuleId ?? string.Empty);
        GeneralResourceLimits? limits = admission.EffectiveLimits;
        AppendTokenField(
            builder,
            "general.admission.resolved",
            limits is null ? "0" : "1");
        if (limits is not null)
        {
            AppendTokenField(
                builder,
                "general.admission.maximum-mappings",
                limits.MaximumMappingCount.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(
                builder,
                "general.admission.maximum-total-write",
                limits.MaximumTotalWriteBytes.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(
                builder,
                "general.admission.maximum-file",
                limits.MaximumFileBytes.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(
                builder,
                "general.admission.maximum-materialization",
                limits.MaximumSafeMaterializationBytes.ToString(CultureInfo.InvariantCulture));
            foreach (GeneralSlotLengthLimits slot in limits.SlotLimits)
            {
                AppendTokenField(builder, "general.admission.slot", slot.SlotId);
                AppendTokenField(
                    builder,
                    "general.admission.slot.minimum",
                    slot.MinimumBytes.ToString(CultureInfo.InvariantCulture));
                AppendTokenField(
                    builder,
                    "general.admission.slot.maximum",
                    slot.MaximumBytes.ToString(CultureInfo.InvariantCulture));
                AppendTokenField(
                    builder,
                    "general.admission.slot.allowed",
                    string.Join(
                        ",",
                        slot.AllowedLengths.Select(length =>
                            length.ToString(CultureInfo.InvariantCulture))));
            }
        }

        foreach (GeneralInputResource resource in admission.InputResources)
        {
            AppendTokenField(
                builder,
                "general.admission.input.slot",
                resource.SlotId);
            AppendTokenField(
                builder,
                "general.admission.input.length",
                resource.LengthBytes.ToString(CultureInfo.InvariantCulture));
        }

        foreach (GeneralOccupancySegment segment in admission.OccupancySegments)
        {
            AppendTokenField(
                builder,
                "general.admission.occupancy.id",
                segment.MappingId);
            AppendTokenField(
                builder,
                "general.admission.occupancy.source-kind",
                segment.SourceKind.ToString());
            AppendTokenField(
                builder,
                "general.admission.occupancy.target-space",
                segment.TargetAddressSpaceId);
            AppendTokenField(
                builder,
                "general.admission.occupancy.start",
                segment.TargetRange.Start.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(
                builder,
                "general.admission.occupancy.length",
                segment.TargetRange.Length.ToString(CultureInfo.InvariantCulture));
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

}
