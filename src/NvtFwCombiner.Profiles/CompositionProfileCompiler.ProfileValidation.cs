using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class CompositionProfileCompiler
{
    private static List<CompositionIssue> ValidateProfileHeader(
        CompositionProfileDefinition profile,
        IReadOnlyList<ExplicitMapping> explicitMappings,
        IReadOnlyList<AddressSpace> requestAddressSpaces)
    {
        List<CompositionIssue> issues = [];
        ValidateInputPaddingPolicy(profile, requestAddressSpaces, issues);
        ValidateIcNumberPolicy(profile, issues);

        AddDuplicateIssues(
            profile.Regions,
            region => region.RegionId,
            "profile.region.duplicate",
            "Profile region id is declared more than once.",
            issues);
        AddDuplicateIssues(
            profile.RegionAccessRules,
            rule => rule.RegionId,
            "profile.region-access.duplicate",
            "Profile region access rule is declared more than once.",
            issues);

        if (!ExperienceCatalog.TryFind(profile.ExperienceId, out ExperienceDescriptor? experience) ||
            experience is null)
        {
            issues.Add(new CompositionIssue(
                "profile.experience.unknown",
                $"Experience '{profile.ExperienceId}' is not in the approved catalog."));
            return issues;
        }

        if (experience.CompositionKind != profile.CompositionKind)
        {
            issues.Add(new CompositionIssue(
                "profile.composition-kind.mismatch",
                "Profile composition kind must match the approved experience catalog."));
        }

        if (experience.RequiredInitialization != profile.Initialization.Kind)
        {
            issues.Add(new CompositionIssue(
                "profile.initialization-kind.mismatch",
                "Profile initializer kind must match merge versus replace semantics."));
        }

        if (explicitMappings.Count > 0 && experience.LayoutPolicy != LayoutPolicy.UserDefined)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.not-allowed",
                "Explicit mappings are allowed only for general user-defined experiences."));
        }

        if (requestAddressSpaces.Count > 0 && experience.InputPolicy != InputPolicy.Extensible)
        {
            issues.Add(new CompositionIssue(
                "profile.request-address-space.not-allowed",
                "Runtime address spaces are allowed only for extensible-input experiences."));
        }

        return issues;
    }

    private static void ValidateIcNumberPolicy(
        CompositionProfileDefinition profile,
        List<CompositionIssue> issues)
    {
        if (profile.IcNumberInputMode is { } inputMode && !Enum.IsDefined(inputMode))
        {
            issues.Add(new CompositionIssue(
                "profile.ic-number-mode.unknown",
                "Profile declares an unknown IC-number input mode."));
            return;
        }

        if (profile.CompositionKind == CompositionKind.Merge && profile.IcNumberInputMode is not null)
        {
            issues.Add(new CompositionIssue(
                "profile.ic-number-mode.not-applicable",
                "Merge profiles cannot declare an IC-number input mode."));
        }
        else if (profile.CompositionKind == CompositionKind.Replace && profile.IcNumberInputMode is null)
        {
            issues.Add(new CompositionIssue(
                "profile.ic-number-mode.required",
                "Replace profiles require an IC-number input mode."));
        }
    }

    private static void ValidateInputPaddingPolicy(
        CompositionProfileDefinition profile,
        IReadOnlyList<AddressSpace> requestAddressSpaces,
        List<CompositionIssue> issues)
    {
        foreach (AddressSpace addressSpace in requestAddressSpaces.Where(space => space.InputPaddingByte is not null))
        {
            issues.Add(new CompositionIssue(
                "profile.input-padding.request-not-allowed",
                $"Runtime address space '{addressSpace.AddressSpaceId}' cannot declare input padding.",
                addressSpace.AddressSpaceId));
        }

        foreach (AddressSpace addressSpace in requestAddressSpaces.Where(space =>
                     space.InputOversizePolicy != InputOversizePolicy.Reject))
        {
            issues.Add(new CompositionIssue(
                "profile.input-truncation.request-not-allowed",
                $"Runtime address space '{addressSpace.AddressSpaceId}' cannot declare input truncation.",
                addressSpace.AddressSpaceId));
        }

        foreach (AddressSpace addressSpace in requestAddressSpaces.Where(space =>
                     space.AllowedInputLengths.Count > 0))
        {
            issues.Add(new CompositionIssue(
                "profile.input-lengths.request-not-allowed",
                $"Runtime address space '{addressSpace.AddressSpaceId}' cannot declare allowed input lengths.",
                addressSpace.AddressSpaceId));
        }

        foreach (AddressSpace addressSpace in requestAddressSpaces.Where(space =>
                     space.ExpectedInputLengths.Count > 0))
        {
            issues.Add(new CompositionIssue(
                "profile.expected-input-lengths.request-not-allowed",
                $"Runtime address space '{addressSpace.AddressSpaceId}' cannot declare expected input lengths.",
                addressSpace.AddressSpaceId));
        }

        ValidateInputOversizePolicy(profile, issues);
        ValidateExpectedInputLengthPolicy(profile, issues);

        if (!ForbidsInputPadding(profile))
        {
            return;
        }

        foreach (AddressSpace addressSpace in profile.AddressSpaces.Where(space => space.InputPaddingByte is not null))
        {
            issues.Add(new CompositionIssue(
                "profile.input-padding.processor-conflict",
                $"Address space '{addressSpace.AddressSpaceId}' declares input padding in a profile with processor-dependent integrity.",
                addressSpace.AddressSpaceId));
        }
    }

    private static void ValidateInputOversizePolicy(
        CompositionProfileDefinition profile,
        List<CompositionIssue> issues)
    {
        foreach (AddressSpace addressSpace in profile.AddressSpaces.Where(space =>
                     space.InputOversizePolicy != InputOversizePolicy.Reject))
        {
            if (addressSpace.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning)
            {
                if (!IsCtrlRamReplaceProfile(profile))
                {
                    issues.Add(new CompositionIssue(
                        "profile.input-truncation.not-allowed",
                        $"Address space '{addressSpace.AddressSpaceId}' declares input truncation outside a CtrlRAM replace profile.",
                        addressSpace.AddressSpaceId));
                    continue;
                }

                ValidateTruncatingAddressSpaceTargetsCtrlRam(profile, addressSpace, issues);
                continue;
            }

            if (addressSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange &&
                !IsStandardMergeDpExtraction(profile, addressSpace))
            {
                issues.Add(new CompositionIssue(
                    "profile.input-extraction.not-allowed",
                    $"Address space '{addressSpace.AddressSpaceId}' may extract its declared source range only for a Standard Merge DP input.",
                    addressSpace.AddressSpaceId));
            }
        }
    }

    private static void ValidateExpectedInputLengthPolicy(
        CompositionProfileDefinition profile,
        List<CompositionIssue> issues)
    {
        foreach (AddressSpace addressSpace in profile.AddressSpaces.Where(space => space.ExpectedInputLengths.Count > 0))
        {
            if (addressSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange &&
                IsStandardMergeDpExtraction(profile, addressSpace))
            {
                continue;
            }

            issues.Add(new CompositionIssue(
                "profile.expected-input-lengths.not-allowed",
                $"Address space '{addressSpace.AddressSpaceId}' may declare expected input lengths only with Standard Merge DP range extraction.",
                addressSpace.AddressSpaceId));
        }
    }

    private static bool IsStandardMergeDpExtraction(
        CompositionProfileDefinition profile,
        AddressSpace addressSpace)
    {
        if (profile.CompositionKind != CompositionKind.Merge ||
            !string.Equals(profile.ExperienceId, StandardMergeExperienceId, StringComparison.Ordinal) ||
            !string.Equals(addressSpace.AddressSpaceId, CompositionAddressSpaceIds.DpInput, StringComparison.Ordinal))
        {
            return false;
        }

        List<CompositionOperation> sourceOperations = [
            .. profile.Operations.Where(operation =>
                string.Equals(operation.SourceSpaceId, addressSpace.AddressSpaceId, StringComparison.Ordinal)),
        ];
        return sourceOperations.Count > 0 && sourceOperations.All(operation =>
            operation.Kind == CompositionOperationKind.CopyRange &&
            operation.SourceRange is { } sourceRange &&
            sourceRange.EndExclusive == addressSpace.Length);
    }

    private static void ValidateTruncatingAddressSpaceTargetsCtrlRam(
        CompositionProfileDefinition profile,
        AddressSpace addressSpace,
        List<CompositionIssue> issues)
    {
        List<(string OperationId, string TargetSpaceId, ByteRange TargetRange)> sourceTargets = [];
        foreach (CompositionOperation operation in profile.Operations)
        {
            if (string.Equals(operation.SourceSpaceId, addressSpace.AddressSpaceId, StringComparison.Ordinal))
            {
                sourceTargets.Add((operation.OperationId, operation.TargetSpaceId, operation.TargetRange));
            }

            if (operation.ExternalProcessorInvocation is not { } invocation)
            {
                continue;
            }

            foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings.Where(binding =>
                         string.Equals(binding.SourceSpaceId, addressSpace.AddressSpaceId, StringComparison.Ordinal)))
            {
                sourceTargets.Add((operation.OperationId, operation.TargetSpaceId, binding.FirmwareRange));
            }
        }

        if (sourceTargets.Count == 0)
        {
            issues.Add(new CompositionIssue(
                "profile.input-truncation.ctrlram-region-required",
                $"Address space '{addressSpace.AddressSpaceId}' declares input truncation but is not used by a CtrlRAM replacement or staged postbuild source.",
                addressSpace.AddressSpaceId));
            return;
        }

        foreach ((string operationId, string targetSpaceId, ByteRange targetRange) in sourceTargets)
        {
            ProfileRegion? targetRegion = ResolveTargetRegionByRange(
                profile,
                targetSpaceId,
                targetRange,
                "profile.input-truncation.target-region-unresolved",
                "profile.input-truncation.target-region-ambiguous",
                addressSpace.AddressSpaceId,
                issues);
            if (targetRegion is null)
            {
                continue;
            }

            if (targetRegion.ClassificationTags.Contains(CtrlRamClassificationTag, StringComparer.Ordinal))
            {
                continue;
            }

            issues.Add(new CompositionIssue(
                "profile.input-truncation.ctrlram-region-required",
                $"Address space '{addressSpace.AddressSpaceId}' declares input truncation but operation '{operationId}' targets non-CtrlRAM region '{targetRegion.RegionId}'.",
                addressSpace.AddressSpaceId));
        }
    }

    private static bool ForbidsInputPadding(CompositionProfileDefinition profile)
    {
        return IsCtrlRamReplaceProfile(profile) ||
            profile.Operations.Any(operation => operation.Kind == CompositionOperationKind.RunExternalProcessor) ||
            profile.Regions.Any(region => region.ProcessorDependencyIds.Count > 0);
    }

    private static bool IsCtrlRamReplaceProfile(CompositionProfileDefinition profile)
    {
        return profile.CompositionKind == CompositionKind.Replace &&
            string.Equals(profile.ExperienceId, CtrlRamReplaceExperienceId, StringComparison.Ordinal);
    }

    private static bool IsGeneralReplaceProfile(CompositionProfileDefinition profile)
    {
        return profile.CompositionKind == CompositionKind.Replace &&
            string.Equals(profile.ExperienceId, GeneralReplaceExperienceId, StringComparison.Ordinal);
    }

    private static void AddDuplicateIssues<T>(
        IEnumerable<T> items,
        Func<T, string> getId,
        string issueCode,
        string message,
        List<CompositionIssue> issues)
    {
        foreach (string id in items.Select(getId).GroupBy(id => id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            issues.Add(new CompositionIssue(issueCode, $"{message} Duplicate id: '{id}'.", id));
        }
    }

}
