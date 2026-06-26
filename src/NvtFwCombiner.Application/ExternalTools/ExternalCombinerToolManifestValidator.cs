using NvtFwCombiner.Contracts.ExternalTools;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Validates external combiner manifests before they can be registered for runtime use.</summary>
public sealed class ExternalCombinerToolManifestValidator
{
    private static readonly char[] VersionSeparators = ['.', '-', '+'];
    private static readonly string[] AllowedTokens = ["{staging.workBin}", "{staging.outputBin}", "{staging.runDir}"];

    /// <summary>Returns deterministic validation errors. An empty list means the manifest is acceptable.</summary>
    public IReadOnlyList<string> Validate(ExternalCombinerToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<string> errors = [];
        RequireExact(manifest.SchemaVersion, "1.0", nameof(manifest.SchemaVersion), errors);
        RequireNotBlank(manifest.ToolBindingId, nameof(manifest.ToolBindingId), errors);
        RequireNotBlank(manifest.ToolId, nameof(manifest.ToolId), errors);
        ValidateToolVersion(manifest.ToolVersion, errors);
        RequireNotBlank(manifest.DisplayName, nameof(manifest.DisplayName), errors);
        RequireOneOf(manifest.Platform, ["win-x64", "win-arm64"], nameof(manifest.Platform), errors);
        ValidateExecutableName(manifest.ExecutableName, errors);
        ValidateSha256(manifest.Sha256, errors);
        RequireNotBlank(manifest.AdapterId, nameof(manifest.AdapterId), errors);
        RequireOneOf(manifest.InputMode, ["in-place", "input-output-file"], nameof(manifest.InputMode), errors);
        RequireExact(manifest.WorkingDirectoryPolicy, "staging-directory", nameof(manifest.WorkingDirectoryPolicy), errors);

        if (manifest.TimeoutSeconds is < 1 or > 120)
        {
            errors.Add("TimeoutSeconds must be between 1 and 120 seconds.");
        }

        if (manifest.ArgumentTemplate.Count == 0)
        {
            errors.Add("ArgumentTemplate must contain at least one argument.");
        }
        else
        {
            ValidateArgumentTemplate(manifest.ArgumentTemplate, errors);
        }

        foreach (string fileName in manifest.AllowedExtraOutputFiles)
        {
            if (!IsPlainFileName(fileName))
            {
                errors.Add($"Allowed extra output file '{fileName}' must be a plain filename.");
            }
        }

        return errors;
    }

    private static void ValidateToolVersion(string version, ICollection<string> errors)
    {
        RequireNotBlank(version, nameof(ExternalCombinerToolManifest.ToolVersion), errors);
        if (version.Contains(' ', StringComparison.Ordinal))
        {
            errors.Add("ToolVersion must not contain spaces.");
        }

        string[] numericParts = version.Split(VersionSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (numericParts.Length == 0 || numericParts.Any(part => !part.All(char.IsAsciiDigit)))
        {
            errors.Add("ToolVersion must start with dot-separated numeric string tokens, for example '1.10'.");
        }
    }

    private static void ValidateExecutableName(string executableName, ICollection<string> errors)
    {
        if (!IsPlainFileName(executableName) || !executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("ExecutableName must be a plain .exe filename.");
        }
    }

    private static void ValidateSha256(string value, ICollection<string> errors)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character) || char.IsUpper(character)))
        {
            errors.Add("Sha256 must be 64 lowercase hexadecimal characters.");
        }
    }

    private static void ValidateArgumentTemplate(IEnumerable<string> arguments, ICollection<string> errors)
    {
        foreach (string argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                errors.Add("ArgumentTemplate must not contain blank arguments.");
                continue;
            }

            int searchIndex = 0;
            while (true)
            {
                int openIndex = argument.IndexOf('{', searchIndex);
                if (openIndex < 0)
                {
                    break;
                }

                int closeIndex = argument.IndexOf('}', openIndex);
                if (closeIndex < 0)
                {
                    errors.Add($"Argument '{argument}' contains an unclosed token.");
                    break;
                }

                string token = argument[openIndex..(closeIndex + 1)];
                if (!AllowedTokens.Contains(token, StringComparer.Ordinal))
                {
                    errors.Add($"Argument '{argument}' contains unsupported token '{token}'.");
                }

                searchIndex = closeIndex + 1;
            }
        }
    }

    private static bool IsPlainFileName(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.IndexOfAny(['/', '\\', ':']) < 0
            && value != "."
            && value != "..";
    }

    private static void RequireNotBlank(string value, string name, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} must not be blank.");
        }
    }

    private static void RequireExact(string actual, string expected, string name, ICollection<string> errors)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add($"{name} must be '{expected}'.");
        }
    }

    private static void RequireOneOf(string actual, IReadOnlyCollection<string> allowed, string name, ICollection<string> errors)
    {
        if (!allowed.Contains(actual, StringComparer.Ordinal))
        {
            errors.Add($"{name} must be one of: {string.Join(", ", allowed)}.");
        }
    }
}
