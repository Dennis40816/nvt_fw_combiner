using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private const string V2FingerprintFormat = "nfc.compiled-composition.profile-v2.v5";
    private const string CapabilityBoundV2FingerprintFormat =
        "nfc.compiled-composition.profile-v2.v7";
    private const string V2CompilerModelVersion = "1.0";

    private static string CalculateCompilationFingerprint(CompiledComposition composition)
    {
        V2CompiledCompositionDetails details = composition.V2Details;
        return details.Provenance.Context switch
        {
            RuntimeReferenceReplaceV2CompilationContext runtimeReference => CalculateMapBoundV2CompilationFingerprint(composition, runtimeReference),
            ResolvedMapV2CompilationContext resolvedMap => CalculateMapBoundV2CompilationFingerprint(composition, resolvedMap),
            LogicalOutputV2CompilationContext logical => CalculateLogicalOutputV2CompilationFingerprint(composition, logical),
            _ => throw new InvalidOperationException("Unknown profile-bundle-v2 compilation context."),
        };
    }

    private static string CalculateMapBoundV2CompilationFingerprint(
        CompiledComposition composition,
        MapBoundV2CompilationContext context)
    {
        V2CompiledCompositionDetails details = composition.V2Details;
        V2CompilationProvenance provenance = details.Provenance;
        bool capabilityBound = composition.CapabilityFingerprint is not null;
        var builder = new StringBuilder();
        AppendField(
            builder,
            "format",
            !capabilityBound
                ? V2FingerprintFormat
                : CapabilityBoundV2FingerprintFormat);
        AppendCapabilityFingerprint(builder, composition);
        if (capabilityBound)
        {
            AppendV2CompilationAdmission(builder, composition);
        }
        else
        {
            AppendV2ProfileIdentity(builder, composition);
        }
        if (context is RuntimeReferenceReplaceV2CompilationContext runtimeReference)
        {
            AppendRuntimeReferenceCompilationContext(
                builder,
                runtimeReference,
                capabilityBound);
        }
        if (!capabilityBound)
        {
            AppendV2ProfileAdmission(builder, composition, provenance);
        }
        AppendField(builder, "resolved-map.fingerprint", provenance.ResolvedMap.ResolutionFingerprint);

        return CompleteV2Fingerprint(
            builder,
            composition,
            details,
            includeDefinitionProvenance: !capabilityBound);
    }

    private static void AppendV2ProfileIdentity(
        StringBuilder builder,
        CompiledComposition composition)
    {
        AppendField(builder, "authority.kind", "profile-bundle-v2");
        AppendField(
            builder,
            "authority.model-version",
            V2CompilerModelVersion);
        V2CompiledCompositionDetails details = composition.V2Details;
        AppendField(builder, "profile.id", details.ProfileId);
        AppendField(builder, "profile.version", details.ProfileVersion);
        AppendField(builder, "profile.ic", details.Provenance.Context.MemberId);
        AppendField(builder, "profile.mode", details.Provenance.Context.ModeId);
        AppendField(builder, "profile.experience", details.ExperienceId);
        AppendEnum(builder, "profile.composition-kind", details.CompositionKind);
    }

    private static void AppendCapabilityFingerprint(
        StringBuilder builder,
        CompiledComposition composition)
    {
        if (composition.CapabilityFingerprint is { } fingerprint)
        {
            AppendField(builder, "capability.fingerprint", fingerprint);
        }
    }

    private static void AppendV2ProfileAdmission(
        StringBuilder builder,
        CompiledComposition composition,
        V2CompilationProvenance provenance)
    {
        AppendIcNumberInputMode(builder, composition.V2Details.IcNumberInputMode);
        AppendEnum(builder, "eligibility", composition.Eligibility);
        AppendField(builder, "bundle.id", provenance.Bundle.BundleId);
        AppendField(builder, "bundle.version", provenance.Bundle.BundleVersion);
        AppendField(builder, "bundle.content-hash", provenance.Bundle.ContentHash);
        AppendField(builder, "bundle.trust-anchor-binding-id", provenance.Bundle.TrustAnchorBindingId);
        AppendField(builder, "profile-entry.id", provenance.ProfileEntry.EntryId);
        AppendField(builder, "profile-entry.content-hash", provenance.ProfileEntry.ContentHash);
    }

    private static void AppendV2CompilationAdmission(
        StringBuilder builder,
        CompiledComposition composition)
    {
        AppendIcNumberInputMode(builder, composition.V2Details.IcNumberInputMode);
        AppendEnum(builder, "eligibility", composition.Eligibility);
    }

    private static void AppendIcNumberInputMode(
        StringBuilder builder,
        IcNumberInputMode? inputMode)
    {
        AppendInteger(
            builder,
            "run-policy.ic-number",
            inputMode is { } mode ? (long)mode + 1 : 0);
    }

    private static string CompleteV2Fingerprint(
        StringBuilder builder,
        CompiledComposition composition,
        V2CompiledCompositionDetails details,
        bool includeDefinitionProvenance)
    {
        V2CompilationProvenance provenance = details.Provenance;
        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        if (includeDefinitionProvenance)
        {
            AppendEnum(builder, "promotion.stage", provenance.Promotion.Stage);
            AppendList(builder, "promotion.blocker", provenance.Promotion.Blockers, AppendPromotionBlocker);

            AppendStringList(builder, "profile.evidence", provenance.ProfileEvidenceRefs);
        }

        AppendValidationRequirements(builder, provenance.ValidationRequirements);
        AppendCapabilityAdmissions(builder, provenance.RequiredCapabilities);
        AppendInputContract(builder, details.InputContract);
        AppendRegionAccessContract(builder, details.RegionAccessContract);
        AppendField(builder, "output.template", output.FileNameTemplate);
        AppendInteger(builder, "output.allow-override", output.AllowOverride ? 1 : 0);
        AppendEnum(builder, "output.invalid-character-policy", output.InvalidCharacterPolicy);
        AppendEnum(builder, "output.renderer", output.RendererKind);
        AppendStringList(
            builder,
            "output.required-token",
            [.. output.TokenRequirements.Select(static requirement => requirement.TokenId)]);
        if (output.RuleId is not null)
        {
            AppendField(builder, "output.rule-id", output.RuleId);
            AppendEnum(builder, "output.artifact-type", output.OutputArtifactType);
            AppendList(builder, "output.token-requirement", output.TokenRequirements, AppendOutputTokenRequirement);
        }
        AppendPlan(builder, composition.Plan);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendPromotionBlocker(
        StringBuilder builder,
        string prefix,
        CompiledProfilePromotionBlocker blocker)
    {
        AppendField(builder, $"{prefix}.id", blocker.BlockerId);
        AppendEnum(builder, $"{prefix}.kind", blocker.Kind);
        AppendField(builder, $"{prefix}.reason", blocker.Reason);
        AppendStringList(builder, $"{prefix}.evidence", blocker.EvidenceRefs);
    }

    private static void AppendOutputTokenRequirement(
        StringBuilder builder,
        string prefix,
        CompiledOutputTokenRequirement requirement)
    {
        AppendField(builder, $"{prefix}.id", requirement.TokenId);
        AppendEnum(builder, $"{prefix}.source", requirement.SourceKind);
        AppendField(builder, $"{prefix}.metadata-binding", requirement.MetadataBindingId ?? string.Empty);
        AppendField(builder, $"{prefix}.metadata-space", requirement.MetadataSpaceId ?? string.Empty);
        AppendEnum(builder, $"{prefix}.missing", requirement.MissingPolicy);
        AppendField(builder, $"{prefix}.placeholder", requirement.Placeholder ?? string.Empty);
    }

    private static void AppendPlan(StringBuilder builder, CompositionPlan plan)
    {
        AppendField(builder, "plan.output-space", plan.OutputSpaceId);

        ImageInitialization[] initializations = [.. plan.Initializations.OrderBy(item => item.TargetSpaceId, StringComparer.Ordinal)];
        AppendList(builder, "plan.initializer", initializations, AppendInitialization);

        AddressSpace[] addressSpaces = [.. plan.AddressSpaces.OrderBy(item => item.AddressSpaceId, StringComparer.Ordinal)];
        AppendList(builder, "plan.address-space", addressSpaces, AppendAddressSpace);

        AppendList(builder, "plan.operation", plan.OrderedOperations, AppendOperation);
    }

    private static void AppendInitialization(
        StringBuilder builder,
        string prefix,
        ImageInitialization initialization)
    {
        AppendEnum(builder, $"{prefix}.kind", initialization.Kind);
        AppendField(builder, $"{prefix}.target-space", initialization.TargetSpaceId);
        AppendField(builder, $"{prefix}.reference-space", initialization.ReferenceSpaceId ?? string.Empty);
        AppendInteger(builder, $"{prefix}.capacity", initialization.Capacity);
        AppendInteger(builder, $"{prefix}.fill-byte", initialization.FillByte);
    }

    private static void AppendAddressSpace(
        StringBuilder builder,
        string prefix,
        AddressSpace addressSpace)
    {
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

    private static void AppendOperation(
        StringBuilder builder,
        string prefix,
        CompositionOperation operation)
    {
        AppendOperationExecutionSemantics(builder, operation, prefix);
        AppendField(builder, $"{prefix}.reason", operation.Reason);
        AppendField(builder, $"{prefix}.provenance.kind", operation.Provenance.Kind);
        AppendField(builder, $"{prefix}.provenance.source-id", operation.Provenance.SourceId ?? string.Empty);
        AppendField(builder, $"{prefix}.provenance.source-version", operation.Provenance.SourceVersion ?? string.Empty);
        AppendProcessor(builder, prefix, operation.ExternalProcessorInvocation);
    }

    private static void AppendOperationExecutionSemantics(
        StringBuilder builder,
        CompositionOperation operation,
        string prefix)
    {
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
        if (transform.AddendSource.Kind == ScalarTransformAddendSourceKind.RegionInstanceDelta)
        {
            AppendEnum(builder, $"{prefix}.addend-source-kind", transform.AddendSource.Kind);
            AppendField(
                builder,
                $"{prefix}.addend-source-instance",
                transform.AddendSource.SourceRegionInstanceId!);
            AppendField(
                builder,
                $"{prefix}.addend-target-instance",
                transform.AddendSource.TargetRegionInstanceId!);
        }

        AppendField(
            builder,
            $"{prefix}.expected-before",
            transform.ExpectedBefore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        AppendEnum(builder, $"{prefix}.overflow-policy", transform.OverflowPolicy);
    }

    private static void AppendRangeList(
        StringBuilder builder,
        string fieldPrefix,
        IReadOnlyList<ByteRange> ranges)
    {
        AppendList(builder, fieldPrefix, ranges, AppendRequiredRange);
    }

    private static void AppendIntegerList(
        StringBuilder builder,
        string fieldPrefix,
        IReadOnlyList<long> values)
    {
        AppendList(builder, fieldPrefix, values, AppendInteger);
    }

    private static void AppendRequiredRange(StringBuilder builder, string fieldName, ByteRange range)
    {
        AppendRange(builder, fieldName, range);
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
            AppendInteger(builder, $"{prefix}.kind", requirement switch
            {
                CompiledMetadataValueValidation => 0,
                CompiledPidSanityValidation => 1,
                CompiledMetadataEqualityValidation => 2,
                CompiledRejectMetadataBytePatternValidation => 3,
                CompiledViewByteAssertionValidation => 4,
                CompiledFirmwareConfigBackupVersionValidation => 5,
                CompiledFirmwareConfigBackupPlacementAuthorityValidation => 6,
                CompiledFirmwareConfigBackupExpectedAddressValidation => 7,
                CompiledUniformInputRangeValidation => 8,
                _ => throw new InvalidOperationException("Unknown compiled validation kind."),
            });
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
                case CompiledFirmwareConfigBackupVersionValidation firmwareConfig:
                    AppendField(builder, $"{prefix}.invalid-issue-code", firmwareConfig.InvalidIssueCode);
                    AppendInteger(builder, $"{prefix}.firmware-version", firmwareConfig.FirmwareVersion);
                    AppendInteger(builder, $"{prefix}.firmware-sub-version", firmwareConfig.FirmwareSubVersion);
                    break;
                case CompiledFirmwareConfigBackupPlacementAuthorityValidation authority:
                    AppendField(builder, $"{prefix}.inactive-mutation-issue-code", authority.InactiveMutationIssueCode);
                    AppendField(builder, $"{prefix}.reference-space-id", authority.ReferenceAddressSpaceId);
                    AppendRange(builder, $"{prefix}.authority-range", authority.AuthorityRange);
                    AppendInteger(builder, $"{prefix}.backup-length", authority.BackupLength);
                    break;
                case CompiledFirmwareConfigBackupExpectedAddressValidation expected:
                    AppendInteger(builder, $"{prefix}.expected-start", expected.ExpectedStart);
                    break;
                case CompiledUniformInputRangeValidation uniform:
                    AppendField(builder, $"{prefix}.address-space-id", uniform.AddressSpaceId);
                    for (int rangeIndex = 0; rangeIndex < uniform.Ranges.Count; rangeIndex++)
                    {
                        AppendRange(
                            builder,
                            $"{prefix}.range[{rangeIndex}]",
                            uniform.Ranges[rangeIndex]);
                    }

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

        if (contract.SelectionGroups.Count == 0)
        {
            return;
        }

        AppendInteger(builder, "input.selection-group.count", contract.SelectionGroups.Count);
        for (int index = 0; index < contract.SelectionGroups.Count; index++)
        {
            CompiledInputSelectionGroup group = contract.SelectionGroups[index];
            string prefix = FormattableString.Invariant($"input.selection-group.{index}");
            AppendField(builder, $"{prefix}.id", group.GroupId);
            AppendStringList(builder, $"{prefix}.member", group.MemberSlotIds);
            AppendStringList(builder, $"{prefix}.applicable", group.ApplicableMemberSlotIds);
            AppendStringList(builder, $"{prefix}.selected", group.SelectedSlotIds);
            AppendInteger(builder, $"{prefix}.minimum-selected", group.MinimumSelected);
            AppendInteger(builder, $"{prefix}.maximum-selected", group.MaximumSelected);
            AppendInteger(builder, $"{prefix}.not-applicable-reason.count", group.NotApplicableReasons.Count);
            int reasonIndex = 0;
            foreach ((string slotId, string reason) in group.NotApplicableReasons.OrderBy(
                         static pair => pair.Key,
                         StringComparer.Ordinal))
            {
                string reasonPrefix = FormattableString.Invariant(
                    $"{prefix}.not-applicable-reason.{reasonIndex++}");
                AppendField(builder, $"{reasonPrefix}.slot", slotId);
                AppendField(builder, $"{reasonPrefix}.message", reason);
            }
        }
    }

    private static void AppendInputLengthRequirement(
        StringBuilder builder,
        string prefix,
        CompiledInputLengthRequirement requirement)
    {
        AppendInteger(builder, $"{prefix}.kind", requirement switch
        {
            CompiledExactBytesInputLengthRequirement => 0,
            CompiledExactResolvedMapCapacityInputLengthRequirement => 1,
            CompiledBoundedInputLengthRequirement => 2,
            CompiledSourceViewCoverageInputLengthRequirement { MaximumBytes: not null } => 4,
            CompiledSourceViewCoverageInputLengthRequirement { RequiredEndExclusive: not null } => 5,
            CompiledSourceViewCoverageInputLengthRequirement => 6,
            _ => throw new InvalidOperationException("Unknown compiled input length requirement."),
        });
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
            case CompiledSourceViewCoverageInputLengthRequirement { RequiredEndExclusive: { } requiredEnd } sourceView:
                AppendInteger(builder, $"{prefix}.required-end-exclusive", requiredEnd);
                AppendIntegerList(builder, $"{prefix}.expected-outer-length", sourceView.ExpectedOuterLengths);
                AppendField(builder, $"{prefix}.short-input-issue-code", sourceView.ShortInputIssueCode!);
                AppendField(
                    builder,
                    $"{prefix}.unexpected-outer-length-issue-code",
                    sourceView.UnexpectedOuterLengthIssueCode!);
                break;
            case CompiledSourceViewCoverageInputLengthRequirement { MaximumBytes: { } maximumBytes }:
                AppendInteger(builder, $"{prefix}.maximum-bytes", maximumBytes);
                break;
            case CompiledSourceViewCoverageInputLengthRequirement sourceView:
                AppendIntegerList(
                    builder,
                    $"{prefix}.expected-outer-length",
                    sourceView.ExpectedOuterLengths);
                if (sourceView.UnexpectedOuterLengthIssueCode is { } unexpectedOuterLengthIssueCode)
                {
                    AppendField(
                        builder,
                        $"{prefix}.unexpected-outer-length-issue-code",
                        unexpectedOuterLengthIssueCode);
                }
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
        AppendInteger(builder, $"{prefix}.kind", normalization switch
        {
            CompiledNoInputNormalization => 0,
            CompiledPadShorterInputNormalization => 1,
            CompiledTruncateCtrlRamInputNormalization => 2,
            _ => throw new InvalidOperationException("Unknown compiled input normalization."),
        });
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
        IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> admissions)
    {
        AppendInteger(builder, "capability-admission.count", admissions.Count);
        for (int index = 0; index < admissions.Count; index++)
        {
            FirmwareMapFactBinding<FirmwareCapabilityFact> binding = admissions[index];
            FirmwareCapabilityFact capability = binding.Value;
            string prefix = FormattableString.Invariant($"capability-admission.{index}");
            AppendField(builder, $"{prefix}.required-capability-id", capability.CapabilityId);
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
            AppendInteger(builder, $"{valuePrefix}.kind", value switch
            {
                CompiledValidationIntegerLiteral => 0,
                CompiledValidationTextLiteral => 1,
                _ => throw new InvalidOperationException("Unknown compiled validation literal kind."),
            });
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

}
