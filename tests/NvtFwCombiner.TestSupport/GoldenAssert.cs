// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Xunit.Sdk;

namespace NvtFwCombiner.TestSupport;

/// <summary>Compares firmware without exposing its contents in assertion diagnostics.</summary>
public static class GoldenAssert
{
    private static readonly object _safeMismatchMarker = new();

    /// <summary>Compares complete spans using only lengths, an offset and abbreviated hashes on failure.</summary>
    public static void AssertEqualsGolden(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, string label)
    {
        if (expected.SequenceEqual(actual))
        {
            return;
        }

        int offset = 0;
        while (offset < expected.Length && offset < actual.Length && expected[offset] == actual[offset])
        {
            offset++;
        }

        throw CreateMismatch(expected, actual, label, offset);
    }

    /// <summary>Compares byte arrays without rendering any byte values.</summary>
    public static void AssertEqualsGolden(byte[]? expected, byte[]? actual, string label)
    {
        if (expected is null && actual is null)
        {
            return;
        }

        if (expected is null || actual is null)
        {
            throw CreateMismatch(expected.AsSpan(), actual.AsSpan(), label, 0);
        }

        AssertEqualsGolden(expected.AsSpan(), actual.AsSpan(), label);
    }

    private static XunitException CreateMismatch(
        ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, string label, int offset)
    {
        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(expected))[..12];
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(actual))[..12];
        var failure = new XunitException(FormattableString.Invariant(
            $"Golden mismatch [{label}]: expected {expected.Length} bytes, actual {actual.Length} bytes, first difference at offset 0x{offset:X}, expected sha256 {expectedHash}, actual sha256 {actualHash}."));
        failure.Data[_safeMismatchMarker] = true;
        return failure;
    }

    /// <summary>Checks every item while exposing only trusted golden mismatch diagnostics.</summary>
    public static void AssertAllGolden<T>(IEnumerable<T> items, Action<T> assertion)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(assertion);
        XunitException? mismatch = null;
        bool otherFailure = false;
        try
        {
            Xunit.Assert.All(items, item =>
            {
                try
                {
                    assertion(item);
                }
                catch (XunitException failure) when (failure.Data.Contains(_safeMismatchMarker))
                {
                    mismatch ??= failure;
                }
            });
        }
        catch (AllException)
        {
            otherFailure = true;
        }

        if (mismatch is not null)
        {
            ExceptionDispatchInfo.Capture(mismatch).Throw();
        }

        if (otherFailure)
        {
            throw new XunitException("Golden sequence check could not complete.");
        }
    }

    /// <summary>Reads an expected file and compares its complete contents without exposing its path.</summary>
    public static void AssertFileEqualsGolden(string expectedPath, ReadOnlySpan<byte> actual, string label)
    {
        byte[] expected;
        try
        {
            using FileStream stream = File.OpenRead(expectedPath);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            expected = buffer.ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new XunitException("Golden expected file could not be read.");
        }

        AssertEqualsGolden(expected.AsSpan(), actual, label);
    }
}

/// <summary>Provides anonymous case labels unless local verbose diagnostics are explicitly enabled.</summary>
public static class GoldenDiagnostics
{
    /// <summary>Checks captured text without passing its contents to xUnit diagnostics.</summary>
    public static bool Contains(string expectedSubstring, string? actual, StringComparison comparison = StringComparison.CurrentCulture)
    {
        return actual?.Contains(expectedSubstring, comparison) == true;
    }

    /// <summary>Checks a captured text prefix without rendering its contents.</summary>
    public static bool StartsWith(string expectedPrefix, string? actual, StringComparison comparison = StringComparison.CurrentCulture)
    {
        return actual?.StartsWith(expectedPrefix, comparison) == true;
    }

    /// <summary>Checks a captured text suffix without rendering its contents.</summary>
    public static bool EndsWith(string expectedSuffix, string? actual, StringComparison comparison = StringComparison.CurrentCulture)
    {
        return actual?.EndsWith(expectedSuffix, comparison) == true;
    }

    /// <summary>Summarizes captured text without exposing its contents.</summary>
    public static string TextSummary(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
        return FormattableString.Invariant($"length {value.Length}, sha256 {hash}");
    }

    /// <summary>Returns the case id only when NFC_GOLDEN_VERBOSE equals exactly 1.</summary>
    public static string Label(string caseId)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        return Environment.ExpandEnvironmentVariables("%NFC_GOLDEN_VERBOSE%") == "1"
            ? caseId
            : "case-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(caseId)))[..12];
    }
}
