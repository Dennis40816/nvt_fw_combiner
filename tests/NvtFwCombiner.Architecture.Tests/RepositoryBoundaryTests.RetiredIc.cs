namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Retired ICs have no production profile, route, processor, package, or catalog owner.</summary>
    [Fact]
    public void RetiredIcCapabilitiesStayOutsideProductionOwners()
    {
        string[] retiredIds = ["51920", "51925", "51930", "51931"];
        string[] productionOwners =
        [
            "src/NvtFwCombiner.Domain/Composition/ExperienceIds.cs",
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs",
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs",
            "src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2RouteRegistry.cs",
            "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
            "src/NvtFwCombiner.Application/ExternalTools/PostbuildWriteSections.cs",
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.ReportMetadata.cs",
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.IntegrityRanges.cs",
            "src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog.cs",
            "src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.Loader.cs",
            "profiles/built-in/ctrlram-postbuild-v2/catalog.json",
            "profiles/built-in/ctrlram-postbuild-v2/flash-map.json",
        ];

        foreach (string owner in productionOwners)
        {
            string source = ReadText(owner);
            Assert.All(retiredIds, retiredId =>
                Assert.DoesNotContain(retiredId, source, StringComparison.Ordinal));
        }

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "FlashMaps",
            "TpHeaderCatalog.Layouts.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "FlashMaps",
            "GenFlashVersionCatalog.cs")));

        string builtInRoot = Path.Combine(Root.FullName, "profiles", "built-in");
        Assert.DoesNotContain(
            Directory.EnumerateFiles(builtInRoot, "*", SearchOption.AllDirectories),
            path => retiredIds.Any(retiredId =>
                Path.GetRelativePath(builtInRoot, path)
                    .StartsWith($"nt{retiredId}-", StringComparison.Ordinal)));

        Assert.True(Directory.Exists(Path.Combine(builtInRoot, "nt51923-ctrlram-replace-candidate")));
        Assert.True(Directory.Exists(Path.Combine(builtInRoot, "nt51926-ctrlram-replace-candidate")));

        string packagePolicy = ReadText("scripts/package.ps1");
        Assert.Contains("$RetiredIcTokens", packagePolicy, StringComparison.Ordinal);
        Assert.Contains("cannot publish retired IC", packagePolicy, StringComparison.Ordinal);
        Assert.All(retiredIds, retiredId =>
            Assert.Contains($"'{retiredId}'", packagePolicy, StringComparison.Ordinal));

        string[] retiredEvidenceRoots =
        [
            "testdata/golden/canonical",
            "testdata/golden/ctrlram-replace",
            "testdata/golden/owner-handoff",
            "testdata/public-synthetic",
        ];
        string[] removableEvidenceIds = ["51920", "51930", "51931"];
        string[] searchableExtensions = [".json", ".md", ".txt", ".bat", ".h"];
        Dictionary<string, string[]> historicalEvidenceStatements = new(StringComparer.Ordinal)
        {
            ["testdata/golden/canonical/manifest.json"] =
            [
                "Nine direct CtrlRAM exact cases were canonicalized from this intake. RET-IC-01 later retired the two NT51920 cases, leaving seven active direct cases; canonicalization does not promote runtime support or release redistribution.",
                "Historical (retired NT51931): InsertSID was an out-of-scope pre-step for this V1 retirement and did not block exact CtrlRAM/Postbuild full-byte parity.",
                "Historical (retired NT51931): NT51931 selected registered Combiner 1.13.0 NT51931BASED_NORMAL_MODE after its output matched the owner 1.2.0.4 NT51930BASED_NORMAL_MODE control byte-for-byte on 2026-07-19.",
            ],
            ["testdata/golden/ctrlram-replace/manifest.20260717.json"] =
            [
                "Remaining diagnostic and cross-workflow duplicate artifacts only. Nine direct CtrlRAM cases moved byte-for-byte to the canonical inventory; RET-IC-01 later retired the two NT51920 cases, leaving seven active direct cases. Physical payload relocation is frozen; this manifest is the hash-pinned legacy quarantine inventory referenced by testdata/diagnostics/golden-evidence/manifest.json and is not canonical expected evidence.",
            ],
        };

        foreach (string evidenceRoot in retiredEvidenceRoots)
        {
            string absoluteRoot = Path.Combine(
                Root.FullName,
                evidenceRoot.Replace('/', Path.DirectorySeparatorChar));
            foreach (string path in Directory.EnumerateFiles(
                         absoluteRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(Root.FullName, path)
                    .Replace(Path.DirectorySeparatorChar, '/');
                Assert.DoesNotContain(
                    removableEvidenceIds,
                    retiredId => relativePath.Contains(retiredId, StringComparison.OrdinalIgnoreCase));
                if (searchableExtensions.Contains(
                        Path.GetExtension(path),
                        StringComparer.OrdinalIgnoreCase))
                {
                    string source = File.ReadAllText(path);
                    if (historicalEvidenceStatements.TryGetValue(
                            relativePath,
                            out string[]? statements))
                    {
                        foreach (string statement in statements)
                        {
                            int first = source.IndexOf(statement, StringComparison.Ordinal);
                            Assert.True(first >= 0, $"Missing historical evidence statement in {relativePath}");
                            Assert.Equal(
                                first,
                                source.LastIndexOf(statement, StringComparison.Ordinal));
                            source = source.Remove(first, statement.Length);
                        }
                    }
                    Assert.DoesNotContain(
                        removableEvidenceIds,
                        retiredId => source.Contains(retiredId, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }
}
