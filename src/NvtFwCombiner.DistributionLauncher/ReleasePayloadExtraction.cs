using System.Reflection;

namespace NvtFwCombiner.DistributionLauncher;

internal static class ReleasePayloadExtraction
{
    internal const string Command = "--extract-release-payload";
    internal const string DescriptorResourceName =
        NvtFwCombiner.Bootstrap.ManagedDistributionLauncherHostServices.PayloadAdmissionResourceName;
    internal const string BootstrapResourceName =
        NvtFwCombiner.Bootstrap.ManagedDistributionLauncherHostServices.BootstrapResourceName;
    internal const string DescriptorFileName = "managed-setup-payload-admission.v1.json";
    internal const string BootstrapFileName = "NvtFwCombiner.Bootstrap.exe";

    internal static int Execute(string[] args)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        return Execute(args, assembly.GetManifestResourceStream);
    }

    internal static int Execute(
        IReadOnlyList<string> args,
        Func<string, Stream?> openResource)
    {
        if (args.Count != 2 || !string.Equals(args[0], Command, StringComparison.Ordinal))
        {
            return (int)DistributionLauncherExitCode.HostUnavailable;
        }

        string outputDirectory = Path.GetFullPath(args[1]);
        if (File.Exists(outputDirectory) ||
            (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any()))
        {
            return (int)DistributionLauncherExitCode.HostUnavailable;
        }

        _ = Directory.CreateDirectory(outputDirectory);
        try
        {
            WriteResource(openResource, DescriptorResourceName, Path.Combine(outputDirectory, DescriptorFileName));
            WriteResource(openResource, BootstrapResourceName, Path.Combine(outputDirectory, BootstrapFileName));
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (int)DistributionLauncherExitCode.HostUnavailable;
        }
    }

    private static void WriteResource(
        Func<string, Stream?> openResource,
        string resourceName,
        string outputPath)
    {
        using Stream source = openResource(resourceName) ??
            throw new IOException($"Required embedded release payload is unavailable: {resourceName}");
        using FileStream destination = new(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        source.CopyTo(destination);
        destination.Flush(flushToDisk: true);
    }
}
