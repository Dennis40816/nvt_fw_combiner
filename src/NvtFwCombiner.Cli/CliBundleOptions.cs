using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Cli;

/// <summary>Shared CLI syntax and host adaptation for Application-owned bundle delivery.</summary>
internal static class CliBundleOptions
{
    internal const string ParentOption = "--bundle-parent";
    internal const string NameOption = "--bundle-name";

    internal static bool IsEnabled(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ContainsKey(ParentOption);
    }

    internal static bool TryValidateCombination(
        string action,
        IReadOnlyDictionary<string, string> values,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(error);
        bool hasParent = values.ContainsKey(ParentOption);
        bool hasName = values.ContainsKey(NameOption);
        if (!hasParent && !hasName)
        {
            return true;
        }

        if (!StringComparer.Ordinal.Equals(action, "build"))
        {
            error.WriteLine("error: --bundle-parent is available only for build");
            return false;
        }

        if (hasName && !hasParent)
        {
            error.WriteLine("error: --bundle-name requires --bundle-parent");
            return false;
        }

        if (values.ContainsKey("--output"))
        {
            error.WriteLine("error: --bundle-parent cannot be combined with --output");
            return false;
        }

        return true;
    }

    internal static bool TryCreateIntent(
        ICompositionOutputNaming outputNaming,
        ActiveSessionSnapshot acceptedSession,
        IReadOnlyDictionary<string, string> values,
        TextWriter error,
        out CompositionOutputBundleIntent? intent,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null,
        string? additionalDeliveryKind = null)
    {
        ArgumentNullException.ThrowIfNull(outputNaming);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(error);
        intent = null;
        if (!IsEnabled(values))
        {
            return true;
        }

        CompositionOutputBundleProposal proposal = outputNaming.ResolveAcceptedBundleProposal(
            acceptedSession,
            ctrlRamVersionEdit);
        string parentDirectory = values[ParentOption];
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            error.WriteLine(
                $"error: {CompositionOutputBundleValidationIssueCodes.ParentInvalid}: Bundle parent directory must not be empty.");
            return false;
        }

        string folderName = values.GetValueOrDefault(NameOption) ?? proposal.FolderName;
        try
        {
            intent = proposal.CreateIntent(
                parentDirectory,
                folderName,
                additionalDeliveryKind);
        }
        catch (ArgumentException exception) when (
            StringComparer.Ordinal.Equals(
                exception.ParamName,
                "additionalDeliveryKind"))
        {
            error.WriteLine($"error: bundle.additional-delivery-unavailable: {exception.Message}");
            return false;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(
                $"error: {CompositionOutputBundleValidationIssueCodes.NameInvalid}: {exception.Message}");
            return false;
        }

        CompositionOutputBundleDestinationValidation validation =
            outputNaming.ValidateBundleDestination(intent);
        if (validation.IsValid)
        {
            return true;
        }

        foreach (CompositionOutputBundleValidationIssue issue in validation.Issues)
        {
            error.WriteLine($"error: {issue.Code}: {issue.Message}");
        }

        intent = null;
        return false;
    }

    internal static async Task PrintReceiptAsync(
        CompositionRunResult result,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);
        CompositionOutputBundleDeliverySummary? bundle = result.Report.BundleDelivery;
        if (bundle is null)
        {
            return;
        }

        await output.WriteLineAsync($"Bundle: {bundle.ResolvedDirectory}").ConfigureAwait(false);
        await output.WriteLineAsync("Bundle artifacts:").ConfigureAwait(false);
        foreach (CompositionOutputBundleDeliveredArtifactSummary artifact in bundle.Artifacts)
        {
            await output.WriteLineAsync(
                    $"  {artifact.Role}: {artifact.DeliveredFileName} ({artifact.Sha256})")
                .ConfigureAwait(false);
        }
    }
}
