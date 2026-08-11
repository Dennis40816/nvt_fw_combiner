using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildPlanCompiler
{
    private const string IntegrityFingerprintFormat =
        "nfc.legacy-combiner.postbuild-plan.integrity.v1";

    /// <summary>
    /// Calculates one canonical identity over the selected postbuild plan and
    /// its maximum declared write authority for the supplied firmware capacity.
    /// </summary>
    public static string CalculateIntegrityFingerprint(
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var builder = new StringBuilder();
        LegacyCombinerPostbuildProfile profile = plan.Profile;
        LegacyCombinerPostbuildPlanSelector selector = plan.Selector;
        AppendFingerprintField(builder, "format", IntegrityFingerprintFormat);
        AppendFingerprintField(builder, "profile.processor-id", profile.ProcessorId);
        AppendFingerprintField(builder, "profile.ic-id", profile.IcId);
        AppendFingerprintField(builder, "profile.tool-binding-id", profile.ToolBindingId);
        AppendFingerprintField(builder, "profile.firmware-file-name", profile.FirmwareFileName);
        AppendFingerprintEnum(builder, "profile.assembly-kind", profile.AssemblyKind);
        AppendFingerprintField(
            builder,
            "profile.effective-common-fw-version",
            profile.EffectiveCommonFwVersion.ToString());
        AppendFingerprintEnum(
            builder,
            "profile.firmware-config-write-route",
            profile.FirmwareConfigWriteRoute);
        AppendFingerprintEnum(builder, "selector.kind", selector.Kind);
        AppendFingerprintEnum(builder, "selector.branch", selector.Branch);
        AppendFingerprintInteger(builder, "selector.minimum-count", selector.MinimumCount);
        AppendFingerprintInteger(builder, "selector.maximum-count", selector.MaximumCount);
        AppendFingerprintInteger(builder, "firmware.capacity", capacity);

        AppendFingerprintInteger(builder, "command.count", plan.Commands.Count);
        for (int commandIndex = 0; commandIndex < plan.Commands.Count; commandIndex++)
        {
            LegacyCombinerPostbuildCommand command = plan.Commands[commandIndex];
            string commandPrefix =
                FormattableString.Invariant($"command.{commandIndex}");
            AppendFingerprintField(builder, $"{commandPrefix}.id", command.CommandId);
            AppendFingerprintEnum(builder, $"{commandPrefix}.family", command.Family);
            AppendFingerprintField(builder, $"{commandPrefix}.mode", command.ModeArgument);
            AppendFingerprintField(
                builder,
                $"{commandPrefix}.crc",
                command.CrcArgument ?? string.Empty);
            AppendFingerprintInteger(
                builder,
                $"{commandPrefix}.block.count",
                command.Blocks.Count);
            for (int blockIndex = 0; blockIndex < command.Blocks.Count; blockIndex++)
            {
                LegacyCombinerBlockArgument block = command.Blocks[blockIndex];
                string blockPrefix = FormattableString.Invariant(
                    $"{commandPrefix}.block.{blockIndex}");
                AppendFingerprintField(builder, $"{blockPrefix}.id", block.BlockId);
                AppendFingerprintEnum(
                    builder,
                    $"{blockPrefix}.source-kind",
                    block.SourceKind);
                AppendFingerprintField(
                    builder,
                    $"{blockPrefix}.source-file-name",
                    block.SourceFileName);
                AppendFingerprintInteger(
                    builder,
                    $"{blockPrefix}.source-offset",
                    block.SourceOffset);
                AppendFingerprintRange(
                    builder,
                    $"{blockPrefix}.firmware-range",
                    block.FirmwareRange);
                AppendFingerprintField(
                    builder,
                    $"{blockPrefix}.staged-artifact-id",
                    block.StagedArtifactId ?? string.Empty);
            }
        }

        ByteRange[] stagedTargetRanges =
        [
            .. plan.Commands
                .SelectMany(static command => command.Blocks)
                .Where(static block =>
                    block.SourceKind is
                        LegacyCombinerBlockSourceKind.StagedFile or
                        LegacyCombinerBlockSourceKind.StagedArtifact)
                .Select(static block => block.FirmwareRange)
                .Distinct()
                .OrderBy(static range => range.Start)
                .ThenBy(static range => range.Length),
        ];
        IReadOnlyList<ExternalProcessorWriteRangeSection> writeSections =
            GetAllowedWriteRangeSectionsForStagedSources(
                plan,
                capacity,
                stagedTargetRanges,
                stagedTargetRanges);
        AppendFingerprintInteger(
            builder,
            "write-section.count",
            writeSections.Count);
        for (int index = 0; index < writeSections.Count; index++)
        {
            ExternalProcessorWriteRangeSection section = writeSections[index];
            string prefix =
                FormattableString.Invariant($"write-section.{index}");
            AppendFingerprintField(builder, $"{prefix}.id", section.SectionId);
            AppendFingerprintRange(builder, $"{prefix}.range", section.Range);
            AppendFingerprintField(
                builder,
                $"{prefix}.source-range",
                section.SourceRange is { } source
                    ? FormattableString.Invariant(
                        $"{source.Start}:{source.Length}")
                    : string.Empty);
        }

        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendFingerprintEnum<TEnum>(
        StringBuilder builder,
        string fieldName,
        TEnum value)
        where TEnum : struct, Enum
    {
        AppendFingerprintInteger(
            builder,
            fieldName,
            Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    private static void AppendFingerprintInteger(
        StringBuilder builder,
        string fieldName,
        long value)
    {
        AppendFingerprintField(
            builder,
            fieldName,
            value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendFingerprintRange(
        StringBuilder builder,
        string fieldName,
        ByteRange range)
    {
        AppendFingerprintField(
            builder,
            fieldName,
            FormattableString.Invariant($"{range.Start}:{range.Length}"));
    }

    private static void AppendFingerprintField(
        StringBuilder builder,
        string fieldName,
        string value)
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
