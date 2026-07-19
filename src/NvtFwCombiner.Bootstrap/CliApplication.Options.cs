using System.Globalization;

namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static string CreateRunId(string action)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"cli-{action}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
    }
}
