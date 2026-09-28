using NvtFwCombiner.PrebuiltProfileCatalogGeneration;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: catalog-generator <materialized-bundle-root> <trust-index> <output.pack>");
    return 2;
}
try
{
    PrebuiltProfileCatalogGenerator.Generate(args[0], args[1], args[2]);
    return 0;
}
catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
    System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine("Prebuilt profile catalog generation failed: " + error.GetType().Name);
    return 1;
}
