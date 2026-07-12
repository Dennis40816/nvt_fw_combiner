using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private const string LegacyFingerprintFormat = "nfc.compiled-composition.legacy.v1";
    private const string V2FingerprintFormat = "nfc.compiled-composition.profile-v2.v3";

    private static string CalculateCompilationFingerprint(CompiledComposition composition)
    {
        return composition.Authority switch
        {
            LegacyProfileCompilationAuthority => CalculateLegacyCompilationFingerprint(composition),
            ProfileBundleV2CompilationAuthority => CalculateV2CompilationFingerprint(composition),
            _ => throw new InvalidOperationException("Unknown compiled composition authority."),
        };
    }

    private static string CalculateLegacyCompilationFingerprint(CompiledComposition composition)
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

    private static string CalculateV2CompilationFingerprint(CompiledComposition composition)
    {
        V2CompiledCompositionDetails details = composition.V2Details ?? throw new InvalidOperationException(
            "Profile-bundle-v2 artifacts require paired v2 details.");
        V2CompilationProvenance provenance = details.Provenance;
        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        var builder = new StringBuilder();
        AppendField(builder, "format", V2FingerprintFormat);
        AppendField(builder, "authority.kind", "profile-bundle-v2");
        AppendField(
            builder,
            "authority.model-version",
            ((ProfileBundleV2CompilationAuthority)composition.Authority).ModelVersion);
        AppendField(builder, "profile.id", composition.ProfileId);
        AppendField(builder, "profile.version", composition.ProfileVersion);
        AppendField(builder, "profile.ic", composition.IcId);
        AppendField(builder, "profile.mode", composition.ModeId);
        AppendField(builder, "profile.experience", composition.ExperienceId);
        AppendEnum(builder, "profile.composition-kind", composition.CompositionKind);
        AppendEnum(builder, "run-policy.ic-number", composition.IcNumberPolicy);
        AppendEnum(builder, "eligibility", composition.Eligibility);
        AppendField(builder, "bundle.id", provenance.Bundle.BundleId);
        AppendField(builder, "bundle.version", provenance.Bundle.BundleVersion);
        AppendField(builder, "bundle.content-hash", provenance.Bundle.ContentHash);
        AppendField(builder, "bundle.trust-anchor-binding-id", provenance.Bundle.TrustAnchorBindingId);
        AppendField(builder, "profile-entry.id", provenance.ProfileEntry.EntryId);
        AppendField(builder, "profile-entry.content-hash", provenance.ProfileEntry.ContentHash);
        AppendField(builder, "resolved-map.fingerprint", provenance.ResolvedMap.ResolutionFingerprint);
        AppendEnum(builder, "promotion.stage", provenance.Promotion.Stage);
        AppendInteger(builder, "promotion.blocker.count", provenance.Promotion.Blockers.Count);
        for (int index = 0; index < provenance.Promotion.Blockers.Count; index++)
        {
            CompiledProfilePromotionBlocker blocker = provenance.Promotion.Blockers[index];
            string prefix = FormattableString.Invariant($"promotion.blocker.{index}");
            AppendField(builder, $"{prefix}.id", blocker.BlockerId);
            AppendEnum(builder, $"{prefix}.kind", blocker.Kind);
            AppendField(builder, $"{prefix}.reason", blocker.Reason);
            AppendStringList(builder, $"{prefix}.evidence", blocker.EvidenceRefs);
        }

        AppendStringList(builder, "profile.evidence", provenance.ProfileEvidenceRefs);
        AppendValidationRequirements(builder, provenance.ValidationRequirements);
        AppendCapabilityAdmissions(builder, provenance.RequiredCapabilities);
        AppendInputContract(builder, details.InputContract);
        AppendRegionAccessContract(builder, details.RegionAccessContract);
        AppendField(builder, "output.template", output.FileNameTemplate);
        AppendInteger(builder, "output.allow-override", output.AllowOverride ? 1 : 0);
        AppendEnum(builder, "output.invalid-character-policy", output.InvalidCharacterPolicy);
        AppendStringList(builder, "output.required-token", output.RequiredTokenIds);
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
            if (addressSpace.UnexpectedInputLengthIssueCode is { } unexpectedInputLengthIssueCode)
            {
                AppendField(builder, $"{prefix}.unexpected-input-length-issue-code", unexpectedInputLengthIssueCode);
            }
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
        AppendScalarTransform(builder, prefix, operation.ScalarTransform);
        AppendField(builder, $"{prefix}.reason", operation.Reason);
        AppendField(builder, $"{prefix}.provenance.kind", operation.Provenance.Kind);
        AppendField(builder, $"{prefix}.provenance.source-id", operation.Provenance.SourceId ?? string.Empty);
        AppendField(builder, $"{prefix}.provenance.source-version", operation.Provenance.SourceVersion ?? string.Empty);
        AppendProcessor(builder, prefix, operation.ExternalProcessorInvocation);
    }

    private static void AppendScalarTransform(
        StringBuilder builder,
        string operationPrefix,
        ScalarTransform? transform)
    {
        if (transform is null)
        {
            return;
        }

        string prefix = $"{operationPrefix}.scalar-transform";
        AppendEnum(builder, $"{prefix}.width", transform.Width);
        AppendEnum(builder, $"{prefix}.byte-order", transform.ByteOrder);
        AppendField(builder, $"{prefix}.addend", transform.Addend.ToString(CultureInfo.InvariantCulture));
        AppendField(
            builder,
            $"{prefix}.expected-before",
            transform.ExpectedBefore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        AppendEnum(builder, $"{prefix}.overflow-policy", transform.OverflowPolicy);
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

    private static void AppendStringList(StringBuilder builder, string fieldPrefix, IReadOnlyList<string> values)
    {
        AppendInteger(builder, $"{fieldPrefix}.count", values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            AppendField(builder, FormattableString.Invariant($"{fieldPrefix}.{index}"), values[index]);
        }
    }

    private static void AppendValidationRequirements(
        StringBuilder builder,
        IReadOnlyList<CompiledValidationRequirement> requirements)
    {
        AppendInteger(builder, "validation.count", requirements.Count);
        for (int index = 0; index < requirements.Count; index++)
        {
            CompiledValidationRequirement requirement = requirements[index];
            string prefix = FormattableString.Invariant($"validation.{index}");
            AppendField(builder, $"{prefix}.rule-id", requirement.RuleId);
            AppendEnum(builder, $"{prefix}.stage", requirement.Stage);
            AppendEnum(builder, $"{prefix}.severity", requirement.Severity);
            AppendField(builder, $"{prefix}.issue-code", requirement.IssueCode);
            AppendEnum(builder, $"{prefix}.kind", requirement.Kind);
            switch (requirement)
            {
                case CompiledMetadataValueValidation metadata:
                    AppendFieldReference(builder, $"{prefix}.field", metadata.Field);
                    AppendEnum(builder, $"{prefix}.comparison", metadata.Comparison);
                    AppendValidationLiterals(builder, $"{prefix}.expected", metadata.ExpectedValues);
                    break;
                case CompiledPidSanityValidation pid:
                    AppendFieldReference(builder, $"{prefix}.field", pid.Field);
                    break;
                case CompiledMetadataEqualityValidation equality:
                    AppendFieldReference(builder, $"{prefix}.left", equality.Left);
                    AppendFieldReference(builder, $"{prefix}.right", equality.Right);
                    break;
                case CompiledRejectMetadataBytePatternValidation rejected:
                    AppendFieldReference(builder, $"{prefix}.field", rejected.Field);
                    AppendInteger(builder, $"{prefix}.rejected-pattern.count", rejected.RejectedPatterns.Count);
                    for (int patternIndex = 0; patternIndex < rejected.RejectedPatterns.Count; patternIndex++)
                    {
                        AppendEnum(
                            builder,
                            FormattableString.Invariant($"{prefix}.rejected-pattern.{patternIndex}"),
                            rejected.RejectedPatterns[patternIndex]);
                    }

                    break;
                case CompiledViewByteAssertionValidation assertion:
                    AppendField(builder, $"{prefix}.view-id", assertion.ViewId);
                    AppendField(builder, $"{prefix}.expected", assertion.Expected.Hex);
                    AppendField(builder, $"{prefix}.mask", assertion.Mask?.Hex ?? string.Empty);
                    break;
                default:
                    throw new InvalidOperationException("Unknown compiled validation requirement kind.");
            }
        }
    }

    private static void AppendInputContract(StringBuilder builder, CompiledInputContract contract)
    {
        AppendInteger(builder, "input.slot.count", contract.Slots.Count);
        for (int index = 0; index < contract.Slots.Count; index++)
        {
            CompiledInputSlotRequirement slot = contract.Slots[index];
            string prefix = FormattableString.Invariant($"input.slot.{index}");
            AppendField(builder, $"{prefix}.id", slot.SlotId);
            AppendField(builder, $"{prefix}.role", slot.Role);
            AppendEnum(builder, $"{prefix}.artifact-class", slot.ArtifactClass);
            AppendInteger(builder, $"{prefix}.required", slot.Required ? 1 : 0);
            AppendEnum(builder, $"{prefix}.cardinality", slot.Cardinality);
            AppendStringList(builder, $"{prefix}.extension", slot.AcceptedExtensions);
            AppendInputLengthRequirement(builder, $"{prefix}.length", slot.LengthRequirement);
            AppendInputNormalization(builder, $"{prefix}.normalization", slot.Normalization);
        }

        AppendInteger(builder, "input.binding.count", contract.SpaceBindings.Count);
        for (int index = 0; index < contract.SpaceBindings.Count; index++)
        {
            CompiledInputSpaceBinding binding = contract.SpaceBindings[index];
            string prefix = FormattableString.Invariant($"input.binding.{index}");
            AppendField(builder, $"{prefix}.address-space", binding.AddressSpaceId);
            AppendField(builder, $"{prefix}.slot", binding.SlotId);
            AppendEnum(builder, $"{prefix}.instance-policy", binding.InstancePolicy);
        }
    }

    private static void AppendInputLengthRequirement(
        StringBuilder builder,
        string prefix,
        CompiledInputLengthRequirement requirement)
    {
        AppendEnum(builder, $"{prefix}.kind", requirement.Kind);
        switch (requirement)
        {
            case CompiledExactBytesInputLengthRequirement exact:
                AppendInteger(builder, $"{prefix}.bytes", exact.Bytes);
                break;
            case CompiledExactResolvedMapCapacityInputLengthRequirement resolved:
                AppendInteger(builder, $"{prefix}.bytes", resolved.Bytes);
                break;
            case CompiledBoundedInputLengthRequirement bounded:
                AppendInteger(builder, $"{prefix}.minimum-bytes", bounded.MinimumBytes);
                AppendInteger(builder, $"{prefix}.maximum-bytes", bounded.MaximumBytes);
                break;
            case CompiledNormalDpExtractWithWarningInputLengthRequirement normalDp:
                AppendField(builder, $"{prefix}.issue-code", normalDp.IssueCode);
                break;
            case CompiledTpMaximum256KInputLengthRequirement:
                AppendInteger(builder, $"{prefix}.maximum-bytes", CompiledTpMaximum256KInputLengthRequirement.MaximumBytes);
                break;
            default:
                throw new InvalidOperationException("Unknown compiled input length requirement.");
        }
    }

    private static void AppendInputNormalization(
        StringBuilder builder,
        string prefix,
        CompiledInputNormalization normalization)
    {
        AppendEnum(builder, $"{prefix}.kind", normalization.Kind);
        switch (normalization)
        {
            case CompiledNoInputNormalization:
                return;
            case CompiledPadShorterInputNormalization padded:
                AppendInteger(builder, $"{prefix}.fill-byte", padded.FillByte);
                AppendField(builder, $"{prefix}.evidence", padded.EvidenceRef);
                return;
            case CompiledTruncateCtrlRamInputNormalization truncated:
                AppendField(builder, $"{prefix}.warning-issue-code", truncated.WarningIssueCode);
                AppendField(builder, $"{prefix}.evidence", truncated.EvidenceRef);
                return;
            default:
                throw new InvalidOperationException("Unknown compiled input normalization.");
        }
    }

    private static void AppendCapabilityAdmissions(
        StringBuilder builder,
        IReadOnlyList<CompiledCapabilityAdmission> admissions)
    {
        AppendInteger(builder, "capability-admission.count", admissions.Count);
        for (int index = 0; index < admissions.Count; index++)
        {
            CompiledCapabilityAdmission admission = admissions[index];
            FirmwareMapFactBinding<FirmwareCapabilityFact> binding = admission.Binding;
            FirmwareCapabilityFact capability = binding.Value;
            string prefix = FormattableString.Invariant($"capability-admission.{index}");
            AppendField(builder, $"{prefix}.required-capability-id", admission.RequiredCapabilityId);
            AppendFactKey(builder, $"{prefix}.effective-key", binding.EffectiveKey);
            AppendFactKey(builder, $"{prefix}.direct-source-key", binding.DirectSourceKey);
            AppendField(builder, $"{prefix}.canonical-fact-id", binding.CanonicalFactId);
            AppendField(builder, $"{prefix}.fact-id", capability.CapabilityFactId);
            AppendField(builder, $"{prefix}.capability-id", capability.CapabilityId);
            AppendEnum(builder, $"{prefix}.state", capability.State);
            AppendField(builder, $"{prefix}.reason", capability.Reason);
            AppendStringList(builder, $"{prefix}.fact-evidence", capability.EvidenceRefs);
            AppendFactApplicability(builder, $"{prefix}.applicability", binding.Applicability);
            AppendFactProvenance(builder, $"{prefix}.provenance", binding.Provenance);
        }
    }

    private static void AppendFactProvenance(
        StringBuilder builder,
        string prefix,
        FirmwareFactProvenance provenance)
    {
        AppendFactKey(builder, $"{prefix}.effective-key", provenance.EffectiveKey);
        AppendFactKey(builder, $"{prefix}.direct-source-key", provenance.DirectSourceKey);
        AppendInteger(builder, $"{prefix}.alias.count", provenance.AliasChain.Count);
        for (int index = 0; index < provenance.AliasChain.Count; index++)
        {
            FirmwareFactAliasHop alias = provenance.AliasChain[index];
            string aliasPrefix = FormattableString.Invariant($"{prefix}.alias.{index}");
            AppendField(builder, $"{aliasPrefix}.id", alias.AliasId);
            AppendFactKey(builder, $"{aliasPrefix}.target-key", alias.TargetKey);
            AppendFactKey(builder, $"{aliasPrefix}.source-key", alias.SourceKey);
            AppendFactApplicability(builder, $"{aliasPrefix}.applicability", alias.Applicability);
            AppendField(builder, $"{aliasPrefix}.reason", alias.Reason);
            AppendStringList(builder, $"{aliasPrefix}.evidence", alias.EvidenceRefs);
        }

        AppendStringList(builder, $"{prefix}.direct-evidence", provenance.DirectEvidenceRefs);
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

    private static void AppendTopologyRequirement(StringBuilder builder, string prefix, TopologyRequirement requirement)
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

    private static void AppendMetadataValue(StringBuilder builder, string prefix, FirmwareMetadataValue value)
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

    private static void AppendNullableInteger(StringBuilder builder, string fieldName, long? value)
    {
        AppendInteger(builder, $"{fieldName}.present", value is null ? 0 : 1);
        if (value is { } known)
        {
            AppendInteger(builder, fieldName, known);
        }
    }

    private static void AppendUnsignedInteger(StringBuilder builder, string fieldName, ulong value)
    {
        AppendField(builder, fieldName, value.ToString(CultureInfo.InvariantCulture));
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

    private static void AppendFieldReference(
        StringBuilder builder,
        string prefix,
        CompiledValidationFieldReference field)
    {
        AppendField(builder, $"{prefix}.binding-id", field.BindingId);
        AppendField(builder, $"{prefix}.field-id", field.FieldId);
    }

    private static void AppendValidationLiterals(
        StringBuilder builder,
        string prefix,
        IReadOnlyList<CompiledValidationScalarLiteral> values)
    {
        AppendInteger(builder, $"{prefix}.count", values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            CompiledValidationScalarLiteral value = values[index];
            string valuePrefix = FormattableString.Invariant($"{prefix}.{index}");
            AppendEnum(builder, $"{valuePrefix}.kind", value.Kind);
            switch (value)
            {
                case CompiledValidationIntegerLiteral integer:
                    AppendField(builder, $"{valuePrefix}.integer", integer.Value.ToString(CultureInfo.InvariantCulture));
                    break;
                case CompiledValidationTextLiteral text:
                    AppendField(builder, $"{valuePrefix}.text", text.Value);
                    break;
                default:
                    throw new InvalidOperationException("Unknown compiled validation literal kind.");
            }
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
