using System.Text.Json.Nodes;
using NvtFwCombiner.CatalogProbe;
using System.Globalization;
using NvtFwCombiner.PrebuiltProfileCatalogGeneration;

try
{
    string mode = args.SingleOrDefault() ?? "evidence";
    JsonObject result;
    if (mode == "reproduce")
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(Environment.GetEnvironmentVariable("CATALOG_PROBE_CULTURE") ?? "en-US");
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
        TimeZoneInfo.ClearCachedData();
        string root = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in");
        byte[] expected = File.ReadAllBytes(Path.Combine(root, "prebuilt-profile-catalog.pack"));
        string output = Path.Combine(AppContext.BaseDirectory, "regenerated.pack");
        var matches = new JsonArray();
        for (int i = 0; i < 2; i++)
        {
            PrebuiltProfileCatalogGenerator.Generate(root, Path.Combine(root, "package-trust-index.json"), output);
            matches.Add(expected.AsSpan().SequenceEqual(File.ReadAllBytes(output)));
        }
        result = new JsonObject
        {
            ["culture"] = CultureInfo.CurrentCulture.Name,
            ["timeZone"] = TimeZoneInfo.Local.Id,
            ["utcOffsetMinutes"] = TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.UnixEpoch).TotalMinutes,
            ["matches"] = matches,
        };
    }
    else { result = CatalogEvidence.Capture(mode); }
    Console.WriteLine(result.ToJsonString());
    return 0;
}
catch (Exception error)
{
    Console.WriteLine(new JsonObject { ["fatal"] = error.ToString() }.ToJsonString());
    return 1;
}
