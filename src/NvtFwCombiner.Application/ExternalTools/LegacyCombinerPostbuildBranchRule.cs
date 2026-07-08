namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>One accepted IC number token for selecting a legacy postbuild branch.</summary>
public sealed class LegacyCombinerPostbuildBranchRule
{
    /// <summary>Creates a normalized branch rule from postbuild script evidence.</summary>
    public LegacyCombinerPostbuildBranchRule(string token, LegacyCombinerPostbuildBranch branch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        Token = NormalizeToken(token);
        Branch = branch;
    }

    /// <summary>Normalized user token, such as single, cascade, 1, 2, or 3.</summary>
    public string Token { get; }

    /// <summary>Branch selected by the token.</summary>
    public LegacyCombinerPostbuildBranch Branch { get; }

    internal static string NormalizeToken(string token)
    {
        string normalized = token.Trim();
        if (normalized.Length >= 2 &&
            normalized[0] == '(' &&
            normalized[^1] == ')')
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized.ToLowerInvariant();
    }
}
