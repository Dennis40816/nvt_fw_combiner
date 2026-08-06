using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies a synthetic future schema model lowers a declared Combiner stage without C# CRC work.</summary>
    [Fact]
    public void SyntheticArtifactBindingModelLowersLegacyCombinerStage()
    {
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithLegacyCombinerStage(SupportedProfileJson(familyHash))));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        CompositionOperation operation = Assert.Single(composition.Plan.OrderedOperations,
            static candidate => candidate.Kind == CompositionOperationKind.RunExternalProcessor);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(operation.ExternalProcessorInvocation);

        Assert.Equal("synthetic-ab", invocation.ProcessorId);
        Assert.Equal("combiner-1-13", invocation.ToolBindingId);
        Assert.Equal(new ByteRange(0, 16), operation.TargetRange);
        Assert.Equal([new ByteRange(0, 16)], invocation.AllowedReadRanges);
        Assert.Equal([new ByteRange(0, 16)], invocation.AllowedWriteRanges);
        ExternalProcessorStagedArtifactBinding artifact = Assert.Single(invocation.StagedArtifactBindings);
        Assert.Equal("a-bank", artifact.ArtifactId);
        Assert.Equal("scratch", artifact.SourceSpaceId);
        Assert.Equal(new ByteRange(0, 16), artifact.SourceRange);
    }

    /// <summary>Verifies staged-source material remains immutable even while named artifact snapshots may use work buffers.</summary>
    [Fact]
    public void SyntheticArtifactBindingModelRejectsMutableStagedSource()
    {
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithLegacyCombinerStage(SupportedProfileJson(familyHash), mutableStagedSource: true)));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies CRC-worker declarations remain unsupported rather than becoming a C# header CRC path.</summary>
    [Fact]
    public void SyntheticArtifactBindingModelRejectsCrcWorkerExecution()
    {
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithCrcWorkerStage(SupportedProfileJson(familyHash))));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    private static string ProfileWithLegacyCombinerStage(string profileJson, bool mutableStagedSource = false)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(ProfileWithWorkBufferCopyFlow(profileJson)));
        Assert.IsType<JsonArray>(profile["processorStages"]).Add(new JsonObject
        {
            ["processorStageId"] = "legacy-combiner",
            ["kind"] = "legacy-combiner-v1",
            ["toolBindingId"] = "combiner-1-13",
            ["invocationProfileId"] = "synthetic-ab",
            ["targetSpaceId"] = "output",
            ["authority"] = "transform",
            ["purpose"] = "header-and-integrity",
            ["integrityDisposition"] = "recalculate-and-write",
            ["allowedReadViewIds"] = new JsonArray("output-code"),
            ["allowedWriteViewIds"] = new JsonArray("output-code"),
            ["stagedSourceBindings"] = mutableStagedSource
                ? new JsonArray(new JsonObject
                {
                    ["sourceViewId"] = "scratch-code",
                    ["targetViewId"] = "output-code",
                })
                : [],
            ["stagedArtifactBindings"] = new JsonArray(new JsonObject
            {
                ["artifactId"] = "a-bank",
                ["sourceViewId"] = "scratch-code",
            }),
            ["evidenceRef"] = "processor-evidence",
            ["failurePolicy"] = "fail-closed",
        });
        Assert.IsType<JsonArray>(profile["operations"]).Add(new JsonObject
        {
            ["operationId"] = "run-combiner",
            ["sequence"] = 2,
            ["overlapPolicy"] = "replace-existing",
            ["reason"] = "Run the owner-selected Combiner over the staged output image.",
            ["kind"] = "run-processor",
            ["processorStageId"] = "legacy-combiner",
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithCrcWorkerStage(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        Assert.IsType<JsonArray>(profile["processorStages"]).Add(new JsonObject
        {
            ["processorStageId"] = "crc-worker",
            ["kind"] = "crc-worker-v1",
            ["contractVersion"] = "1.0.0",
            ["calculationSetId"] = "synthetic-header-crc",
            ["targetSpaceId"] = "output",
            ["authority"] = "calculate",
            ["purpose"] = "checksum",
            ["integrityDisposition"] = "verify-existing",
            ["allowedReadViewIds"] = new JsonArray("output-code"),
            ["allowedWriteViewIds"] = new JsonArray(),
            ["failurePolicy"] = "fail-closed",
        });
        Assert.IsType<JsonArray>(profile["operations"]).Add(new JsonObject
        {
            ["operationId"] = "run-crc-worker",
            ["sequence"] = 1,
            ["overlapPolicy"] = "reject",
            ["reason"] = "Do not lower header CRC through the C# worker.",
            ["kind"] = "run-processor",
            ["processorStageId"] = "crc-worker",
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
