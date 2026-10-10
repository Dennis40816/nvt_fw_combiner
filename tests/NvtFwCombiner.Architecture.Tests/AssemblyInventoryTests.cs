using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Xml.Linq;
using NvtFwCombiner.Architecture.Tests.Metadata;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Acquisition must reject incomplete, stale or mismatched evidence.</summary>
public sealed class AssemblyInventoryTests
{
    private static readonly BuildTuple _target = new("Release", "net10.0", "");
    private static XElement Entry(string project = "Fixture", string path = "Fixture.dll")
    {
        return new("project", new XAttribute("path", project + ".csproj"), new XAttribute("assembly", path),
            new XAttribute("configuration", _target.Configuration), new XAttribute("framework", _target.Framework), new XAttribute("runtime", _target.Runtime),
            new XAttribute("sha256", Convert.ToHexString(SHA256.HashData(MetadataFixtureBuilder.Create(assembly: project).AsSpan()))));
    }
    private static ImmutableArray<AssemblyInput> Validate(XElement? manifest, Func<string, byte[]>? read = null)
    {
        return AssemblyInventory.Validate(manifest, _target, ["Fixture.csproj", "Second.csproj"], read ?? (path => [.. MetadataFixtureBuilder.Create(assembly: Path.GetFileNameWithoutExtension(path))]));
    }

    private static XElement Fixture()
    {
        return new("inputs", Entry(), Entry("Second", "Second.dll"));
    }

    /// <summary>The normal test build supplies every production assembly.</summary>
    [Fact]
    public void Load_CurrentBuild_CoversProductionAssemblies() { Assert.Equal(15, AssemblyInventory.Load().Length); }
    /// <summary>A complete matching manifest is accepted.</summary>
    [Fact]
    public void Validate_CompleteManifest_Passes() { Assert.Equal(2, Validate(Fixture()).Length); }
    /// <summary>Absence and emptiness are distinct invalid input states.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_MissingOrEmptyManifest_NamesManifestRule(bool missing)
    {
        Assert.Contains("Inputs.Manifest:", Assert.Throws<InvalidDataException>(() => Validate(missing ? null : new XElement("inputs"))).Message, StringComparison.Ordinal);
    }
    /// <summary>Missing, repeated and unlisted projects cannot supply complete evidence.</summary>
    [Theory]
    [InlineData("Missing", "Inputs.Coverage: missing Second.csproj")]
    [InlineData("Duplicate", "Inputs.Coverage: duplicate or unlisted project Fixture.csproj")]
    [InlineData("Unlisted", "Inputs.Coverage: duplicate or unlisted project Extra.csproj")]
    [InlineData("DuplicatePath", "Inputs.Duplicate: Fixture.dll")]
    public void Validate_InvalidCoverage_NamesRuleAndOffender(string scenario, string expected)
    {
        XElement manifest = scenario switch
        {
            "Missing" => new("inputs", Entry()),
            "Duplicate" => new("inputs", Entry(), Entry()),
            "Unlisted" => new("inputs", Entry(), Entry("Extra", "Extra.dll")),
            _ => new("inputs", Entry(), Entry("Second", "Fixture.dll")),
        };
        Assert.Contains(expected, Assert.Throws<InvalidDataException>(() => Validate(manifest)).Message, StringComparison.Ordinal);
    }
    /// <summary>Bytes are checked before metadata traversal.</summary>
    [Theory]
    [InlineData("Missing", "Inputs.Missing: Fixture.dll")]
    [InlineData("Empty", "Inputs.Empty: Fixture.dll")]
    [InlineData("Stale", "Inputs.Stale: Fixture.dll")]
    public void Validate_InvalidAssembly_NamesRuleAndOffender(string scenario, string expected)
    {
        byte[] Read(string path)
        {
            return scenario switch { "Missing" => throw new FileNotFoundException(path), "Empty" => [], _ => [1] };
        }

        Assert.Contains(expected, Assert.Throws<InvalidDataException>(() => Validate(Fixture(), Read)).Message, StringComparison.Ordinal);
    }
    /// <summary>Matching hashes do not excuse wrong or mixed build tuples.</summary>
    [Theory]
    [InlineData("configuration", "Debug", 0)]
    [InlineData("framework", "net9.0", 0)]
    [InlineData("runtime", "win-x64", 0)]
    [InlineData("configuration", "Debug", 1)]
    [InlineData("runtime", "win-x64", 1)]
    public void Validate_WrongOrMixedTuple_NamesTargetRule(string attribute, string value, int index)
    {
        XElement manifest = Fixture();
        manifest.Elements().ElementAt(index).SetAttributeValue(attribute, value);
        Assert.Contains($"Inputs.Target: {(index == 0 ? "Fixture" : "Second")}.csproj", Assert.Throws<InvalidDataException>(() => Validate(manifest)).Message, StringComparison.Ordinal);
    }
}
