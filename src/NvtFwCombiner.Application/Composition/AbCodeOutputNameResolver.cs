using System.Globalization;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

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
        CompiledOutputNamingRequirement? output = request.CompiledComposition.V2Details?.OutputNamingRequirement;
        if (output?.RendererKind != CompiledOutputNameRendererKind.AbCodeV1)
        {
            return OutputNameResolution.Static(request.OutputFileName);
        }

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
                request.CompiledComposition.IcId)[2..];
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
        return !TryGetAcceptedSnapshot(
                request,
                inputBytes,
                inputSummaries,
            CompositionAddressSpaceIds.DpAbInput,
                out ReadOnlyMemory<byte> snapshot,
                out string? hash)
            ? UnknownToken(output, tokenId, CompositionAddressSpaceIds.DpAbInput, hash, "cmi-reg16-18")
            : TryGetProfileCmiOffset(request.CompiledComposition, cmiRegionId, snapshot.Length, out int cmiOffset)
                ? KnownDpToken(tokenId, snapshot.Span.Slice(cmiOffset, 3), hash, "profile-cmi-reg16-18")
            : UnknownToken(output, tokenId, CompositionAddressSpaceIds.DpAbInput, hash, "cmi-reg16-18");
    }

    private static bool TryGetProfileCmiOffset(
        CompiledComposition composition,
        string regionId,
        int snapshotLength,
        out int offset)
    {
        offset = 0;
        FirmwareRegion? region = composition.V2Details?.Provenance.ResolvedMap.ImageMap.Regions.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.RegionId, regionId));
        if (region is null || region.Range.Length != 3 || region.Range.Start < 0 ||
            region.Range.EndExclusive > snapshotLength || region.Range.Start > int.MaxValue)
        {
            return false;
        }

        offset = checked((int)region.Range.Start);
        return true;
    }

    private static TokenResolution KnownDpToken(
        string tokenId,
        ReadOnlySpan<byte> registers16To18,
        string? hash,
        string parserId)
    {
        byte register16 = registers16To18[0];
        byte major = registers16To18[1];
        byte register18 = registers16To18[2];
        byte minor = (byte)(register18 >> 4);
        ushort jira = (ushort)(register16 | ((register18 & 0x0F) << 8));
        return new TokenResolution(new OutputNamingTokenSummary(
            tokenId,
            FormattableString.Invariant($"D{major:X2}{minor:X2}"),
            IsKnown: true,
            CompositionAddressSpaceIds.DpAbInput,
            hash,
            FormattableString.Invariant($"{parserId};reg16=0x{register16:X2};reg17=0x{major:X2};reg18=0x{register18:X2};jira={jira}")));
    }

    private static TokenResolution ReadTpToken(
        CompositionRunRequest request,
        CompiledOutputNamingRequirement output,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string addressSpaceId,
        string tokenId)
    {
        return TryGetAcceptedSnapshot(request, inputBytes, inputSummaries, addressSpaceId, out ReadOnlyMemory<byte> snapshot, out string? hash) &&
               FirmwareConfigMetadataReader.TryReadBackup(snapshot.Span, out FirmwareConfigMetadata metadata) &&
               metadata.IsFirmwareVersionBarValid
            ? new TokenResolution(new OutputNamingTokenSummary(
                tokenId,
                FormattableString.Invariant($"T{metadata.FirmwareVersion:X2}{metadata.FirmwareSubVersion:X2}"),
                IsKnown: true,
                addressSpaceId,
                hash,
                "fwconfig-backup"))
            : new TokenResolution(new OutputNamingTokenSummary(
                tokenId,
                GetCompiledPlaceholder(output, tokenId),
                IsKnown: false,
                addressSpaceId,
                hash,
                "fwconfig-backup"));
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

        // Exact contracts accept the complete immutable input; they deliberately have no
        // declared-prefix execution snapshot.  The profile contract, not an IC, PID, or
        // version value, is the sole authority for this fallback.
        CompiledInputSpaceBinding? spaceBinding = request.CompiledComposition.V2Details?
            .InputContract.SpaceBindings
            .SingleOrDefault(binding => StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId));
        CompiledInputSlotRequirement? slot = spaceBinding is null
            ? null
            : request.CompiledComposition.V2Details!.InputContract.Slots.SingleOrDefault(candidate =>
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
