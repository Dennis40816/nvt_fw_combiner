using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private const string EntryHashDriftCaseId = "entry-hash-drift";
    private static int _materializationDriverLaunchCount;
    private static readonly string[] TrustIndexMutations =
    [
        "unknown-field",
        "source-traversal",
        "leading-dot-source",
        "illegal-character-source",
        "canonical-family-wrong-type",
        "metadata-providers-wrong-type",
        "trust-index-version-wrong-type",
        "bundle-version-wrong-type",
        "materialization-schema-wrong-type",
        "canonical-source-wrong-type",
        "metadata-family-version-wrong-type",
        "registration-profile-version-wrong-type",
    ];
    private static readonly Lazy<ReadOnlyDictionary<string, MaterializationResult>>
        MaterializationBatch = new(
            CreateMaterializationBatch,
            LazyThreadSafetyMode.ExecutionAndPublication);

    private static MaterializationResult GetTrustIndexMaterializationResult(string mutation)
    {
        return GetMaterializationResult($"trust-index-{mutation}");
    }

    private static MaterializationResult GetManifestMaterializationResult(string mutation)
    {
        return GetMaterializationResult($"manifest-{mutation}");
    }

    private static MaterializationResult GetMaterializationResult(string caseId)
    {
        return MaterializationBatch.Value.TryGetValue(caseId, out MaterializationResult? result)
            ? result
            : throw new InvalidOperationException(
                $"The package-trust materialization batch did not produce '{caseId}'.");
    }

    private static ReadOnlyDictionary<string, MaterializationResult> CreateMaterializationBatch()
    {
        string batchRoot = Path.Combine(
            Path.GetTempPath(),
            $"nfc-package-materialization-batch-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(batchRoot);
        try
        {
            List<MaterializationCase> cases =
            [
                .. TrustIndexMutations.Select(
                    mutation => PrepareTrustIndexDriftCase(batchRoot, mutation)),
                PrepareEntryHashDriftCase(batchRoot),
                PrepareManifestSchemaDriftCase(batchRoot, "entry-path"),
                PrepareManifestSchemaDriftCase(batchRoot, "schema-id"),
            ];
            if (cases.Count != 15 ||
                cases.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count() != 15)
            {
                throw new InvalidOperationException(
                    "The package-trust materialization batch must contain 15 distinct cases.");
            }

            string driverPath = Path.Combine(batchRoot, "PackageTrustMaterializationBatch.proj");
            string completionPath = Path.Combine(batchRoot, "complete.txt");
            WriteMaterializationDriver(driverPath, completionPath, cases);
            BatchProcessResult processResult = RunMaterializationDriver(driverPath);
            return ParseMaterializationResults(cases, completionPath, processResult);
        }
        finally
        {
            Directory.Delete(batchRoot, recursive: true);
        }
    }

    private static MaterializationCase PrepareTrustIndexDriftCase(
        string batchRoot,
        string mutation)
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
        if (StringComparer.Ordinal.Equals(source, changed))
        {
            throw new InvalidOperationException(
                $"Trust-index mutation '{mutation}' did not change its input.");
        }

        string workspace = CreateCaseWorkspace(batchRoot, $"trust-index-{mutation}");
        string sourceRoot = Path.Combine(Root.FullName, "profiles", "built-in");
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

        string indexPath = Path.Combine(workspace, "package-trust-index.json");
        File.WriteAllText(indexPath, changed);
        return CreateMaterializationCase(
            $"trust-index-{mutation}",
            workspace,
            indexPath,
            sourceRoot);
    }

    private static MaterializationCase PrepareEntryHashDriftCase(string batchRoot)
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

        string workspace = CreateCaseWorkspace(batchRoot, EntryHashDriftCaseId);
        string sourceRoot = Path.Combine(workspace, "built-in");
        CopyDirectory(Path.Combine(Root.FullName, "profiles", "built-in"), sourceRoot);
        string copiedBundleRoot = Path.Combine(sourceRoot, "nt51928-dp-replace");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            copiedBundleRoot,
            "profile-bundle.json")));
        string entryPath = manifest.RootElement.GetProperty("entries")
            .EnumerateArray()
            .First(static entry =>
                entry.GetProperty("kind").GetString() == "composition-profile")
            .GetProperty("path")
            .GetString()!;
        File.AppendAllText(
            Path.Combine(
                copiedBundleRoot,
                entryPath.Replace('/', Path.DirectorySeparatorChar)),
            "\n");

        string indexPath = Path.Combine(workspace, "package-trust-index.json");
        File.WriteAllText(
            indexPath,
            trustIndex.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return CreateMaterializationCase(
            EntryHashDriftCaseId,
            workspace,
            indexPath,
            sourceRoot);
    }

    private static MaterializationCase PrepareManifestSchemaDriftCase(
        string batchRoot,
        string mutation)
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

        string caseId = $"manifest-{mutation}";
        string workspace = CreateCaseWorkspace(batchRoot, caseId);
        string sourceRoot = Path.Combine(workspace, "built-in");
        CopyDirectory(Path.Combine(Root.FullName, "profiles", "built-in"), sourceRoot);
        string bundleRoot = Path.Combine(sourceRoot, "nt51928-dp-replace");
        string manifestPath = Path.Combine(bundleRoot, "profile-bundle.json");
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        JsonObject entry = manifest["entries"]!.AsArray()
            .Select(static node => node!.AsObject())
            .First(static value =>
                value["kind"]!.GetValue<string>() == "composition-profile");
        switch (mutation)
        {
            case "entry-path":
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
                break;
            case "schema-id":
                entry["schemaId"] = "https://evil.example/schema.json";
                break;
            default:
                throw new InvalidOperationException("Unknown manifest mutation.");
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
        return CreateMaterializationCase(caseId, workspace, indexPath, sourceRoot);
    }

    private static string CreateCaseWorkspace(string batchRoot, string caseId)
    {
        string workspace = Path.Combine(batchRoot, caseId);
        _ = Directory.CreateDirectory(workspace);
        return workspace;
    }

    private static MaterializationCase CreateMaterializationCase(
        string caseId,
        string workspace,
        string indexPath,
        string sourceRoot)
    {
        return new MaterializationCase(
            caseId,
            workspace,
            indexPath,
            sourceRoot,
            Path.Combine(workspace, "result.txt"));
    }

    private static void WriteMaterializationDriver(
        string driverPath,
        string completionPath,
        IReadOnlyList<MaterializationCase> cases)
    {
        string bootstrapProject = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "NvtFwCombiner.Bootstrap.csproj");
        var targets = new List<XElement>(cases.Count + 1);
        var targetNames = new List<string>(cases.Count);
        for (int index = 0; index < cases.Count; index++)
        {
            MaterializationCase item = cases[index];
            string targetName = $"RunCase{index:D2}";
            targetNames.Add(targetName);
            string properties = string.Join(
                ";",
                "Configuration=Release",
                $"BuiltInProfileTrustIndex={item.IndexPath}",
                $"BuiltInProfileSourceRoot={item.SourceRoot}",
                $"BaseIntermediateOutputPath={Path.Combine(item.Workspace, "obj")}{Path.DirectorySeparatorChar}",
                $"OutDir={Path.Combine(item.Workspace, "out")}{Path.DirectorySeparatorChar}");
            targets.Add(new XElement(
                "Target",
                new XAttribute("Name", targetName),
                new XElement(
                    "Message",
                    new XAttribute("Importance", "high"),
                    new XAttribute("Text", $"NFC_CASE_BEGIN::{item.Id}")),
                new XElement(
                    "MSBuild",
                    new XAttribute("Projects", bootstrapProject),
                    new XAttribute("Targets", "MaterializeBuiltInProfileBundles"),
                    new XAttribute("Properties", properties),
                    new XAttribute("BuildInParallel", "false"),
                    new XAttribute("StopOnFirstFailure", "false"),
                    new XAttribute("ContinueOnError", "WarnAndContinue")),
                new XElement(
                    "PropertyGroup",
                    new XElement("_CaseSucceeded", "$(MSBuildLastTaskResult)")),
                new XElement(
                    "WriteLinesToFile",
                    new XAttribute("File", item.ResultPath),
                    new XAttribute("Lines", "$(_CaseSucceeded)"),
                    new XAttribute("Overwrite", "true")),
                new XElement(
                    "Message",
                    new XAttribute("Importance", "high"),
                    new XAttribute(
                        "Text",
                        $"NFC_CASE_END::{item.Id}::$(_CaseSucceeded)"))));
        }

        targets.Add(new XElement(
            "Target",
            new XAttribute("Name", "RunAll"),
            new XAttribute("DependsOnTargets", string.Join(';', targetNames)),
            new XElement(
                "WriteLinesToFile",
                new XAttribute("File", completionPath),
                new XAttribute("Lines", cases.Count),
                new XAttribute("Overwrite", "true")),
            new XElement(
                "Message",
                new XAttribute("Importance", "high"),
                new XAttribute("Text", $"NFC_BATCH_COMPLETE::{cases.Count}"))));
        var document = new XDocument(
            new XElement(
                "Project",
                new XAttribute("DefaultTargets", "RunAll"),
                targets));
        document.Save(driverPath);
    }

    private static BatchProcessResult RunMaterializationDriver(string driverPath)
    {
        if (Interlocked.Increment(ref _materializationDriverLaunchCount) != 1)
        {
            throw new InvalidOperationException(
                "The package-trust materialization driver may start only once per test process.");
        }

        string? inheritedDotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        var startInfo = new ProcessStartInfo(
            string.IsNullOrWhiteSpace(inheritedDotnetHost) ? "dotnet" : inheritedDotnetHost)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Root.FullName,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(driverPath);
        startInfo.ArgumentList.Add("-m:1");
        startInfo.ArgumentList.Add("-nr:false");
        startInfo.ArgumentList.Add("-p:Configuration=Release");
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The materialization batch host did not start.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            TestContext.Current.CancellationToken);
        Task completion = Task.WhenAll(
            process.WaitForExitAsync(cancellation.Token),
            standardOutput,
            standardError);
        try
        {
            completion.WaitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            TerminateMaterializationDriver(process, standardOutput, standardError);
            throw new TimeoutException("The materialization batch host exceeded two minutes.");
        }
        catch
        {
            TerminateMaterializationDriver(process, standardOutput, standardError);
            throw;
        }

        return new BatchProcessResult(
            process.ExitCode,
            standardOutput.Result + Environment.NewLine + standardError.Result);
    }

    private static void TerminateMaterializationDriver(
        Process process,
        Task<string> standardOutput,
        Task<string> standardError)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between cancellation and the kill request.
        }

        if (!process.WaitForExit(10_000))
        {
            throw new TimeoutException(
                "The materialization batch host did not exit after termination.");
        }

        if (!Task.WaitAll([standardOutput, standardError], 10_000))
        {
            throw new TimeoutException(
                "The materialization batch host streams did not close after termination.");
        }
    }

    private static ReadOnlyDictionary<string, MaterializationResult>
        ParseMaterializationResults(
            IReadOnlyList<MaterializationCase> cases,
            string completionPath,
            BatchProcessResult processResult)
    {
        string output = processResult.Output;
        if (processResult.ExitCode != 1)
        {
            throw new InvalidOperationException(
                "The expected-failure materialization batch exited with " +
                $"{processResult.ExitCode} instead of 1.\n{output}");
        }

        string completionText = File.Exists(completionPath)
            ? File.ReadAllText(completionPath).Trim()
            : string.Empty;
        if (!StringComparer.Ordinal.Equals(
                completionText,
                cases.Count.ToString(CultureInfo.InvariantCulture)))
        {
            throw new InvalidOperationException(
                "The materialization batch completion record is invalid: " +
                $"'{completionText}'.\n{output}");
        }

        string completionToken = $"NFC_BATCH_COMPLETE::{cases.Count}";
        if (!output.Contains(completionToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The materialization batch did not reach completion.\n{output}");
        }

        var results = new Dictionary<string, MaterializationResult>(StringComparer.Ordinal);
        int searchStart = 0;
        foreach (MaterializationCase item in cases)
        {
            string beginToken = $"NFC_CASE_BEGIN::{item.Id}";
            string endToken = $"NFC_CASE_END::{item.Id}::";
            int begin = output.IndexOf(beginToken, searchStart, StringComparison.Ordinal);
            int end = begin < 0
                ? -1
                : output.IndexOf(endToken, begin + beginToken.Length, StringComparison.Ordinal);
            if (begin < 0 || end < 0)
            {
                throw new InvalidOperationException(
                    $"The materialization batch output omitted markers for '{item.Id}'.\n{output}");
            }

            string succeededText = File.ReadAllText(item.ResultPath).Trim();
            if (!bool.TryParse(succeededText, out bool succeeded))
            {
                throw new InvalidOperationException(
                    $"The materialization result for '{item.Id}' is invalid: '{succeededText}'.");
            }

            string diagnostic = output.Substring(
                begin + beginToken.Length,
                end - begin - beginToken.Length);
            if (!results.TryAdd(item.Id, new MaterializationResult(!succeeded, diagnostic)))
            {
                throw new InvalidOperationException(
                    $"The materialization batch returned duplicate case '{item.Id}'.");
            }

            searchStart = end + endToken.Length;
        }

        return results.Count == cases.Count
            ? new ReadOnlyDictionary<string, MaterializationResult>(results)
            : throw new InvalidOperationException(
                "The materialization batch did not return one result for every case.");
    }

    private sealed record MaterializationCase(
        string Id,
        string Workspace,
        string IndexPath,
        string SourceRoot,
        string ResultPath);

    private sealed record MaterializationResult(bool Failed, string Output);

    private sealed record BatchProcessResult(int ExitCode, string Output);
}
