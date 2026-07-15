using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application binding from an address space to a report-safe id and infrastructure artifact locator.</summary>
public sealed class InputArtifactBinding
{
    /// <summary>Creates an immutable input artifact binding.</summary>
    public InputArtifactBinding(
        string addressSpaceId,
        string bindingId,
        string artifactId,
        string? originalFileName = null,
        CompiledInputArtifactClass? artifactClass = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        EnsureReportSafeId(bindingId);
        if (originalFileName is not null)
        {
            EnsureOriginalFileName(originalFileName);
        }

        if (artifactClass is { } value && !Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(artifactClass), value, "Unknown input artifact class.");
        }

        AddressSpaceId = addressSpaceId;
        BindingId = bindingId;
        ArtifactId = artifactId;
        OriginalFileName = originalFileName;
        ArtifactClass = artifactClass;
    }

    /// <summary>Address space populated by this binding.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Report-safe input id that never contains a host path.</summary>
    public string BindingId { get; }

    /// <summary>Infrastructure-specific artifact locator used only by artifact reader adapters.</summary>
    public string ArtifactId { get; }

    /// <summary>Original plain file name retained for V2 profile acceptance and traceability; null for legacy bindings.</summary>
    public string? OriginalFileName { get; }

    /// <summary>Caller-declared typed slot assertion matched to a V2 compiled input slot; null for legacy bindings.</summary>
    public CompiledInputArtifactClass? ArtifactClass { get; }

    private static void EnsureReportSafeId(string value)
    {
        if (value.IndexOfAny(['/', '\\', ':']) >= 0 || value is "." or "..")
        {
            throw new ArgumentException("Binding id must be report-safe and must not contain path syntax.", nameof(value));
        }
    }

    private static void EnsureOriginalFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(['/', '\\', ':']) >= 0 ||
            value is "." or ".." ||
            value.Any(char.IsControl) ||
            Path.GetFileName(value) != value)
        {
            throw new ArgumentException(
                "Original file name must be a plain filename without path or control syntax.",
                nameof(value));
        }
    }
}
