using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Serializes the MSBuild processes that share repository build inputs.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "This type defines an xUnit test collection.")]
[CollectionDefinition(Name)]
public sealed class ArchitectureMsBuildCollection
{
    /// <summary>The xUnit collection name used by MSBuild-backed architecture checks.</summary>
    public const string Name = nameof(ArchitectureMsBuildCollection);
}

/// <summary>Package trust-index checks that invoke the repository MSBuild target.</summary>
[Collection(ArchitectureMsBuildCollection.Name)]
public sealed partial class PackageTrustIndexBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>MSBuild materialization rejects authority fields and paths outside the normative index.</summary>
    [Theory]
    [InlineData("unknown-field")]
    [InlineData("source-traversal")]
    [InlineData("leading-dot-source")]
    [InlineData("illegal-character-source")]
    [InlineData("canonical-family-wrong-type")]
    [InlineData("metadata-providers-wrong-type")]
    [InlineData("trust-index-version-wrong-type")]
    [InlineData("bundle-version-wrong-type")]
    [InlineData("materialization-schema-wrong-type")]
    [InlineData("canonical-source-wrong-type")]
    [InlineData("metadata-family-version-wrong-type")]
    [InlineData("registration-profile-version-wrong-type")]
    public void BuiltInBundleMaterializationRejectsTrustIndexDrift(string mutation)
    {
        string source = File.ReadAllText(Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "package-trust-index.json"));
        string changed = mutation switch
        {
            "unknown-field" => source.Replace(
                "\"trustIndexId\": \"built-in-profile-bundles\",",
                "\"trustIndexId\": \"built-in-profile-bundles\",\n  \"executablePath\": \"forbidden.exe\",",
                StringComparison.Ordinal),
            "source-traversal" => source.Replace(
                "nt51927-standard-merge/families/nt51927-nt51928.json",
                "../built-in-evil/family.json",
                StringComparison.Ordinal),
            "leading-dot-source" => source.Replace(
                "nt51927-standard-merge/families/nt51927-nt51928.json",
                ".hidden/family.json",
                StringComparison.Ordinal),
            "illegal-character-source" => source.Replace(
                "nt51927-standard-merge/families/nt51927-nt51928.json",
                "invalid path/family.json",
                StringComparison.Ordinal),
            "canonical-family-wrong-type" => MutateTrustIndex(
                source,
                static root =>
                {
                    JsonObject bundle = root["bundles"]!.AsArray()[0]!.AsObject();
                    JsonObject materialization = bundle["materialization"]!.AsObject();
                    materialization["canonicalFirmwareFamily"] = "ignored";
                }),
            "metadata-providers-wrong-type" => MutateTrustIndex(
                source,
                static root =>
                {
                    JsonObject bundle = root["bundles"]!.AsArray()[0]!.AsObject();
                    bundle["metadataProviderFamilies"] = new JsonObject();
                }),
            "trust-index-version-wrong-type" => MutateTrustIndex(
                source,
                static root => root["trustIndexVersion"] = 123),
            "bundle-version-wrong-type" => MutateTrustIndex(
                source,
                static root => root["bundles"]!.AsArray()[0]!["bundleVersion"] = 123),
            "materialization-schema-wrong-type" => MutateTrustIndex(
                source,
                static root => root["bundles"]!.AsArray()[0]!["materialization"]![
                    "compositionProfileSchemaFile"] = 123),
            "canonical-source-wrong-type" => MutateTrustIndex(
                source,
                static root => root["bundles"]!.AsArray()[0]!["materialization"]![
                    "canonicalFirmwareFamily"]!["source"] = 123),
            "metadata-family-version-wrong-type" => MutateTrustIndex(
                source,
                static root => root["bundles"]!.AsArray()
                    .First(static bundle => bundle!["metadataProviderFamilies"] is not null)![
                        "metadataProviderFamilies"]!.AsArray()[0]!["familyVersion"] = 123),
            "registration-profile-version-wrong-type" => MutateTrustIndex(
                source,
                static root => root["bundles"]!.AsArray()[0]!["runtimeRegistrations"]!
                    .AsArray()[0]!["profileVersion"] = 123),
            _ => throw new InvalidOperationException("Unknown trust-index mutation."),
        };
        Assert.NotEqual(source, changed);

        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"nfc-package-trust-index-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(workspace);
        try
        {
            string indexPath = Path.Combine(workspace, "package-trust-index.json");
            File.WriteAllText(indexPath, changed);
            string? sourceRoot = null;
            if (mutation is "leading-dot-source" or "illegal-character-source")
            {
                sourceRoot = Path.Combine(workspace, "built-in");
                CopyDirectory(Path.Combine(Root.FullName, "profiles", "built-in"), sourceRoot);
                string relativePath = mutation == "leading-dot-source"
                    ? ".hidden/family.json"
                    : "invalid path/family.json";
                string target = Path.Combine(
                    sourceRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(
                    Path.Combine(
                        sourceRoot,
                        "nt51927-standard-merge",
                        "families",
                        "nt51927-nt51928.json"),
                    target);
            }
            (int exitCode, string output) = RunMaterialization(
                workspace,
                indexPath,
                sourceRoot);

            Assert.NotEqual(0, exitCode);
            Assert.Contains(
                mutation switch
                {
                    "unknown-field" => "closed package trust-index shape",
                    "source-traversal" or
                    "leading-dot-source" or
                    "illegal-character-source" => "firmware-family paths",
                    "canonical-family-wrong-type" => "materialization must be an object",
                    "metadata-providers-wrong-type" => "families must be an array",
                    "trust-index-version-wrong-type" or
                    "bundle-version-wrong-type" or
                    "materialization-schema-wrong-type" or
                    "canonical-source-wrong-type" or
                    "metadata-family-version-wrong-type" or
                    "registration-profile-version-wrong-type" => "must be a JSON string",
                    _ => throw new InvalidOperationException("Unknown trust-index mutation."),
                },
                output,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>Build materialization verifies the exact bytes named by each trusted bundle manifest.</summary>
    [Fact]
    public void BuiltInBundleMaterializationRejectsEntryHashDriftBeforeCopy()
    {
        string source = File.ReadAllText(Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "package-trust-index.json"));
        JsonObject trustIndex = JsonNode.Parse(source)!.AsObject();
        JsonObject selectedBundle = trustIndex["bundles"]!.AsArray()
            .Select(static node => node!.AsObject())
            .Single(static bundle =>
                bundle["bundleDirectory"]!.GetValue<string>() == "nt51928-dp-replace")
            .DeepClone()
            .AsObject();
        trustIndex["bundles"] = new JsonArray(selectedBundle);

        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"nfc-package-bundle-drift-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(workspace);
        try
        {
            string indexPath = Path.Combine(workspace, "package-trust-index.json");
            File.WriteAllText(
                indexPath,
                trustIndex.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string sourceRoot = Path.Combine(workspace, "built-in");
            string builtInSource = Path.Combine(
                Root.FullName,
                "profiles",
                "built-in");
            CopyDirectory(builtInSource, sourceRoot);
            string copiedBundleRoot = Path.Combine(sourceRoot, "nt51928-dp-replace");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                copiedBundleRoot,
                "profile-bundle.json")));
            string entryPath = manifest.RootElement.GetProperty("entries")
                .EnumerateArray()
                .First(static entry => entry.GetProperty("kind").GetString() == "composition-profile")
                .GetProperty("path")
                .GetString()!;
            File.AppendAllText(Path.Combine(
                copiedBundleRoot,
                entryPath.Replace('/', Path.DirectorySeparatorChar)), "\n");

            (int exitCode, string output) = RunMaterialization(
                workspace,
                indexPath,
                sourceRoot);

            Assert.NotEqual(0, exitCode);
            Assert.True(
                output.Contains("content hash", StringComparison.OrdinalIgnoreCase),
                output);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>Build materialization rejects manifest authority outside its normative schema.</summary>
    [Theory]
    [InlineData("entry-path")]
    [InlineData("schema-id")]
    public void BuiltInBundleMaterializationRejectsManifestSchemaDrift(string mutation)
    {
        JsonObject trustIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "package-trust-index.json")))!.AsObject();
        JsonObject selectedBundle = trustIndex["bundles"]!.AsArray()
            .Select(static node => node!.AsObject())
            .Single(static bundle =>
                bundle["bundleDirectory"]!.GetValue<string>() == "nt51928-dp-replace")
            .DeepClone()
            .AsObject();
        trustIndex["bundles"] = new JsonArray(selectedBundle);

        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"nfc-package-manifest-schema-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(workspace);
        try
        {
            string sourceRoot = Path.Combine(workspace, "built-in");
            CopyDirectory(Path.Combine(Root.FullName, "profiles", "built-in"), sourceRoot);
            string bundleRoot = Path.Combine(sourceRoot, "nt51928-dp-replace");
            string manifestPath = Path.Combine(bundleRoot, "profile-bundle.json");
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            JsonObject entry = manifest["entries"]!.AsArray()
                .Select(static node => node!.AsObject())
                .First(static value =>
                    value["kind"]!.GetValue<string>() == "composition-profile");
            if (mutation == "entry-path")
            {
                string sourcePath = Path.Combine(
                    bundleRoot,
                    entry["path"]!.GetValue<string>()
                        .Replace('/', Path.DirectorySeparatorChar));
                const string invalidPath = "profiles/.hidden.json";
                string invalidSource = Path.Combine(
                    bundleRoot,
                    invalidPath.Replace('/', Path.DirectorySeparatorChar));
                File.Copy(sourcePath, invalidSource);
                entry["path"] = invalidPath;
            }
            else
            {
                entry["schemaId"] = "https://evil.example/schema.json";
            }

            string contentHash = CalculateBundleEntryArrayHash(manifest["entries"]!.AsArray());
            manifest["contentHash"] = contentHash;
            selectedBundle["contentHash"] = contentHash;
            File.WriteAllText(
                manifestPath,
                manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string indexPath = Path.Combine(workspace, "package-trust-index.json");
            File.WriteAllText(
                indexPath,
                trustIndex.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            (int exitCode, string output) = RunMaterialization(
                workspace,
                indexPath,
                sourceRoot);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("entry identity", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string CalculateBundleEntryArrayHash(JsonArray entries)
    {
        JsonObject[] ordered =
        [
            .. entries
                .Select(static node => node!.AsObject())
                .OrderBy(static entry => entry["entryId"]!.GetValue<string>(), StringComparer.Ordinal)
                .ThenBy(static entry => entry["kind"]!.GetValue<string>(), StringComparer.Ordinal)
                .ThenBy(static entry => entry["path"]!.GetValue<string>(), StringComparer.Ordinal)
                .ThenBy(static entry => entry["schemaId"]!.GetValue<string>(), StringComparer.Ordinal)
                .ThenBy(static entry => entry["contentHash"]!.GetValue<string>(), StringComparer.Ordinal),
        ];
        var canonical = new JsonArray();
        foreach (JsonObject entry in ordered)
        {
            canonical.Add(new JsonObject
            {
                ["contentHash"] = entry["contentHash"]!.GetValue<string>(),
                ["entryId"] = entry["entryId"]!.GetValue<string>(),
                ["kind"] = entry["kind"]!.GetValue<string>(),
                ["path"] = entry["path"]!.GetValue<string>(),
                ["schemaId"] = entry["schemaId"]!.GetValue<string>(),
            });
        }

        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToJsonString()))).ToLowerInvariant();
    }

    private static (int ExitCode, string Output) RunMaterialization(
        string workspace,
        string indexPath,
        string? sourceRoot = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "NvtFwCombiner.Bootstrap.csproj"));
        startInfo.ArgumentList.Add("-t:MaterializeBuiltInProfileBundles");
        startInfo.ArgumentList.Add($"-p:BuiltInProfileTrustIndex={indexPath}");
        if (sourceRoot is not null)
        {
            startInfo.ArgumentList.Add($"-p:BuiltInProfileSourceRoot={sourceRoot}");
        }
        startInfo.ArgumentList.Add(
            $"-p:BaseIntermediateOutputPath={Path.Combine(workspace, "obj")}{Path.DirectorySeparatorChar}");
        startInfo.ArgumentList.Add(
            $"-p:OutDir={Path.Combine(workspace, "out")}{Path.DirectorySeparatorChar}");
        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, path));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target);
        }
    }

    private static string MutateTrustIndex(string source, Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(source)?.AsObject()
            ?? throw new InvalidOperationException("The package trust index must contain a JSON object.");
        mutation(root);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>The versioned package trust index is the sole built-in bundle admission list.</summary>
    [Fact]
    public void BuiltInBundleAdmissionIsDataOnlyAndHashClosed()
    {
        string trustIndexPath = Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "package-trust-index.json");
        Assert.True(File.Exists(trustIndexPath), "The package trust index must be checked in.");

        using var document = JsonDocument.Parse(File.ReadAllText(trustIndexPath));
        JsonElement root = document.RootElement;
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("built-in-profile-bundles", root.GetProperty("trustIndexId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("trustIndexVersion").GetString()));
        Assert.Equal("built-in-profile-bundle-v2", root.GetProperty("trustAnchorBindingId").GetString());

        JsonElement[] bundles = [.. root.GetProperty("bundles").EnumerateArray()];
        Assert.Equal(25, bundles.Length);
        Assert.Equal(
            bundles.Length,
            bundles.Select(static bundle => bundle.GetProperty("bundleDirectory").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(bundles, static bundle =>
        {
            Assert.Matches(
                "^[a-z0-9]+(?:-[a-z0-9]+)*$",
                bundle.GetProperty("bundleDirectory").GetString());
            Assert.Matches(
                "^[0-9a-f]{64}$",
                bundle.GetProperty("contentHash").GetString());
            Assert.False(string.IsNullOrWhiteSpace(bundle.GetProperty("bundleVersion").GetString()));
            Assert.Equal("1.0", bundle.GetProperty("bundleSchemaVersion").GetString());
            _ = bundle.GetProperty("materialization");
            _ = bundle.GetProperty("runtimeRegistrations");
        });

        Assert.Equal(
            61,
            bundles.Sum(static bundle =>
                bundle.GetProperty("runtimeRegistrations").GetArrayLength()));
        JsonElement generalReplace = bundles
            .SelectMany(static bundle =>
                bundle.GetProperty("runtimeRegistrations").EnumerateArray())
            .Single(static registration =>
                registration.GetProperty("workflowId").GetString() ==
                    "general-replace");
        Assert.Equal("NT51926", generalReplace.GetProperty("icId").GetString());
        Assert.Equal(
            2,
            bundles.SelectMany(static bundle =>
                    bundle.GetProperty("runtimeRegistrations").EnumerateArray())
                .Count(static registration =>
                    registration.TryGetProperty("mapVariantSetId", out _)));
        Assert.Equal(
            4,
            bundles.Sum(static bundle =>
                bundle.TryGetProperty("metadataProviderFamilies", out JsonElement providers)
                    ? providers.GetArrayLength()
                    : 0));

        string registry = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs");
        string registrations = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs");
        string ctrlRamRoutes = ReadText("src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2RouteRegistry.cs");
        string generalMerge = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs");
        string generalMergePlanning = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs");
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        string packager = ReadText("scripts/package.ps1");
        string releaseSmoke = ReadText("scripts/smoke-release.ps1");
        string metadataResolver = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInCanonicalMetadataDefinitionResolver.cs");
        string trustIndexLoader = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundlePackageTrustIndex.cs");
        string generalReplaceSource = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        Assert.DoesNotContain("new (string Directory, string ContentHash)[]", registry, StringComparison.Ordinal);
        Assert.DoesNotContain("new BuiltInV2Registration(\"NT", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("new(\"NT", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("Route(\"NT", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralMergeV2CandidateProfileVersion", generalMerge, StringComparison.Ordinal);
        Assert.Contains("registration.ProfileVersion", generalMergePlanning, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51927-standard-merge", metadataResolver, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51929-dp-replace", metadataResolver, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2BundleRegistry.TrustIndex.Bundles", metadataResolver, StringComparison.Ordinal);
        Assert.Contains("GeneralReplaceByIc", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("<BuiltInProfileBundle Include=", project, StringComparison.Ordinal);
        Assert.Contains("package-trust-index.json", project, StringComparison.Ordinal);
        Assert.Contains("TrustIndexSchemaPath", project, StringComparison.Ordinal);
        string trustIndexSchemaHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(
            Root.FullName,
            "docs",
            "contracts",
            "profile-bundle-package-trust-index-v1.schema.json")))).ToLowerInvariant();
        Assert.Contains(trustIndexSchemaHash, project, StringComparison.Ordinal);
        Assert.Contains("validator is not bound to the normative schema bytes", project, StringComparison.Ordinal);
        Assert.Contains("DuplicatePropertyNameHandling.Error", project, StringComparison.Ordinal);
        Assert.Contains("additionalProperties", ReadText(
            "docs/contracts/profile-bundle-package-trust-index-v1.schema.json"), StringComparison.Ordinal);
        Assert.Contains("GetFullPath('$(BuiltInProfileSourceRoot)\\')", project, StringComparison.Ordinal);
        Assert.DoesNotContain("new FileInfo(TrustIndexPath).Length", project, StringComparison.Ordinal);
        Assert.DoesNotContain("File.OpenText(TrustIndexPath)", project, StringComparison.Ordinal);
        Assert.Contains("readBoundedFile(TrustIndexPath, 131072)", project, StringComparison.Ordinal);
        Assert.Contains(
            "7aa5e063c0c7f8f059e1032f6719f3cd5be778217f75e6e843f29e04dd6ae47e",
            project,
            StringComparison.Ordinal);
        Assert.Contains("$PackageTrustIndexPackagePath", packager, StringComparison.Ordinal);
        Assert.Contains("$TrustIndex.bundles", packager, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes(path)", trustIndexLoader, StringComparison.Ordinal);
        Assert.Contains("ReadBoundedSnapshot(path)", trustIndexLoader, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GeneralReplaceByIc.Values.Single()",
            generalReplaceSource,
            StringComparison.Ordinal);
        string trustIndexHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(trustIndexPath)))
            .ToLowerInvariant();
        Assert.Contains(trustIndexHash, releaseSmoke, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectNodes(\"//*[local-name()='BuiltInProfileBundle']", packager, StringComparison.Ordinal);
    }
}
