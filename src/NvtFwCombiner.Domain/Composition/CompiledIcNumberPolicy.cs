namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed IC-number input policy compiled into a composition artifact.</summary>
public enum CompiledIcNumberPolicy
{
    /// <summary>The composition does not accept IC-number input.</summary>
    NotApplicable,

    /// <summary>The caller selects one direct IC number.</summary>
    SingleSelector,

    /// <summary>The caller selects an IC through a cascading selector.</summary>
    CascadeSelector,

    /// <summary>The caller supplies an approved positive numeric IC count.</summary>
    NumericSelector,
}
