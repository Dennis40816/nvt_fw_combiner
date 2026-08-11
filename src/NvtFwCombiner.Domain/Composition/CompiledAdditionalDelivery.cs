namespace NvtFwCombiner.Domain.Composition;

/// <summary>One optional artifact derived from the immutable completed primary output.</summary>
public sealed class CompiledAdditionalDelivery
{
    /// <summary>Stable kind for the AB A-bank FlashCode artifact.</summary>
    public const string AbAFlashCodeKind = "ab-a-flashcode";

    /// <summary>Canonical A-bank filename rendered from the accepted AB output-naming tokens.</summary>
    public const string AbAFlashCodeFileNameTemplate =
        "NT{ic}_FlashCode_{dp-a}{tp-a}_{date}.bin";

    private readonly string[] _requiredTokenIds;

    internal CompiledAdditionalDelivery(
        string kind,
        ByteRange sourceRange,
        string fileNameTemplate,
        IEnumerable<string> requiredTokenIds)
    {
        Kind = RequiredValue.NotBlank(kind);
        if (sourceRange.Length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRange),
                "Additional delivery source ranges must be non-empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameTemplate);
        if (fileNameTemplate.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new ArgumentException(
                "Additional delivery templates must be plain file names.",
                nameof(fileNameTemplate));
        }

        _requiredTokenIds = ImmutableStringSnapshot.Create(
            requiredTokenIds,
            nameof(requiredTokenIds),
            "Additional deliveries require at least one naming token.",
            "Additional delivery token ids must be non-empty.",
            "Additional delivery token ids must be unique.");
        SourceRange = sourceRange;
        FileNameTemplate = fileNameTemplate;
        RequiredTokenIds = Array.AsReadOnly(_requiredTokenIds);
    }

    /// <summary>Stable delivery kind selected by the compiled profile.</summary>
    public string Kind { get; }

    /// <summary>Exact range extracted from the completed primary output.</summary>
    public ByteRange SourceRange { get; }

    /// <summary>Unrendered plain-file-name template.</summary>
    public string FileNameTemplate { get; }

    /// <summary>Accepted primary-output naming tokens required by the template.</summary>
    public IReadOnlyList<string> RequiredTokenIds { get; }
}
