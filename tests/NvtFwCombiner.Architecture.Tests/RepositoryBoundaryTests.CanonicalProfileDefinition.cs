using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The normalized profile aggregate and its semantic values are Domain-owned.</summary>
    [Fact]
    public void CanonicalProfileDefinitionIsDomainOwned()
    {
        string[] definitionFiles =
        [
            "CanonicalProfileValueRules.cs",
            "CompositionProfileDefinition.cs",
            "CompositionProfileDefinition.Graph.cs",
            "CompositionProfileHeader.cs",
            "CompositionProfileMapBinding.cs",
            "CompositionProfileProcessor.cs",
            "CompositionProfileSpace.cs",
            "CompositionProfileView.cs",
        ];

        foreach (string file in definitionFiles)
        {
            Assert.True(File.Exists(Path.Combine(
                Root.FullName,
                "src",
                "NvtFwCombiner.Domain",
                "Composition",
                file)));
            Assert.False(File.Exists(Path.Combine(
                Root.FullName,
                "src",
                "NvtFwCombiner.Profiles",
                "V2",
                file)));
        }

        Assert.Contains(
            "namespace NvtFwCombiner.Domain.Composition;",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompositionProfileDefinition.cs"),
            StringComparison.Ordinal);

        string domainProfileSources = string.Join(
            Environment.NewLine,
            definitionFiles.Select(file => ReadText($"src/NvtFwCombiner.Domain/Composition/{file}")));
        Assert.DoesNotContain("schemaVersion", domainProfileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Contracts", domainProfileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", domainProfileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.Json", domainProfileSources, StringComparison.Ordinal);

        string profileSources = ReadProfileSources();
        Assert.DoesNotMatch(ProfileDefinitionDeclarationRegex(), profileSources);
        Assert.DoesNotContain(
            "IReadOnlyList<CompositionInputSlotDefinition> InputSlots { get; }",
            profileSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "IReadOnlyList<CompositionOperationDefinition> Operations { get; }",
            profileSources,
            StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2",
            "CompositionProfileValueRules.cs")));
        Assert.DoesNotContain("SemanticVersionPattern", profileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalToolBindingIdPattern", profileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyCombinerInvocationProfileIdPattern", profileSources, StringComparison.Ordinal);
        Assert.DoesNotMatch(ProfileRangeValidatorDeclarationRegex(), profileSources);

        string compositionNormalizerSources = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Profiles", "V2"),
                    "CompositionProfileNormalizer*.cs")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("OutputTokenBuilder", compositionNormalizerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateAudience", compositionNormalizerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateTopologyAuthoring", compositionNormalizerSources, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(
            compositionNormalizerSources,
            "ArgumentNullException.ThrowIfNull(document);"));
        Assert.Equal(1, CountOccurrences(
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledInputSlotRequirement.cs"),
            "internal CompiledInputSlotRequirement("));
        Assert.DoesNotContain(
            "CompiledInputLengthRequirementKind",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledInputSlotRequirement.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledValidationScalarLiteralKind",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledValidationRequirement.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledValidationKind",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledValidationRequirement.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledRegionAccessKind",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledRegionAccessContract.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public V2CompilationContextKind Kind",
            ReadText("src/NvtFwCombiner.Domain/Composition/V2CompilationContext.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledInputNormalizationKind",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledInputSlotRequirement.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ValidateDefaultOutputFileName(",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "IntegrityFingerprint",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs"),
            StringComparison.Ordinal);
    }

    /// <summary>Raw profile-admission vocabulary remains an internal normalization and compilation seam.</summary>
    [Fact]
    public void RawProfileAdmissionVocabularyRemainsInternal()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Domain",
            "Composition",
            "CompiledCapabilityAdmission.cs")));
        Assert.DoesNotContain("CompiledCapabilityAdmission", ReadDomainSources(), StringComparison.Ordinal);
        string capabilitySources = ReadText(
            "src/NvtFwCombiner.Domain/Firmware/FirmwareCapabilityFact.cs");
        Assert.DoesNotContain("public enum FirmwareCapabilityState", capabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class FirmwareCapabilityFact", capabilitySources, StringComparison.Ordinal);

        string familyRelationshipSources = ReadText(
            "src/NvtFwCombiner.Domain/Firmware/FirmwareFamilyRelationship.cs");
        Assert.DoesNotContain("public enum FirmwareSharedFactRole", familyRelationshipSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum FirmwareSharedFactKind", familyRelationshipSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class FirmwareSharedFactReference", familyRelationshipSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public abstract class FirmwareFamilyRelationship", familyRelationshipSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class PerfectFamilyRelationship", familyRelationshipSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class SharedFactRelationship", familyRelationshipSources, StringComparison.Ordinal);

        string regionTemplateSources = ReadText(
            "src/NvtFwCombiner.Domain/Firmware/FirmwareRegionTemplate.cs");
        Assert.DoesNotContain("public sealed record FirmwareRelativeRegion", regionTemplateSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class FirmwareRegionTemplate", regionTemplateSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class FirmwareRegionInstance", regionTemplateSources, StringComparison.Ordinal);

        string compiledRegionAccessSources = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledRegionAccessContract.cs");
        Assert.DoesNotContain("CompiledPhysicalRegionConstraint", compiledRegionAccessSources, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<FirmwareRegion> GoverningRegionChain", compiledRegionAccessSources, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ToCompiledRegionChain(",
            ReadText("src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.cs"),
            StringComparison.Ordinal);

        string familyNormalizerSources = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyNormalizationException.cs"),
            ReadText("src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareMetadataStructureDefinitionResolver.cs"),
            ReadText("src/NvtFwCombiner.Profiles/IcIdentifier.cs"));
        Assert.DoesNotContain("public sealed class FirmwareFamilyNormalizationException", familyNormalizerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("record FirmwareMetadataStructureDefinitionReference", familyNormalizerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface IFirmwareMetadataStructureDefinitionResolver", familyNormalizerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public static class IcIdentifier", familyNormalizerSources, StringComparison.Ordinal);

        string imageMapSources = ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareImageMap.cs");
        Assert.DoesNotContain("public FirmwareImageMap(", imageMapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public IReadOnlyList<FirmwareMapFactBinding<FirmwareRegionSet>> RegionSetBindings", imageMapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("public IReadOnlyList<FirmwareRegionSet> RegionSets", imageMapSources, StringComparison.Ordinal);

        string metadataDefinitionSources = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareMetadataLocator.cs"),
            ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareMetadataStructureDefinition.cs"),
            ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareTpFlashHeaderDefinition.cs"),
            ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareMetadataFieldSeries.cs"));
        foreach (string internalDefinitionType in new[]
                 {
                     "FirmwareMetadataLocator",
                     "FirmwareAbsoluteRangeLocator",
                     "FirmwareRegionRelativeLocator",
                     "FirmwareMarkerSelection",
                     "FirmwareUniqueMarkerSelection",
                     "FirmwareTerminalMarkerSelection",
                     "FirmwareMarkerRelativeLocator",
                     "FirmwareMetadataFieldSelectedBranch",
                     "FirmwareMetadataFieldSelectedLocator",
                     "FirmwareMetadataTypedDefinition",
                     "FirmwareMetadataNamedSpan",
                     "FirmwareTpFlashHeaderStoredAddressSemantics",
                     "FirmwareTpFlashHeaderFieldSemantics",
                     "FirmwareTpFlashHeaderDefinition",
                     "FirmwareMetadataFieldSeriesMember",
                     "FirmwareMetadataFieldSeriesApplicability",
                     "FirmwareMetadataFieldSeries",
                     "FirmwareMetadataFieldGroup",
                 })
        {
            Assert.DoesNotContain($"public abstract record {internalDefinitionType}", metadataDefinitionSources, StringComparison.Ordinal);
            Assert.DoesNotContain($"public sealed record {internalDefinitionType}", metadataDefinitionSources, StringComparison.Ordinal);
            Assert.DoesNotContain($"public abstract class {internalDefinitionType}", metadataDefinitionSources, StringComparison.Ordinal);
            Assert.DoesNotContain($"public sealed class {internalDefinitionType}", metadataDefinitionSources, StringComparison.Ordinal);
        }

        string validationDefinitionSources = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledValidationRequirement.cs");
        foreach (string internalValidationType in new[]
                 {
                     "CompiledValidationFieldReference",
                     "CompiledValidationScalarLiteral",
                     "CompiledValidationIntegerLiteral",
                     "CompiledValidationTextLiteral",
                     "CompiledMetadataValueValidation",
                     "CompiledPidSanityValidation",
                     "CompiledMetadataEqualityValidation",
                     "CompiledRejectMetadataBytePatternValidation",
                     "CompiledViewByteAssertionValidation",
                 })
        {
            Assert.DoesNotContain($"public abstract record {internalValidationType}", validationDefinitionSources, StringComparison.Ordinal);
            Assert.DoesNotContain($"public sealed record {internalValidationType}", validationDefinitionSources, StringComparison.Ordinal);
            Assert.DoesNotContain($"public sealed class {internalValidationType}", validationDefinitionSources, StringComparison.Ordinal);
        }
    }

    /// <summary>The plan compiler lowers closed semantic kinds without workflow-name branches or private range algebra.</summary>
    [Fact]
    public void V2PlanCompilerUsesClosedDomainSemantics()
    {
        string compilerRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2");
        string compilerSources = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(compilerRoot, "V2CompositionPlanCompiler*.cs")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("ExperienceIds.", compilerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateSupportedProfile(", compilerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateMapBoundOutputShape(", compilerSources, StringComparison.Ordinal);
        string trustedCompiler = ReadText(
            "src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.Compilation.cs");
        string compilationSources = string.Join(
            Environment.NewLine,
            compilerSources,
            trustedCompiler);
        Assert.DoesNotContain("ExperienceIds.", trustedCompiler, StringComparison.Ordinal);
        Assert.DoesNotContain("TrustedV2CompositionCompiler", trustedCompiler, StringComparison.Ordinal);
        foreach (string workflowLiteral in new[]
                 {
                     "\"standard-merge\"",
                     "\"ab-merge\"",
                     "\"general-merge\"",
                     "\"dp-replace\"",
                     "\"ctrlram-replace\"",
                     "\"general-replace\"",
                 })
        {
            Assert.DoesNotContain(workflowLiteral, compilationSources, StringComparison.Ordinal);
        }
        Assert.Empty(CompiledIcIdentityRegex().Matches(compilationSources));

        string profileSources = ReadProfileSources();
        Assert.DoesNotContain("V2LogicalOutputInputBinding", profileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("V2RuntimeReferenceReplaceInputBinding", profileSources, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(profileSources, "record V2ExplicitMappingInputBinding("));
        Assert.Equal(1, CountOccurrences(profileSources, "abstract class V2ExplicitMappingCompileRequest"));
        int planConstructionCount = CompositionPlanConstructionRegex().Count(compilerSources);
        int detailsConstructionCount = CompiledDetailsConstructionRegex().Count(compilerSources);
        Assert.NotEqual(0, planConstructionCount);
        Assert.NotEqual(0, detailsConstructionCount);
        Assert.Equal(
            planConstructionCount,
            CompositionPlanConstructionRegex().Count(profileSources));
        Assert.Equal(
            detailsConstructionCount,
            CompiledDetailsConstructionRegex().Count(profileSources));

        Assert.Equal(1, CountOccurrences(compilerSources, ".ExperienceId"));
        Assert.Equal(0, CountOccurrences(compilerSources, ".ModeId"));
        Assert.Equal(1, CountOccurrences(compilerSources, ".ProfileId"));
        Assert.Equal(1, CountOccurrences(compilerSources, ".FamilyId"));
        string contractLowering = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs");
        Assert.Contains("profile.ProfileId,", contractLowering, StringComparison.Ordinal);
        Assert.Contains("profile.Header.ExperienceId,", contractLowering, StringComparison.Ordinal);
        Assert.Contains(
            "profile.Output.AllowsRuntimeExecution(profile.CompositionKind)",
            compilerSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AllowsRuntimeExecution(",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private static IEnumerable<ByteRange> Subtract(",
            compilerSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private static bool IsSlotApplicable(",
            compilerSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private static bool MapSupportsSelectedOptionalSlots(",
            trustedCompiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetDeclaredWriteRanges(", compilerSources, StringComparison.Ordinal);
        Assert.DoesNotContain("TryResolveSourceAndTarget(", compilerSources, StringComparison.Ordinal);
        Assert.Contains(
            "internal bool IsInputSlotApplicable(",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompositionProfileDefinition.Graph.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "internal string? GetProfileOverlapError(",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompositionOperation.cs"),
            StringComparison.Ordinal);
    }

    /// <summary>Locks trusted compilation and bundle identity to their existing catalog owners.</summary>
    [Fact]
    public void TrustedCatalogDoesNotReintroduceCompilerOrBundleIdentityFacades()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2",
            "TrustedV2CompositionCompiler.cs")));

        string source = ReadText(
            "src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogSource.cs");
        _ = Assert.Single(BundleIdentityParameterRegex().Matches(source));
        Assert.Empty(LooseBundleIdentityParameterRegex().Matches(source));
        Assert.Empty(CatalogSourceConstructionRegex().Matches(ReadProfileSources()));
        _ = Assert.Single(CatalogSourceConstructionRegex().Matches(ReadBootstrapSources()));
    }

    [GeneratedRegex(
        @"\bnew\s+CompositionPlan\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CompositionPlanConstructionRegex();

    [GeneratedRegex(
        @"\bnew\s+V2CompiledCompositionDetails\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CompiledDetailsConstructionRegex();

    [GeneratedRegex(
        @"\bProfileBundleIdentity\s+bundleIdentity\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex BundleIdentityParameterRegex();

    [GeneratedRegex(
        @"\bstring\s+(?:bundleId|bundleVersion|bundleContentHash|trustAnchorBindingId)\s*,",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex LooseBundleIdentityParameterRegex();

    [GeneratedRegex(
        @"\bNT\d{5}\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CompiledIcIdentityRegex();

    [GeneratedRegex(
        @"\bnew\s+TrustedProfileBundleCatalogSource\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CatalogSourceConstructionRegex();

    [GeneratedRegex(
        @"(?m)^\s*internal\s+(?:(?:sealed|abstract|partial)\s+)*(?:class|record)\s+\w*(?:ProfileDefinition|CompositionDefinition)\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ProfileDefinitionDeclarationRegex();

    [GeneratedRegex(
        @"(?m)^\s*(?:internal|private)\s+static\s+ByteRange\s+RequireRange\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ProfileRangeValidatorDeclarationRegex();
}
