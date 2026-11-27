// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace NvtFwCombiner.Domain.Firmware.Integrity;

/// <summary>Pure named calculators, independent of profile-owned method codes.</summary>
internal static class VendorFooterCalculator
{
    /// <summary>
    /// Covers exactly the supplied span without modifying it. Integer results are four little-endian bytes;
    /// digests retain their full digest byte order. Names are exact, ordinal identifiers.
    /// </summary>
    internal static VendorFooterCalculationResult Calculate(string? calculatorName, ReadOnlySpan<byte> coveredBytes)
    {
        return calculatorName switch
        {
            "checksum32" => VendorFooterCalculationResult.Success(CalculateChecksum32(coveredBytes)),
            "crc32" => VendorFooterCalculationResult.Success(CalculateCrc32(coveredBytes)),
            "md5" => VendorFooterCalculationResult.Success(CalculateMd5(coveredBytes)),
            "sha1" => VendorFooterCalculationResult.Success(CalculateSha1(coveredBytes)),
            "sha256" => VendorFooterCalculationResult.Success(SHA256.HashData(coveredBytes)),
            _ => VendorFooterCalculationResult.Failure(VendorFooterError.UnknownCalculator),
        };
    }

    private static byte[] CalculateChecksum32(ReadOnlySpan<byte> coveredBytes)
    {
        uint sum = 0;
        foreach (byte value in coveredBytes)
        {
            sum = unchecked(sum + value);
        }

        return LittleEndianBytes(unchecked(0U - sum));
    }

    private static byte[] CalculateCrc32(ReadOnlySpan<byte> coveredBytes)
    {
        // CRC-32/ISO-HDLC: reflected IEEE 802.3 polynomial, initial and final XOR 0xFFFFFFFF.
        uint crc = uint.MaxValue;
        foreach (byte value in coveredBytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1U) == 0 ? crc >> 1 : (crc >> 1) ^ 0xEDB88320U;
            }
        }

        return LittleEndianBytes(~crc);
    }

    [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "The vendor format requires MD5 as a compatibility checksum, not a security primitive.")]
    private static byte[] CalculateMd5(ReadOnlySpan<byte> coveredBytes)
    {
        return MD5.HashData(coveredBytes);
    }

    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "The vendor format requires SHA-1 as a compatibility checksum, not a security primitive.")]
    private static byte[] CalculateSha1(ReadOnlySpan<byte> coveredBytes)
    {
        return SHA1.HashData(coveredBytes);
    }

    private static byte[] LittleEndianBytes(uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
}
