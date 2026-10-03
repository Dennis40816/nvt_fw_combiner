using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DesktopCaptureProcessTests
{
    /// <summary>A stale copied assembly fails; an older executable does not reject current Presentation output.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void DesktopHostMustContainCurrentPresentationOutput(int copiedAssemblyAgeMinutes)
    {
        using var workspace = TempWorkspace.Create("desktop-host-freshness");
        string executable = workspace.Write("NvtFwCombiner.Desktop.exe", []);
        string presentation = workspace.Write("NvtFwCombiner.Presentation.Avalonia.dll", []);
        string currentPresentation = workspace.Write("presentation-build/NvtFwCombiner.Presentation.Avalonia.dll", []);
        var assemblyTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(currentPresentation, assemblyTime);
        File.SetLastWriteTimeUtc(presentation, assemblyTime.AddMinutes(copiedAssemblyAgeMinutes));
        File.SetLastWriteTimeUtc(executable, assemblyTime.AddMinutes(Math.Min(-1, copiedAssemblyAgeMinutes)));

        if (copiedAssemblyAgeMinutes < 0)
        {
            Exception failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => RequireCurrentDesktopHost(executable, currentPresentation));
            Assert.Contains("Build the Desktop host first", failure.Message, StringComparison.Ordinal);
            Assert.Contains("src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj", failure.Message, StringComparison.Ordinal);
        }
        else
        {
            RequireCurrentDesktopHost(executable, currentPresentation);
        }
    }

    private static void RequireCurrentDesktopHost(string executable, string currentPresentation)
    {
        string presentation = Path.Combine(Path.GetDirectoryName(executable)!, "NvtFwCombiner.Presentation.Avalonia.dll");
        const string instruction = "Build the Desktop host first: dotnet build src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj -c Release --no-restore.";
        Assert.True(File.Exists(presentation), $"The Desktop Presentation assembly is required: {presentation}. {instruction}");
        Assert.True(File.Exists(currentPresentation), $"The project's Presentation output is required: {currentPresentation}. {instruction}");
        Assert.True(File.GetLastWriteTimeUtc(presentation) >= File.GetLastWriteTimeUtc(currentPresentation),
            $"The Desktop Presentation copy is older than the project's output: {presentation}; {currentPresentation}. {instruction}");
    }
}
