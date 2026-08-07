using System.Globalization;
using System.Text;

namespace NvtFwCombiner.Domain.Firmware;

internal static class FirmwareFingerprintWriter
{
    internal static void AppendList<T>(
        StringBuilder builder,
        string prefix,
        IReadOnlyList<T> values,
        Action<StringBuilder, string, T> appendItem)
    {
        AppendInteger(builder, $"{prefix}.count", values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            appendItem(builder, FormattableString.Invariant($"{prefix}.{index}"), values[index]);
        }
    }

    internal static void AppendFactProvenance(
        StringBuilder builder,
        IReadOnlyList<FirmwareFactProvenance> provenance)
    {
        AppendList(builder, "fact-provenance", provenance, AppendFactProvenance);
    }

    internal static void AppendFactProvenance(
        StringBuilder builder,
        string prefix,
        FirmwareFactProvenance provenance)
    {
        AppendFactKey(builder, $"{prefix}.effective-key", provenance.EffectiveKey);
        AppendFactKey(builder, $"{prefix}.direct-source-key", provenance.DirectSourceKey);
        AppendList(builder, $"{prefix}.alias", provenance.AliasChain, AppendFactAlias);

        AppendStringList(builder, $"{prefix}.direct-evidence", provenance.DirectEvidenceRefs);
    }

    private static void AppendFactAlias(
        StringBuilder builder,
        string prefix,
        FirmwareFactAliasHop alias)
    {
        AppendField(builder, $"{prefix}.id", alias.AliasId);
        AppendFactKey(builder, $"{prefix}.target-key", alias.TargetKey);
        AppendFactKey(builder, $"{prefix}.source-key", alias.SourceKey);
        AppendFactApplicability(builder, $"{prefix}.applicability", alias.Applicability);
        AppendField(builder, $"{prefix}.reason", alias.Reason);
        AppendStringList(builder, $"{prefix}.evidence", alias.EvidenceRefs);
    }

    internal static void AppendFactKey(StringBuilder builder, string prefix, FirmwareMapFactKey key)
    {
        AppendField(builder, $"{prefix}.member-id", key.MemberId);
        AppendField(builder, $"{prefix}.map-id", key.MapId);
        AppendEnum(builder, $"{prefix}.kind", key.FactKind);
        AppendField(builder, $"{prefix}.fact-id", key.FactId);
    }

    internal static void AppendFactApplicability(
        StringBuilder builder,
        string prefix,
        FirmwareFactApplicability applicability)
    {
        AppendStringList(builder, $"{prefix}.mode", applicability.ModeIds);
        AppendTopologyRequirement(builder, $"{prefix}.topology", applicability.TopologyRequirement);
        AppendInteger(builder, $"{prefix}.capacity", applicability.CapacityBytes);
        AppendStringList(builder, $"{prefix}.common-category", applicability.CommonFirmwareCategoryIds);
        AppendPredicateDefinitions(builder, $"{prefix}.predicate", applicability.MetadataPredicates);
    }

    internal static void AppendPredicateDefinitions(
        StringBuilder builder,
        string prefix,
        IReadOnlyList<FirmwareMetadataPredicate> predicates)
    {
        FirmwareMetadataPredicate[] ordered = [.. predicates];
        Array.Sort(ordered, ComparePredicates);
        AppendList(builder, prefix, ordered, static (target, predicatePrefix, predicate) =>
        {
            AppendField(target, $"{predicatePrefix}.structure-id", predicate.MetadataStructureId);
            AppendField(target, $"{predicatePrefix}.field-id", predicate.FieldId);
            AppendEnum(target, $"{predicatePrefix}.comparison", predicate.Comparison);
            AppendMetadataValueList(target, $"{predicatePrefix}.expected", predicate.ExpectedValues);
        });
    }

    internal static void AppendMetadataValueList(
        StringBuilder builder,
        string prefix,
        IReadOnlyList<FirmwareMetadataValue> values)
    {
        FirmwareMetadataValue[] ordered = [.. values];
        Array.Sort(ordered, CompareMetadataValues);
        AppendList(builder, prefix, ordered, AppendMetadataValue);
    }

    internal static void AppendNullableMetadataValue(
        StringBuilder builder,
        string prefix,
        FirmwareMetadataValue? value)
    {
        AppendInteger(builder, $"{prefix}.present", value is null ? 0 : 1);
        if (value is not null)
        {
            AppendMetadataValue(builder, prefix, value);
        }
    }

    internal static void AppendMetadataValue(StringBuilder builder, string prefix, FirmwareMetadataValue value)
    {
        AppendEnum(builder, $"{prefix}.kind", value.Kind);
        switch (value.Kind)
        {
            case FirmwareMetadataValueKind.SignedInteger:
                AppendInteger(builder, $"{prefix}.signed", value.SignedIntegerValue!.Value);
                break;
            case FirmwareMetadataValueKind.UnsignedInteger:
                AppendUnsignedInteger(builder, $"{prefix}.unsigned", value.UnsignedIntegerValue!.Value);
                break;
            case FirmwareMetadataValueKind.Bytes:
                AppendField(builder, $"{prefix}.bytes", value.BytesValue!.Hex);
                break;
            case FirmwareMetadataValueKind.Text:
                AppendField(builder, $"{prefix}.text", value.TextValue!);
                break;
            default:
                throw new InvalidOperationException("Unknown firmware metadata value kind.");
        }
    }

    internal static void AppendStringList(StringBuilder builder, string prefix, IReadOnlyList<string> values)
    {
        AppendList(builder, prefix, values, AppendField);
    }

    internal static void AppendNullableInteger(StringBuilder builder, string fieldName, long? value)
    {
        AppendInteger(builder, $"{fieldName}.present", value is null ? 0 : 1);
        if (value is { } known)
        {
            AppendInteger(builder, fieldName, known);
        }
    }

    internal static void AppendEnum<TEnum>(StringBuilder builder, string fieldName, TEnum value)
        where TEnum : struct, Enum
    {
        AppendInteger(builder, fieldName, Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    internal static void AppendInteger(StringBuilder builder, string fieldName, long value)
    {
        AppendField(builder, fieldName, value.ToString(CultureInfo.InvariantCulture));
    }

    internal static void AppendUnsignedInteger(StringBuilder builder, string fieldName, ulong value)
    {
        AppendField(builder, fieldName, value.ToString(CultureInfo.InvariantCulture));
    }

    internal static void AppendField(StringBuilder builder, string fieldName, string value)
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

    internal static int ComparePredicates(FirmwareMetadataPredicate left, FirmwareMetadataPredicate right)
    {
        int structure = StringComparer.Ordinal.Compare(left.MetadataStructureId, right.MetadataStructureId);
        if (structure != 0)
        {
            return structure;
        }

        int field = StringComparer.Ordinal.Compare(left.FieldId, right.FieldId);
        if (field != 0)
        {
            return field;
        }

        int comparison = left.Comparison.CompareTo(right.Comparison);
        if (comparison != 0)
        {
            return comparison;
        }

        FirmwareMetadataValue[] leftValues = [.. left.ExpectedValues];
        FirmwareMetadataValue[] rightValues = [.. right.ExpectedValues];
        Array.Sort(leftValues, CompareMetadataValues);
        Array.Sort(rightValues, CompareMetadataValues);
        int count = leftValues.Length.CompareTo(rightValues.Length);
        if (count != 0)
        {
            return count;
        }

        for (int index = 0; index < leftValues.Length; index++)
        {
            int value = CompareMetadataValues(leftValues[index], rightValues[index]);
            if (value != 0)
            {
                return value;
            }
        }

        return 0;
    }

    private static void AppendTopologyRequirement(
        StringBuilder builder,
        string prefix,
        TopologyRequirement requirement)
    {
        AppendEnum(builder, $"{prefix}.kind", requirement.Kind);
        AppendNullableInteger(builder, $"{prefix}.minimum-chip-count", requirement.MinimumChipCount);
        AppendNullableInteger(builder, $"{prefix}.maximum-chip-count", requirement.MaximumChipCount);
        AppendNullableInteger(builder, $"{prefix}.exact-chip-count", requirement.ExactChipCount);
    }

    private static int CompareMetadataValues(FirmwareMetadataValue left, FirmwareMetadataValue right)
    {
        int kind = left.Kind.CompareTo(right.Kind);
        return kind != 0
            ? kind
            : left.Kind switch
            {
                FirmwareMetadataValueKind.SignedInteger => left.SignedIntegerValue!.Value.CompareTo(
                    right.SignedIntegerValue!.Value),
                FirmwareMetadataValueKind.UnsignedInteger => left.UnsignedIntegerValue!.Value.CompareTo(
                    right.UnsignedIntegerValue!.Value),
                FirmwareMetadataValueKind.Bytes => StringComparer.Ordinal.Compare(
                    left.BytesValue!.Hex,
                    right.BytesValue!.Hex),
                FirmwareMetadataValueKind.Text => StringComparer.Ordinal.Compare(left.TextValue, right.TextValue),
                _ => throw new InvalidOperationException("Unknown firmware metadata value kind."),
            };
    }
}
