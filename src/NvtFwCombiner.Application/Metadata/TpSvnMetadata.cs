using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>
/// Stable vocabulary of the read-only TP SVN stamp (owner A-9, decisions 31, 34 and 36): four bytes at
/// TP start + <c>0x24</c> in storage order, written by <c>InsertPID.py</c> after postbuild. It is not a
/// TP Flash Header field and never reads or mirrors the FWConfig <c>u8AutoBuildSvnVer1-4</c> bytes.
/// Locations are profile facts; this contract names only the canonical definition and its two fields.
/// </summary>
public static class TpSvnMetadataContract
{
    /// <summary>Canonical definition declared once by the neutral provider family.</summary>
    public const string DefinitionId = "tp-svn";

    /// <summary>Byte 0: owner-defined flags combined with bitwise OR.</summary>
    public const string FlagsFieldId = "svn-flags";

    /// <summary>Bytes 1-3: six BCD revision digits, most significant pair first.</summary>
    public const string RevisionFieldId = "svn-revision";

    /// <summary>The only owner-defined flag bits (<c>0x80 | 0x40 | 0x20</c>).</summary>
    public const byte DefinedFlagMask = 0xE0;

    private static readonly (TpSvnBuildOrigin Flag, string Name)[] FlagNames =
    [
        (TpSvnBuildOrigin.LocalBuild, "LOCAL_BUILD"),
        (TpSvnBuildOrigin.DiffExist, "DIFF_EXIST"),
        (TpSvnBuildOrigin.NoSvnRecord, "NO_SVN_RECORD"),
    ];

    /// <summary>Returns the owner flag names verbatim, most significant bit first; empty when none is set.</summary>
    public static IReadOnlyList<string> GetFlagNames(TpSvnBuildOrigin flags)
    {
        return [.. FlagNames.Where(item => flags.HasFlag(item.Flag)).Select(static item => item.Name)];
    }
}

/// <summary>Owner-defined flags in byte 0 (A-9); any other bit is undefined.</summary>
[Flags]
public enum TpSvnBuildOrigin : byte
{
    /// <summary>No defined flag is set.</summary>
    None = 0,

    /// <summary><c>NO_SVN_RECORD</c>: no last-changed revision record.</summary>
    NoSvnRecord = 0x20,

    /// <summary><c>DIFF_EXIST</c>: the working copy had local modifications.</summary>
    DiffExist = 0x40,

    /// <summary><c>LOCAL_BUILD</c>: stamped on a Windows host.</summary>
    LocalBuild = 0x80,
}

/// <summary>
/// Owner-defined TP SVN anomalies (decision 34). They are mutually exclusive: undefined flag bits
/// imply a nonzero byte 0. <c>0x60</c> with a nonzero revision is a normal Jenkins build.
/// </summary>
public enum TpSvnAnomaly
{
    /// <summary>The stamp conforms to the owner definition.</summary>
    None,

    /// <summary>Byte 0 has bits outside <c>0xE0</c>; <c>InsertPID.py</c> may be faulty.</summary>
    UndefinedFlagBits,

    /// <summary>All four bytes are zero; <c>InsertPID.py</c> may not have run.</summary>
    NotStamped,
}

/// <summary>One decoded, read-only TP SVN stamp and its exact source range in the evaluated capture.</summary>
public sealed class TpSvnObservation
{
    private readonly byte[] _raw;

    internal TpSvnObservation(string structureId, FirmwareAddressedRange range, ReadOnlySpan<byte> raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        ArgumentNullException.ThrowIfNull(range);
        if (raw.Length != 4 || range.Range.Length != 4)
        {
            throw new ArgumentException("A TP SVN stamp is exactly four bytes.", nameof(raw));
        }

        StructureId = structureId;
        Range = range;
        _raw = raw.ToArray();
    }

    /// <summary>Exact profile structure that located this stamp.</summary>
    public string StructureId { get; }

    /// <summary>Half-open source range in the evaluated capture; bank-local for an AB Reference bank.</summary>
    public FirmwareAddressedRange Range { get; }

    /// <summary>The four stored bytes in storage order.</summary>
    public ReadOnlyMemory<byte> RawBytes => _raw;

    /// <summary>Raw byte 0.</summary>
    public byte FlagByte => _raw[0];

    /// <summary>Owner-defined flags only (<c>byte 0 &amp; 0xE0</c>).</summary>
    public TpSvnBuildOrigin Flags => (TpSvnBuildOrigin)(FlagByte & TpSvnMetadataContract.DefinedFlagMask);

    /// <summary>Bits of byte 0 outside the owner definition; zero when conforming.</summary>
    public byte UndefinedFlagBits => (byte)(FlagByte & ~TpSvnMetadataContract.DefinedFlagMask);

    /// <summary>
    /// Six decimal digits from bytes 1-3 when every nibble is a BCD digit; otherwise null.
    /// The revision never reads byte 0, even when byte 0 has undefined bits.
    /// </summary>
    public string? RevisionDigits =>
        IsBcd(_raw[1]) && IsBcd(_raw[2]) && IsBcd(_raw[3]) ? Convert.ToHexString(_raw, 1, 3) : null;

    /// <summary>The single owner-defined anomaly of this stamp.</summary>
    public TpSvnAnomaly Anomaly =>
        UndefinedFlagBits != 0 ? TpSvnAnomaly.UndefinedFlagBits
        : Array.TrueForAll(_raw, static value => value == 0) ? TpSvnAnomaly.NotStamped
        : TpSvnAnomaly.None;

    internal bool IsSameStamp(TpSvnObservation other)
    {
        return StringComparer.Ordinal.Equals(StructureId, other.StructureId) &&
            Range == other.Range &&
            _raw.AsSpan().SequenceEqual(other._raw);
    }

    private static bool IsBcd(byte value)
    {
        return (value >> 4) <= 9 && (value & 0x0F) <= 9;
    }
}

/// <summary>
/// Reads the TP SVN stamp of one input space from one captured image through the exact current plan,
/// without selecting IC, map, route or location. Only a display-bound entry of the canonical
/// definition grants authority; missing, ambiguous or unresolvable declarations yield no value and
/// never block inspection or Build.
/// </summary>
public static class TpSvnMetadataProjector
{
    /// <summary>Returns the stamp of <paramref name="spaceId"/>, or null when it is undeclared or unreadable.</summary>
    public static TpSvnObservation? Read(ResolvedMetadataPlan plan, string spaceId, ReadOnlyMemory<byte> image)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(spaceId);
        if (plan.Definition.FullImageContext is not null)
        {
            // General full-image views declare no TP SVN binding; that scope is a later version.
            return null;
        }

        MetadataPlanEntry[] entries =
        [
            .. plan.Entries.Select(static item => item.Definition)
                .Where(entry =>
                    StringComparer.Ordinal.Equals(entry.SpaceId, spaceId) &&
                    StringComparer.Ordinal.Equals(
                        entry.StructureDefinition.Definition.DefinitionId,
                        TpSvnMetadataContract.DefinitionId) &&
                    entry.Purposes.Contains(MetadataReferencePurpose.Display))
                .Take(2),
        ];
        if (entries.Length != 1)
        {
            return null;
        }

        MetadataPlanEntry entry = entries[0];
        if (image.Length > entry.ResolvedMap.CapacityBytes ||
            !StringComparer.Ordinal.Equals(entry.SpaceId, entry.StructureDefinition.ArtifactBindingId))
        {
            return null;
        }

        var inputs = new FirmwareMapResolutionInputs(entry.MemberId, entry.ResolvedMap.ModeId,
            entry.ResolvedMap.CapacityBytes, requestedTopology: null,
            [new FirmwareArtifactPayload(entry.SpaceId, image.Span)]);
        FirmwareResolvedMetadataStructure? resolved = entry.FamilyDefinition.ResolveMetadataStructure(
            entry.ImageMap.MapId, entry.StructureDefinition.StructureId, inputs).Resolved;
        return resolved is not null &&
            TryReadBytes(resolved.DecodedStructure, TpSvnMetadataContract.FlagsFieldId, 1, out byte[] flags) &&
            TryReadBytes(resolved.DecodedStructure, TpSvnMetadataContract.RevisionFieldId, 3, out byte[] revision)
                ? new TpSvnObservation(entry.StructureDefinition.StructureId,
                    resolved.LocatorOutcome.ResolvedRange, [.. flags, .. revision])
                : null;
    }

    /// <summary>
    /// Returns the stamp only when every candidate plan resolves the same structure, range and bytes
    /// from the same capture (TP-only consensus); any missing or disagreeing candidate yields null.
    /// </summary>
    internal static TpSvnObservation? ReadConsensus(
        IReadOnlyList<ResolvedMetadataPlan> plans, string spaceId, ReadOnlyMemory<byte> image)
    {
        ArgumentNullException.ThrowIfNull(plans);
        TpSvnObservation? first = null;
        foreach (ResolvedMetadataPlan plan in plans)
        {
            TpSvnObservation? observed = Read(plan, spaceId, image);
            if (observed is null || (first is not null && !first.IsSameStamp(observed)))
            {
                return null;
            }

            first ??= observed;
        }

        return first;
    }

    private static bool TryReadBytes(
        FirmwareDecodedMetadataStructure decoded, string fieldId, int length, out byte[] bytes)
    {
        bytes = [];
        FirmwareDecodedMetadataFact? fact = decoded.Facts.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.FieldId, fieldId));
        if (fact?.Value is not { Kind: FirmwareMetadataValueKind.Bytes, BytesValue: { } value } ||
            value.Length != length)
        {
            return false;
        }

        bytes = Convert.FromHexString(value.Hex);
        return true;
    }
}
