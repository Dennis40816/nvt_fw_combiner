// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

internal static partial class CompiledOutputNameResolver
{
    private static OutputNameResolution ResolveBankFields(
        CompositionRunRequest request,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc)
    {
        CompiledOutputNamingRequirement output = request.CompiledComposition.V2Details.OutputNamingRequirement;
        OutputNameTokenValue dpA = ReadDpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            "a-cmi-dp-version",
            "a");
        OutputNameTokenValue dpB = ReadDpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            "b-cmi-dp-version",
            "b");
        OutputNameTokenValue tpA = ReadTpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            CompositionAddressSpaceIds.TpAInput,
            "a");
        OutputNameTokenValue tpB = ReadTpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            CompositionAddressSpaceIds.TpBInput,
            "b");
        OutputNameTokenValue[] tokenValues =
        [
            OutputNameTokenValue.ForIc(
                request.CompiledComposition.V2Details.Provenance.Context.MemberId,
                numberOnly: true),
            dpA,
            tpA,
            dpB,
            tpB,
            OutputNameTokenValue.ForDate(startedAtUtc),
        ];
        string automaticFileName = ResolveTemplate(output.FileNameTemplate, tokenValues);
        OutputNamingTokenSummary[] tokens = [.. tokenValues.Select(static token => token.Summary)];
        string actualFileName = request.IsOutputFileNameOverride ? request.OutputFileName : automaticFileName;
        OutputNamingSummary summary = new(
            "ab-code-v1",
            output.FileNameTemplate,
            automaticFileName,
            actualFileName,
            request.IsOutputFileNameOverride,
            "utc",
            startedAtUtc,
            tokens);
        List<CompositionIssue> issues = [];
        if (dpA.ParserId == "compiled-profile-dummy-dp;fill=0xFF")
        {
            issues.Add(new CompositionIssue(
                "output-naming.dummy-dp",
                "Dummy DP: non-TP output regions are filled with 0xFF.",
                severity: CompositionIssueSeverity.Info));
        }

        issues.AddRange(tokens.Where(static token => !token.IsKnown).Select(token => new CompositionIssue(
                "output-naming.metadata-unknown",
                $"AB Code filename token '{token.TokenId}' could not be read from its accepted immutable input snapshot; '{token.Value}' was used.",
                token.SourceAddressSpaceId,
                CompositionIssueSeverity.Warning)));
        return new OutputNameResolution(actualFileName, summary, issues);
    }

    private static OutputNameTokenValue ReadDpToken(
        CompositionRunRequest request,
        CompiledOutputNamingRequirement output,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string cmiRegionId,
        string bankId)
    {
        if (!request.CompiledComposition.V2Details.InputContract.SpaceBindings.Any(static binding =>
                binding.AddressSpaceId == CompositionAddressSpaceIds.DpAbInput))
        {
            ImageInitialization initialization = request.CompiledComposition.Plan.OutputInitialization;
            return initialization.Kind != ImageInitializationKind.Blank || initialization.FillByte != 0xFF
                ? throw new InvalidOperationException("AB Dummy naming requires compiled blank 0xFF output initialization.")
                : new OutputNameTokenValue("dp", "Dummy",
                    null, null, "compiled-profile-dummy-dp;fill=0xFF", bankId);
        }
        if (!TryGetAcceptedSnapshot(
                request,
                inputBytes,
                inputSummaries,
                CompositionAddressSpaceIds.DpAbInput,
                out ReadOnlyMemory<byte> snapshot,
                out string? hash))
        {
            return UnknownToken(
                output,
                "dp",
                bankId,
                CompositionAddressSpaceIds.DpAbInput,
                hash,
                "cmi-reg16-18");
        }

        CompiledInputVersionKind kind = cmiRegionId switch
        {
            "a-cmi-dp-version" => CompiledInputVersionKind.DpA,
            "b-cmi-dp-version" => CompiledInputVersionKind.DpB,
            _ => throw new InvalidOperationException($"Unsupported AB CMI region '{cmiRegionId}'."),
        };
        CompiledInputVersionObservation version =
            CompiledInputArtifactObservationService.DecodeDpRegion(
                request.CompiledComposition,
                kind,
                cmiRegionId,
                snapshot);
        return version.IsKnown
            ? KnownDpToken(bankId, version, hash)
            : UnknownToken(
                output,
                "dp",
                bankId,
                CompositionAddressSpaceIds.DpAbInput,
                hash,
                "cmi-reg16-18");
    }

    private static OutputNameTokenValue KnownDpToken(
        string bankId,
        CompiledInputVersionObservation version,
        string? hash)
    {
        ushort trackerId = version.TrackerId ?? 0;
        byte register18 = (byte)((version.Minor!.Value << 4) | (trackerId >> 8));
        return new OutputNameTokenValue(
            "dp",
            FormattableString.Invariant($"D{version.Major!.Value:X2}{version.Minor!.Value:X2}"),
            CompositionAddressSpaceIds.DpAbInput,
            hash,
            FormattableString.Invariant($"profile-cmi-reg16-18;reg16=0x{(byte)trackerId:X2};reg17=0x{version.Major!.Value:X2};reg18=0x{register18:X2};jira={trackerId}"),
            bankId);
    }

    private static OutputNameTokenValue ReadTpToken(
        CompositionRunRequest request,
        CompiledOutputNamingRequirement output,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string addressSpaceId,
        string bankId)
    {
        bool accepted = TryGetAcceptedSnapshot(
            request,
            inputBytes,
            inputSummaries,
            addressSpaceId,
            out ReadOnlyMemory<byte> snapshot,
            out string? hash);
        CompiledInputVersionKind kind = addressSpaceId switch
        {
            CompositionAddressSpaceIds.TpAInput => CompiledInputVersionKind.TpA,
            CompositionAddressSpaceIds.TpBInput => CompiledInputVersionKind.TpB,
            _ => throw new InvalidOperationException(
                $"Unsupported AB TP address space '{addressSpaceId}'."),
        };
        CompiledInputVersionObservation? version = accepted
            ? CompiledInputArtifactObservationService.DecodeTp(kind, snapshot,
                CompiledInputArtifactObservationService.DeclaredNvtEndFlag(request.CompiledComposition))
            : null;
        return version?.IsKnown == true
            ? new OutputNameTokenValue(
                "tp",
                FormattableString.Invariant($"T{version.Major:X2}{version.Minor:X2}"),
                addressSpaceId,
                hash,
                "fwconfig-backup",
                bankId)
            : UnknownToken(output, "tp", bankId, addressSpaceId, hash, "fwconfig-backup");
    }

    private static OutputNameTokenValue UnknownToken(
        CompiledOutputNamingRequirement output,
        string fieldId,
        string bankId,
        string addressSpaceId,
        string? hash,
        string parserId)
    {
        OutputNameTokenValue token = new(
            fieldId,
            Value: null,
            addressSpaceId,
            hash,
            parserId,
            bankId);
        return token with { MissingValue = GetCompiledPlaceholder(output, token.TokenId) };
    }

    private static string GetCompiledPlaceholder(
        CompiledOutputNamingRequirement output,
        string tokenId)
    {
        CompiledOutputTokenRequirement requirement =
            output.TokenRequirements.Single(candidate =>
                StringComparer.Ordinal.Equals(candidate.TokenId, tokenId));
        return requirement.MissingPolicy !=
                CompiledOutputTokenMissingPolicy.UsePlaceholder
            ? throw new InvalidOperationException(
                $"AB Code output token '{tokenId}' has no compiled placeholder.")
            : requirement.Placeholder!;
    }

    private static bool TryGetAcceptedSnapshot(
        CompositionRunRequest request,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string addressSpaceId,
        out ReadOnlyMemory<byte> snapshot,
        out string? acceptedSnapshotSha256)
    {
        snapshot = default;
        acceptedSnapshotSha256 = null;
        InputArtifactSummary? summary = inputSummaries.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
        if (!inputBytes.TryGetValue(addressSpaceId, out byte[]? input))
        {
            return false;
        }

        if (summary?.ExecutionSnapshot is { } executionSnapshot &&
            executionSnapshot.AcceptedRange.Start == 0 &&
            executionSnapshot.AcceptedRange.Length <= input.LongLength)
        {
            snapshot = input.AsMemory(0, checked((int)executionSnapshot.AcceptedRange.Length));
            acceptedSnapshotSha256 = executionSnapshot.AcceptedSha256;
            return true;
        }

        // Exact contracts use the complete immutable input solely because the compiled profile authorizes it.
        CompiledInputSpaceBinding? spaceBinding = request.CompiledComposition.V2Details
            .InputContract.SpaceBindings
            .SingleOrDefault(binding => StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId));
        CompiledInputSlotRequirement? slot = spaceBinding is null
            ? null
            : request.CompiledComposition.V2Details.InputContract.Slots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, spaceBinding.SlotId));
        long? exactBytes = slot?.LengthRequirement switch
        {
            CompiledExactBytesInputLengthRequirement exact => exact.Bytes,
            CompiledExactResolvedMapCapacityInputLengthRequirement exact => exact.Bytes,
            _ => null,
        };
        if (exactBytes is null || input.LongLength != exactBytes.Value || input.LongLength > int.MaxValue)
        {
            return false;
        }

        snapshot = input;
        acceptedSnapshotSha256 = summary?.Sha256;
        return acceptedSnapshotSha256 is not null;
    }
}
