// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

internal static partial class CompiledOutputNameResolver
{
    /// <summary>Expands field and bank-field tokens once, then applies canonical literal-name validation.</summary>
    internal static string ResolveTemplate(
        string template,
        IReadOnlyList<OutputNameTokenValue> tokenValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(tokenValues);
        Dictionary<string, string> tokens = tokenValues.ToDictionary(
            static token => token.TokenId,
            static token => token.Summary.Value,
            StringComparer.Ordinal);
        string? expanded = Expand(template, tokens, out TemplateExpansionFailure failure);
        if (failure == TemplateExpansionFailure.UnmatchedBrace)
        {
            throw new ArgumentException("Output templates cannot contain unmatched braces.", nameof(template));
        }

        if (failure == TemplateExpansionFailure.NonLiteralValue)
        {
            throw new ArgumentException("Output token values must be literal text.", nameof(tokenValues));
        }

        string fileName = expanded ??
            throw new ArgumentException("Output template tokens require one supplied field value.", nameof(template));
        CompiledOutputNamingRequirement.ValidateRuntimeLiteralFileName(fileName, nameof(template));
        return fileName;
    }

    /// <summary>
    /// Expands accepted token values into a name or folder template with the same token rules as
    /// <see cref="ResolveTemplate"/>. Returns null when a brace is unmatched, a token has no value, or a
    /// value is not literal text. Callers keep their own name validation.
    /// </summary>
    internal static string? ExpandTemplate(string template, IReadOnlyDictionary<string, string> tokenValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(tokenValues);
        return Expand(template, tokenValues, out _);
    }

    private static string? Expand(string template, IReadOnlyDictionary<string, string> tokenValues,
        out TemplateExpansionFailure failure)
    {
        StringBuilder builder = new(template.Length);
        for (int index = 0; index < template.Length; index++)
        {
            char current = template[index];
            if (current == '}')
            {
                failure = TemplateExpansionFailure.UnmatchedBrace;
                return null;
            }

            if (current != '{')
            {
                _ = builder.Append(current);
                continue;
            }

            int close = template.IndexOf('}', index + 1);
            if (close < 0 || !tokenValues.TryGetValue(template[(index + 1)..close], out string? value))
            {
                failure = TemplateExpansionFailure.MissingToken;
                return null;
            }

            if (value.IndexOfAny(['{', '}']) >= 0)
            {
                failure = TemplateExpansionFailure.NonLiteralValue;
                return null;
            }

            _ = builder.Append(value);
            index = close;
        }

        failure = TemplateExpansionFailure.None;
        return builder.ToString();
    }

    private enum TemplateExpansionFailure
    {
        None,
        UnmatchedBrace,
        MissingToken,
        NonLiteralValue,
    }
}
