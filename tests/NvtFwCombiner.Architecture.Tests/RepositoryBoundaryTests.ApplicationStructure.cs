namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ApplicationBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Domain normalization reuses only its own caller-isolated immutable input backing.</summary>
    [Fact]
    public void CompositionInputNormalizationRetainsTheDomainOwnershipBarrier()
    {
        string input = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionExecutionInput.cs");
        string normalization = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionEngine.Inputs.cs");

        Assert.Contains("_addressSpaceBytes.Add(item.Key, [.. item.Value]);", input, StringComparison.Ordinal);
        Assert.Contains("TryGetImmutableBuffer", input, StringComparison.Ordinal);
        Assert.Contains("buffer = immutableBytes;", normalization, StringComparison.Ordinal);
        Assert.Contains("immutableBytes.CopyTo(buffer, 0);", normalization, StringComparison.Ordinal);
        Assert.DoesNotContain("bytes.ToArray()", normalization, StringComparison.Ordinal);
    }

    /// <summary>Engine-created staging ranges avoid a second copy without weakening public isolation.</summary>
    [Fact]
    public void ExternalProcessorStagingRetainsOnlyEngineOwnedRanges()
    {
        string engine = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionEngine.ExternalProcessors.cs");
        string artifact = ReadText(
            "src/NvtFwCombiner.Domain/Composition/ExternalProcessorStagedArtifact.cs");
        string source = ReadText(
            "src/NvtFwCombiner.Domain/Composition/ExternalProcessorStagedSource.cs");
        string normalizedEngine = engine.ReplaceLineEndings("\n");

        Assert.Contains(
            "byte[] sourceBytes = ReadSlice(sourceBuffer, binding.SourceRange);\n"
                + "            stagedSources.Add(new ExternalProcessorStagedSource("
                + "binding.FirmwareRange, sourceBytes));",
            normalizedEngine,
            StringComparison.Ordinal);
        Assert.Contains(
            "stagedArtifacts.Add(new ExternalProcessorStagedArtifact(\n"
                + "                binding.ArtifactId,\n"
                + "                ReadSlice(sourceBuffer, binding.SourceRange)));",
            normalizedEngine,
            StringComparison.Ordinal);
        Assert.Contains(": this(artifactId, ClonePublicBytes(artifactId, bytes))", artifact, StringComparison.Ordinal);
        Assert.Contains(": this(firmwareRange, ClonePublicBytes(firmwareRange, bytes))", source, StringComparison.Ordinal);
        Assert.Contains(": bytes.ToArray();", artifact, StringComparison.Ordinal);
        Assert.Contains(": bytes.ToArray();", source, StringComparison.Ordinal);
    }

    /// <summary>Verifies typed run progress stays Application-owned and cannot invoke host callbacks inline.</summary>
    [Fact]
    public void CompositionRunProgressStaysApplicationOwnedAndAsynchronous()
    {
        string progress = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunProgress.cs");
        string root = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string externalProcessors = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.ExternalProcessors.cs");
        string domainSources = ReadDomainSources();

        Assert.Contains("public enum CompositionRunPhase", progress, StringComparison.Ordinal);
        Assert.Contains("public sealed class CompositionRunProgressSnapshot", progress, StringComparison.Ordinal);
        Assert.Contains("public string? CommittedOutputId", progress, StringComparison.Ordinal);
        Assert.Contains("public sealed class CompositionRunProgressFeed", progress, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded<CompositionRunProgressSnapshot>", progress, StringComparison.Ordinal);
        Assert.Contains("_feed?.Publish", progress, StringComparison.Ordinal);
        Assert.DoesNotContain("IProgress<", progress, StringComparison.Ordinal);
        Assert.Contains("progress.Complete()", root, StringComparison.Ordinal);
        Assert.Contains(
            "progressPublisher.Report(CompositionRunPhase.PreparingReport, committedOutputId)",
            root,
            StringComparison.Ordinal);
        Assert.Contains(
            "progressPublisher.Report(CompositionRunPhase.RunningExternalProcessor)",
            externalProcessors,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunPhase", domainSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunProgressSnapshot", domainSources, StringComparison.Ordinal);
    }

    /// <summary>Verifies Presentation projects typed lifecycle state instead of inferring firmware progress.</summary>
    [Fact]
    public void CompositionProgressPresentationConsumesOnlyTypedSnapshots()
    {
        string projection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunProgressViewModel.cs");

        Assert.Contains("TryApply(CompositionRunProgressSnapshot snapshot)", projection, StringComparison.Ordinal);
        Assert.Contains("snapshot.ApplicablePhases", projection, StringComparison.Ordinal);
        Assert.Contains("snapshot.CompletedPhases", projection, StringComparison.Ordinal);
        Assert.Contains("snapshot.CommittedOutputId", projection, StringComparison.Ordinal);
        Assert.Contains("CompositionRunDeliveryState.ArtifactCommitted", projection, StringComparison.Ordinal);
        Assert.Contains("IsReducedMotionEnabled", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationId", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("FileName", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("Elapsed", projection, StringComparison.Ordinal);
    }

    /// <summary>Verifies the Application run service root stays split from processor, report, and hash helpers.</summary>
    [Fact]
    public void CompositionRunServiceConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string runResult = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunResult.cs");
        string domainResult = ReadText("src/NvtFwCombiner.Domain/Composition/CompositionExecutionResult.cs");
        string externalProcessors = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.ExternalProcessors.cs");
        string reports = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.Reports.cs");
        string hashing = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.Hashing.cs");
        string inputs = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.Inputs.cs");
        string previewTokens = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.PreviewTokens.cs");
        string outputDifferences = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferences.cs");
        string outputDifferenceBytes = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferenceBytes.cs");
        string outputDifferenceExpectations = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferenceExpectations.cs");

        Assert.Contains("public sealed partial class CompositionRunService", root, StringComparison.Ordinal);
        Assert.Contains("PreviewAsync", root, StringComparison.Ordinal);
        Assert.Contains("BuildAsync", root, StringComparison.Ordinal);
        Assert.Contains("PreviewOrBuildAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunAsync", root, StringComparison.Ordinal);
        Assert.Contains("_outputBytes = [.. outputBytes];", domainResult, StringComparison.Ordinal);
        Assert.Contains("internal CompositionRunResult(", runResult, StringComparison.Ordinal);
        Assert.DoesNotContain("ClonePublicOutputBytes", runResult, StringComparison.Ordinal);
        Assert.Contains("OutputBytes = immutableOutputBytes;", runResult, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputBytes = outputBytes.ToArray();", runResult, StringComparison.Ordinal);
        Assert.DoesNotContain("TransformExternalProcessorAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalProcessorRequest", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionRunReport CreateReport", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string ToSha256Hex", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256", root, StringComparison.Ordinal);
        Assert.Contains("ExecutePlanAsync", externalProcessors, StringComparison.Ordinal);
        Assert.Contains("TransformExternalProcessorAsync", externalProcessors, StringComparison.Ordinal);
        Assert.Contains("ExternalProcessorRequest", externalProcessors, StringComparison.Ordinal);
        Assert.Contains("private static CompositionRunReport CreateReport", reports, StringComparison.Ordinal);
        Assert.Contains("private static MutationRunSummary ToMutationSummary", reports, StringComparison.Ordinal);
        Assert.Contains("private static OperationRunSummary ToOperationSummary", reports, StringComparison.Ordinal);
        Assert.Contains("private static string ToSha256Hex", hashing, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", hashing, StringComparison.Ordinal);
        Assert.Contains("Convert.ToHexStringLower", hashing, StringComparison.Ordinal);
        Assert.DoesNotContain("ToLowerInvariant", hashing, StringComparison.Ordinal);
        Assert.Contains("Dictionary<string, ArtifactReadSnapshot>", inputs, StringComparison.Ordinal);
        Assert.Contains("buffer = [.. snapshot.Bytes];", inputs, StringComparison.Ordinal);
        Assert.Contains("sha256 = snapshot.Sha256;", inputs, StringComparison.Ordinal);
        Assert.Contains("artifactSnapshots.Add(binding.ArtifactId", inputs, StringComparison.Ordinal);
        Assert.Contains("ToSha256Hex(buffer)", inputs, StringComparison.Ordinal);
        Assert.Contains("ToSha256Hex(execution.OutputBytes.Span)", previewTokens, StringComparison.Ordinal);
        Assert.Contains("CreateOutputDifferences", outputDifferences, StringComparison.Ordinal);
        Assert.Contains("CanShareOutputDifferenceSemantic", outputDifferences, StringComparison.Ordinal);
        int sharingPolicyStart = outputDifferences.IndexOf(
            "private static bool CanShareOutputDifferenceSemantic",
            StringComparison.Ordinal);
        int sharingPolicyEnd = outputDifferences.IndexOf(
            "private static IEnumerable<CompositionIssue>",
            sharingPolicyStart,
            StringComparison.Ordinal);
        Assert.True(sharingPolicyStart >= 0 && sharingPolicyEnd > sharingPolicyStart);
        string sharingPolicy = outputDifferences[sharingPolicyStart..sharingPolicyEnd];
        Assert.Contains("OutputDifferenceClassifications.DeclaredReplacement", sharingPolicy, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.PreservedReference", sharingPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputDifferenceClassifications.PostbuildCrcHeader", sharingPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputDifferenceClassifications.Unexpected", sharingPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IEnumerable<OutputDifferenceExpectation> CreateOutputDifferenceExpectations", outputDifferences, StringComparison.Ordinal);
        Assert.DoesNotContain("execution.OutputBytes.ToArray()", reports, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string ToSliceSha256Hex", outputDifferences, StringComparison.Ordinal);
        Assert.Contains("ToSliceSha256Hex", outputDifferenceBytes, StringComparison.Ordinal);
        Assert.Contains("ToSliceHexPreview", outputDifferenceBytes, StringComparison.Ordinal);
        Assert.Contains("Convert.ToHexStringLower", outputDifferenceBytes, StringComparison.Ordinal);
        Assert.DoesNotContain("ToLowerInvariant", outputDifferenceBytes, StringComparison.Ordinal);
        Assert.Contains("CreateOutputDifferenceExpectations", outputDifferenceExpectations, StringComparison.Ordinal);
        Assert.Contains("ClassifyDifferenceSegment", outputDifferenceExpectations, StringComparison.Ordinal);
    }

    /// <summary>Verifies final-output postconditions are compiled policy, not caller-provided run callbacks.</summary>
    [Fact]
    public void FinalOutputPostconditionsStayArtifactBound()
    {
        string root = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string finalOutputValidations = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.FinalOutputValidations.cs");
        string composition = ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        string fingerprint = ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.Fingerprint.cs");
        string validationRequirement = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledValidationRequirement.cs");
        string bootstrapSources = ReadBootstrapSources();

        Assert.Contains(
            "finalOutputValidations = EvaluateFinalOutput(",
            root,
            StringComparison.Ordinal);
        Assert.Contains("boundInputs.InputBytes,", root, StringComparison.Ordinal);
        Assert.Contains("execution.OutputBytes);", root, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidationRequirements =>", composition, StringComparison.Ordinal);
        Assert.Contains(
            "AppendValidationRequirements(builder, provenance.ValidationRequirements)",
            fingerprint,
            StringComparison.Ordinal);
        Assert.Contains("CompiledFirmwareConfigBackupVersionValidation", finalOutputValidations, StringComparison.Ordinal);
        Assert.Contains(
            "CompiledFirmwareConfigBackupPlacementAuthorityValidation",
            finalOutputValidations,
            StringComparison.Ordinal);
        Assert.Contains("FirmwareConfigMetadataReader.TryReadBackup", finalOutputValidations, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Bootstrap", finalOutputValidations, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyProfileValidationRequirements", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("replace.ctrlram", validationRequirement, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOutputValidator", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateCtrlRamFirmwareVersionOutput", bootstrapSources, StringComparison.Ordinal);
    }

    /// <summary>FWConfig authoring names distinguish the original source from the canonical postbuild copy.</summary>
    [Fact]
    public void FirmwareConfigAuthoringNamesSeparateSourceFromCanonicalBackup()
    {
        string metadata = ReadText(
            "src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigMetadataReader.cs");
        string writePlan = ReadText(
            "src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigVersionWritePlan.cs");
        string runtimeEdit = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2RuntimeReferenceReplaceCompileRequest.cs");

        Assert.Contains("long StructureStart", metadata, StringComparison.Ordinal);
        Assert.DoesNotContain("long FirmwareConfigStart", metadata, StringComparison.Ordinal);
        Assert.Contains("SourceStructureStart", writePlan, StringComparison.Ordinal);
        Assert.Contains("CanonicalBackupStructureStart", writePlan, StringComparison.Ordinal);
        Assert.Contains("CreateFromCanonicalBackup", writePlan, StringComparison.Ordinal);
        Assert.Contains("RebaseToSourceStructure", writePlan, StringComparison.Ordinal);
        Assert.DoesNotContain("RebaseToCombinerSource", writePlan, StringComparison.Ordinal);
        Assert.Contains("SourceFirmwareVersionAndBarRange", runtimeEdit, StringComparison.Ordinal);
        Assert.Contains("SourceFirmwareSubVersionRange", runtimeEdit, StringComparison.Ordinal);
    }

    /// <summary>Unexpected Replace differences become run errors before any output commit is attempted.</summary>
    [Fact]
    public void OutputDifferenceVerdictPrecedesTheCommitGate()
    {
        string root = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string reports = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.Reports.cs");
        int differences = root.IndexOf("OutputDifferenceSummary[] outputDifferences", StringComparison.Ordinal);
        int verdict = root.IndexOf("bool outputDifferencesAccepted", StringComparison.Ordinal);
        int runStatus = root.IndexOf("CompositionExecutionStatus runStatus", StringComparison.Ordinal);
        int commit = root.IndexOf(".CommitAsync(outputName.FileName", StringComparison.Ordinal);

        Assert.True(differences >= 0 && differences < verdict && verdict < runStatus && runStatus < commit);
        Assert.Contains("CreateOutputDifferenceIssues(outputDifferences)", root, StringComparison.Ordinal);
        Assert.Contains("outputDifferences.All(static difference => difference.IsAccepted)", root, StringComparison.Ordinal);
        Assert.Contains("finalOutputAccepted && outputDifferencesAccepted", root, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateOutputDifferences(request", reports, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateOutputDifferenceIssues(outputDifferences)", reports, StringComparison.Ordinal);
    }

    /// <summary>Authoring state keeps only the canonical session and mapping transitions.</summary>
    [Fact]
    public void AuthoringConvenienceFacadesStayCollapsed()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "Authoring",
            "MergeAuthoringSessionSet.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "Authoring",
            "ReplaceAuthoringSessionSet.cs")));
        string mergeState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.State.cs");
        string replaceState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.State.cs");
        string rangeCodec = ReadText(
            "src/NvtFwCombiner.Application/Authoring/AuthoringByteRangeCodec.cs");
        string mappingDraft = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralMappingDraftState.cs");
        string mergeDraft = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralMergeDraftState.cs");

        Assert.Equal(3, CountOccurrences(mergeState, "new(ExperienceIds."));
        Assert.Equal(3, CountOccurrences(replaceState, "new(ExperienceIds."));
        Assert.Contains("ExperienceIds.StandardMerge", mergeState, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.AbMerge", mergeState, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.GeneralMerge", mergeState, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.DpReplace", replaceState, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.CtrlRamReplace", replaceState, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.GeneralReplace", replaceState, StringComparison.Ordinal);
        Assert.Equal(0, CountOccurrences(rangeCodec, "GetEndInclusive("));
        Assert.Equal(1, CountOccurrences(mappingDraft, "WithAcceptedFileStamp("));
        Assert.Equal(1, CountOccurrences(mappingDraft, "RebindSelectedFile("));
        Assert.Equal(1, CountOccurrences(mergeDraft, "bool HasSameValue("));
        Assert.Contains(
            "Equals(OutputInitializer, merge.OutputInitializer)",
            mergeDraft,
            StringComparison.Ordinal);
        Assert.Contains(
            "Mappings.HasSameValue(merge.Mappings)",
            mergeDraft,
            StringComparison.Ordinal);
    }
}
