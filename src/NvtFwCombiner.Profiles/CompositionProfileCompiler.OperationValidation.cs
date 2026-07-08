using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class CompositionProfileCompiler
{
    private static List<CompositionIssue> ValidateProfileOperations(CompositionProfileDefinition profile)
    {
        List<CompositionIssue> issues = [];
        if (profile.Regions.Count == 0)
        {
            return issues;
        }

        foreach (CompositionOperation operation in profile.Operations)
        {
            if (operation.Kind == CompositionOperationKind.RunExternalProcessor)
            {
                ValidateExternalProcessorOperation(profile, operation, issues);
                continue;
            }

            ProfileRegion? targetRegion = ResolveTargetRegionByRange(
                profile,
                operation.TargetSpaceId,
                operation.TargetRange,
                "profile.operation.target-region-unresolved",
                "profile.operation.target-region-ambiguous",
                operation.OperationId,
                issues);
            if (targetRegion is null)
            {
                continue;
            }

            RegionAccessRule? accessRule = FindAccessRule(profile, targetRegion.RegionId);
            ValidateProfileOperationRegionPolicy(profile, operation, targetRegion, accessRule, issues);
            if (OverlapsProtectedRegion(profile, targetRegion, operation.TargetSpaceId, operation.TargetRange))
            {
                issues.Add(new CompositionIssue(
                    "profile.operation.protected-overlap",
                    $"Operation '{operation.OperationId}' overlaps a protected or processor-owned profile region.",
                    operation.OperationId));
            }
        }

        return issues;
    }

    private static void ValidateExternalProcessorOperation(
        CompositionProfileDefinition profile,
        CompositionOperation operation,
        List<CompositionIssue> issues)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
        foreach (ByteRange writeRange in invocation.AllowedWriteRanges)
        {
            ProfileRegion? targetRegion = ResolveTargetRegionByRange(
                profile,
                operation.TargetSpaceId,
                writeRange,
                "profile.external-processor.target-region-unresolved",
                "profile.external-processor.target-region-ambiguous",
                operation.OperationId,
                issues);
            if (targetRegion is null)
            {
                continue;
            }

            if (!targetRegion.ProcessorDependencyIds.Contains(invocation.ProcessorId, StringComparer.Ordinal))
            {
                issues.Add(new CompositionIssue(
                    "profile.external-processor.region-not-owned",
                    $"Operation '{operation.OperationId}' writes region '{targetRegion.RegionId}' without matching processor ownership.",
                    operation.OperationId));
            }

            if (writeRange.Start % targetRegion.Alignment != 0 ||
                writeRange.Length % targetRegion.Alignment != 0)
            {
                issues.Add(new CompositionIssue(
                    "profile.external-processor.region-alignment",
                    $"Operation '{operation.OperationId}' write range does not satisfy region '{targetRegion.RegionId}' alignment.",
                    operation.OperationId));
            }
        }
    }

    private static void ValidateProfileOperationRegionPolicy(
        CompositionProfileDefinition profile,
        CompositionOperation operation,
        ProfileRegion targetRegion,
        RegionAccessRule? accessRule,
        List<CompositionIssue> issues)
    {
        if (accessRule is null ||
            accessRule.Access is RegionAccessKind.Hidden or RegionAccessKind.ReadOnly ||
            targetRegion.WritePolicy == RegionWritePolicy.Forbidden)
        {
            issues.Add(new CompositionIssue(
                "profile.operation.region-not-enabled",
                $"Operation '{operation.OperationId}' targets region '{targetRegion.RegionId}' without write access.",
                operation.OperationId));
        }

        if (targetRegion.ProcessorDependencyIds.Count > 0 &&
            !AllowsCtrlRamReplaceBeforeProcessor(profile, operation, targetRegion))
        {
            issues.Add(new CompositionIssue(
                "profile.operation.processor-dependency",
                $"Operation '{operation.OperationId}' targets region '{targetRegion.RegionId}' with processor dependencies.",
                operation.OperationId));
        }

        if ((targetRegion.Atomicity == RegionAtomicity.Whole ||
                targetRegion.WritePolicy == RegionWritePolicy.WholeOnly ||
                accessRule?.Access == RegionAccessKind.Whole) &&
            operation.TargetRange != targetRegion.Range)
        {
            issues.Add(new CompositionIssue(
                "profile.operation.atomicity",
                $"Operation '{operation.OperationId}' must write whole region '{targetRegion.RegionId}'.",
                operation.OperationId));
        }

        if (operation.TargetRange.Start % targetRegion.Alignment != 0 ||
            operation.TargetRange.Length % targetRegion.Alignment != 0)
        {
            issues.Add(new CompositionIssue(
                "profile.operation.region-alignment",
                $"Operation '{operation.OperationId}' target range does not satisfy region '{targetRegion.RegionId}' alignment.",
                operation.OperationId));
        }
    }

    private static bool AllowsCtrlRamReplaceBeforeProcessor(
        CompositionProfileDefinition profile,
        CompositionOperation operation,
        ProfileRegion targetRegion)
    {
        return IsCtrlRamReplaceProfile(profile) &&
            operation.Kind == CompositionOperationKind.ReplaceRange &&
            operation.TargetRange == targetRegion.Range &&
            targetRegion.ClassificationTags.Contains(CtrlRamClassificationTag, StringComparer.Ordinal);
    }

}
