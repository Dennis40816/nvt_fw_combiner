using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Guards the pinned UI event source and its two project consumers.</summary>
public sealed class UiEventSourceConsumptionTests
{
    private const string SourcePath = "src/Nvt.Core/Threading/UiEventRunner.cs";
    private const string CompileSymbol = "NVT_CORE_SOURCE_CONSUMPTION";

    /// <summary>The exact vendored bytes must match the single manifest record.</summary>
    [Fact]
    public void VendoredSourceManifestPinMatchesRawBytes()
    {
        var source = File.ReadAllBytes(RepositoryFile("Vendor", "Core", "UiEventRunner.cs"));
        Assert.True(MatchesManifest(source));
    }

    /// <summary>A one-byte edit invalidates the source pin.</summary>
    [Fact]
    public void VendoredSourceOneByteModifiedRejectsManifestPin()
    {
        var source = File.ReadAllBytes(RepositoryFile("Vendor", "Core", "UiEventRunner.cs"));
        source[0] ^= 1;
        Assert.False(MatchesManifest(source));
    }

    /// <summary>Newline conversion invalidates the source pin.</summary>
    [Fact]
    public void VendoredSourceCrlfConversionRejectsManifestPin()
    {
        var source = File.ReadAllBytes(RepositoryFile("Vendor", "Core", "UiEventRunner.cs"));
        var converted = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(source).Replace("\n", "\r\n", StringComparison.Ordinal));
        Assert.False(MatchesManifest(converted));
    }

    /// <summary>Presentation consumes the linked source with its symbol and no package duplicate.</summary>
    [Fact]
    public void PresentationProjectLinkedSourceIsSafeConsumer()
    {
        Assert.True(IsSafeConsumer(ReadProject("NvtFwCombiner.Presentation.Avalonia")));
    }

    /// <summary>The launcher consumes the linked source with its symbol and no package duplicate.</summary>
    [Fact]
    public void DistributionLauncherProjectLinkedSourceIsSafeConsumer()
    {
        Assert.True(IsSafeConsumer(ReadProject("NvtFwCombiner.DistributionLauncher")));
    }

    /// <summary>A project that copies the source and references the package is rejected.</summary>
    [Fact]
    public void ProjectCopyAndPackageRejectsConsumer()
    {
        Assert.False(IsSafeConsumer(SyntheticProject(includePackage: true)));
    }

    /// <summary>A project that copies the source alone is accepted.</summary>
    [Fact]
    public void ProjectCopyOnlyAcceptsConsumer()
    {
        Assert.True(IsSafeConsumer(SyntheticProject(includePackage: false)));
    }

    private static XDocument ReadProject(string name)
    {
        return XDocument.Load(
        RepositoryFile("src", name, $"{name}.csproj"));
    }

    private static XDocument SyntheticProject(bool includePackage)
    {
        return XDocument.Parse($"""
        <Project>
          <PropertyGroup><DefineConstants>$(DefineConstants);NVT_CORE_SOURCE_CONSUMPTION</DefineConstants></PropertyGroup>
          <ItemGroup>
            <Compile Include="..\..\Vendor\Core\UiEventRunner.cs" Link="Threading\UiEventRunner.cs" />
            {(includePackage ? "<PackageReference Include=\"Nvt.Core\" />" : string.Empty)}
          </ItemGroup>
        </Project>
        """);
    }

    private static bool IsSafeConsumer(XDocument project)
    {
        IEnumerable<XElement> elements = project.Descendants();
        return elements.Where(element => element.Name.LocalName == "Compile")
                .Select(element => (string?)element.Attribute("Include"))
                .Any(include => include?.Replace('\\', '/').EndsWith(
                    "Vendor/Core/UiEventRunner.cs", StringComparison.OrdinalIgnoreCase) == true)
            && elements.Where(element => element.Name.LocalName == "DefineConstants")
                .Any(element => element.Value.Split(';').Contains(CompileSymbol, StringComparer.Ordinal))
            && !elements.Where(element => element.Name.LocalName == "PackageReference")
                .Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))
                .Any(include => string.Equals(include, "Nvt.Core", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesManifest(byte[] source)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(RepositoryFile("Vendor", "Core", "manifest.json")));
        JsonElement files = manifest.RootElement.GetProperty("files");
        JsonElement entry = files[0];

        return files.GetArrayLength() == 1
            && entry.GetProperty("path").GetString() == SourcePath
            && entry.GetProperty("compileSymbol").GetString() == CompileSymbol
            && entry.GetProperty("byteLength").GetInt32() == source.Length
            && entry.GetProperty("sha256").GetString()?.Equals(
                Convert.ToHexStringLower(SHA256.HashData(source)), StringComparison.Ordinal) == true
            && Array.IndexOf(source, (byte)'\r') < 0;
    }

    private static string RepositoryFile(params string[] segments)
    {
        return Path.Combine(RepositoryPaths.FindRepositoryRoot(), Path.Combine(segments));
    }
}
