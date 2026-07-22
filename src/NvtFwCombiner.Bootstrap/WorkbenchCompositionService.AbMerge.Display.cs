using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string AbDpRole = "dp-ab";
    private const string AbTpARole = "tp-a";
    private const string AbTpBRole = "tp-b";
    private const string UnknownAbVersion = "Unknown";

    /// <summary>Gets required AB input cards directly from the compiled profile contract.</summary>
    public static IReadOnlyList<WorkbenchAbMergeInputSlot> GetAbMergeInputSlots(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return !AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
                out CompiledComposition? composition,
                out _) || composition.V2Details is null
            ? []
            : CreateAbMergeInputSlots(composition);
    }

    private static IReadOnlyList<WorkbenchAbMergeInputSlot> CreateAbMergeInputSlots(
        CompiledComposition composition)
    {
        V2CompiledCompositionDetails details = composition.V2Details ??
            throw new InvalidOperationException("Supported AB profiles require a compiled V2 input contract.");
        return
        [
            .. composition.Plan.RequiredInputAddressSpaceIds.Select(addressSpaceId =>
            {
                CompiledInputSpaceBinding binding = details.InputContract.SpaceBindings.Single(candidate =>
                    StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
                CompiledInputSlotRequirement slot = details.InputContract.Slots.Single(candidate =>
                    StringComparer.Ordinal.Equals(candidate.SlotId, binding.SlotId));
                CompiledDeclaredPrefixWithWarningInputLengthRequirement length =
                    slot.LengthRequirement as CompiledDeclaredPrefixWithWarningInputLengthRequirement ??
                    throw new InvalidOperationException(
                        $"Supported AB input '{addressSpaceId}' must use declared-prefix authority.");
                return new WorkbenchAbMergeInputSlot(
                    slot.SlotId,
                    binding.AddressSpaceId,
                    MapAbInputRole(slot.Role),
                    length.RequiredEndExclusive,
                    length.ExpectedOuterLengths);
            }),
        ];
    }

    /// <summary>Reads one selected AB file once and projects compiled health plus informational versions.</summary>
    public static WorkbenchAbMergeInputInspection InspectAbMergeInput(
        string icId,
        string addressSpaceId,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return InspectAbMergeInput(icId, addressSpaceId, TryReadFirmwareImage(path));
    }

    internal static WorkbenchAbMergeInputInspection InspectAbMergeInput(
        string icId,
        string addressSpaceId,
        byte[]? image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        if (!AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> compileIssues) ||
            composition.V2Details is not { } details)
        {
            throw new InvalidOperationException(FormatIssues(compileIssues));
        }

        WorkbenchAbMergeInputSlot slot = CreateAbMergeInputSlots(composition).Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
        if (image is null)
        {
            return new WorkbenchAbMergeInputInspection(
                addressSpaceId,
                ActualLength: null,
                slot.RequiredEndExclusive,
                slot.ExpectedOuterLengths,
                IgnoredTrailingRange: null,
                [new WorkbenchInputInspectionIssue(
                    WorkbenchInputInspectionSeverity.Blocking,
                    WorkbenchIssueCodes.InputArtifactReadFailed,
                    BlocksBuild: true,
                    WorkbenchInputInspectionNextAction.SelectReadableInput)],
                UnknownVersions(slot.Role));
        }

        CompiledInputArtifactInspectionResult inspected =
            CompiledInputArtifactInspectionService.InspectDeclaredPrefix(
                details.InputContract,
                addressSpaceId,
                image);
        List<WorkbenchAbVersionValue> versions = ReadAbVersions(icId, composition, slot.Role, image, inspected);
        List<WorkbenchInputInspectionIssue> issues =
        [
            new(
                MapSeverity(inspected.Severity),
                inspected.IssueCode,
                inspected.BlocksBuild,
                MapNextAction(inspected.NextAction)),
        ];
        if (!inspected.BlocksBuild && versions.Any(static value => value.IsUnknown))
        {
            issues.Add(new WorkbenchInputInspectionIssue(
                WorkbenchInputInspectionSeverity.Warning,
                WorkbenchIssueCodes.AbInputVersionUnknown,
                BlocksBuild: false,
                WorkbenchInputInspectionNextAction.ReviewUnknownVersion));
        }

        return new WorkbenchAbMergeInputInspection(
            addressSpaceId,
            inspected.ActualLength,
            inspected.RequiredEndExclusive,
            inspected.ExpectedOuterLengths,
            inspected.IgnoredTrailingRange,
            Array.AsReadOnly(issues.ToArray()),
            Array.AsReadOnly(versions.ToArray()));
    }

    /// <summary>Gets AB final output ownership directly from the compiled plan.</summary>
    public static WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(string icId)
    {
        if (!AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            string detail = issues.Count == 0 ? $"AB Merge is not available for {icId}." : FormatIssues(issues);
            return CreateMessageDisplay(
                detail,
                ("Profile", "No output", "Blocked", "No output", detail),
                ("No range", "AB Merge unavailable", detail, "#CBD5E1"));
        }

        ImageInitialization initialization = composition.Plan.OutputInitialization;
        CoverageSegment[] coverage =
        [
            new(
                new ByteRange(0, initialization.Capacity),
                $"Blank 0x{initialization.FillByte:X2}",
                "No AB input writes this output range.",
                "#CBD5E1",
                false,
                WorkbenchMemoryCoverageRole.Standard),
        ];
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(initialization.Capacity),
                "No output",
                "Initialize",
                $"Blank output 0x{initialization.FillByte:X2}",
                "Initialize the compiled AB output before applying the ordered profile operations."),
        ];
        foreach (CompositionOperation operation in composition.Plan.OrderedOperations)
        {
            string targetSpace = AddressSpaceLabel(operation.TargetSpaceId);
            string sourceSpace = operation.SourceSpaceId is null
                ? operation.Kind.ToString()
                : AddressSpaceLabel(operation.SourceSpaceId);
            rows.Add(new WorkbenchMemoryMapRow(
                $"{targetSpace} {FormatDisplayRange(operation.TargetRange)}",
                targetSpace,
                ActionLabel(operation.Kind),
                sourceSpace,
                $"Sequence {operation.Sequence}: {operation.Reason}"));
            if (!StringComparer.Ordinal.Equals(operation.TargetSpaceId, CompositionAddressSpaceIds.OutputImage))
            {
                continue;
            }

            coverage = ApplyCoverageWrite(
                coverage,
                new CoverageSegment(
                    operation.TargetRange,
                    sourceSpace,
                    operation.Reason,
                    CoverageFill(sourceSpace),
                    false,
                    WorkbenchMemoryCoverageRole.Standard));
        }

        return new WorkbenchMemoryDisplay(
            FormatFullRange(initialization.Capacity),
            rows,
            ToWorkbenchCoverageSegments(coverage, initialization.Capacity));
    }

    private static List<WorkbenchAbVersionValue> ReadAbVersions(
        string icId,
        CompiledComposition composition,
        WorkbenchAbMergeInputRole role,
        byte[] image,
        CompiledInputArtifactInspectionResult inspected)
    {
        if (inspected.AcceptedSnapshotRange is not { } accepted)
        {
            return [.. UnknownVersions(role)];
        }

        ReadOnlySpan<byte> snapshot = image.AsSpan(checked((int)accepted.Start), checked((int)accepted.Length));
        return role switch
        {
            WorkbenchAbMergeInputRole.DpAb => ReadAbDpVersions(icId, composition, snapshot),
            WorkbenchAbMergeInputRole.TpA => [ReadAbTpVersion(WorkbenchAbVersionKind.TpA, snapshot)],
            WorkbenchAbMergeInputRole.TpB => [ReadAbTpVersion(WorkbenchAbVersionKind.TpB, snapshot)],
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    private static List<WorkbenchAbVersionValue> ReadAbDpVersions(
        string icId,
        CompiledComposition composition,
        ReadOnlySpan<byte> snapshot)
    {
        CompositionOperation tpA = FindAbOutputCopy(composition, CompositionAddressSpaceIds.TpAInput);
        CompositionOperation tpB = FindAbOutputCopy(composition, CompositionAddressSpaceIds.TpBWork);
        long bankLength = checked(tpB.TargetRange.Start - tpA.TargetRange.Start);
        if (bankLength <= 0 || bankLength > int.MaxValue || checked(bankLength * 2) != snapshot.Length)
        {
            throw new InvalidOperationException("Compiled AB bank geometry is not a symmetric two-bank layout.");
        }

        int length = checked((int)bankLength);
        return
        [
            ReadAbDpVersion(icId, WorkbenchAbVersionKind.Dp1, snapshot[..length]),
            ReadAbDpVersion(icId, WorkbenchAbVersionKind.Dp2, snapshot.Slice(length, length)),
        ];
    }

    private static CompositionOperation FindAbOutputCopy(
        CompiledComposition composition,
        string sourceSpaceId)
    {
        return composition.Plan.OrderedOperations.Single(operation =>
            operation.Kind == CompositionOperationKind.CopyRange &&
            StringComparer.Ordinal.Equals(operation.SourceSpaceId, sourceSpaceId) &&
            StringComparer.Ordinal.Equals(operation.TargetSpaceId, CompositionAddressSpaceIds.OutputImage));
    }

    private static WorkbenchAbVersionValue ReadAbDpVersion(
        string icId,
        WorkbenchAbVersionKind kind,
        ReadOnlySpan<byte> bank)
    {
        return GenFlashVersionCatalog.TryReadCmiDpCode(icId, bank, out CmiDpCodeMetadata metadata)
            ? new WorkbenchAbVersionValue(
                kind,
                FormattableString.Invariant($"D{metadata.MajorVersionByte:X2}{metadata.MinorVersionNibble:X2}"),
                metadata.JiraBadge,
                IsUnknown: false)
            : new WorkbenchAbVersionValue(kind, UnknownAbVersion, JiraBadge: null, IsUnknown: true);
    }

    private static WorkbenchAbVersionValue ReadAbTpVersion(
        WorkbenchAbVersionKind kind,
        ReadOnlySpan<byte> snapshot)
    {
        return FirmwareConfigMetadataReader.TryReadBackup(snapshot, out FirmwareConfigMetadata metadata) &&
            metadata.IsFirmwareVersionBarValid
                ? new WorkbenchAbVersionValue(
                    kind,
                    FormattableString.Invariant($"T{metadata.FirmwareVersion:X2}-{metadata.FirmwareSubVersion:X2}"),
                    JiraBadge: null,
                    IsUnknown: false)
                : new WorkbenchAbVersionValue(kind, UnknownAbVersion, JiraBadge: null, IsUnknown: true);
    }

    private static IReadOnlyList<WorkbenchAbVersionValue> UnknownVersions(WorkbenchAbMergeInputRole role)
    {
        return role switch
        {
            WorkbenchAbMergeInputRole.DpAb =>
            [
                new WorkbenchAbVersionValue(WorkbenchAbVersionKind.Dp1, UnknownAbVersion, null, true),
                new WorkbenchAbVersionValue(WorkbenchAbVersionKind.Dp2, UnknownAbVersion, null, true),
            ],
            WorkbenchAbMergeInputRole.TpA =>
                [new WorkbenchAbVersionValue(WorkbenchAbVersionKind.TpA, UnknownAbVersion, null, true)],
            WorkbenchAbMergeInputRole.TpB =>
                [new WorkbenchAbVersionValue(WorkbenchAbVersionKind.TpB, UnknownAbVersion, null, true)],
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    private static WorkbenchAbMergeInputRole MapAbInputRole(string role)
    {
        return role switch
        {
            AbDpRole => WorkbenchAbMergeInputRole.DpAb,
            AbTpARole => WorkbenchAbMergeInputRole.TpA,
            AbTpBRole => WorkbenchAbMergeInputRole.TpB,
            _ => throw new InvalidOperationException($"Supported AB profile declares unknown input role '{role}'."),
        };
    }

    private static WorkbenchInputInspectionSeverity MapSeverity(
        CompiledInputArtifactInspectionSeverity severity)
    {
        return severity switch
        {
            CompiledInputArtifactInspectionSeverity.Valid => WorkbenchInputInspectionSeverity.Valid,
            CompiledInputArtifactInspectionSeverity.Warning => WorkbenchInputInspectionSeverity.Warning,
            CompiledInputArtifactInspectionSeverity.Blocking => WorkbenchInputInspectionSeverity.Blocking,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
    }

    private static WorkbenchInputInspectionNextAction MapNextAction(
        CompiledInputArtifactInspectionNextAction nextAction)
    {
        return nextAction switch
        {
            CompiledInputArtifactInspectionNextAction.None => WorkbenchInputInspectionNextAction.None,
            CompiledInputArtifactInspectionNextAction.SelectCompatibleInput =>
                WorkbenchInputInspectionNextAction.SelectCompatibleInput,
            CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes =>
                WorkbenchInputInspectionNextAction.ReviewIgnoredTrailingBytes,
            CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength =>
                WorkbenchInputInspectionNextAction.ReviewUnexpectedOuterLength,
            _ => throw new ArgumentOutOfRangeException(nameof(nextAction), nextAction, null),
        };
    }
}
