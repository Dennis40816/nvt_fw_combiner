using System.Security.Cryptography;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static string ToSha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
