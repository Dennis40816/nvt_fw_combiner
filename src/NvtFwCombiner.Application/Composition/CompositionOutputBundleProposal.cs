using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Editable default bundle folder name paired with the accepted output naming projection that produced it.</summary>
public sealed class CompositionOutputBundleProposal
{
    internal CompositionOutputBundleProposal(
        string folderName,
        CompositionOutputPreparation outputPreparation,
        DateTimeOffset? resolvedAtUtc,
        CompositionOutputBundleAdmission admission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        ArgumentNullException.ThrowIfNull(outputPreparation);
        ArgumentNullException.ThrowIfNull(admission);
        FolderName = folderName;
        OutputPreparation = outputPreparation;
        ResolvedAtUtc = resolvedAtUtc;
        Admission = admission;
        Sources = Array.AsReadOnly([
            .. admission.Sources.Select(static source => source.Summary),
        ]);
    }

    /// <summary>Application-proposed plain folder name that the host may let the user edit.</summary>
    public string FolderName { get; }

    /// <summary>The exact output-name preparation resolved in the same Application call.</summary>
    public CompositionOutputPreparation OutputPreparation { get; }

    /// <summary>The injected UTC instant retained by typed output naming, when that route uses one.</summary>
    public DateTimeOffset? ResolvedAtUtc { get; }

    /// <summary>Exact path-free accepted source manifest in canonical binding order.</summary>
    public IReadOnlyList<CompositionOutputBundleSourceSummary> Sources { get; }

    internal CompositionOutputBundleAdmission Admission { get; }

    /// <summary>Applies an editable host destination to this exact prepared admission.</summary>
    public CompositionOutputBundleIntent CreateIntent(
        string parentDirectory,
        string folderName,
        string? additionalDeliveryKind = null)
    {
        return new CompositionOutputBundleIntent(
            Admission,
            parentDirectory,
            folderName,
            additionalDeliveryKind);
    }
}

/// <summary>Derives bundle defaults from accepted typed naming facts without parsing filenames in Presentation.</summary>
internal static class CompositionOutputBundleProposer
{
    internal static CompositionOutputBundleProposal Create(
        ActiveSessionSnapshot session,
        CompositionOutputPreparation outputPreparation,
        ICompositionArtifactIdentityPolicy identityPolicy)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(outputPreparation);
        ArgumentNullException.ThrowIfNull(identityPolicy);
        OutputNamingSummary? naming = outputPreparation.OutputName.OutputNaming;
        string folderName = CreateFolderName(session.WorkflowId, outputPreparation);
        CompositionOutputBundleAdmission admission = new(
            session,
            outputPreparation,
            CompositionOutputBundleSourcePlanner.Create(session, identityPolicy));
        return new CompositionOutputBundleProposal(
            folderName,
            outputPreparation,
            naming?.ResolvedAtUtc,
            admission);
    }

    internal static string CreateFolderName(
        string workflowId,
        CompositionOutputPreparation outputPreparation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(outputPreparation);
        OutputNamingSummary? naming = outputPreparation.OutputName.OutputNaming;
        return StringComparer.Ordinal.Equals(workflowId, ExperienceIds.StandardMerge)
            ? CreateStandardMergeFolderName(naming ?? throw new InvalidOperationException(
                "Standard Merge bundle naming requires accepted typed output-naming facts."))
            : $"{GetCanonicalOutputBasename(
                naming?.AutomaticFileName ?? outputPreparation.OutputName.FileName)}_bundle";
    }

    private static string CreateStandardMergeFolderName(OutputNamingSummary naming)
    {
        string date = GetToken(naming, "date");
        string expectedDate = naming.ResolvedAtUtc.UtcDateTime.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);
        return StringComparer.Ordinal.Equals(date, expectedDate)
            ? $"{GetToken(naming, "ic")}_D{GetToken(naming, "dp-version")}" +
                $"T{GetToken(naming, "tp-version")}_{date}_bundle"
            : throw new InvalidOperationException(
                "Standard Merge bundle date must come from the accepted output-naming UTC instant.");
    }

    private static string GetToken(OutputNamingSummary naming, string tokenId)
    {
        OutputNamingTokenSummary token = naming.Tokens.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.TokenId, tokenId));
        return string.IsNullOrWhiteSpace(token.Value)
            ? throw new InvalidOperationException(
                $"Bundle naming token '{tokenId}' has no accepted value.")
            : token.Value;
    }

    private static string GetCanonicalOutputBasename(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        int extensionSeparator = fileName.LastIndexOf('.');
        return extensionSeparator > 0
            ? fileName[..extensionSeparator]
            : fileName;
    }
}
