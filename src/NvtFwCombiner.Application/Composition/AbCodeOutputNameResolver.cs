using System.Globalization;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Renders the closed AB Code v1 filename from accepted immutable execution snapshots.</summary>
internal static class AbCodeOutputNameResolver
{
    private const int DpBankLength = 0x40000;
    private const int DpAbLength = DpBankLength * 2;

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

        TokenResolution dpA = ReadDpToken(request.CompiledComposition.IcId, inputBytes, inputSummaries, 0, "dp-a");
        TokenResolution dpB = ReadDpToken(request.CompiledComposition.IcId, inputBytes, inputSummaries, DpBankLength, "dp-b");
        TokenResolution tpA = ReadTpToken(inputBytes, inputSummaries, CompositionAddressSpaceIds.TpAInput, "tp-a");
        TokenResolution tpB = ReadTpToken(inputBytes, inputSummaries, CompositionAddressSpaceIds.TpBInput, "tp-b");
        string date = startedAtUtc.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string canonicalIcNumber = GetCanonicalIcNumber(request.CompiledComposition.IcId);
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
            actualFileName,
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

    private static string GetCanonicalIcNumber(string compiledIcId)
    {
        const string Prefix = "NT";
        if (compiledIcId.Length != Prefix.Length + 5 ||
            !compiledIcId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AB Code v1 output naming requires a compiled canonical NTxxxxx IC identity.");
        }

        foreach (char character in compiledIcId.AsSpan(Prefix.Length))
        {
            if (!char.IsAsciiDigit(character))
            {
                throw new InvalidOperationException(
                    "AB Code v1 output naming requires a compiled canonical NTxxxxx IC identity.");
            }
        }

        return compiledIcId[Prefix.Length..];
    }

    private static TokenResolution ReadDpToken(
        string icId,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        int bankStart,
        string tokenId)
    {
        return TryGetAcceptedSnapshot(inputBytes, inputSummaries, CompositionAddressSpaceIds.DpAbInput, out ReadOnlyMemory<byte> snapshot, out string? hash) &&
               snapshot.Length == DpAbLength &&
               GenFlashVersionCatalog.TryReadCmiDpCode(icId, snapshot.Span.Slice(bankStart, DpBankLength), out CmiDpCodeMetadata metadata)
            ? new TokenResolution(new OutputNamingTokenSummary(
                tokenId,
                FormattableString.Invariant($"D{metadata.MajorVersionByte:X2}{metadata.MinorVersionNibble:X2}"),
                IsKnown: true,
                CompositionAddressSpaceIds.DpAbInput,
                hash,
                "cmi-dp-code"))
            : new TokenResolution(new OutputNamingTokenSummary(
                tokenId,
                "Dxxxx",
                IsKnown: false,
                CompositionAddressSpaceIds.DpAbInput,
                hash,
                "cmi-dp-code"));
    }

    private static TokenResolution ReadTpToken(
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        string addressSpaceId,
        string tokenId)
    {
        return TryGetAcceptedSnapshot(inputBytes, inputSummaries, addressSpaceId, out ReadOnlyMemory<byte> snapshot, out string? hash) &&
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
                "Txxxx",
                IsKnown: false,
                addressSpaceId,
                hash,
                "fwconfig-backup"));
    }

    private static bool TryGetAcceptedSnapshot(
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
        if (summary?.ExecutionSnapshot is not { } executionSnapshot ||
            !inputBytes.TryGetValue(addressSpaceId, out byte[]? input) ||
            executionSnapshot.AcceptedRange.Start != 0 ||
            executionSnapshot.AcceptedRange.Length > input.LongLength)
        {
            return false;
        }

        snapshot = input.AsMemory(0, checked((int)executionSnapshot.AcceptedRange.Length));
        acceptedSnapshotSha256 = executionSnapshot.AcceptedSha256;
        return true;
    }

    private sealed record TokenResolution(OutputNamingTokenSummary Summary);
}

/// <summary>Resolved runtime output name with optional naming provenance and non-blocking diagnostics.</summary>
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
