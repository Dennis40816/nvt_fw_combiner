using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Physical owner of one canonical firmware region.</summary>
public enum FirmwareRegionOwner
{
    /// <summary>System-owned bytes.</summary>
    System,

    /// <summary>Display firmware-owned bytes.</summary>
    Dp,

    /// <summary>Touch firmware-owned bytes.</summary>
    Tp,

    /// <summary>LDC-owned bytes.</summary>
    Ldc,

    /// <summary>Register-owned bytes.</summary>
    Register,

    /// <summary>Customer-owned bytes.</summary>
    Customer,

    /// <summary>Bytes shared by documented owners.</summary>
    Shared,

    /// <summary>Reserved bytes.</summary>
    Reserved,

    /// <summary>Bytes whose owner is not yet established.</summary>
    Unknown,
}

/// <summary>Closed physical kind of one canonical firmware region.</summary>
public enum FirmwareRegionKind
{
    /// <summary>Root or nested image container.</summary>
    Image,

    /// <summary>Executable code.</summary>
    Code,

    /// <summary>Firmware header.</summary>
    Header,

    /// <summary>Structured or opaque data.</summary>
    Data,

    /// <summary>Command block.</summary>
    Command,

    /// <summary>Firmware configuration structure.</summary>
    FirmwareConfig,

    /// <summary>Touch control RAM.</summary>
    CtrlRam,

    /// <summary>Customer information.</summary>
    CustomerInformation,

    /// <summary>Checksum or integrity field.</summary>
    Checksum,

    /// <summary>Declared padding bytes.</summary>
    Padding,

    /// <summary>Reserved gap with known reservation.</summary>
    Reserved,

    /// <summary>Explicit gap whose semantics are not established.</summary>
    Unmapped,
}

/// <summary>Non-relaxable physical write constraint.</summary>
public enum FirmwareWriteConstraint
{
    /// <summary>No profile may write the region.</summary>
    Forbidden,

    /// <summary>Only a whole-region write may be authorized.</summary>
    WholeRegion,

    /// <summary>Only declared child regions may be authorized.</summary>
    DeclaredSubregions,

    /// <summary>A profile may authorize checked explicit ranges.</summary>
    ExplicitRange,
}

/// <summary>One immutable physical region in a canonical firmware image map.</summary>
public sealed record FirmwareRegion
{
    /// <summary>Creates a validated physical region.</summary>
    public FirmwareRegion(
        string regionId,
        string? parentRegionId,
        FirmwareRegionOwner owner,
        FirmwareRegionKind kind,
        ByteRange range,
        FirmwareWriteConstraint writeConstraint,
        int alignment = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        if (parentRegionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parentRegionId);
            if (StringComparer.Ordinal.Equals(regionId, parentRegionId))
            {
                throw new ArgumentException("A firmware region cannot be its own parent.", nameof(parentRegionId));
            }
        }

        EnsureDefined(owner, nameof(owner));
        EnsureDefined(kind, nameof(kind));
        EnsureDefined(writeConstraint, nameof(writeConstraint));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);
        if (range.Start % alignment != 0 || range.Length % alignment != 0)
        {
            throw new ArgumentException("Firmware region start and length must satisfy alignment.", nameof(range));
        }

        ValidatePhysicalClassification(owner, kind, writeConstraint);
        RegionId = regionId;
        ParentRegionId = parentRegionId;
        Owner = owner;
        Kind = kind;
        Range = range;
        WriteConstraint = writeConstraint;
        Alignment = alignment;
    }

    /// <summary>Stable physical region identifier.</summary>
    public string RegionId { get; }

    /// <summary>Optional containing region identifier.</summary>
    public string? ParentRegionId { get; }

    /// <summary>Physical byte owner.</summary>
    public FirmwareRegionOwner Owner { get; }

    /// <summary>Physical region kind.</summary>
    public FirmwareRegionKind Kind { get; }

    /// <summary>Half-open range in the owning map address space.</summary>
    public ByteRange Range { get; }

    /// <summary>Non-relaxable physical write constraint.</summary>
    public FirmwareWriteConstraint WriteConstraint { get; }

    /// <summary>Required start and length alignment.</summary>
    public int Alignment { get; }

    private static void ValidatePhysicalClassification(
        FirmwareRegionOwner owner,
        FirmwareRegionKind kind,
        FirmwareWriteConstraint writeConstraint)
    {
        if (kind == FirmwareRegionKind.CtrlRam && owner != FirmwareRegionOwner.Tp)
        {
            throw new ArgumentException("CtrlRAM regions must be physically owned by TP.", nameof(owner));
        }

        if (kind == FirmwareRegionKind.CustomerInformation && owner != FirmwareRegionOwner.Customer)
        {
            throw new ArgumentException(
                "Customer-information regions must be physically owned by the customer.",
                nameof(kind));
        }

        if (kind is FirmwareRegionKind.Reserved or FirmwareRegionKind.Unmapped &&
            writeConstraint != FirmwareWriteConstraint.Forbidden)
        {
            throw new ArgumentException("Reserved and unmapped regions must be forbidden to write.", nameof(kind));
        }

        if (kind == FirmwareRegionKind.Reserved && owner != FirmwareRegionOwner.Reserved)
        {
            throw new ArgumentException("Reserved regions must use the reserved owner.", nameof(owner));
        }

        if (kind == FirmwareRegionKind.Unmapped && owner != FirmwareRegionOwner.Unknown)
        {
            throw new ArgumentException("Unmapped regions must use the unknown owner.", nameof(owner));
        }

        if (owner == FirmwareRegionOwner.Unknown && writeConstraint != FirmwareWriteConstraint.Forbidden)
        {
            throw new ArgumentException("Unknown ownership cannot grant write authority.", nameof(owner));
        }
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown firmware enum value.");
        }
    }
}
