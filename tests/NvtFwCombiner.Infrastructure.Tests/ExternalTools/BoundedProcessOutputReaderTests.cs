using System.Buffers;
using System.Text;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Contracts for bounded, deadlock-free external-process diagnostics.</summary>
public sealed class BoundedProcessOutputReaderTests
{
    /// <summary>Small process output remains byte-for-character exact.</summary>
    [Fact]
    public async Task SmallOutputRemainsExact()
    {
        const string expected = "first line\r\nsecond line";
        using var reader = new StringReader(expected);

        string actual = await BoundedProcessOutputReader.ReadAsync(reader);

        Assert.Equal(expected, actual);
    }

    /// <summary>Output exactly at the cap remains complete and receives no truncation marker.</summary>
    [Fact]
    public async Task ExactCaptureLimitRemainsExact()
    {
        string expected = new('X', BoundedProcessOutputReader.MaximumCapturedCharacters);
        using var reader = new StringReader(expected);

        string actual = await BoundedProcessOutputReader.ReadAsync(reader);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain(BoundedProcessOutputReader.TruncationMarker, actual, StringComparison.Ordinal);
    }

    /// <summary>The first truncated character retains the exact prefix, marker, and suffix.</summary>
    [Fact]
    public async Task FirstTruncatedCharacterRetainsExactPrefixAndTail()
    {
        string input = CreatePattern(BoundedProcessOutputReader.MaximumCapturedCharacters + 1);
        int prefixLength = BoundedProcessOutputReader.MaximumCapturedCharacters / 2;
        int tailLength = BoundedProcessOutputReader.MaximumCapturedCharacters -
            prefixLength -
            BoundedProcessOutputReader.TruncationMarker.Length;
        string expected = input[..prefixLength] +
            BoundedProcessOutputReader.TruncationMarker +
            input[^tailLength..];
        using var reader = new StringReader(input);

        string actual = await BoundedProcessOutputReader.ReadAsync(reader);

        Assert.Equal(expected, actual);
    }

    /// <summary>Multiple ring wraps preserve the exact deterministic suffix ordering.</summary>
    [Fact]
    public async Task MultipleRingWrapsRetainExactOrderedTail()
    {
        string input = CreatePattern((BoundedProcessOutputReader.MaximumCapturedCharacters * 3) + 137);
        int prefixLength = BoundedProcessOutputReader.MaximumCapturedCharacters / 2;
        int tailLength = BoundedProcessOutputReader.MaximumCapturedCharacters -
            prefixLength -
            BoundedProcessOutputReader.TruncationMarker.Length;
        string expected = input[..prefixLength] +
            BoundedProcessOutputReader.TruncationMarker +
            input[^tailLength..];
        using var reader = new StringReader(input);

        string actual = await BoundedProcessOutputReader.ReadAsync(reader);

        Assert.Equal(expected, actual);
    }

    /// <summary>A surrogate split at the prefix boundary is omitted rather than published unpaired.</summary>
    [Fact]
    public async Task TruncationDoesNotRetainUnpairedHighSurrogateBeforeMarker()
    {
        int prefixLength = BoundedProcessOutputReader.MaximumCapturedCharacters / 2;
        string expectedPrefix = new('P', prefixLength - 1);
        string input = expectedPrefix + "\U0001F600" + new string('T', BoundedProcessOutputReader.MaximumCapturedCharacters);
        int tailLength = BoundedProcessOutputReader.MaximumCapturedCharacters -
            expectedPrefix.Length -
            BoundedProcessOutputReader.TruncationMarker.Length;
        string expected = expectedPrefix +
            BoundedProcessOutputReader.TruncationMarker +
            new string('T', tailLength);
        using var reader = new StringReader(input);

        string actual = await BoundedProcessOutputReader.ReadAsync(reader);

        Assert.Equal(expected, actual);
        AssertWellFormedUtf16(actual);
    }

    /// <summary>A retained tail starts at the next scalar when its raw boundary lands on a low surrogate.</summary>
    [Fact]
    public async Task TruncationDoesNotRetainUnpairedLowSurrogateAtTailStart()
    {
        int prefixLength = BoundedProcessOutputReader.MaximumCapturedCharacters / 2;
        int rawTailLength = BoundedProcessOutputReader.MaximumCapturedCharacters -
            prefixLength -
            BoundedProcessOutputReader.TruncationMarker.Length;
        string input = new string('A', BoundedProcessOutputReader.MaximumCapturedCharacters) +
            "\U0001F600" +
            new string('T', rawTailLength - 1);
        string expected = new string('A', prefixLength) +
            BoundedProcessOutputReader.TruncationMarker +
            new string('T', rawTailLength - 1);
        using var reader = new StringReader(input);

        string actual = await BoundedProcessOutputReader.ReadAsync(reader);

        Assert.Equal(expected, actual);
        AssertWellFormedUtf16(actual);
    }

    private static string CreatePattern(int length)
    {
        return string.Create(length, 0, static (buffer, _) =>
        {
            for (int index = 0; index < buffer.Length; index++)
            {
                buffer[index] = (char)('!' + (index % 90));
            }
        });
    }

    private static void AssertWellFormedUtf16(string value)
    {
        ReadOnlySpan<char> remaining = value;
        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(remaining, out _, out int consumed);
            Assert.Equal(OperationStatus.Done, status);
            remaining = remaining[consumed..];
        }
    }
}
