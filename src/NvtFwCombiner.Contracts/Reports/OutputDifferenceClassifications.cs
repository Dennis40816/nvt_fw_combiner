namespace NvtFwCombiner.Contracts.Reports;

/// <summary>Stable machine-readable classifications for Replace output difference report rows.</summary>
public static class OutputDifferenceClassifications
{
    /// <summary>Difference bytes are produced by an explicit replacement mapping or staged replacement source.</summary>
    public const string DeclaredReplacement = "DeclaredReplacement";

    /// <summary>Difference bytes are produced by an approved postbuild CRC/header refresh range.</summary>
    public const string PostbuildCrcHeader = "PostbuildCrcHeader";

    /// <summary>Difference bytes occur inside a range that should have stayed equal to the reference base.</summary>
    public const string PreservedReference = "PreservedReference";

    /// <summary>Difference bytes are outside accepted replacement and postbuild refresh ranges.</summary>
    public const string Unexpected = "Unexpected";
}
