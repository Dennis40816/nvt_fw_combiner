namespace NvtFwCombiner.Application.Composition;

/// <summary>Optional host delivery intent for one atomic output-and-sources folder.</summary>
public sealed class CompositionOutputBundleIntent
{
    internal CompositionOutputBundleIntent(
        CompositionOutputBundleAdmission admission,
        string parentDirectory,
        string folderName,
        string? additionalDeliveryKind = null)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        EnsurePlainFolderName(folderName);
        Admission = admission;
        ParentDirectory = parentDirectory;
        FolderName = folderName;
        AdditionalDelivery = CompositionExecutionBundleDelivery.ResolveAdditionalDelivery(
            admission.OutputPreparation.AdditionalDeliveries,
            additionalDeliveryKind);
    }

    internal CompositionOutputBundleAdmission Admission { get; }

    /// <summary>Host-selected existing parent directory.</summary>
    public string ParentDirectory { get; }

    /// <summary>Validated plain proposed folder name; Infrastructure applies platform validation.</summary>
    public string FolderName { get; }

    /// <summary>Selected compiled additional delivery retained by this exact prepared admission.</summary>
    internal CompositionAdditionalDeliveryPlan? AdditionalDelivery { get; }

    private static void EnsurePlainFolderName(string folderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        if (folderName is "." or ".." ||
            folderName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            folderName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Bundle folder name must be one plain name without path or control syntax.",
                nameof(folderName));
        }
    }
}
