namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Keeps the desktop executable as wiring while Presentation consumes Application ports directly.</summary>
    [Fact]
    public void DesktopHostOwnsBootstrapWiringOutsidePresentation()
    {
        string presentationProject = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj");
        string desktopProject = ReadText(
            "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj");
        string desktopProgram = ReadText(
            "src/NvtFwCombiner.Desktop/Program.cs");
        string solution = ReadText("NvtFwCombiner.slnx");
        string presentationSources = ReadPresentationSources();

        Assert.DoesNotContain("NvtFwCombiner.Bootstrap.csproj", presentationProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<OutputType>WinExe</OutputType>", presentationProject, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Bootstrap.csproj", desktopProject, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Presentation.Avalonia.csproj", desktopProject, StringComparison.Ordinal);
        Assert.Contains("<OutputType>WinExe</OutputType>", desktopProject, StringComparison.Ordinal);
        Assert.Contains("<AssemblyName>NvtFwCombiner.Desktop</AssemblyName>", desktopProject, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(desktopProgram, "CompositionHostServices.Create()"));
        Assert.Equal(1, CountOccurrences(desktopProgram, "new PresentationHostServices("));
        Assert.Equal(2, CountOccurrences(desktopProgram, "CreatePresentationHostServices"));
        Assert.Contains(
            "DesktopApplication.Run(CreatePresentationHostServices, args)",
            desktopProgram,
            StringComparison.Ordinal);
        Assert.Contains("src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("DesktopCompositionRoot", presentationSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionHostServices", presentationSources, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Presentation.Avalonia/Program.cs")));
    }
}
