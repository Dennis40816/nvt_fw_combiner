using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
{
    private static CompositionProfileDefinition CreateProfile(
        CompositionKind compositionKind,
        string experienceId,
        ImageInitialization initialization,
        IReadOnlyList<AddressSpace>? addressSpaces = null,
        IReadOnlyList<CompositionOperation>? operations = null,
        IReadOnlyList<ProfileRegion>? regions = null,
        IReadOnlyList<RegionAccessRule>? accessRules = null,
        IcNumberInputMode? icNumberInputMode = null)
    {
        AddressSpace[] defaultAddressSpaces =
        [
            new("source", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        return new CompositionProfileDefinition(
            "demo-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "standard-merge",
            compositionKind,
            experienceId,
            "demo-output.bin",
            initialization,
            addressSpaces ?? defaultAddressSpaces,
            operations ?? [],
            regions ??
            [
                new ProfileRegion(
                    "header",
                    "output-image",
                    new ByteRange(0, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden),
                new ProfileRegion(
                    "payload",
                    "output-image",
                    new ByteRange(1, 3),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit),
            ],
            accessRules ??
            [
                new RegionAccessRule("header", RegionAccessKind.Hidden),
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange),
            ],
            icNumberInputMode ?? (compositionKind == CompositionKind.Replace
                ? IcNumberInputMode.SingleSelector
                : null));
    }

    private static ExplicitMapping CreateMapping(
        ExplicitMappingOperationKind operationKind,
        string sourceBindingId = "source",
        ByteRange? sourceRange = null,
        ByteRange? targetRange = null,
        int alignment = 1,
        string? targetRegionId = null)
    {
        ByteRange resolvedTargetRange = targetRange ?? new ByteRange(1, 2);
        return new ExplicitMapping(
            "mapping-1",
            10,
            operationKind,
            sourceBindingId,
            sourceRange ?? new ByteRange(0, resolvedTargetRange.Length),
            "output-image",
            resolvedTargetRange,
            OverlapPolicy.Reject,
            alignment,
            "compile explicit mapping",
            targetRegionId);
    }

    private static CompositionOperation CreateExternalProcessorOperation(string processorId, int sequence = 10)
    {
        return CompositionOperation.RunExternalProcessor(
            "run-crc",
            sequence,
            "output-image",
            new ByteRange(0, 4),
            new ExternalProcessorInvocation(
                processorId,
                "tool-v1",
                [new ByteRange(0, 4)],
                [new ByteRange(3, 1)]),
            OverlapPolicy.Reject,
            "run synthetic crc processor");
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
