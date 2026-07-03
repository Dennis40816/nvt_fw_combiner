namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares how a profile expects the UI to collect IC number context.</summary>
public enum IcNumberInputMode
{
    /// <summary>One direct IC number selector or input.</summary>
    SingleSelector,

    /// <summary>Cascading IC number selection, such as family followed by concrete IC number.</summary>
    CascadeSelector,

    /// <summary>Positive integer IC count for approved postbuild exceptions.</summary>
    NumericSelector,
}
