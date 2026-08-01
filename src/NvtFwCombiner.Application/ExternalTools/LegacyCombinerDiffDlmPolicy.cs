using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>How one record-relative DiffDLM range is treated by a preservation policy.</summary>
public enum LegacyCombinerDiffDlmMaskKind
{
    /// <summary>The immutable reference bytes remain authoritative for this range.</summary>
    KeepReference,
}

/// <summary>One complete record-relative preservation range.</summary>
public sealed record LegacyCombinerDiffDlmMask
{
    /// <summary>Creates one checked record-relative mask.</summary>
    public LegacyCombinerDiffDlmMask(
        LegacyCombinerDiffDlmMaskKind kind,
        ByteRange range)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown DiffDLM mask kind.");
        }

        Kind = kind;
        Range = range;
    }

    /// <summary>Mask behavior.</summary>
    public LegacyCombinerDiffDlmMaskKind Kind { get; }

    /// <summary>Half-open range relative to one full-stride record.</summary>
    public ByteRange Range { get; }
}

/// <summary>
/// Canonical count-dependent DiffDLM scatter and DiffNF-preservation policy used by
/// the legacy postbuild adapter while the shared V2 compiler owns the resulting operations.
/// </summary>
public sealed class LegacyCombinerDiffDlmPolicy
{
    private readonly LegacyCombinerDiffDlmMask[] _preservationMasks;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates and validates one complete full-stride record policy.</summary>
    public LegacyCombinerDiffDlmPolicy(
        string policyId,
        string sourceFileName,
        string stagedArtifactId,
        long sourceRecordStride,
        long targetBase,
        long targetRecordStride,
        ByteRange writableRange,
        IEnumerable<LegacyCombinerDiffDlmMask> preservationMasks,
        int minimumIcCount,
        int maximumIcCount,
        int activeRecordCountOffset,
        string independentNfSourceFileName,
        int firmwareConfigBackupAlignment,
        long firmwareConfigBackupLength,
        ByteRange firmwareConfigBackupAuthority,
        IEnumerable<string> evidenceRefs,
        long? fixedFirmwareConfigBackupStart = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        ValidatePlainFileName(sourceFileName, nameof(sourceFileName));
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedArtifactId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceRecordStride);
        ArgumentOutOfRangeException.ThrowIfNegative(targetBase);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetRecordStride);
        if (sourceRecordStride != targetRecordStride)
        {
            throw new ArgumentException(
                "DiffDLM source and target records must use one common full-record stride.",
                nameof(targetRecordStride));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(minimumIcCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumIcCount, minimumIcCount);
        if (activeRecordCountOffset >= 0 ||
            checked(minimumIcCount + activeRecordCountOffset) <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeRecordCountOffset),
                "DiffDLM active-record count must be a positive count-relative reduction.");
        }

        ValidatePlainFileName(independentNfSourceFileName, nameof(independentNfSourceFileName));
        if (firmwareConfigBackupAlignment <= 0 ||
            (firmwareConfigBackupAlignment & (firmwareConfigBackupAlignment - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firmwareConfigBackupAlignment),
                "FWConfig Backup alignment must be a positive power of two.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firmwareConfigBackupLength);
        if (fixedFirmwareConfigBackupStart is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fixedFirmwareConfigBackupStart),
                "Fixed FWConfig Backup start must not be negative.");
        }

        if (!new ByteRange(0, sourceRecordStride).Contains(writableRange) ||
            !new ByteRange(0, targetRecordStride).Contains(writableRange))
        {
            throw new ArgumentException(
                "DiffDLM writable range must fit the full record stride.",
                nameof(writableRange));
        }

        ArgumentNullException.ThrowIfNull(preservationMasks);
        _preservationMasks = [.. preservationMasks];
        if (_preservationMasks.Length == 0 ||
            _preservationMasks.Any(static mask =>
                mask is null ||
                mask.Kind != LegacyCombinerDiffDlmMaskKind.KeepReference))
        {
            throw new ArgumentException(
                "DiffDLM policy requires explicit keep-reference masks.",
                nameof(preservationMasks));
        }

        ValidateCompleteRecordCoverage(targetRecordStride, writableRange, _preservationMasks);
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        _evidenceRefs = [.. evidenceRefs];
        if (_evidenceRefs.Length == 0 ||
            _evidenceRefs.Any(string.IsNullOrWhiteSpace) ||
            _evidenceRefs.Distinct(StringComparer.Ordinal).Count() != _evidenceRefs.Length)
        {
            throw new ArgumentException(
                "DiffDLM policy evidence must contain unique nonblank references.",
                nameof(evidenceRefs));
        }

        for (int icCount = minimumIcCount; icCount <= maximumIcCount; icCount++)
        {
            long activeEnd = checked(
                targetBase +
                ((icCount + activeRecordCountOffset) * targetRecordStride));
            long expectedBackupStart = fixedFirmwareConfigBackupStart ??
                AlignUp(activeEnd, firmwareConfigBackupAlignment);
            if (!firmwareConfigBackupAuthority.Contains(
                    new ByteRange(expectedBackupStart, firmwareConfigBackupLength)))
            {
                throw new ArgumentException(
                    "FWConfig Backup authority must contain every supported Backup envelope.",
                    nameof(firmwareConfigBackupAuthority));
            }
        }

        PolicyId = policyId;
        SourceFileName = sourceFileName;
        StagedArtifactId = stagedArtifactId;
        SourceRecordStride = sourceRecordStride;
        TargetBase = targetBase;
        TargetRecordStride = targetRecordStride;
        WritableRange = writableRange;
        PreservationMasks = Array.AsReadOnly(_preservationMasks);
        MinimumIcCount = minimumIcCount;
        MaximumIcCount = maximumIcCount;
        ActiveRecordCountOffset = activeRecordCountOffset;
        IndependentNfSourceFileName = independentNfSourceFileName;
        FirmwareConfigBackupAlignment = firmwareConfigBackupAlignment;
        FirmwareConfigBackupLength = firmwareConfigBackupLength;
        FirmwareConfigBackupAuthority = firmwareConfigBackupAuthority;
        FixedFirmwareConfigBackupStart = fixedFirmwareConfigBackupStart;
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable canonical policy id.</summary>
    public string PolicyId { get; }

    /// <summary>AE full-stride source file.</summary>
    public string SourceFileName { get; }

    /// <summary>Immutable staged-artifact identity used by the postbuild adapter.</summary>
    public string StagedArtifactId { get; }

    /// <summary>Distance between AE records.</summary>
    public long SourceRecordStride { get; }

    /// <summary>Flash start of record zero.</summary>
    public long TargetBase { get; }

    /// <summary>Distance between Flash records.</summary>
    public long TargetRecordStride { get; }

    /// <summary>Only record-relative source/target bytes that Replace may copy.</summary>
    public ByteRange WritableRange { get; }

    /// <summary>Record-relative ranges that remain from the immutable reference.</summary>
    public IReadOnlyList<LegacyCombinerDiffDlmMask> PreservationMasks { get; }

    /// <summary>Inclusive supported IC Count minimum.</summary>
    public int MinimumIcCount { get; }

    /// <summary>Inclusive supported IC Count maximum.</summary>
    public int MaximumIcCount { get; }

    /// <summary>Count-relative offset used to derive active record count.</summary>
    public int ActiveRecordCountOffset { get; }

    /// <summary>Postbuild-owned NF source that must not be exposed as an independent selector.</summary>
    public string IndependentNfSourceFileName { get; }

    /// <summary>Required alignment when the expected FWConfig Backup start is active-record-derived.</summary>
    public int FirmwareConfigBackupAlignment { get; }

    /// <summary>Owner-declared fixed Backup start, or null when placement is active-record-derived.</summary>
    public long? FixedFirmwareConfigBackupStart { get; }

    /// <summary>Complete FWConfig Backup envelope length.</summary>
    public long FirmwareConfigBackupLength { get; }

    /// <summary>Static bounded postbuild authority covering every supported Backup position.</summary>
    public ByteRange FirmwareConfigBackupAuthority { get; }

    /// <summary>Owner-approved evidence references.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

    /// <summary>Returns whether this policy applies to one resolved cascade count.</summary>
    public bool AppliesTo(int icCount)
    {
        return icCount >= MinimumIcCount && icCount <= MaximumIcCount;
    }

    /// <summary>Returns the exact number of active full-stride records.</summary>
    public int GetActiveRecordCount(int icCount)
    {
        return AppliesTo(icCount)
            ? checked(icCount + ActiveRecordCountOffset)
            : throw new ArgumentOutOfRangeException(
                nameof(icCount),
                icCount,
                $"DiffDLM policy '{PolicyId}' supports IC Count {MinimumIcCount} through {MaximumIcCount}.");
    }

    /// <summary>
    /// Returns the complete active source prefix. Every active record must retain its full stride,
    /// including bytes protected by preservation masks, so truncated AE records fail closed.
    /// </summary>
    public long GetRequiredSourceLength(int icCount)
    {
        return checked(GetActiveRecordCount(icCount) * SourceRecordStride);
    }

    /// <summary>Returns the first byte after all active full-stride target records.</summary>
    public long GetActiveTargetEndExclusive(int icCount)
    {
        return checked(TargetBase + (GetActiveRecordCount(icCount) * TargetRecordStride));
    }

    /// <summary>Returns the owner-declared fixed or active-record-derived expected FWConfig Backup start.</summary>
    public long GetExpectedFirmwareConfigBackupStart(int icCount)
    {
        _ = GetActiveRecordCount(icCount);
        return FixedFirmwareConfigBackupStart ??
            AlignUp(GetActiveTargetEndExclusive(icCount), FirmwareConfigBackupAlignment);
    }

    /// <summary>
    /// Returns postbuild authority after active records. This excludes every active DLM and DiffNF byte.
    /// </summary>
    public ByteRange GetResolvedFirmwareConfigBackupAuthority(int icCount)
    {
        if (FixedFirmwareConfigBackupStart is { } fixedStart)
        {
            _ = GetActiveRecordCount(icCount);
            return new ByteRange(fixedStart, FirmwareConfigBackupLength);
        }

        long start = Math.Max(
            GetActiveTargetEndExclusive(icCount),
            FirmwareConfigBackupAuthority.Start);
        return ByteRange.FromStartEndExclusive(start, FirmwareConfigBackupAuthority.EndExclusive);
    }

    /// <summary>Returns whether a staged block is the full-envelope template replaced by this policy.</summary>
    public bool IsTemplateBlock(LegacyCombinerBlockArgument block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile &&
            StringComparer.Ordinal.Equals(block.SourceFileName, SourceFileName) &&
            StringComparer.Ordinal.Equals(block.StagedArtifactId, StagedArtifactId);
    }

    /// <summary>Returns whether this source must stay postbuild-owned and unavailable to UI/CLI selection.</summary>
    public bool IsIndependentNfBlock(LegacyCombinerBlockArgument block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return StringComparer.Ordinal.Equals(block.SourceFileName, IndependentNfSourceFileName);
    }

    /// <summary>Expands one maximum-envelope declaration into exact active DLM scatter blocks.</summary>
    public IReadOnlyList<LegacyCombinerBlockArgument> Expand(
        LegacyCombinerBlockArgument template,
        int icCount)
    {
        ArgumentNullException.ThrowIfNull(template);
        int maximumRecordCount = GetActiveRecordCount(MaximumIcCount);
        if (!IsTemplateBlock(template) ||
            template.SourceOffset != 0 ||
            template.FirmwareRange != new ByteRange(
                TargetBase,
                checked(maximumRecordCount * TargetRecordStride)))
        {
            throw new ArgumentException(
                $"DiffDLM template block does not match policy '{PolicyId}'.",
                nameof(template));
        }

        int activeRecordCount = GetActiveRecordCount(icCount);
        var blocks = new LegacyCombinerBlockArgument[activeRecordCount];
        for (int index = 0; index < activeRecordCount; index++)
        {
            blocks[index] = new LegacyCombinerBlockArgument(
                FormattableString.Invariant($"{template.BlockId}-record-{index}"),
                template.SourceKind,
                template.SourceFileName,
                checked((index * SourceRecordStride) + WritableRange.Start),
                new ByteRange(
                    checked(TargetBase + (index * TargetRecordStride) + WritableRange.Start),
                    WritableRange.Length),
                template.StagedArtifactId,
                template.SectionId);
        }

        return Array.AsReadOnly(blocks);
    }

    private static void ValidateCompleteRecordCoverage(
        long targetRecordStride,
        ByteRange writableRange,
        IReadOnlyList<LegacyCombinerDiffDlmMask> preservationMasks)
    {
        ByteRange[] coverage =
        [
            writableRange,
            .. preservationMasks.Select(static mask => mask.Range),
        ];
        Array.Sort(coverage, static (left, right) => left.Start.CompareTo(right.Start));
        long cursor = 0;
        foreach (ByteRange range in coverage)
        {
            if (range.Start != cursor || range.EndExclusive > targetRecordStride)
            {
                throw new ArgumentException(
                    "DiffDLM writable and preservation ranges must form one non-overlapping complete record.");
            }

            cursor = range.EndExclusive;
        }

        if (cursor != targetRecordStride)
        {
            throw new ArgumentException(
                "DiffDLM writable and preservation ranges must cover the complete target record.");
        }
    }

    private static long AlignUp(long value, int alignment)
    {
        return checked((value + alignment - 1) / alignment * alignment);
    }

    private static void ValidatePlainFileName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.IndexOfAny(['/', '\\', ':']) >= 0 ||
            value is "." or ".." ||
            Path.GetFileName(value) != value)
        {
            throw new ArgumentException("Value must be a plain file name.", parameterName);
        }
    }
}
