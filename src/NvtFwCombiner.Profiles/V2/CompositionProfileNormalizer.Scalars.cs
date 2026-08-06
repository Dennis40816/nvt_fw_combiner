using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.Normalization;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    private static BigInteger ReadInteger(JsonElement value, string path)
    {
        try
        {
            return ContractJsonValueReader.ReadInteger(value);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static long ReadInt64(JsonElement value, long minimum, long maximum, string path)
    {
        BigInteger integer = ReadInteger(value, path);
        return integer < minimum || integer > maximum
            ? throw Error(path, $"Integer must be between {minimum} and {maximum}.")
            : (long)integer;
    }

    private static byte ReadByte(JsonElement value, string path)
    {
        return (byte)ReadInt64(value, byte.MinValue, byte.MaxValue, path);
    }

    private static ulong ReadUInt64(JsonElement value, string path)
    {
        BigInteger integer = ReadInteger(value, path);
        if (integer.Sign < 0)
        {
            throw Error(path, $"Integer must be between {ulong.MinValue} and {ulong.MaxValue}.");
        }

        try
        {
            return (ulong)integer;
        }
        catch (OverflowException exception)
        {
            throw Error(
                path,
                $"Integer must be between {ulong.MinValue} and {ulong.MaxValue}.",
                exception);
        }
    }

    private static CompiledValidationBytes ReadBytes(string value, string path)
    {
        try
        {
            return new CompiledValidationBytes(ContractJsonValueReader.ParseCanonicalHex(value));
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static string ReadString(JsonElement value, string path)
    {
        try
        {
            return ContractJsonValueReader.ReadString(value);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static JsonElement Require(JsonElement? value, string path)
    {
        return value ?? throw Error(path, "Required integer is missing.");
    }
}
