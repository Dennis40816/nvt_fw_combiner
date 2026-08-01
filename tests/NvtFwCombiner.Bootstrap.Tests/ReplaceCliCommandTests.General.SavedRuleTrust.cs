using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>The public manual runner cannot accept caller-forged Saved Rule authority.</summary>
    [Fact]
    public void PublicGeneralReplaceRunnerDoesNotExposeSavedRulePolicy()
    {
        Assert.DoesNotContain(
            typeof(WorkbenchCompositionService).GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static),
            method =>
                method.Name == "RunGeneralReplaceEphemeralDraftAsync" &&
                method.GetParameters().Any(parameter =>
                    parameter.ParameterType ==
                        typeof(GeneralSavedRuleResourcePolicy)));
    }

    private static (
        GeneralMappingDraftState Draft,
        GeneralSavedRuleResourcePolicy Policy) LoadTrustedGeneralReplaceRule(
            string rulePath,
            string referencePath,
            string? sourcePath)
    {
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchCompositionService.Nt51926GeneralReplaceReferenceSlotId] =
                referencePath,
        };
        if (sourcePath is not null)
        {
            bindings["source-bin"] = sourcePath;
        }

        SavedRuleV2DraftLoadResult<GeneralMappingDraftState> load =
            SavedRuleV2GeneralMergeDraftLoader.LoadGeneralReplace(
                rulePath,
                bindings,
                WorkbenchCompositionService
                    .GetNt51926GeneralReplaceSavedRuleAdmissionContext());
        Assert.True(
            load.IsValid,
            string.Join(
                Environment.NewLine,
                load.Issues.Select(static issue => issue.Message)));
        var lifecycle = new SavedRuleLifecycleSnapshot(
            load.ExecutionIdentity!,
            SavedRuleStorageKind.TrustedCatalog,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);
        return (
            load.Draft!,
            new GeneralSavedRuleResourcePolicy(
                lifecycle,
                load.ResourcePolicy!.Limits));
    }
}
