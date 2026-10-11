using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests.Metadata;

internal sealed record AssemblyInput(string Project, string Path, Guid Mvid, ImmutableArray<byte> Image);
internal sealed record BuildTuple(string Configuration, string Framework, string Runtime);

internal static class AssemblyInventory
{
    internal static ImmutableArray<AssemblyInput> Load()
    {
        var values = typeof(AssemblyInventory).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(item => item.Key, item => item.Value ?? string.Empty, StringComparer.Ordinal);
        var target = new BuildTuple(values["Architecture.Configuration"], values["Architecture.Framework"], values["Architecture.Runtime"]);
        string manifest = Path.Combine(AppContext.BaseDirectory, "architecture-assemblies.xml");
        return LoadManifest(manifest, target, EvaluatedProjectGraphTests.ProductionProjects.Select(EvaluatedProjectGraphTests.ProjectPath), ReadBytes);
    }
    internal static byte[] ReadBytes(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    internal static ImmutableArray<AssemblyInput> LoadManifest(string path, BuildTuple target, IEnumerable<string> expected, Func<string, byte[]> read)
    {
        return !File.Exists(path) ? throw new InvalidDataException($"Inputs.Manifest: missing {Path.GetFileName(path)}")
            : Validate(XElement.Load(path), target, expected, read);
    }
    internal static ImmutableArray<AssemblyInput> Validate(XElement? manifest, BuildTuple target, IEnumerable<string> expectedProjects, Func<string, byte[]> read)
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

            if (new BuildTuple(EvaluatedProjectGraphTests.Value(entry, "configuration"),
                EvaluatedProjectGraphTests.Value(entry, "framework"), EvaluatedProjectGraphTests.Value(entry, "runtime")) != target)
            {
                throw new InvalidDataException($"Inputs.Target: {project}");
            }

            byte[] bytes;
            try { bytes = read(assembly); }
            catch (IOException error) { throw new InvalidDataException($"Inputs.Missing: {assembly}", error); }
            if (bytes.Length == 0) { throw new InvalidDataException($"Inputs.Empty: {assembly}"); }
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
