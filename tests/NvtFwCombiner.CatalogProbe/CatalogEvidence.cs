using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Bootstrap.Tests;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Composition;
using NvtFwCombiner.Infrastructure.Diagnostics;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.CatalogProbe;

internal static class CatalogEvidence
{
    internal static JsonObject Capture(string mode)
    {
        var result = new JsonObject { ["passiveBeforeLoad"] = BuiltInProfileAdmissionStatus.Instance.Current is null };
        if (mode == "lifecycle") { ObserveLifecycle(result); }
        if (mode == "replace-after-selection")
        {
            BuiltInV2BundleRegistry.All[BuiltInV2BundlePreload.Layers[0][0]].Preload();
            string pack = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", "prebuilt-profile-catalog.pack");
            if (File.Exists(pack)) { File.Delete(pack); }
            else { File.Copy(Path.Combine(AppContext.BaseDirectory, "original.pack"), pack); }
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", "package-trust-index.json"), " ");
        }
        if (mode == "damage-after-selection")
        {
            BuiltInV2BundleRegistry.All[BuiltInV2BundlePreload.Layers[0][0]].Preload();
            foreach (string directory in BuiltInV2BundleRegistry.All.Keys)
            {
                // Test-only destruction of the isolated child copy, never the repository/build output.
                string root = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", directory);
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) { File.Delete(file); }
            }
        }
        if (mode == "race")
        {
            _ = Parallel.For(0, 8, _ => BuiltInV2BundleRegistry.All[BuiltInV2BundlePreload.Layers[0][0]].Preload());
        }
        CompositionHostServices host = CompositionHostServices.Create(Path.Combine(AppContext.BaseDirectory, ".unused-state"));
        CapabilityCatalogReloadResult? publication = Reload(host, result);
        result["loaded"] = publication?.Succeeded == true;
        result["hasPublishedSnapshot"] = publication?.Snapshot is not null;
        result["issues"] = new JsonArray((publication?.Issues ?? []).Select(i => (JsonNode)new JsonObject
        {
            ["code"] = i.Code,
            ["message"] = i.Message.Replace(AppContext.BaseDirectory, "<host>/", StringComparison.Ordinal),
            ["subject"] = i.Subject,
        }).ToArray());
        if (publication?.Snapshot is { } snapshot)
        {
            CanonicalCatalogSnapshotDigest digest = CanonicalCatalogSnapshotDigest.Create(snapshot);
            result["publication"] = new JsonObject
            {
                ["text"] = digest.Text,
                ["sha256"] = digest.Sha256,
                ["sections"] = new JsonArray(digest.Sections.Select(s => (JsonNode)new JsonObject
                {
                    ["name"] = s.Name,
                    ["length"] = s.Length,
                    ["sha256"] = s.Sha256,
                }).ToArray()),
            };
        }
        var bundles = new JsonArray();
        try
        {
            foreach (string directory in BuiltInV2BundleRegistry.All.Keys.Order(StringComparer.Ordinal))
            {
                try
                {
                    SelectedBuiltInBundleEvidence selected = BuiltInV2BundleRegistry.GetSelectedBundleEvidence(directory);
                    TrustedProfileBundleDocumentProjection projection = selected.Projection;
                    bundles.Add(new JsonObject
                    {
                        ["directory"] = directory,
                        ["bundleId"] = projection.BundleId,
                        ["bundleVersion"] = projection.BundleVersion,
                        ["contentHash"] = projection.BundleContentHash,
                        ["manifestHash"] = projection.ManifestSnapshot.ActualSha256,
                        ["manifest"] = Convert.ToBase64String(projection.ManifestSnapshot.Content),
                        ["normalized"] = Normalized(selected.Catalog, projection),
                        ["documents"] = new JsonArray(projection.Documents.Select(d => (JsonNode)new JsonObject
                        {
                            ["id"] = d.Entry.EntryId,
                            ["kind"] = d.Entry.Kind.ToString(),
                            ["path"] = d.Entry.Path,
                            ["schema"] = d.Entry.SchemaId,
                            ["hash"] = d.Entry.ContentHash,
                            ["bytes"] = Convert.ToBase64String(d.FileSnapshot.Content),
                        }).ToArray()),
                    });
                    if (selected.Admission != BuiltInProfileAdmissionStatus.Instance.Current) { throw new InvalidOperationException("Mixed source evidence."); }
                }
                catch (Exception error)
                {
                    bundles.Add(new JsonObject
                    {
                        ["directory"] = directory,
                        ["error"] = error.Message.Replace(AppContext.BaseDirectory, "<host>/", StringComparison.Ordinal),
                        ["errorType"] = error.GetType().Name,
                        ["errorCode"] = error is TrustedProfileBundleCatalogException catalog ? catalog.Code : null,
                    });
                }
            }
        }
        catch (TypeInitializationException error) { result["indexError"] = error.InnerException?.GetType().Name; }
        result["bundles"] = bundles;
        if (publication?.Succeeded == true && bundles.All(b => b!["error"] is null))
        {
            result["parents"] = Parents();
            result["sharedMetadata"] = SharedMetadata();
        }
        if (mode is "compilation" or "damage-after-selection")
        {
            result["compilations"] = CompilationScenarios.Capture();
            result["reports"] = new JsonArray(CtrlRamV2RouteRegistry.All.Where(r => r.Key.IcId == "NT51923")
                .Select(r => (JsonNode)JsonValue.Create(CanonicalCatalogSnapshotDigest.DescribeMetadataPlan(
                    BuiltInCtrlRamAuthoringAdapter.CreateCtrlRamReportMetadataPlan(r)))!).ToArray());
        }
        BuiltInProfileAdmission? admission = BuiltInProfileAdmissionStatus.Instance.Current;
        result["source"] = admission?.SourceToken;
        result["reason"] = admission?.RejectionCode;
        result["entryValidationCalls"] = ProfileBundleSchemaValidator.EntryValidationCalls;
        result["entryEvaluations"] = ProfileBundleSchemaValidator.EntryEvaluations;
        var information = new SystemInformationService("test", host.CanonicalSupportMatrixQuery, new Reloader(host),
            new ExternalProcessorEnvironmentLoader(static (_, _) => throw new NotSupportedException()),
            new RuntimeProbe(), new Clock(), admissionStatus: BuiltInProfileAdmissionStatus.Instance);
        _ = information.Refresh(false, CancellationToken.None);
        _ = information.Refresh(false, CancellationToken.None);
        result["warnings"] = information.Current.ActiveDiagnostics.Count(d => d.Code == SystemDiagnosticCodes.PrebuiltCatalogUnused && d.Severity == SystemDiagnosticSeverity.Warning);
        _ = Reload(host, result);
        result["sameDecisionAfterReload"] = ReferenceEquals(admission, BuiltInProfileAdmissionStatus.Instance.Current);
        return result;
    }

    private static CapabilityCatalogReloadResult? Reload(CompositionHostServices host, JsonObject result)
    {
        try { return host.Catalog.Reload(CancellationToken.None); }
        catch (ProfileBundleManifestNormalizationException error)
        {
            // Preserve the existing JSON failure boundary, including exceptions before publication.
            result["publicationException"] = error.GetType().Name + ": " + error.Message;
            return null;
        }
    }

    private static JsonObject Normalized(TrustedProfileBundleCatalog catalog, TrustedProfileBundleDocumentProjection projection)
    {
        return new JsonObject
        {
            ["bundle"] = JsonSerializer.SerializeToNode(catalog.BundleIdentity),
            ["families"] = new JsonArray(catalog.Families.Select(f => (JsonNode)new JsonObject
            {
                ["id"] = f.Family.FamilyId,
                ["version"] = f.Family.FamilyVersion,
                ["hash"] = f.Family.FamilyContentHash,
                ["entry"] = JsonSerializer.SerializeToNode(f.Identity),
                ["members"] = JsonSerializer.SerializeToNode(f.MemberIds),
            }).ToArray()),
            ["profiles"] = new JsonArray(catalog.Profiles.Select(p => (JsonNode)new JsonObject
            {
                ["identity"] = ResolvedProfileIdentity(catalog, p, projection),
                ["entry"] = JsonSerializer.SerializeToNode(p.Identity),
                ["family"] = p.Family.Family.FamilyId,
                ["sameFamilyInstance"] = catalog.Families.Any(f => ReferenceEquals(f, p.Family)),
            }).ToArray()),
        };
    }

    private static JsonObject ResolvedProfileIdentity(TrustedProfileBundleCatalog catalog,
        TrustedCompositionProfileCatalogEntry profile, TrustedProfileBundleDocumentProjection projection)
    {
        JsonNode document = JsonNode.Parse(projection.Documents.Single(d => d.Entry.EntryId == profile.Identity.EntryId).FileSnapshot.Content)!;
        string id = document["profileId"]!.GetValue<string>();
        string version = document["profileVersion"]!.GetValue<string>();
        // Selection proves these exact identities resolve to the normalized entry being compared.
        return ReferenceEquals(profile, catalog.SelectProfile(id, version, out _))
            ? new JsonObject { ["id"] = id, ["version"] = version }
            : throw new InvalidOperationException("Normalized profile identity differs from its admitted document.");
    }

    private static JsonArray Parents()
    {
        var parents = new JsonArray();
        foreach (GeneralMergeV2CandidateRegistration registration in BuiltInV2RegistrationRegistry.GeneralMergeByIc.Values.OrderBy(r => r.IcId, StringComparer.Ordinal))
        {
            parents.Add(JsonSerializer.SerializeToNode(registration.Bundle.GetGeneralMergeSavedRuleParentBinding(registration.ProfileId)));
        }
        foreach (GeneralReplaceV2Registration registration in BuiltInV2RegistrationRegistry.GeneralReplaceByIc.Values.OrderBy(r => r.IcId, StringComparer.Ordinal))
        {
            parents.Add(JsonSerializer.SerializeToNode(registration.ExactParent.Admission.ParentBinding));
        }
        return parents;
    }

    private static bool SharedMetadata()
    {
        FirmwareMetadataStructureDefinition provider = BuiltInV2BundleRegistry.GetSelectedBundleEvidence("nt51927-standard-merge").Catalog.Families
            .SelectMany(f => f.Family.MetadataSets).SelectMany(s => s.Structures)
            .First(s => s.Definition.DefinitionId == "firmware-config-general-parameters").Definition;
        FirmwareMetadataStructure[] consumers = [.. BuiltInV2BundleRegistry.GetSelectedBundleEvidence("nt51950-nt51951-standard-merge").Catalog.Families
            .SelectMany(f => f.Family.MetadataSets).SelectMany(s => s.Structures)
            .Where(s => s.Definition.DefinitionId == "firmware-config-general-parameters")];
        return consumers.Length > 0 && consumers.All(s => ReferenceEquals(provider, s.Definition));
    }

    private static void ObserveLifecycle(JsonObject result)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { BuiltInV2BundlePreload.Run(cancellation.Token); }
        catch (OperationCanceledException) { }
        result["cancelledBeforeSelection"] = BuiltInProfileAdmissionStatus.Instance.Current is null;
        int caller = Environment.CurrentManagedThreadId;
        int warmupThread = 0;
        int unresolvedWorkerEntries = 0;
        string warmup = BuiltInV2BundlePreload.Layers[0][0];
        BuiltInV2BundlePreload.RunLayers(BuiltInV2BundlePreload.Layers, directory =>
        {
            if (directory == warmup) { warmupThread = Environment.CurrentManagedThreadId; }
            else if (BuiltInProfileAdmissionStatus.Instance.Current is null) { _ = Interlocked.Increment(ref unresolvedWorkerEntries); }
            BuiltInV2BundleRegistry.All[directory].Preload();
        }, 4, CancellationToken.None);
        result["warmupThread"] = warmupThread == caller;
        result["resolvedBeforeWorkers"] = unresolvedWorkerEntries == 0;
    }

    private sealed class RuntimeProbe : ISystemRuntimeProbe
    {
        public SystemRuntimeFacts Probe() { return new("test", "test", "test"); }
    }
    private sealed class Reloader(CompositionHostServices host) : ICanonicalCapabilityCatalogReloader
    {
        public void Reload(CancellationToken cancellationToken) { _ = host.Catalog.Reload(cancellationToken); }
    }
    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }
}
