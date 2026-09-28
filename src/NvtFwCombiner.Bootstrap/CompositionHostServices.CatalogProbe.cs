using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Diagnostics;

namespace NvtFwCombiner.Bootstrap;

public sealed partial class CompositionHostServices
{
    /// <summary>Handles the closed catalog probe before UI, managed startup, or user-state resolution.</summary>
    public static bool TryHandleProfileCatalogProbe(string[] arguments, TextWriter output, out int exitCode)
    {
        return TryHandleProfileCatalogProbe(arguments, output, static () =>
        {
            // Explicit, never-created placeholder. Only the normal catalog loader is invoked.
            CompositionHostServices host = Create(Path.Combine(AppContext.BaseDirectory, ".catalog-probe-unused-state"));
            return (host.CanonicalCatalogLoader, host.CanonicalSupportMatrixQuery);
        }, BuiltInProfileAdmissionStatus.Instance, out exitCode);
    }

    internal static bool TryHandleProfileCatalogProbe(string[] arguments, TextWriter output,
        Func<(ICanonicalCapabilityCatalogLoader Loader, ICanonicalSupportMatrixQuery Query)> createCatalog,
        IBuiltInProfileAdmissionStatus status, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(createCatalog);
        ArgumentNullException.ThrowIfNull(status);
        const string command = "--profile-catalog-probe-v1";
        exitCode = 0;
        if (!arguments.Contains(command, StringComparer.Ordinal))
        {
            return false;
        }
        if (arguments.Length != 1)
        {
            exitCode = 2;
            return true;
        }

        bool loaded;
        try
        {
            (ICanonicalCapabilityCatalogLoader loader, ICanonicalSupportMatrixQuery query) = createCatalog();
            loaded = LoadProbeCatalogAsync(loader, query).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // The executable boundary reports a closed failure; private exception text never reaches stdout.
            loaded = false;
        }
        BuiltInProfileAdmission? admission = status.Current;
        output.WriteLine(JsonSerializer.Serialize(new ProfileCatalogProbeResponse(
            "nfc-profile-catalog-probe-v1", admission?.SourceToken, admission?.RejectionCode, loaded),
            ProfileCatalogProbeJson.Default.ProfileCatalogProbeResponse));
        exitCode = loaded && admission is not null ? 0 : 1;
        return true;
    }

    private static async Task<bool> LoadProbeCatalogAsync(
        ICanonicalCapabilityCatalogLoader loader, ICanonicalSupportMatrixQuery query)
    {
        CapabilityCatalogReloadResult? terminal = null;
        await foreach (CanonicalCapabilityCatalogLoadUpdate update in loader.LoadAsync(CancellationToken.None)
                           .ConfigureAwait(false))
        {
            if (terminal is not null)
            {
                return false;
            }
            terminal = update.Result;
        }
        CanonicalSupportMatrixQueryResult publication = query.Query();
        return terminal?.Succeeded == true && publication.State == CanonicalSupportMatrixCatalogState.Current &&
            publication.Matrix is not null;
    }
}

internal sealed record ProfileCatalogProbeResponse(
    string SchemaVersion, string? AdmissionSource, string? RejectionCode, bool CatalogLoaded);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProfileCatalogProbeResponse))]
internal sealed partial class ProfileCatalogProbeJson : JsonSerializerContext;
