using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap;

internal static class WorkbenchAbMergeInputProjection
{
    private const string AbDpRole = "dp-ab";
    private const string AbTpARole = "tp-a";
    private const string AbTpBRole = "tp-b";
    private const string UnknownAbVersion = "Unknown";

    internal static IReadOnlyList<WorkbenchAbMergeInputSlot> GetInputSlots(
        string icId,
        TopologySelection? requestedTopology = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return !AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
                requestedTopology,
                out CompiledComposition? composition,
                out _) || composition.V2Details is null
            ? []
            : CreateInputSlots(composition);
    }

    internal static WorkbenchAbMergeInputInspection Inspect(
        string icId,
        string addressSpaceId,
        byte[]? image,
        TopologySelection? requestedTopology = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        if (!AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
                requestedTopology,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> compileIssues) ||
            composition.V2Details is not { } details)
        {
            throw new InvalidOperationException(WorkbenchCompositionService.FormatIssues(compileIssues));
        }

        WorkbenchAbMergeInputSlot slot = CreateInputSlots(composition).Single(candidate =>
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
            CompiledInputArtifactInspectionService.Inspect(
                details.InputContract,
                addressSpaceId,
                image);
        List<WorkbenchAbVersionValue> versions = ReadVersions(composition, slot.Role, image, inspected);
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

    private static IReadOnlyList<WorkbenchAbMergeInputSlot> CreateInputSlots(
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
                (long requiredEndExclusive, IReadOnlyList<long> expectedOuterLengths) =
                    ProjectLengthRequirement(slot.LengthRequirement, addressSpaceId);
                return new WorkbenchAbMergeInputSlot(
                    slot.SlotId,
                    binding.AddressSpaceId,
                    MapRole(slot.Role),
                    requiredEndExclusive,
                    expectedOuterLengths);
            }),
        ];
    }

    private static List<WorkbenchAbVersionValue> ReadVersions(
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
            WorkbenchAbMergeInputRole.DpAb => ReadDpVersions(composition, snapshot),
            WorkbenchAbMergeInputRole.TpA => [ReadTpVersion(WorkbenchAbVersionKind.TpA, snapshot)],
            WorkbenchAbMergeInputRole.TpB => [ReadTpVersion(WorkbenchAbVersionKind.TpB, snapshot)],
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    private static List<WorkbenchAbVersionValue> ReadDpVersions(
        CompiledComposition composition,
        ReadOnlySpan<byte> snapshot)
    {
        return TryReadDeclaredCmiVersions(
                composition,
                snapshot,
                out List<WorkbenchAbVersionValue>? declaredVersions)
            ? declaredVersions!
            :
            [
                new WorkbenchAbVersionValue(WorkbenchAbVersionKind.Dp1, UnknownAbVersion, null, true),
                new WorkbenchAbVersionValue(WorkbenchAbVersionKind.Dp2, UnknownAbVersion, null, true),
            ];
    }

    private static bool TryReadDeclaredCmiVersions(
        CompiledComposition composition,
        ReadOnlySpan<byte> snapshot,
        out List<WorkbenchAbVersionValue>? versions)
    {
        versions = null;
        IReadOnlyList<FirmwareRegion>? regions = composition.V2Details?.Provenance.ResolvedMap.ImageMap.Regions;
        if (regions is null)
        {
            return false;
        }

        FirmwareRegion? a = regions.SingleOrDefault(region =>
            StringComparer.Ordinal.Equals(region.RegionId, "a-cmi-dp-version"));
        FirmwareRegion? b = regions.SingleOrDefault(region =>
            StringComparer.Ordinal.Equals(region.RegionId, "b-cmi-dp-version"));
        if (a is null && b is null)
        {
            return false;
        }

        versions =
        [
            ReadDeclaredCmiVersion(WorkbenchAbVersionKind.Dp1, snapshot, a),
            ReadDeclaredCmiVersion(WorkbenchAbVersionKind.Dp2, snapshot, b),
        ];
        return true;
    }

    private static WorkbenchAbVersionValue ReadDeclaredCmiVersion(
        WorkbenchAbVersionKind kind,
        ReadOnlySpan<byte> snapshot,
        FirmwareRegion? region)
    {
        if (region is null || region.Range.Length != 3 || region.Range.Start < 0 ||
            region.Range.EndExclusive > snapshot.Length || region.Range.Start > int.MaxValue)
        {
            return new WorkbenchAbVersionValue(kind, UnknownAbVersion, JiraBadge: null, IsUnknown: true);
        }

        ReadOnlySpan<byte> registers = snapshot.Slice(checked((int)region.Range.Start), 3);
        byte register16 = registers[0];
        byte major = registers[1];
        byte register18 = registers[2];
        byte minor = (byte)(register18 >> 4);
        ushort jira = (ushort)(register16 | ((register18 & 0x0F) << 8));
        return new WorkbenchAbVersionValue(
            kind,
            FormattableString.Invariant($"D{major:X2}{minor:X2}"),
            jira == 0 ? null : $"AUTO_PRJ-{jira}",
            IsUnknown: false);
    }

    private static WorkbenchAbVersionValue ReadTpVersion(
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

    private static WorkbenchAbMergeInputRole MapRole(string role)
    {
        return role switch
        {
            AbDpRole => WorkbenchAbMergeInputRole.DpAb,
            AbTpARole => WorkbenchAbMergeInputRole.TpA,
            AbTpBRole => WorkbenchAbMergeInputRole.TpB,
            _ => throw new InvalidOperationException($"Supported AB profile declares unknown input role '{role}'."),
        };
    }

    private static (long RequiredEndExclusive, IReadOnlyList<long> ExpectedOuterLengths) ProjectLengthRequirement(
        CompiledInputLengthRequirement requirement,
        string addressSpaceId)
    {
        return requirement switch
        {
            CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix =>
                (declaredPrefix.RequiredEndExclusive, declaredPrefix.ExpectedOuterLengths),
            CompiledExactBytesInputLengthRequirement exact =>
                (exact.Bytes, [exact.Bytes]),
            CompiledExactResolvedMapCapacityInputLengthRequirement exact =>
                (exact.Bytes, [exact.Bytes]),
            _ => throw new InvalidOperationException(
                $"Supported AB input '{addressSpaceId}' has no displayable length contract."),
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
