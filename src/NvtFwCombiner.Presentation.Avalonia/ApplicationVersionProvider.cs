using System.Reflection;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Provides the product version exposed by the desktop shell.</summary>
internal static class ApplicationVersionProvider
{
    /// <summary>Gets the product informational version.</summary>
    internal static string InformationalVersion
    {
        get
        {
            Assembly assembly = typeof(ApplicationVersionProvider).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                assembly.GetName().Version?.ToString() ??
                "unknown";
        }
    }

    /// <summary>Gets the shell version label shown in the header.</summary>
    internal static string WorkbenchLabel => $"{InformationalVersion} workbench";
}
