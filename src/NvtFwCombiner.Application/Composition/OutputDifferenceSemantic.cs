namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Stable semantic subject for a final-output difference. It is emitted by Application policy so report renderers
/// do not infer firmware fields from offsets.
/// </summary>
public sealed class OutputDifferenceSemantic
{
    /// <summary>Creates a semantic output-difference subject.</summary>
    public OutputDifferenceSemantic(
        string categoryId,
        string categoryLabel,
        string subjectId,
        string subjectLabel,
        string explanation,
        string? parentId = null,
        string? parentLabel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);

        CategoryId = categoryId;
        CategoryLabel = categoryLabel;
        SubjectId = subjectId;
        SubjectLabel = subjectLabel;
        Explanation = explanation;
        ParentId = string.IsNullOrWhiteSpace(parentId) ? categoryId : parentId;
        ParentLabel = string.IsNullOrWhiteSpace(parentLabel) ? categoryLabel : parentLabel;
    }

    /// <summary>Stable top-level binary category id.</summary>
    public string CategoryId { get; }

    /// <summary>Human-facing category label.</summary>
    public string CategoryLabel { get; }

    /// <summary>Stable field or section subject id.</summary>
    public string SubjectId { get; }

    /// <summary>Human-facing field or section title.</summary>
    public string SubjectLabel { get; }

    /// <summary>Plain-language explanation for the expected or review-required difference.</summary>
    public string Explanation { get; }

    /// <summary>Stable physical parent section id used to group report rows before their field subject.</summary>
    public string ParentId { get; }

    /// <summary>Human-facing physical parent section title used to group report rows before their field subject.</summary>
    public string ParentLabel { get; }
}
