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
        if (!IsCtrlRamReplaceProfile(profile) && !IsGeneralReplaceProfile(profile))
        {
            issues.Add(new CompositionIssue(
                "profile.legacy-compiler.workflow-retired",
                "Legacy profile compilation is retained only for CtrlRAM Replace and General Replace compatibility."));
            return issues;
        }

        ValidateInputPaddingPolicy(profile, requestAddressSpaces, issues);
        ValidateIcNumberPolicy(profile, issues);
        ValidateCtrlRamReplaceProcessorShape(profile, issues);

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

        if (profile.IcNumberInputMode is null)
        {
            issues.Add(new CompositionIssue(
                "profile.ic-number-mode.required",
                "Replace profiles require an IC-number input mode."));
        }
    }

    private static void ValidateCtrlRamReplaceProcessorShape(
        CompositionProfileDefinition profile,
        List<CompositionIssue> issues)
    {
        if (!IsCtrlRamReplaceProfile(profile))
        {
            return;
        }

        CompositionOperation[] processors = [.. profile.Operations.Where(operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor)];
        if (processors.Length != 1 ||
            processors[0].ExternalProcessorInvocation!.StagedSourceBindings.Count == 0)
        {
            issues.Add(new CompositionIssue(
                "profile.ctrlram-replace.staged-processor-required",
                "CtrlRAM Replace compatibility profiles require exactly one external postbuild processor with staged source bindings."));
            return;
        }

        ExternalProcessorInvocation invocation = processors[0].ExternalProcessorInvocation!;
        foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
        {
            ProfileRegion? targetRegion = ResolveTargetRegionByRange(
                profile,
                processors[0].TargetSpaceId,
                binding.FirmwareRange,
                "profile.ctrlram-replace.staged-source-region-unresolved",
                "profile.ctrlram-replace.staged-source-region-ambiguous",
                binding.SourceSpaceId,
                issues);
            if (targetRegion is not null &&
                (!targetRegion.ClassificationTags.Contains(CtrlRamClassificationTag, StringComparer.Ordinal) ||
                 !targetRegion.ProcessorDependencyIds.Contains(invocation.ProcessorId, StringComparer.Ordinal) ||
                 !invocation.AllowedWriteRanges.Any(range => range.Contains(binding.FirmwareRange))))
            {
                issues.Add(new CompositionIssue(
                    "profile.ctrlram-replace.staged-source-region-required",
                    $"Staged source '{binding.SourceSpaceId}' must target a processor-owned CtrlRAM region inside the postbuild write authority.",
                    binding.SourceSpaceId));
            }
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
        foreach (AddressSpace addressSpace in profile.AddressSpaces.Where(space =>
                     space.AllowedInputLengths.Count + space.ExpectedInputLengths.Count > 0))
        {
            issues.Add(new CompositionIssue(
                "profile.input-lengths.not-allowed",
                $"Address space '{addressSpace.AddressSpaceId}' declares retired profile-owned input-length policy.",
                addressSpace.AddressSpaceId));
        }

        foreach (AddressSpace addressSpace in profile.AddressSpaces.Where(space => space.InputPaddingByte is not null))
        {
            issues.Add(new CompositionIssue(
                "profile.input-padding.processor-conflict",
                $"Address space '{addressSpace.AddressSpaceId}' declares retired profile-owned input padding.",
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
            if (addressSpace.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning &&
                IsCtrlRamReplaceProfile(profile))
            {
                ValidateTruncatingAddressSpaceTargetsCtrlRam(profile, addressSpace, issues);
                continue;
            }

            issues.Add(new CompositionIssue(
                "profile.input-truncation.not-allowed",
                $"Address space '{addressSpace.AddressSpaceId}' declares input truncation outside the CtrlRAM Replace compatibility workflow.",
                addressSpace.AddressSpaceId));
        }
    }

    private static void ValidateTruncatingAddressSpaceTargetsCtrlRam(
        CompositionProfileDefinition profile,
        AddressSpace addressSpace,
        List<CompositionIssue> issues)
    {
        bool isStaged = profile.Operations
            .Where(operation => operation.ExternalProcessorInvocation is not null)
            .SelectMany(operation => operation.ExternalProcessorInvocation!.StagedSourceBindings)
            .Any(binding => StringComparer.Ordinal.Equals(binding.SourceSpaceId, addressSpace.AddressSpaceId));
        if (!isStaged)
        {
            issues.Add(new CompositionIssue(
                "profile.input-truncation.ctrlram-region-required",
                $"Address space '{addressSpace.AddressSpaceId}' declares input truncation but is not used by a staged CtrlRAM postbuild source.",
                addressSpace.AddressSpaceId));
        }
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
