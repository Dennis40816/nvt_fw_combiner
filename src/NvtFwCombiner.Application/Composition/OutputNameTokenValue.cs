// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// A field value, optionally scoped to a bank, with its accepted source provenance.
/// A null value means unreadable; compiled profiles may supply a prefixed missing-value marker.
/// </summary>
internal sealed record OutputNameTokenValue(
    string FieldId,
    string? Value,
    string? SourceAddressSpaceId,
    string? AcceptedSnapshotSha256,
    string ParserId,
    string? BankId = null,
    string MissingValue = "xxxx")
{
    internal static OutputNameTokenValue ForIc(string canonicalIcId, bool numberOnly = false)
    {
        CompiledOutputNamingRequirement.ValidateCanonicalIcIdentity(canonicalIcId, nameof(canonicalIcId));
        return new OutputNameTokenValue(
            "ic", numberOnly ? canonicalIcId[2..] : canonicalIcId, null, null, "compiled-profile");
    }

    internal static OutputNameTokenValue ForDate(DateTimeOffset resolvedAtUtc)
    {
        return new OutputNameTokenValue(
            "date",
            resolvedAtUtc.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            null,
            null,
            "utc-clock");
    }

    internal string TokenId => BankId is null ? FieldId : $"{FieldId}-{BankId}";

    internal OutputNamingTokenSummary Summary => new(
        TokenId,
        Value ?? MissingValue,
        Value is not null,
        SourceAddressSpaceId,
        AcceptedSnapshotSha256,
        ParserId);
}
