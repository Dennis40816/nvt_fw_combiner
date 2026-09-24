using System.Collections.ObjectModel;
using System.Text.Json;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Optional build-time materialization of one canonical family document.</summary>
internal sealed record ProfileBundleCanonicalFamilyMaterialization(
    string Source,
    string Destination);

/// <summary>Closed build-time sources needed to materialize one trusted bundle.</summary>
internal sealed record ProfileBundleMaterialization(
    string CompositionProfileSchemaFile,
    string FirmwareFamilySchemaFile,
    ProfileBundleCanonicalFamilyMaterialization? CanonicalFirmwareFamily);

/// <summary>One exact family identity supplied as canonical metadata authority.</summary>
internal sealed record ProfileBundleMetadataProviderFamily(
    string FamilyId,
    string FamilyVersion);

/// <summary>One fixed runtime route admitted by a hash-pinned bundle entry.</summary>
internal sealed record ProfileBundleRuntimeRegistration(
    string WorkflowId,
    string IcId,
    string ProfileId,
    string ProfileVersion,
    string? MapVariantSetId,
    string? FamilyId,
    string? PostbuildProcessorId,
    string? PostbuildBranch,
    string? ReportMetadataMapId,
    string? MemoryLayoutContextMapId = null);

/// <summary>One exact bundle root admitted by the reviewed package trust index.</summary>
internal sealed record ProfileBundlePackageTrustEntry(
    string BundleDirectory,
    string BundleSchemaVersion,
    string BundleVersion,
    string ContentHash,
    ProfileBundleMaterialization Materialization,
    IReadOnlyList<ProfileBundleMetadataProviderFamily> MetadataProviderFamilies,
    IReadOnlyList<ProfileBundleMetadataProviderFamily> FamilyDisclosureFamilies,
    IReadOnlyList<ProfileBundleRuntimeRegistration> RuntimeRegistrations);

/// <summary>Immutable versioned package trust material.</summary>
internal sealed class ProfileBundlePackageTrustIndex
{
    internal ProfileBundlePackageTrustIndex(
        string schemaVersion,
        string trustIndexId,
        string trustIndexVersion,
        string trustAnchorBindingId,
        IEnumerable<ProfileBundlePackageTrustEntry> bundles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustIndexId);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustIndexVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustAnchorBindingId);
        ArgumentNullException.ThrowIfNull(bundles);
        ProfileBundlePackageTrustEntry[] bundleSnapshot = [.. bundles];
        if (bundleSnapshot.Length == 0 || bundleSnapshot.Any(static bundle => bundle is null))
        {
            throw new ArgumentException("A package trust index requires non-null bundle entries.", nameof(bundles));
        }

        if (bundleSnapshot.Select(static bundle => bundle.BundleDirectory)
            .Distinct(StringComparer.Ordinal)
            .Count() != bundleSnapshot.Length)
        {
            throw new InvalidDataException("Package trust-index bundle directories must be unique.");
        }

        ProfileBundleRuntimeRegistration[] registrations =
        [
            .. bundleSnapshot.SelectMany(static bundle => bundle.RuntimeRegistrations),
        ];
        if (registrations.Select(CreateRegistrationKey)
            .Distinct(StringComparer.Ordinal)
            .Count() != registrations.Length)
        {
            throw new InvalidDataException("Package trust-index runtime registrations must be unique.");
        }
        RequireUniqueFamilies(bundleSnapshot.SelectMany(static bundle => bundle.MetadataProviderFamilies), "metadata provider");
        RequireUniqueFamilies(bundleSnapshot.SelectMany(static bundle => bundle.FamilyDisclosureFamilies), "family disclosure");

        Array.Sort(bundleSnapshot, static (left, right) =>
            StringComparer.Ordinal.Compare(left.BundleDirectory, right.BundleDirectory));
        SchemaVersion = schemaVersion;
        TrustIndexId = trustIndexId;
        TrustIndexVersion = trustIndexVersion;
        TrustAnchorBindingId = trustAnchorBindingId;
        Bundles = Array.AsReadOnly(bundleSnapshot);
    }

    internal string SchemaVersion { get; }

    internal string TrustIndexId { get; }

    internal string TrustIndexVersion { get; }

    internal string TrustAnchorBindingId { get; }

    internal IReadOnlyList<ProfileBundlePackageTrustEntry> Bundles { get; }

    private static void RequireUniqueFamilies(IEnumerable<ProfileBundleMetadataProviderFamily> families, string authority)
    {
        var identities = new HashSet<ProfileBundleMetadataProviderFamily>();
        if (families.Any(family => !identities.Add(family)))
        {
            throw new InvalidDataException($"Package trust-index {authority} families must be unique.");
        }
    }

    private static string CreateRegistrationKey(ProfileBundleRuntimeRegistration registration)
    {
        return string.Join(
            '\n',
            registration.WorkflowId,
            registration.IcId,
            registration.MapVariantSetId ?? string.Empty,
            registration.PostbuildProcessorId ?? string.Empty,
            registration.PostbuildBranch ?? string.Empty);
    }
}

/// <summary>Loads one bounded, strict, schema-closed package trust index.</summary>
internal static class ProfileBundlePackageTrustIndexLoader
{
    internal const int MaximumBytes = 131072;
    internal const int MaximumDepth = 16;

    internal static ProfileBundlePackageTrustIndex Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] bytes = ReadBoundedSnapshot(path);
        using JsonDocument document = StrictJsonDocumentReader.ParseOwnedSnapshot(
            bytes,
            MaximumBytes,
            MaximumDepth);
        if (!ProfileBundleSchemaValidator.IsInstanceValid(
                ProfileBundlePackageTrustIndexSchema.Schema,
                document.RootElement))
        {
            throw new InvalidDataException(
                $"Package trust index '{path}' does not satisfy schema " +
                $"'{ProfileBundlePackageTrustIndexSchema.SchemaId}'.");
        }

        JsonElement root = document.RootElement;
        return new ProfileBundlePackageTrustIndex(
            root.GetProperty("schemaVersion").GetString()!,
            root.GetProperty("trustIndexId").GetString()!,
            root.GetProperty("trustIndexVersion").GetString()!,
            root.GetProperty("trustAnchorBindingId").GetString()!,
            root.GetProperty("bundles").EnumerateArray().Select(ParseBundle));
    }

    private static byte[] ReadBoundedSnapshot(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        long length = stream.Length;
        if (length > MaximumBytes)
        {
            throw new InvalidDataException(
                $"Package trust index exceeds the {MaximumBytes}-byte limit.");
        }

        byte[] bytes = new byte[checked((int)length)];
        stream.ReadExactly(bytes);
        return stream.ReadByte() != -1
            ? throw new IOException("Package trust index changed while it was being read.")
            : bytes;
    }

    private static ProfileBundlePackageTrustEntry ParseBundle(JsonElement element)
    {
        JsonElement materialization = element.GetProperty("materialization");
        ProfileBundleCanonicalFamilyMaterialization? canonical =
            materialization.TryGetProperty("canonicalFirmwareFamily", out JsonElement value)
                ? new ProfileBundleCanonicalFamilyMaterialization(
                    RequireRelativeJsonPath(value.GetProperty("source").GetString()!),
                    RequireRelativeJsonPath(
                        value.GetProperty("destination").GetString()!,
                        requireFamiliesRoot: true))
                : null;
        return new ProfileBundlePackageTrustEntry(
            element.GetProperty("bundleDirectory").GetString()!,
            element.GetProperty("bundleSchemaVersion").GetString()!,
            element.GetProperty("bundleVersion").GetString()!,
            element.GetProperty("contentHash").GetString()!,
            new ProfileBundleMaterialization(
                materialization.GetProperty("compositionProfileSchemaFile").GetString()!,
                materialization.GetProperty("firmwareFamilySchemaFile").GetString()!,
                canonical),
            ParseFamilies(element, "metadataProviderFamilies"),
            ParseFamilies(element, "familyDisclosureFamilies"),
            Array.AsReadOnly(
                element.GetProperty("runtimeRegistrations")
                    .EnumerateArray()
                    .Select(ParseRuntimeRegistration)
                    .ToArray()));
    }

    private static ReadOnlyCollection<ProfileBundleMetadataProviderFamily> ParseFamilies(JsonElement element, string propertyName)
    {
        return Array.AsReadOnly(element.TryGetProperty(propertyName, out JsonElement families)
            ? families.EnumerateArray().Select(static family => new ProfileBundleMetadataProviderFamily(
                family.GetProperty("familyId").GetString()!, family.GetProperty("familyVersion").GetString()!)).ToArray()
            : []);
    }

    private static ProfileBundleRuntimeRegistration ParseRuntimeRegistration(JsonElement element)
    {
        return new ProfileBundleRuntimeRegistration(
            element.GetProperty("workflowId").GetString()!,
            element.GetProperty("icId").GetString()!,
            element.GetProperty("profileId").GetString()!,
            element.GetProperty("profileVersion").GetString()!,
            GetOptionalString(element, "mapVariantSetId"),
            GetOptionalString(element, "familyId"),
            GetOptionalString(element, "postbuildProcessorId"),
            GetOptionalString(element, "postbuildBranch"),
            GetOptionalString(element, "reportMetadataMapId"),
            GetOptionalString(element, "memoryLayoutContextMapId"));
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value)
            ? value.GetString()
            : null;
    }

    private static string RequireRelativeJsonPath(
        string path,
        bool requireFamiliesRoot = false)
    {
        string[] segments = path.Split('/');
        return path.Contains('\\') ||
            Path.IsPathRooted(path) ||
            segments.Any(static segment => segment is "" or "." or "..") ||
            (requireFamiliesRoot && (segments.Length != 2 || segments[0] != "families"))
                ? throw new InvalidDataException(
                    $"Package trust-index path '{path}' is not a closed relative JSON path.")
                : path;
    }
}
