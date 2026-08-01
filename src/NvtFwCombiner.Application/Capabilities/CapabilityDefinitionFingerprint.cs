using System.Security.Cryptography;
using System.Text;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Deterministic identity of one complete reviewed capability definition.</summary>
public static class CapabilityDefinitionFingerprint
{
    /// <summary>Language-neutral framed format bound by ADR 0046.</summary>
    public const string FormatId = "nfc.capability-definition.v1";

    /// <summary>Reviewed lowering semantics for one resolved physical-map plan.</summary>
    public const string MapBoundCompilerSemanticId =
        "nfc.compiler.profile-bundle-v2.map-bound.v1";

    /// <summary>Reviewed lowering semantics for bounded logical-output authoring.</summary>
    public const string LogicalOutputCompilerSemanticId =
        "nfc.compiler.profile-bundle-v2.logical-output.v1";

    /// <summary>Reviewed lowering semantics for bounded runtime-reference replacement.</summary>
    public const string RuntimeReferenceReplaceCompilerSemanticId =
        "nfc.compiler.profile-bundle-v2.runtime-reference-replace.v1";

    /// <summary>Computes one definition identity without per-compilation authoring state.</summary>
    public static string Compute(
        CapabilityRouteIdentity identity,
        string definitionId,
        string definitionVersion,
        string trustedDefinitionSha256,
        IEnumerable<string> allowedMapVariantIds,
        string compilerSemanticId,
        IEnumerable<string>? semanticBindingIds = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionVersion);
        ArgumentNullException.ThrowIfNull(trustedDefinitionSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerSemanticId);
        ArgumentNullException.ThrowIfNull(allowedMapVariantIds);
        if (!CapabilityRouteIdentity.IsSha256(trustedDefinitionSha256))
        {
            throw new ArgumentException(
                "Capability definitions require a lowercase SHA-256 trust identity.",
                nameof(trustedDefinitionSha256));
        }

        string[] maps = NormalizeSet(
            allowedMapVariantIds,
            nameof(allowedMapVariantIds));
        if (maps.Length == 0)
        {
            throw new ArgumentException(
                "Capability definitions require at least one allowed map variant.",
                nameof(allowedMapVariantIds));
        }

        string[] bindings = NormalizeSet(
            semanticBindingIds ?? [],
            nameof(semanticBindingIds));
        var builder = new StringBuilder();
        Append(builder, "format", FormatId);
        Append(builder, "ic", identity.IcId);
        Append(builder, "workflow", identity.WorkflowId);
        Append(builder, "ic-count", identity.IcCountVariant);
        Append(builder, "route-map", identity.MapVariant);
        Append(builder, "definition-id", definitionId);
        Append(builder, "definition-version", definitionVersion);
        Append(builder, "definition-sha256", trustedDefinitionSha256);
        Append(builder, "compiler-semantic", compilerSemanticId);
        AppendSet(builder, "allowed-map", maps);
        AppendSet(builder, "semantic-binding", bindings);
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string[] NormalizeSet(
        IEnumerable<string> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] normalized =
        [
            .. values
                .Select(value => string.IsNullOrWhiteSpace(value)
                    ? throw new ArgumentException(
                        "Capability fingerprint set values must be non-empty.",
                        parameterName)
                    : value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        return normalized;
    }

    private static void AppendSet(
        StringBuilder builder,
        string label,
        string[] values)
    {
        Append(builder, $"{label}-count", values.Length.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        for (int index = 0; index < values.Length; index++)
        {
            Append(builder, $"{label}-{index}", values[index]);
        }
    }

    private static void Append(
        StringBuilder builder,
        string label,
        string value)
    {
        _ = builder.Append(label.Length)
            .Append(':')
            .Append(label)
            .Append('=')
            .Append(value.Length)
            .Append(':')
            .Append(value)
            .Append(';');
    }
}
