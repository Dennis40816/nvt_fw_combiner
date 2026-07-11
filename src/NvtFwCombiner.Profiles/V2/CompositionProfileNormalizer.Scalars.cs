using System.Numerics;
using System.Text.Json;
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

    private static JsonElement Require(JsonElement? value, string path)
    {
        return value ?? throw Error(path, "Required integer is missing.");
    }
}
