using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Validates closed local Draft 2020-12 schemas and their JSON instances.</summary>
internal static class ClosedContentSchemaValidator
{
    private const string Draft202012SchemaId = "https://json-schema.org/draft/2020-12/schema";

    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.Flag,
        RequireFormatValidation = true,
    };

    internal static JsonSchema ParseSchema(
        string schemaPath,
        string schemaId,
        JsonElement root,
        string contentKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentKind);
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Error(contentKind, schemaPath, "Schema root must be an object.");
        }

        ValidateRequiredRootString(root, "$schema", Draft202012SchemaId, contentKind, schemaPath);
        ValidateRequiredRootString(root, "$id", schemaId, contentKind, schemaPath);
        ValidateSchemaReferences(root, isRoot: true, contentKind, schemaPath);

        EvaluationResults metaValidation = MetaSchemas.Draft202012.Evaluate(root, EvaluationOptions);
        if (!metaValidation.IsValid)
        {
            throw Error(contentKind, schemaPath, "Schema does not satisfy Draft 2020-12.");
        }

        try
        {
            return JsonSchema.FromText(root.GetRawText(), new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry(),
            });
        }
        catch (JsonSchemaException exception)
        {
            throw Error(contentKind, schemaPath, "Schema could not be parsed.", exception);
        }
        catch (JsonException exception)
        {
            throw Error(contentKind, schemaPath, "Schema could not be parsed.", exception);
        }
    }

    internal static void ValidateInstance(
        JsonSchema schema,
        JsonElement document,
        string documentPath,
        string schemaId,
        string contentKind)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentKind);

        EvaluationResults result = schema.Evaluate(document, EvaluationOptions);
        if (!result.IsValid)
        {
            throw Error(contentKind, documentPath, $"Document does not satisfy schema '{schemaId}'.");
        }
    }

    private static void ValidateRequiredRootString(
        JsonElement root,
        string propertyName,
        string expectedValue,
        string contentKind,
        string schemaPath)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            !StringComparer.Ordinal.Equals(property.GetString(), expectedValue))
        {
            throw Error(contentKind, schemaPath, $"Schema requires {propertyName} '{expectedValue}'.");
        }
    }

    private static void ValidateSchemaReferences(
        JsonElement element,
        bool isRoot,
        string contentKind,
        string schemaPath)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateSchemaReferences(item, isRoot: false, contentKind, schemaPath);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.Name is "$ref" or "$dynamicRef" or "$recursiveRef")
            {
                if (property.Value.ValueKind != JsonValueKind.String ||
                    !property.Value.GetString()!.StartsWith('#'))
                {
                    throw Error(contentKind, schemaPath, $"Schema {property.Name} must be a local fragment reference.");
                }
            }
            else if (!isRoot && property.Name is "$id" or "$schema")
            {
                throw Error(contentKind, schemaPath, $"Schema cannot declare nested {property.Name}.");
            }

            ValidateSchemaReferences(property.Value, isRoot: false, contentKind, schemaPath);
        }
    }

    private static InvalidDataException Error(
        string contentKind,
        string path,
        string message,
        Exception? innerException = null)
    {
        return new InvalidDataException(
            $"{contentKind} schema validation failed for '{path}': {message}",
            innerException);
    }
}
