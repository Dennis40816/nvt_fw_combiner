namespace NvtFwCombiner.Domain.Composition;

/// <summary>Domain-owned canonical validation definitions before range-dependent lowering.</summary>
internal static class CanonicalValidationDefinitionRules
{
    internal static T RequireProfileDefinition<T>(T definition)
        where T : CompiledValidationRequirement
    {
        ArgumentNullException.ThrowIfNull(definition);
        _ = CanonicalPolicyValueRules.RequireCanonicalId(definition.RuleId, nameof(definition.RuleId));
        _ = CanonicalPolicyValueRules.RequireIssueCode(definition.IssueCode, nameof(definition.IssueCode));
        switch (definition)
        {
            case CompiledMetadataValueValidation metadata:
                _ = RequireProfileField(metadata.Field);
                break;
            case CompiledPidSanityValidation pid:
                _ = RequireProfileField(pid.Field);
                break;
            case CompiledMetadataEqualityValidation equality:
                _ = RequireProfileField(equality.Left);
                _ = RequireProfileField(equality.Right);
                break;
            case CompiledRejectMetadataBytePatternValidation rejected:
                _ = RequireProfileField(rejected.Field);
                break;
            case CompiledViewByteAssertionValidation assertion:
                _ = CanonicalPolicyValueRules.RequireCanonicalId(assertion.ViewId, nameof(assertion.ViewId));
                ValidatePartialMask(assertion);
                break;
            default:
                throw new ArgumentException("Validation is not a profile-declared canonical definition.", nameof(definition));
        }

        return definition;
    }

    internal static CompiledValidationFieldReference RequireProfileField(
        CompiledValidationFieldReference field)
    {
        ArgumentNullException.ThrowIfNull(field);
        _ = CanonicalPolicyValueRules.RequireCanonicalId(field.BindingId, nameof(field.BindingId));
        _ = CanonicalPolicyValueRules.RequireCanonicalId(field.FieldId, nameof(field.FieldId));
        return field;
    }

    private static void ValidatePartialMask(CompiledViewByteAssertionValidation assertion)
    {
        if (assertion.Mask is not { } mask)
        {
            return;
        }

        if (mask.Bytes.IndexOfAnyExcept((byte)0) < 0)
        {
            throw new ArgumentException("Assertion mask must contain a set bit.", nameof(assertion));
        }

        if (mask.Bytes.IndexOfAnyExcept(byte.MaxValue) < 0)
        {
            throw new ArgumentException("An all-FF assertion mask must use exact-match form.", nameof(assertion));
        }

        for (int index = 0; index < mask.Length; index++)
        {
            if ((assertion.Expected.Bytes[index] & ~mask.Bytes[index]) != 0)
            {
                throw new ArgumentException(
                    "Assertion expected bits outside the mask must be zero.",
                    nameof(assertion));
            }
        }
    }
}

/// <summary>Warns when one unresolved source view contains only one repeated byte.</summary>
internal sealed record SourceViewNonUniformValidationDefinition : ValidationRequirementDefinition
{
    internal SourceViewNonUniformValidationDefinition(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        string viewId)
        : base(ruleId, stage, severity, issueCode)
    {
        if (stage != CompiledValidationStage.InputLoad ||
            severity != CompiledValidationSeverity.Warning)
        {
            throw new ArgumentException(
                "Non-uniform region validation is restricted to warning-only input-load checks.");
        }

        _ = CanonicalPolicyValueRules.RequireCanonicalId(ruleId, nameof(ruleId));
        _ = CanonicalPolicyValueRules.RequireIssueCode(issueCode, nameof(issueCode));
        ViewId = CanonicalPolicyValueRules.RequireCanonicalId(viewId, nameof(viewId));
    }

    internal string ViewId { get; }
}
