using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Closed host-expanded tokens permitted in approved external combiner manifests.</summary>
public static class ExternalCombinerStagingTokens
{
    /// <summary>Host-created target-image staging file token.</summary>
    public const string WorkBin = "{staging.workBin}";

    /// <summary>Host-created transformed-output staging file token.</summary>
    public const string OutputBin = "{staging.outputBin}";

    /// <summary>Host-created private staging-directory token.</summary>
    public const string RunDirectory = "{staging.runDir}";

    /// <summary>Returns a closed named-artifact token for one valid artifact id.</summary>
    public static string Artifact(string artifactId)
    {
        ExternalProcessorStagedArtifact.ValidateArtifactId(artifactId, nameof(artifactId));
        return $"{{staging.artifact.{artifactId}}}";
    }

    /// <summary>Returns whether a complete token is one of the approved host expansions.</summary>
    public static bool IsSupported(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token is WorkBin or OutputBin or RunDirectory || TryGetArtifactId(token, out _);
    }

    /// <summary>Returns validation errors for a host-expanded command argument template.</summary>
    public static IReadOnlyList<string> FindArgumentTemplateErrors(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var errors = new List<string>();
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
                if (!IsSupported(token))
                {
                    errors.Add($"Argument '{argument}' contains unsupported token '{token}'.");
                }

                searchIndex = closeIndex + 1;
            }
        }

        return errors;
    }

    /// <summary>Parses one complete named-artifact staging token.</summary>
    public static bool TryGetArtifactId(string token, out string artifactId)
    {
        const string Prefix = "{staging.artifact.";
        ArgumentNullException.ThrowIfNull(token);
        artifactId = string.Empty;
        if (!token.StartsWith(Prefix, StringComparison.Ordinal) || !token.EndsWith('}'))
        {
            return false;
        }

        string candidate = token[Prefix.Length..^1];
        if (!ExternalProcessorStagedArtifact.IsValidArtifactId(candidate))
        {
            return false;
        }

        artifactId = candidate;
        return true;
    }
}
