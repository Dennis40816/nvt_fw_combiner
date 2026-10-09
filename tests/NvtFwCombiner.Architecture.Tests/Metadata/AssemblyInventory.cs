using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests.Metadata;

internal sealed record AssemblyInput(string Project, string Path, Guid Mvid, ImmutableArray<byte> Image);

internal static class AssemblyInventory
{
    internal static ImmutableArray<AssemblyInput> Load()
    {
        string manifest = Path.Combine(AppContext.BaseDirectory, "architecture-assemblies.xml");
        return !File.Exists(manifest)
            ? throw new InvalidDataException("Inputs.Manifest: missing architecture-assemblies.xml")
            : Validate(XElement.Load(manifest), EvaluatedProjectGraphTests.ProductionProjects.Select(EvaluatedProjectGraphTests.ProjectPath), File.ReadAllBytes);
    }
    internal static ImmutableArray<AssemblyInput> Validate(XElement? manifest, IEnumerable<string> expectedProjects, Func<string, byte[]> read)
    {
        if (manifest is null || !manifest.Elements("project").Any())
        {
            throw new InvalidDataException("Inputs.Manifest: missing or empty manifest");
        }

        var expected = expectedProjects.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<AssemblyInput>.Builder result = ImmutableArray.CreateBuilder<AssemblyInput>();
        foreach (XElement entry in manifest.Elements("project"))
        {
            string project = EvaluatedProjectGraphTests.Value(entry, "path");
            string assembly = EvaluatedProjectGraphTests.Value(entry, "assembly");
            if (!expected.Remove(project))
            {
                throw new InvalidDataException($"Inputs.Coverage: duplicate or unlisted project {project}");
            }

            if (!paths.Add(assembly))
            {
                throw new InvalidDataException($"Inputs.Duplicate: {assembly}");
            }

            if (EvaluatedProjectGraphTests.Value(entry, "framework") != "net10.0"
                || EvaluatedProjectGraphTests.Value(entry, "configuration") is not ("Debug" or "Release")
                || EvaluatedProjectGraphTests.Value(entry, "runtime") is not ("" or "win-x64"))
            {
                throw new InvalidDataException($"Inputs.Target: {project}");
            }

            byte[] bytes;
            try { bytes = read(assembly); }
            catch (IOException error) { throw new InvalidDataException($"Inputs.Missing: {assembly}", error); }
            if (!StringComparer.OrdinalIgnoreCase.Equals(Convert.ToHexString(SHA256.HashData(bytes)), EvaluatedProjectGraphTests.Value(entry, "sha256")))
            {
                throw new InvalidDataException($"Inputs.Stale: {assembly}");
            }

            ImmutableArray<byte> image = [.. bytes];
            using var pe = new PEReader(image);
            MetadataReader metadata = pe.GetMetadataReader();
            string identity = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            if (identity != System.IO.Path.GetFileNameWithoutExtension(project))
            {
                throw new InvalidDataException($"Inputs.Identity: {project} -> {identity}");
            }

            if (!identities.Add(identity))
            {
                throw new InvalidDataException($"Inputs.Duplicate: {identity}");
            }

            result.Add(new AssemblyInput(project, assembly, metadata.GetGuid(metadata.GetModuleDefinition().Mvid), image));
        }
        return expected.Count != 0
            ? throw new InvalidDataException($"Inputs.Coverage: missing {string.Join(", ", expected.Order(StringComparer.Ordinal))}")
            : result.ToImmutable();
    }
}
