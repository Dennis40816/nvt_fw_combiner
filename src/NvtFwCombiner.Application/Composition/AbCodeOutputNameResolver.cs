using System.Globalization;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Renders the closed AB Code v1 filename from accepted immutable execution snapshots.</summary>
internal static class AbCodeOutputNameResolver
{
    internal static OutputNameResolution Resolve(
        CompositionRunRequest request,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc)
    {
        CompiledOutputNamingRequirement output = request.CompiledComposition.V2Details.OutputNamingRequirement;
        TokenResolution dpA = ReadDpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            "a-cmi-dp-version",
            "dp-a");
        TokenResolution dpB = ReadDpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            "b-cmi-dp-version",
            "dp-b");
        TokenResolution tpA = ReadTpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            CompositionAddressSpaceIds.TpAInput,
            "tp-a");
        TokenResolution tpB = ReadTpToken(
            request,
            output,
            inputBytes,
            inputSummaries,
            CompositionAddressSpaceIds.TpBInput,
            "tp-b");
        string date = startedAtUtc.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string canonicalIcNumber =
            CompiledOutputNameResolver.GetCanonicalIcIdentity(
                request.CompiledComposition.V2Details.Provenance.Context.MemberId)[2..];
        OutputNamingTokenSummary[] tokens =
        [
            new OutputNamingTokenSummary("ic", canonicalIcNumber, IsKnown: true, null, null, "compiled-profile"),
            dpA.Summary,
            tpA.Summary,
            dpB.Summary,
            tpB.Summary,
            new OutputNamingTokenSummary("date", date, IsKnown: true, null, null, "utc-clock"),
        ];
        string automaticFileName = $"NT{canonicalIcNumber}_FlashCode_A_{dpA.Summary.Value}{tpA.Summary.Value}_B_{dpB.Summary.Value}{tpB.Summary.Value}_{date}.bin";
        CompiledOutputNamingRequirement.ValidateRuntimeLiteralFileName(automaticFileName, nameof(automaticFileName));
        string actualFileName = request.IsOutputFileNameOverride ? request.OutputFileName : automaticFileName;
        var summary = new OutputNamingSummary(
            "ab-code-v1",
            output.FileNameTemplate,
            automaticFileName,
            actualFileName,
            request.IsOutputFileNameOverride,
            "utc",
            startedAtUtc,
            tokens);
        CompositionIssue[] issues =
        [
            .. tokens.Where(static token => !token.IsKnown).Select(token => new CompositionIssue(
                "output-naming.metadata-unknown",
                $"AB Code filename token '{token.TokenId}' could not be read from its accepted immutable input snapshot; '{token.Value}' was used.",
                token.SourceAddressSpaceId,
                CompositionIssueSeverity.Warning)),
        ];
        return new OutputNameResolution(actualFileName, summary, issues);
    }

    private static TokenResolution ReadDpToken(
        CompositionRunRequest request,
        CompiledOutputNamingRequirement output,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string cmiRegionId,
        string tokenId)
    {
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
                tokenId,
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
            ? KnownDpToken(tokenId, version, hash)
            : UnknownToken(
                output,
                tokenId,
                CompositionAddressSpaceIds.DpAbInput,
                hash,
                "cmi-reg16-18");
    }

    private static TokenResolution KnownDpToken(
        string tokenId,
        CompiledInputVersionObservation version,
        string? hash)
    {
        ushort trackerId = version.TrackerId ?? 0;
        byte register18 = (byte)((version.Minor!.Value << 4) | (trackerId >> 8));
        return new TokenResolution(new OutputNamingTokenSummary(
            tokenId,
            FormattableString.Invariant($"D{version.Major!.Value:X2}{version.Minor!.Value:X2}"),
            IsKnown: true,
            CompositionAddressSpaceIds.DpAbInput,
            hash,
            FormattableString.Invariant($"profile-cmi-reg16-18;reg16=0x{(byte)trackerId:X2};reg17=0x{version.Major!.Value:X2};reg18=0x{register18:X2};jira={trackerId}")));
    }

    private static TokenResolution ReadTpToken(
        CompositionRunRequest request,
        CompiledOutputNamingRequirement output,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string addressSpaceId,
        string tokenId)
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
            ? CompiledInputArtifactObservationService.DecodeTp(kind, snapshot)
            : null;
        return version?.IsKnown == true
            ? new TokenResolution(new OutputNamingTokenSummary(
                tokenId,
                FormattableString.Invariant($"T{version.Major:X2}{version.Minor:X2}"),
                IsKnown: true,
                addressSpaceId,
                hash,
                "fwconfig-backup"))
            : UnknownToken(output, tokenId, addressSpaceId, hash, "fwconfig-backup");
    }

    private static TokenResolution UnknownToken(
        CompiledOutputNamingRequirement output,
        string tokenId,
        string addressSpaceId,
        string? hash,
        string parserId)
    {
        return new TokenResolution(new OutputNamingTokenSummary(
            tokenId,
            GetCompiledPlaceholder(output, tokenId),
            IsKnown: false,
            addressSpaceId,
            hash,
            parserId));
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

    private sealed record TokenResolution(OutputNamingTokenSummary Summary);
}

/// <summary>Resolved runtime output name with optional naming provenance and typed diagnostics.</summary>
internal sealed record OutputNameResolution(
    string FileName,
    OutputNamingSummary? Summary,
    IReadOnlyList<CompositionIssue> Issues)
{
    internal static OutputNameResolution Static(string fileName)
    {
        return new OutputNameResolution(fileName, null, []);
    }
}
