using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    public sealed partial class ResolvedFirmwareImageMap
    {
        private const string ResolutionFingerprintFormat = "nfc.resolved-firmware-map.v1";

        private static string CalculateResolutionFingerprint(ResolvedFirmwareImageMap resolvedMap)
        {
            var builder = new StringBuilder();
            AppendField(builder, "format", ResolutionFingerprintFormat);
            AppendField(builder, "family.id", resolvedMap.FamilyId);
            AppendField(builder, "family.version", resolvedMap.FamilyVersion);
            AppendField(builder, "family.content-hash", resolvedMap.FamilyContentHash);
            AppendMap(builder, resolvedMap);
            AppendTopology(builder, resolvedMap.TopologySelection);
            AppendArtifacts(builder, resolvedMap.ArtifactIdentities);
            AppendMetadataStructures(builder, resolvedMap.ResolvedMetadataStructures);
            AppendPredicateOutcomes(builder, resolvedMap.PredicateOutcomes);
            AppendFactProvenance(builder, resolvedMap.FactProvenance);

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
                .ToLowerInvariant();
        }

        private static void AppendMap(StringBuilder builder, ResolvedFirmwareImageMap resolvedMap)
        {
            FirmwareImageMap map = resolvedMap.ImageMap;
            AppendField(builder, "map.id", map.MapId);
            AppendField(builder, "map.address-space", map.AddressSpaceId);
            AppendEnum(builder, "map.coverage-policy", map.CoveragePolicy);
            AppendStringList(builder, "map.evidence", map.EvidenceRefs);
            AppendField(builder, "selection.member", resolvedMap.MemberId);
            AppendField(builder, "selection.mode", resolvedMap.ModeId);
            AppendInteger(builder, "selection.capacity", resolvedMap.CapacityBytes);
        }

        private static void AppendTopology(StringBuilder builder, TopologySelection? selection)
        {
            AppendInteger(builder, "selection.topology.present", selection is null ? 0 : 1);
            if (selection is null)
            {
                return;
            }

            AppendInteger(builder, "selection.topology.chip-count", selection.ChipCount);
            AppendField(builder, "selection.topology.label", selection.Label);
            AppendEnum(builder, "selection.topology.source", selection.Source);
            AppendField(builder, "selection.topology.source-id", selection.SourceId);
        }

        private static void AppendArtifacts(
            StringBuilder builder,
            IReadOnlyList<FirmwareArtifactIdentity> identities)
        {
            AppendInteger(builder, "artifact.count", identities.Count);
            for (int index = 0; index < identities.Count; index++)
            {
                FirmwareArtifactIdentity identity = identities[index];
                string prefix = FormattableString.Invariant($"artifact.{index}");
                AppendField(builder, $"{prefix}.id", identity.ArtifactId);
                AppendField(builder, $"{prefix}.sha256", identity.Sha256);
                AppendInteger(builder, $"{prefix}.length", identity.LengthBytes);
            }
        }

        private static void AppendMetadataStructures(
            StringBuilder builder,
            IReadOnlyList<FirmwareResolvedMetadataStructure> structures)
        {
            AppendInteger(builder, "metadata-structure.count", structures.Count);
            for (int index = 0; index < structures.Count; index++)
            {
                FirmwareResolvedMetadataStructure structure = structures[index];
                FirmwareMetadataLocatorOutcome locator = structure.LocatorOutcome;
                FirmwareDecodedMetadataStructure decoded = structure.DecodedStructure;
                string prefix = FormattableString.Invariant($"metadata-structure.{index}");
                AppendField(builder, $"{prefix}.map-id", structure.MapId);
                AppendArtifactIdentity(builder, $"{prefix}.artifact", structure.ArtifactIdentity);
                AppendEnum(builder, $"{prefix}.locator.kind", locator.LocatorKind);
                AppendAddressedRange(builder, $"{prefix}.locator.range", locator.ResolvedRange);
                AppendNullableInteger(builder, $"{prefix}.locator.marker-match-count", locator.MarkerMatchCount);
                AppendNullableInteger(builder, $"{prefix}.locator.selected-marker-start", locator.SelectedMarkerStart);
                AppendField(builder, $"{prefix}.decoded.artifact-id", decoded.ArtifactBindingId);
                AppendField(builder, $"{prefix}.decoded.structure-id", decoded.MetadataStructureId);
                FirmwareDecodedMetadataFact[] facts = [.. decoded.Facts.OrderBy(static fact => fact.FieldId, StringComparer.Ordinal)];
                AppendInteger(builder, $"{prefix}.decoded.fact.count", facts.Length);
                for (int factIndex = 0; factIndex < facts.Length; factIndex++)
                {
                    FirmwareDecodedMetadataFact fact = facts[factIndex];
                    string factPrefix = FormattableString.Invariant($"{prefix}.decoded.fact.{factIndex}");
                    AppendField(builder, $"{factPrefix}.artifact-id", fact.ArtifactBindingId);
                    AppendField(builder, $"{factPrefix}.structure-id", fact.MetadataStructureId);
                    AppendField(builder, $"{factPrefix}.field-id", fact.FieldId);
                    AppendMetadataValue(builder, $"{factPrefix}.value", fact.Value);
                }
            }
        }

        private static void AppendPredicateOutcomes(
            StringBuilder builder,
            IReadOnlyList<FirmwareMetadataPredicateOutcome> outcomes)
        {
            FirmwareMetadataPredicateOutcome[] ordered = [.. outcomes];
            Array.Sort(ordered, static (left, right) => ComparePredicates(left.Predicate, right.Predicate));
            AppendInteger(builder, "predicate.count", ordered.Length);
            for (int index = 0; index < ordered.Length; index++)
            {
                FirmwareMetadataPredicateOutcome outcome = ordered[index];
                FirmwareMetadataPredicate predicate = outcome.Predicate;
                string prefix = FormattableString.Invariant($"predicate.{index}");
                AppendField(builder, $"{prefix}.structure-id", predicate.MetadataStructureId);
                AppendField(builder, $"{prefix}.field-id", predicate.FieldId);
                AppendEnum(builder, $"{prefix}.comparison", predicate.Comparison);
                AppendMetadataValueList(builder, $"{prefix}.expected", predicate.ExpectedValues);
                AppendEnum(builder, $"{prefix}.result", outcome.Result);
                AppendNullableMetadataValue(builder, $"{prefix}.actual", outcome.ActualValue);
            }
        }

        private static void AppendFactProvenance(
            StringBuilder builder,
            IReadOnlyList<FirmwareFactProvenance> provenance)
        {
            AppendInteger(builder, "fact-provenance.count", provenance.Count);
            for (int index = 0; index < provenance.Count; index++)
            {
                FirmwareFactProvenance item = provenance[index];
                string prefix = FormattableString.Invariant($"fact-provenance.{index}");
                AppendFactKey(builder, $"{prefix}.effective-key", item.EffectiveKey);
                AppendFactKey(builder, $"{prefix}.direct-source-key", item.DirectSourceKey);
                AppendInteger(builder, $"{prefix}.alias.count", item.AliasChain.Count);
                for (int aliasIndex = 0; aliasIndex < item.AliasChain.Count; aliasIndex++)
                {
                    FirmwareFactAliasHop alias = item.AliasChain[aliasIndex];
                    string aliasPrefix = FormattableString.Invariant($"{prefix}.alias.{aliasIndex}");
                    AppendField(builder, $"{aliasPrefix}.id", alias.AliasId);
                    AppendFactKey(builder, $"{aliasPrefix}.target-key", alias.TargetKey);
                    AppendFactKey(builder, $"{aliasPrefix}.source-key", alias.SourceKey);
                    AppendFactApplicability(builder, $"{aliasPrefix}.applicability", alias.Applicability);
                    AppendField(builder, $"{aliasPrefix}.reason", alias.Reason);
                    AppendStringList(builder, $"{aliasPrefix}.evidence", alias.EvidenceRefs);
                }

                AppendStringList(builder, $"{prefix}.direct-evidence", item.DirectEvidenceRefs);
            }
        }

        private static void AppendArtifactIdentity(
            StringBuilder builder,
            string prefix,
            FirmwareArtifactIdentity identity)
        {
            AppendField(builder, $"{prefix}.id", identity.ArtifactId);
            AppendField(builder, $"{prefix}.sha256", identity.Sha256);
            AppendInteger(builder, $"{prefix}.length", identity.LengthBytes);
        }

        private static void AppendAddressedRange(
            StringBuilder builder,
            string prefix,
            FirmwareAddressedRange addressedRange)
        {
            AppendField(builder, $"{prefix}.address-space", addressedRange.AddressSpaceId);
            AppendInteger(builder, $"{prefix}.start", addressedRange.Range.Start);
            AppendInteger(builder, $"{prefix}.length", addressedRange.Range.Length);
        }

        private static void AppendFactKey(StringBuilder builder, string prefix, FirmwareMapFactKey key)
        {
            AppendField(builder, $"{prefix}.member-id", key.MemberId);
            AppendField(builder, $"{prefix}.map-id", key.MapId);
            AppendEnum(builder, $"{prefix}.kind", key.FactKind);
            AppendField(builder, $"{prefix}.fact-id", key.FactId);
        }

        private static void AppendFactApplicability(
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

        private static void AppendPredicateDefinitions(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<FirmwareMetadataPredicate> predicates)
        {
            FirmwareMetadataPredicate[] ordered = [.. predicates];
            Array.Sort(ordered, ComparePredicates);
            AppendInteger(builder, $"{prefix}.count", ordered.Length);
            for (int index = 0; index < ordered.Length; index++)
            {
                FirmwareMetadataPredicate predicate = ordered[index];
                string predicatePrefix = FormattableString.Invariant($"{prefix}.{index}");
                AppendField(builder, $"{predicatePrefix}.structure-id", predicate.MetadataStructureId);
                AppendField(builder, $"{predicatePrefix}.field-id", predicate.FieldId);
                AppendEnum(builder, $"{predicatePrefix}.comparison", predicate.Comparison);
                AppendMetadataValueList(builder, $"{predicatePrefix}.expected", predicate.ExpectedValues);
            }
        }

        private static void AppendMetadataValueList(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<FirmwareMetadataValue> values)
        {
            FirmwareMetadataValue[] ordered = [.. values];
            Array.Sort(ordered, CompareMetadataValues);
            AppendInteger(builder, $"{prefix}.count", ordered.Length);
            for (int index = 0; index < ordered.Length; index++)
            {
                AppendMetadataValue(builder, FormattableString.Invariant($"{prefix}.{index}"), ordered[index]);
            }
        }

        private static void AppendNullableMetadataValue(
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

        private static void AppendMetadataValue(
            StringBuilder builder,
            string prefix,
            FirmwareMetadataValue value)
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

        private static void AppendStringList(StringBuilder builder, string prefix, IReadOnlyList<string> values)
        {
            AppendInteger(builder, $"{prefix}.count", values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                AppendField(builder, FormattableString.Invariant($"{prefix}.{index}"), values[index]);
            }
        }

        private static void AppendNullableInteger(StringBuilder builder, string fieldName, long? value)
        {
            AppendInteger(builder, $"{fieldName}.present", value is null ? 0 : 1);
            if (value is { } known)
            {
                AppendInteger(builder, fieldName, known);
            }
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

        private static void AppendUnsignedInteger(StringBuilder builder, string fieldName, ulong value)
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

        private static int ComparePredicates(FirmwareMetadataPredicate left, FirmwareMetadataPredicate right)
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
}
