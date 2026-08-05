namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies canonical NT-prefixed IC normalization stays identifier-owned.</summary>
    [Fact]
    public void IcIdentifierNormalizationStaysIdentifierOwned()
    {
        string identifier = ReadText("src/NvtFwCombiner.Profiles/IcIdentifier.cs");
        string[] consumers =
        [
            ReadText("src/NvtFwCombiner.Bootstrap/CanonicalAuthoringAdapter.DpReplace.cs"),
            ReadText("src/NvtFwCombiner.Bootstrap/CanonicalCapabilityProjection.cs"),
            ReadText("src/NvtFwCombiner.Bootstrap/CompositionOutputNaming.cs"),
            ReadText("src/NvtFwCombiner.Bootstrap/CanonicalCapabilityProjection.DpReplace.cs"),
        ];

        const string implementationToken = "StartsWith(\"NT\", StringComparison.OrdinalIgnoreCase)";
        Assert.Contains("public static string Normalize", identifier, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(identifier, implementationToken));
        foreach (string consumer in consumers)
        {
            Assert.Contains("IcIdentifier.Normalize", consumer, StringComparison.Ordinal);
            Assert.DoesNotContain(implementationToken, consumer, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies workflow ids stay Domain-backed instead of being duplicated in profile adapters.</summary>
    [Fact]
    public void WorkflowIdsStayDomainBacked()
    {
        string workflowIds = ReadText("src/NvtFwCombiner.Profiles/IcWorkflowIds.cs");
        string experienceIds = ReadText("src/NvtFwCombiner.Domain/Composition/ExperienceIds.cs");
        string experienceCatalog = ReadText("src/NvtFwCombiner.Domain/Composition/ExperienceCatalog.cs");
        string profileSources = ReadProfileSources();
        string profilesWithoutWorkflowIds = profileSources.Replace(
            workflowIds,
            string.Empty,
            StringComparison.Ordinal);
        string bootstrapSources = ReadBootstrapSources();

        Assert.Contains("public const string StandardMerge = \"standard-merge\"", experienceIds, StringComparison.Ordinal);
        Assert.Contains("public const string AbMerge = \"ab-merge\"", experienceIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralMerge = \"general-merge\"", experienceIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpReplace = \"dp-replace\"", experienceIds, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRamReplace = \"ctrlram-replace\"", experienceIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralReplace = \"general-replace\"", experienceIds, StringComparison.Ordinal);
        Assert.Contains("new(ExperienceIds.StandardMerge,", experienceCatalog, StringComparison.Ordinal);
        Assert.Contains("new(ExperienceIds.AbMerge,", experienceCatalog, StringComparison.Ordinal);
        Assert.Contains("new(ExperienceIds.GeneralMerge,", experienceCatalog, StringComparison.Ordinal);
        Assert.Contains("new(ExperienceIds.DpReplace,", experienceCatalog, StringComparison.Ordinal);
        Assert.Contains("new(ExperienceIds.CtrlRamReplace,", experienceCatalog, StringComparison.Ordinal);
        Assert.Contains("new(ExperienceIds.GeneralReplace,", experienceCatalog, StringComparison.Ordinal);
        Assert.Contains("public const string StandardMerge = ExperienceIds.StandardMerge", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpReplace = ExperienceIds.DpReplace", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRamReplace = ExperienceIds.CtrlRamReplace", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralMerge = ExperienceIds.GeneralMerge", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralReplace = ExperienceIds.GeneralReplace", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("IcWorkflowIds.CtrlRamReplace", profilesWithoutWorkflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("IcWorkflowIds.GeneralReplace", profilesWithoutWorkflowIds, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowIds.StandardMerge", bootstrapSources, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowIds.DpReplace", bootstrapSources, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowIds.CtrlRamReplace", bootstrapSources, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowIds.GeneralMerge", bootstrapSources, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowIds.GeneralReplace", bootstrapSources, StringComparison.Ordinal);

        foreach (string workflowLiteral in new[]
        {
            "\"standard-merge\"",
            "\"dp-replace\"",
            "\"ctrlram-replace\"",
            "\"general-merge\"",
            "\"general-replace\"",
        })
        {
            Assert.DoesNotContain(workflowLiteral, experienceCatalog, StringComparison.Ordinal);
            Assert.DoesNotContain(workflowLiteral, workflowIds, StringComparison.Ordinal);
            Assert.DoesNotContain(workflowLiteral, profilesWithoutWorkflowIds, StringComparison.Ordinal);
            Assert.DoesNotContain(workflowLiteral, bootstrapSources, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies shared address-space ids stay Domain-owned and adapter-projected.</summary>
    [Fact]
    public void SharedAddressSpaceIdsStayCatalogOwned()
    {
        string addressSpaceIds = ReadText("src/NvtFwCombiner.Domain/Composition/CompositionAddressSpaceIds.cs");
        string workbenchAddressSpaceIds = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchAddressSpaceIds.cs");
        string profileSources = ReadProfileSources();
        string bootstrapSources = ReadBootstrapSources()
            .Replace(workbenchAddressSpaceIds, string.Empty, StringComparison.Ordinal);
        string presentationSources = ReadPresentationSources();

        Assert.Contains("public const string OutputImage = \"output-image\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string ReferenceBase = \"reference-base\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpInput = \"dp-input\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string TpInput = \"tp-input\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string LdcInput = \"ldc-input\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpReplacement = \"dp-replacement\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string LdcReplacement = \"ldc-replacement\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRamReplacement = \"ctrlram-replacement\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string DynamicCtrlRamReplacementPrefix = \"replace-ctrlram-\"", addressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("CompositionAddressSpaceIds.OutputImage", profileSources, StringComparison.Ordinal);
        Assert.Contains("CompositionAddressSpaceIds.DpReplacement", bootstrapSources, StringComparison.Ordinal);
        Assert.Contains("public const string DpInput = CompositionAddressSpaceIds.DpInput;", workbenchAddressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string TpInput = CompositionAddressSpaceIds.TpInput;", workbenchAddressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string LdcInput = CompositionAddressSpaceIds.LdcInput;", workbenchAddressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpReplacement = CompositionAddressSpaceIds.DpReplacement;", workbenchAddressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("public const string DynamicCtrlRamReplacementPrefix = CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix;", workbenchAddressSpaceIds, StringComparison.Ordinal);
        Assert.Contains("WorkbenchAddressSpaceIds.DpInput", presentationSources, StringComparison.Ordinal);
        Assert.Contains(
            "DynamicCtrlRamReplacementIds.TryFormatDisplayLabel",
            ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferenceLabels.cs"),
            StringComparison.Ordinal);

        foreach (string addressSpaceLiteral in new[]
        {
            "\"output-image\"",
            "\"reference-base\"",
            "\"dp-input\"",
            "\"tp-input\"",
            "\"ldc-input\"",
            "\"dp-replacement\"",
            "\"ldc-replacement\"",
            "\"ctrlram-replacement\"",
            "\"replace-ctrlram-\"",
        })
        {
            Assert.DoesNotContain(addressSpaceLiteral, profileSources, StringComparison.Ordinal);
            Assert.DoesNotContain(addressSpaceLiteral, bootstrapSources, StringComparison.Ordinal);
            Assert.DoesNotContain(addressSpaceLiteral, presentationSources, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies profile JSON vocabulary remains Contracts-owned instead of masquerading as address-space ids.</summary>
    [Fact]
    public void CompositionProfileWireTokensStayContractOwned()
    {
        string profileWireTokens = ReadText(
            "src/NvtFwCombiner.Contracts/Profiles/CompositionProfileWireTokens.cs");
        string profileSources = ReadProfileSources();

        Assert.Contains(
            "public const string OutputImageSpaceKind = \"output-image\"",
            profileWireTokens,
            StringComparison.Ordinal);
        Assert.Contains(
            "public const string CtrlRamReplacementArtifactClass = \"ctrlram-replacement\"",
            profileWireTokens,
            StringComparison.Ordinal);
        Assert.Contains("CompositionProfileWireTokens.OutputImageSpaceKind", profileSources, StringComparison.Ordinal);
        Assert.Contains(
            "CompositionProfileWireTokens.CtrlRamReplacementArtifactClass",
            profileSources,
            StringComparison.Ordinal);
    }

    /// <summary>Verifies dynamic CtrlRAM replacement report labels are parsed by one Domain helper.</summary>
    [Fact]
    public void DynamicCtrlRamReplacementLabelsStayDomainOwned()
    {
        string domainHelper = ReadText("src/NvtFwCombiner.Domain/Composition/DynamicCtrlRamReplacementIds.cs");
        string applicationLabels = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferenceLabels.cs");
        string presentationParser = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Parsing.cs");
        string workbenchSlotIds = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchSlotIds.cs");

        Assert.Contains("TryFormatDisplayLabel", domainHelper, StringComparison.Ordinal);
        Assert.Contains("FormatRegionDisplayLabel", domainHelper, StringComparison.Ordinal);
        Assert.Contains("DynamicCtrlRamReplacementIds.TryFormatDisplayLabel", applicationLabels, StringComparison.Ordinal);
        Assert.Contains("DynamicCtrlRamReplacementIds.TryFormatDisplayLabel", workbenchSlotIds, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.TryFormatReplaceCtrlRamLabel", presentationParser, StringComparison.Ordinal);
        Assert.DoesNotContain("Split('-',", applicationLabels, StringComparison.Ordinal);
        Assert.DoesNotContain("Split('-',", presentationParser, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceCtrlRamPrefix", presentationParser, StringComparison.Ordinal);
    }

    /// <summary>Verifies IC number selector tokens are shared instead of repeated in catalogs and adapters.</summary>
    [Fact]
    public void IcNumberSelectionTokensStayDomainOwned()
    {
        string domainTokens = ReadText("src/NvtFwCombiner.Domain/Composition/IcNumberSelectionTokens.cs");
        string flashMapChoices = ReadText("src/NvtFwCombiner.Application/FlashMaps/IcNumberChoicePolicy.cs");
        string postbuildSelector = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanSelector.cs");
        string workbenchTokens = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchIcNumberTokens.cs");
        string workbenchSelections = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchIcNumberSelections.cs");
        string workflowContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContext.cs");

        Assert.Contains("public const string SingleChip = \"single\"", domainTokens, StringComparison.Ordinal);
        Assert.Contains("public const string Cascade = \"cascade\"", domainTokens, StringComparison.Ordinal);
        Assert.Contains("IcNumberSelectionTokens.SingleChip", flashMapChoices, StringComparison.Ordinal);
        Assert.Contains("IcNumberSelectionTokens.SingleChip", postbuildSelector, StringComparison.Ordinal);
        Assert.Contains("IcNumberSelectionTokens.Cascade", postbuildSelector, StringComparison.Ordinal);
        Assert.Contains("public const string SingleChip = IcNumberSelectionTokens.SingleChip;", workbenchTokens, StringComparison.Ordinal);
        Assert.Contains("public const string CascadeTwoToEight = \"cascade_2to8\";", workbenchTokens, StringComparison.Ordinal);
        Assert.Contains("IcNumberSelectionTokens.IsSingle(number)", workbenchSelections, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberTokens.SingleChip", workflowContext, StringComparison.Ordinal);
        Assert.DoesNotContain("\"single\"", flashMapChoices, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cascade\"", flashMapChoices, StringComparison.Ordinal);
    }

    /// <summary>Verifies shared composition engine issue codes stay Domain-owned.</summary>
    [Fact]
    public void CoreCompositionIssueCodesStayDomainOwned()
    {
        string issueCodes = ReadText("src/NvtFwCombiner.Domain/Composition/CompositionIssueCodes.cs");
        string domainSources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Domain"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string testSources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(Root.FullName, "tests"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string sourcesWithoutCatalog = (domainSources + Environment.NewLine + testSources)
            .Replace(issueCodes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("CompositionIssueCodes.InputAddressSpaceLengthMismatch", domainSources, StringComparison.Ordinal);
        Assert.Contains("CompositionIssueCodes.InputAddressSpaceTruncated", testSources, StringComparison.Ordinal);
        foreach (string issueCodeLiteral in new[]
        {
            QuotedIssueCode("input", "address-space.missing"),
            QuotedIssueCode("input", "mutable-address-space.missing"),
            QuotedIssueCode("input", "address-space.length-mismatch"),
            QuotedIssueCode("input", "address-space.truncated"),
            QuotedIssueCode("execution", "capacity.unsupported"),
            QuotedIssueCode("execution", "external-processor.unavailable"),
            QuotedIssueCode("execution", "external-processor.failed"),
            QuotedIssueCode("execution", "external-processor.length-mismatch"),
        })
        {
            Assert.DoesNotContain(issueCodeLiteral, sourcesWithoutCatalog, StringComparison.Ordinal);
        }
    }

    private static string QuotedIssueCode(string prefix, string suffix)
    {
        return $"\"{prefix}.{suffix}\"";
    }
}
