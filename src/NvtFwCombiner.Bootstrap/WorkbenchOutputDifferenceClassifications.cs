using NvtFwCombiner.Contracts.Reports;

namespace NvtFwCombiner.Bootstrap;

/// <summary>UI-facing projection of stable report output-difference classification tokens.</summary>
public static class WorkbenchOutputDifferenceClassifications
{
    /// <summary>Difference bytes are produced by an explicit replacement mapping or staged replacement source.</summary>
    public const string DeclaredReplacement = OutputDifferenceClassifications.DeclaredReplacement;

    /// <summary>Difference bytes are produced by an approved postbuild CRC/header refresh range.</summary>
    public const string PostbuildCrcHeader = OutputDifferenceClassifications.PostbuildCrcHeader;

    /// <summary>Difference bytes occur inside a range that should have stayed equal to the reference base.</summary>
    public const string PreservedReference = OutputDifferenceClassifications.PreservedReference;

    /// <summary>Difference bytes are outside accepted replacement and postbuild refresh ranges.</summary>
    public const string Unexpected = OutputDifferenceClassifications.Unexpected;
}
