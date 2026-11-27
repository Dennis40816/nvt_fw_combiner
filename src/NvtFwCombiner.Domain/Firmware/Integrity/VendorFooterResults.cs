// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Domain.Firmware.Integrity;

/// <summary>Closed user-input failures from pure footer calculation and construction.</summary>
internal enum VendorFooterError
{
    UnknownCalculator,
    YearOutOfRange,
    InvalidLocalDateTime,
}

/// <summary>An immutable full calculation, or a typed failure with no result bytes.</summary>
internal sealed record VendorFooterCalculationResult
{
    private readonly byte[] _fullResult;

    private VendorFooterCalculationResult(ReadOnlySpan<byte> fullResult, VendorFooterError? error)
    {
        _fullResult = fullResult.ToArray();
        Error = error;
    }

    internal VendorFooterError? Error { get; }

    internal bool Succeeded => Error is null;

    /// <summary>Four little-endian integer bytes, or the complete MD5/SHA digest; empty on failure.</summary>
    internal ReadOnlySpan<byte> FullResult => _fullResult;

    /// <summary>The first four result bytes in their existing order; empty on failure.</summary>
    internal ReadOnlySpan<byte> FooterValue => Succeeded ? _fullResult.AsSpan(0, 4) : [];

    internal static VendorFooterCalculationResult Success(ReadOnlySpan<byte> fullResult)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fullResult.Length, 4);
        return new VendorFooterCalculationResult(fullResult, null);
    }

    internal static VendorFooterCalculationResult Failure(VendorFooterError error)
    {
        return new VendorFooterCalculationResult([], error);
    }
}

/// <summary>An immutable 16-byte footer with its full calculation, or a typed failure with neither.</summary>
internal sealed record VendorFooterBuildResult
{
    private readonly byte[] _bytes;

    private VendorFooterBuildResult(
        ReadOnlySpan<byte> bytes,
        VendorFooterCalculationResult? calculation,
        VendorFooterError? error)
    {
        _bytes = bytes.ToArray();
        Calculation = calculation;
        Error = error;
    }

    internal VendorFooterError? Error { get; }

    internal bool Succeeded => Error is null;

    /// <summary>Exactly 16 bytes on success; empty on failure.</summary>
    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>The complete calculation for later reports; absent on failure.</summary>
    internal VendorFooterCalculationResult? Calculation { get; }

    internal static VendorFooterBuildResult Success(ReadOnlySpan<byte> bytes, VendorFooterCalculationResult calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        ArgumentOutOfRangeException.ThrowIfNotEqual(bytes.Length, 16);
        DomainInvariant.Reject(!calculation.Succeeded, "A footer requires a successful calculation.", nameof(calculation));
        return new VendorFooterBuildResult(bytes, calculation, null);
    }

    internal static VendorFooterBuildResult Failure(VendorFooterError error)
    {
        return new VendorFooterBuildResult([], null, error);
    }
}
