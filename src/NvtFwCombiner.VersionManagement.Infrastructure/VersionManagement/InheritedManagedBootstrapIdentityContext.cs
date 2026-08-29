using System.Diagnostics;
using System.Globalization;
using System.Security;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Carries one exact Root Bootstrap identity through the managed process chain.</summary>
internal static class InheritedManagedBootstrapIdentityContext
{
    internal const string EnvironmentName = "NVT_FW_COMBINER_ROOT_BOOTSTRAP_IDENTITY";
    private const string BootstrapFileName = "NvtFwCombiner.Bootstrap.exe";
    private const int MaximumSerializedCharacters = 128;

    internal static void Apply(
        ProcessStartInfo startInfo,
        ManagedImmutableBootstrapIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(identity.FileName, BootstrapFileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Inherited identity must name the Root Bootstrap.", nameof(identity));
        }
        string serialized = string.Create(
            CultureInfo.InvariantCulture,
            $"1|{identity.FileName}|{identity.Length}|{identity.Sha256}");
        if (serialized.Length > MaximumSerializedCharacters)
        {
            throw new ArgumentException("Inherited Bootstrap identity is oversized.", nameof(identity));
        }
        startInfo.Environment[EnvironmentName] = serialized;
    }

    internal static ManagedImmutableBootstrapIdentity? CaptureAndClear()
    {
        return CaptureAndClear(
            Environment.GetEnvironmentVariable,
            Environment.SetEnvironmentVariable);
    }

    internal static ManagedImmutableBootstrapIdentity? CaptureAndClear(
        Func<string, string?> read,
        Action<string, string?> clear)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(clear);
        string? serialized;
        try
        {
            serialized = read(EnvironmentName);
            clear(EnvironmentName, null);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityException)
        {
            return null;
        }
        if (serialized is null ||
            serialized.Length is 0 or > MaximumSerializedCharacters)
        {
            return null;
        }
        string[] parts = serialized.Split('|');
        if (parts is not ["1", BootstrapFileName, _, _] ||
            !long.TryParse(
                parts[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long length))
        {
            return null;
        }
        try
        {
            return new ManagedImmutableBootstrapIdentity(parts[1], length, parts[3]);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
