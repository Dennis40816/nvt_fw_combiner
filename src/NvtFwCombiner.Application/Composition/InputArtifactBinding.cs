namespace NvtFwCombiner.Application.Composition;

/// <summary>Application binding from an address space to a report-safe id and infrastructure artifact locator.</summary>
public sealed class InputArtifactBinding
{
    /// <summary>Creates an immutable input artifact binding.</summary>
    public InputArtifactBinding(string addressSpaceId, string bindingId, string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        EnsureReportSafeId(bindingId);

        AddressSpaceId = addressSpaceId;
        BindingId = bindingId;
        ArtifactId = artifactId;
    }

    /// <summary>Address space populated by this binding.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Report-safe input id that never contains a host path.</summary>
    public string BindingId { get; }

    /// <summary>Infrastructure-specific artifact locator used only by artifact reader adapters.</summary>
    public string ArtifactId { get; }

    private static void EnsureReportSafeId(string value)
    {
        if (value.IndexOfAny(['/', '\\', ':']) >= 0 || value is "." or "..")
        {
            throw new ArgumentException("Binding id must be report-safe and must not contain path syntax.", nameof(value));
        }
    }
}
