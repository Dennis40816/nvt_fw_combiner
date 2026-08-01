using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies one TP slot retains its 256 KiB policy while its immutable plan space extracts the exact referenced source span.</summary>
    [Fact]
    public void BlankOutputLoweringDerivesTpInputSpaceFromReferencedSourceSpan()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTpMaximumInput(SupportedProfileJson(familyHash), new ByteRange(0, 12)),
            FamilyJsonWithRootWriteConstraint("explicit-range")));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        Assert.Equal(12, input.Length);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
        CompiledInputSlotRequirement slot = Assert.Single(composition.V2Details.InputContract.Slots);
        Assert.Equal(CompiledInputArtifactClass.TpFirmware, slot.ArtifactClass);
        _ = Assert.IsType<CompiledTpMaximum256KInputLengthRequirement>(slot.LengthRequirement);
        CompositionOperation operation = Assert.Single(composition.Plan.OrderedOperations);
        Assert.Equal(new ByteRange(0, 12), operation.SourceRange);
        Assert.Equal(new ByteRange(0, 12), operation.TargetRange);
    }

    /// <summary>Verifies TP geometry uses the greatest end-exclusive coordinate across all source views, including gaps.</summary>
    [Fact]
    public void BlankOutputLoweringDerivesTpInputSpaceFromAllResolvedSourceViews()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTpMaximumInput(
                SupportedProfileJson(familyHash),
                new ByteRange(0, 4),
                [new ByteRange(20, 8)]),
            FamilyJsonWithRootWriteConstraint("explicit-range", capacity: 64),
            capacityBytes: 64));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        Assert.Equal(28, input.Length);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
    }

    /// <summary>Verifies the real TP-overlay shape accepts a 192 KiB source inside a 256 KiB output map.</summary>
    [Fact]
    public void BlankOutputLoweringAcceptsTpSourceSpanBelowLargerResolvedMapCapacity()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTpMaximumInput(
                SupportedProfileJson(familyHash),
                new ByteRange(0, 0x30000)),
            FamilyJsonWithRootWriteConstraint("explicit-range", capacity: 0x40000),
            capacityBytes: 0x40000));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        Assert.Equal(0x30000, input.Length);
        Assert.Equal(0x40000, composition.Plan.OutputInitialization.Capacity);
    }

    /// <summary>Verifies the fixed TP boundary is accepted exactly at 256 KiB and rejects one byte beyond it.</summary>
    [Fact]
    public void BlankOutputLoweringBoundsTpInputSourceSpanAt256KiB()
    {
        V2CompositionPlanCompileResult exact = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTpMaximumInput(
                SupportedProfileJson(familyHash),
                new ByteRange(0, 0x40000)),
            FamilyJsonWithRootWriteConstraint("explicit-range", capacity: 0x40000),
            capacityBytes: 0x40000));
        V2CompositionPlanCompileResult overflow = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTpMaximumInput(
                SupportedProfileJson(familyHash),
                new ByteRange(0, 0x40001)),
            FamilyJsonWithRootWriteConstraint("explicit-range", capacity: 0x40001),
            capacityBytes: 0x40001));

        Assert.Equal(0x40000, Assert.Single(Assert.IsType<CompiledComposition>(exact.CompiledComposition).Plan.AddressSpaces,
            space => space.AddressSpaceId == "tp-source").Length);
        Assert.Null(overflow.CompiledComposition);
        Assert.Equal("profile.v2.plan.invalid-input-geometry", Assert.Single(overflow.Issues).Code);
    }

    /// <summary>Verifies an exact TP source can clone a same-capacity engine-owned work buffer without extraction or padding.</summary>
    [Fact]
    public void BlankOutputLoweringClonesWorkBufferFromExactTpInput()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithWorkBuffer(
                ProfileWithExactTpInput(SupportedProfileJson(familyHash), 16),
                cloneSourceSlotId: "tp-input")));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        ImageInitialization scratch = Assert.Single(
            composition.Plan.Initializations,
            static initialization => initialization.TargetSpaceId == "scratch");
        CompiledInputSlotRequirement slot = Assert.Single(composition.V2Details.InputContract.Slots);

        Assert.Equal(16, input.Length);
        Assert.Null(input.InputPaddingByte);
        Assert.Equal(InputOversizePolicy.Reject, input.InputOversizePolicy);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Empty(input.ExpectedInputLengths);
        Assert.Equal(ImageInitializationKind.Reference, scratch.Kind);
        Assert.Equal("tp-source", scratch.ReferenceSpaceId);
        Assert.Equal(16, Assert.IsType<CompiledExactBytesInputLengthRequirement>(slot.LengthRequirement).Bytes);
    }

    /// <summary>Verifies generic exact-byte inputs remain declarative until their artifact class gains a lowering rule.</summary>
    [Fact]
    public void BlankOutputLoweringDefersNonTpExactInput()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithAuxiliaryExactInput(SupportedProfileJson(familyHash))));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies Normal DP extraction lowers into a declared source span with map-capacity expectation and profile warning code.</summary>
    [Fact]
    public void BlankOutputLoweringBindsNormalDpExtractionPolicy()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithNormalDpExtraction(SupportedProfileJson(familyHash))));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        CompiledInputSlotRequirement slot = Assert.Single(composition.V2Details.InputContract.Slots);

        Assert.Equal(16, input.Length);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal([16L], input.ExpectedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
        Assert.Equal("DP_SIZE_WARNING", input.UnexpectedInputLengthIssueCode);
        Assert.Equal(
            "DP_SIZE_WARNING",
            Assert.IsType<CompiledNormalDpExtractWithWarningInputLengthRequirement>(slot.LengthRequirement).IssueCode);
    }

    /// <summary>Verifies a Normal-DP profile can declare known outer containers without changing its source span.</summary>
    [Fact]
    public void BlankOutputLoweringBindsDeclaredNormalDpOuterContainerLengths()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithNormalDpExtraction(
                SupportedProfileJson(familyHash),
                [0x80000, 0x200000])));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        CompiledInputSlotRequirement slot = Assert.Single(composition.V2Details.InputContract.Slots);

        Assert.Equal(16, input.Length);
        Assert.Equal([0x80000L, 0x200000L], input.ExpectedInputLengths);
        Assert.Equal(
            [0x80000L, 0x200000L],
            Assert.IsType<CompiledNormalDpExtractWithWarningInputLengthRequirement>(slot.LengthRequirement)
                .ExpectedInputLengths);
    }

    /// <summary>Verifies the pilot DP_AB, TPA, and TPB facts lower independently through one generic declared-prefix rule.</summary>
    [Theory]
    [InlineData("dp-ab", "dp-firmware", 0x80000)]
    [InlineData("tp-a", "tp-firmware", 0x40000)]
    [InlineData("tp-b", "tp-firmware", 0x40000)]
    public void BlankOutputLoweringBindsPilotDeclaredPrefixAuthority(
        string role,
        string artifactClass,
        int requiredEndExclusive)
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithDeclaredPrefix(
                SupportedProfileJson(familyHash),
                role,
                artifactClass,
                requiredEndExclusive),
            FamilyJsonWithRootWriteConstraint("explicit-range", capacity: requiredEndExclusive),
            capacityBytes: requiredEndExclusive));

        Assert.True(
            result.IsCompiled,
            string.Join(Environment.NewLine, result.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        CompiledInputSlotRequirement slot = Assert.Single(composition.V2Details.InputContract.Slots);
        CompiledDeclaredPrefixWithWarningInputLengthRequirement requirement = Assert.IsType<
            CompiledDeclaredPrefixWithWarningInputLengthRequirement>(slot.LengthRequirement);

        Assert.Equal(role, slot.Role);
        Assert.Equal(requiredEndExclusive, input.Length);
        Assert.Equal(new ByteRange(0, requiredEndExclusive), new ByteRange(0, input.Length));
        Assert.Null(input.InputPaddingByte);
        Assert.Empty(input.AllowedInputLengths);
        Assert.Equal([requiredEndExclusive], input.ExpectedInputLengths);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, input.InputOversizePolicy);
        Assert.Equal("INPUT_OUTER_LENGTH", input.UnexpectedInputLengthIssueCode);
        Assert.Equal(requiredEndExclusive, requirement.RequiredEndExclusive);
        Assert.Equal([requiredEndExclusive], requirement.ExpectedOuterLengths);
        Assert.Equal("INPUT_SHORT", requirement.ShortInputIssueCode);
        Assert.Equal("INPUT_OUTER_LENGTH", requirement.UnexpectedOuterLengthIssueCode);
    }

    /// <summary>Verifies a source view one byte beyond the declared execution prefix fails before artifact construction.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsViewBeyondDeclaredPrefix()
    {
        const int requiredEndExclusive = 0x40000;
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithDeclaredPrefix(
                SupportedProfileJson(familyHash),
                "tp-a",
                "tp-firmware",
                requiredEndExclusive),
            FamilyJsonWithRootWriteConstraint("explicit-range", capacity: requiredEndExclusive + 1),
            capacityBytes: requiredEndExclusive + 1));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.invalid-view", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies a TP input slot without a resolved source view fails before any plan artifact is produced.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsTpInputWithoutResolvedSourceView()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithNoTpSourceView(ProfileWithTpMaximumInput(
                SupportedProfileJson(familyHash),
                new ByteRange(0, 12))),
            FamilyJsonWithRootWriteConstraint("explicit-range")));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.invalid-input-geometry", Assert.Single(result.Issues).Code);
    }

    private static string ProfileWithTpMaximumInput(
        string profileJson,
        ByteRange sourceRange,
        IReadOnlyList<ByteRange>? additionalSourceRanges = null)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["artifactClass"] = "tp-firmware";
        JsonObject acceptance = Assert.IsType<JsonObject>(slot["acceptance"]);
        acceptance["lengthRule"] = new JsonObject
        {
            ["kind"] = "tp-maximum-256k",
            ["maximumBytes"] = 262144,
        };

        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        Assert.IsType<JsonObject>(views[0])["selector"] = new JsonObject
        {
            ["kind"] = "map-region-slice",
            ["regionId"] = "root",
            ["offset"] = sourceRange.Start,
            ["length"] = sourceRange.Length,
        };
        Assert.IsType<JsonObject>(views[1])["selector"] = new JsonObject
        {
            ["kind"] = "space-range",
            ["range"] = new JsonObject { ["start"] = 0, ["length"] = sourceRange.Length },
        };
        int index = 0;
        foreach (ByteRange additionalRange in additionalSourceRanges ?? [])
        {
            views.Add(new JsonObject
            {
                ["viewId"] = $"tp-extra-{index}",
                ["spaceId"] = "tp-source",
                ["selector"] = new JsonObject
                {
                    ["kind"] = "map-region-slice",
                    ["regionId"] = "root",
                    ["offset"] = additionalRange.Start,
                    ["length"] = additionalRange.Length,
                },
            });
            index++;
        }
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["regionAccessRules"])[0])["access"] = "explicit-range";
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithExactTpInput(string profileJson, long bytes)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["artifactClass"] = "tp-firmware";
        Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"] = new JsonObject
        {
            ["kind"] = "exact-bytes",
            ["bytes"] = bytes,
        };
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithAuxiliaryExactInput(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["artifactClass"] = "auxiliary";
        Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"] = new JsonObject
        {
            ["kind"] = "exact-bytes",
            ["bytes"] = 16,
        };
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithNormalDpExtraction(
        string profileJson,
        IReadOnlyList<long>? expectedInputLengths = null)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["artifactClass"] = "dp-firmware";
        Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"] = new JsonObject
        {
            ["kind"] = "normal-dp-extract-with-warning",
            ["issueCode"] = "DP_SIZE_WARNING",
        };
        if (expectedInputLengths is not null)
        {
            JsonObject lengthRule = Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"]);
            lengthRule["expectedInputLengths"] = new JsonArray(
                expectedInputLengths.Select(static value => JsonValue.Create(value)).ToArray());
        }
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithDeclaredPrefix(
        string profileJson,
        string role,
        string artifactClass,
        long requiredEndExclusive)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        profile["schemaVersion"] = "2.10";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["role"] = role;
        slot["artifactClass"] = artifactClass;
        Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"] = new JsonObject
        {
            ["kind"] = "declared-prefix-with-warning",
            ["requiredEndExclusive"] = requiredEndExclusive,
            ["expectedOuterLengths"] = new JsonArray(requiredEndExclusive),
            ["shortInputIssueCode"] = "INPUT_SHORT",
            ["unexpectedOuterLengthIssueCode"] = "INPUT_OUTER_LENGTH",
        };
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["views"])[1])["selector"] = new JsonObject
        {
            ["kind"] = "space-range",
            ["range"] = new JsonObject { ["start"] = 0, ["length"] = requiredEndExclusive },
        };
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["regionAccessRules"])[0])["access"] =
            "explicit-range";
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithNoTpSourceView(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        views.RemoveAt(0);
        JsonObject operation = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["operations"])[0]);
        operation.Clear();
        operation["operationId"] = "fill-output";
        operation["sequence"] = 0;
        operation["overlapPolicy"] = "reject";
        operation["reason"] = "Keep the synthetic output blank.";
        operation["kind"] = "fill-range";
        operation["targetViewId"] = "output-code";
        operation["fillByte"] = 0;
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["regionAccessRules"])[0])["access"] = "explicit-range";
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
