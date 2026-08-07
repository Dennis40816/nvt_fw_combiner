using System.Security.Cryptography;
using System.Text;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    public sealed partial class ResolvedFirmwareImageMap
    {
        private const string LegacyResolutionFingerprintFormat =
            "nfc.resolved-firmware-map.v1";
        private const string TypedMetadataResolutionFingerprintFormat =
            "nfc.resolved-firmware-map.v2";

        private static string CalculateResolutionFingerprint(ResolvedFirmwareImageMap resolvedMap)
        {
            bool includesTypedMetadata = resolvedMap.ResolvedMetadataStructures.Any(
                static structure =>
                    structure.StructureDefinition.Definition.TypedDefinition is not null);
            var builder = new StringBuilder();
            AppendField(
                builder,
                "format",
                includesTypedMetadata
                    ? TypedMetadataResolutionFingerprintFormat
                    : LegacyResolutionFingerprintFormat);
            AppendField(builder, "family.id", resolvedMap.FamilyId);
            AppendField(builder, "family.version", resolvedMap.FamilyVersion);
            AppendField(builder, "family.content-hash", resolvedMap.FamilyContentHash);
            AppendMap(builder, resolvedMap);
            AppendTopology(builder, resolvedMap.TopologySelection);
            AppendArtifacts(builder, resolvedMap.ArtifactIdentities);
            AppendMetadataStructures(
                builder,
                resolvedMap.ResolvedMetadataStructures,
                includesTypedMetadata);
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
            AppendList(builder, "artifact", identities, AppendArtifactIdentity);
        }

        private static void AppendMetadataStructures(
            StringBuilder builder,
            IReadOnlyList<FirmwareResolvedMetadataStructure> structures,
            bool includeDefinitionIdentity)
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
                if (includeDefinitionIdentity)
                {
                    AppendField(
                        builder,
                        $"{prefix}.definition.binding-id",
                        structure.StructureDefinition.StructureId);
                    AppendField(
                        builder,
                        $"{prefix}.definition.logical-id",
                        structure.StructureDefinition.Definition.DefinitionId);
                    AppendEnum(
                        builder,
                        $"{prefix}.definition.kind",
                        structure.StructureDefinition.Definition.StructureKind);
                    AppendInteger(builder, $"{prefix}.field.count", structure.Fields.Count);
                    for (int fieldIndex = 0; fieldIndex < structure.Fields.Count; fieldIndex++)
                    {
                        FirmwareResolvedMetadataField field = structure.Fields[fieldIndex];
                        string fieldPrefix =
                            FormattableString.Invariant($"{prefix}.field.{fieldIndex}");
                        AppendField(builder, $"{fieldPrefix}.id", field.Field.FieldId);
                        AppendEnum(
                            builder,
                            $"{fieldPrefix}.applicability",
                            field.Applicability);
                    }
                }

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

                if (decoded.Relations.Count > 0)
                {
                    FirmwareDecodedMetadataRelation[] relations =
                    [
                        .. decoded.Relations.OrderBy(
                            static relation => relation.RelationId,
                            StringComparer.Ordinal),
                    ];
                    AppendInteger(builder, $"{prefix}.decoded.relation.count", relations.Length);
                    for (int relationIndex = 0; relationIndex < relations.Length; relationIndex++)
                    {
                        FirmwareDecodedMetadataRelation relation = relations[relationIndex];
                        string relationPrefix =
                            FormattableString.Invariant($"{prefix}.decoded.relation.{relationIndex}");
                        AppendField(builder, $"{relationPrefix}.id", relation.RelationId);
                        AppendEnum(builder, $"{relationPrefix}.kind", relation.Kind);
                        AppendField(builder, $"{relationPrefix}.source-field-id", relation.SourceFieldId);
                        AppendField(builder, $"{relationPrefix}.related-field-id", relation.RelatedFieldId);
                        AppendInteger(builder, $"{relationPrefix}.satisfied", relation.IsSatisfied ? 1 : 0);
                    }
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
    }
}
