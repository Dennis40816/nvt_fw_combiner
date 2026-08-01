using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>MSBuild materialization rejects authority fields and paths outside the normative index.</summary>
    [Theory]
    [InlineData("unknown-field")]
    [InlineData("source-traversal")]
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
            startInfo.ArgumentList.Add($"-p:BaseIntermediateOutputPath={Path.Combine(workspace, "obj")}{Path.DirectorySeparatorChar}");
            startInfo.ArgumentList.Add($"-p:OutDir={Path.Combine(workspace, "out")}{Path.DirectorySeparatorChar}");
            using Process process = Process.Start(startInfo)!;
            string output = process.StandardOutput.ReadToEnd() +
                process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(
                mutation switch
                {
                    "unknown-field" => "closed package trust-index shape",
                    "source-traversal" => "firmware-family paths",
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

        string registry = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs");
        string registrations = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        string ctrlRamRoutes = ReadText("src/NvtFwCombiner.Bootstrap/CtrlRamV2RouteRegistry.cs");
        string generalMerge = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.V2.cs");
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        string packager = ReadText("scripts/package.ps1");
        string metadataResolver = ReadText(
            "src/NvtFwCombiner.Bootstrap/BuiltInCanonicalMetadataDefinitionResolver.cs");
        Assert.DoesNotContain("new (string Directory, string ContentHash)[]", registry, StringComparison.Ordinal);
        Assert.DoesNotContain("new BuiltInV2Registration(\"NT", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("new(\"NT", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("Route(\"NT", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalDynamicRouteInventory.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralMergeV2CandidateProfileVersion", generalMerge, StringComparison.Ordinal);
        Assert.Contains("registration.ProfileVersion", generalMerge, StringComparison.Ordinal);
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
        Assert.Contains("$PackageTrustIndexPackagePath", packager, StringComparison.Ordinal);
        Assert.Contains("$TrustIndex.bundles", packager, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectNodes(\"//*[local-name()='BuiltInProfileBundle']", packager, StringComparison.Ordinal);
    }
}
