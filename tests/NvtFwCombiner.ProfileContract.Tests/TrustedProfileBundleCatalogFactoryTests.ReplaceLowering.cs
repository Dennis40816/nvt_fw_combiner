using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies V2 DP Replace keeps the reference clone, pads only the approved DP input, and lowers replace semantics through the shared engine.</summary>
    [Fact]
    public void DpReplaceLoweringClonesReferenceAndPadsDeclaredDpInput()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(PrepareSupportedDpReplace());

        Assert.Empty(compilation.Issues);
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(CompositionKind.Replace, composition.CompositionKind);
        Assert.Equal(CompiledIcNumberPolicy.SingleSelector, composition.IcNumberPolicy);
        ImageInitialization initialization = composition.Plan.OutputInitialization;
        Assert.Equal(ImageInitializationKind.Reference, initialization.Kind);
        Assert.Equal("reference-source", initialization.ReferenceSpaceId);
        CompiledInputSlotRequirement referenceSlot = Assert.Single(
            composition.V2Details.InputContract.Slots,
            static slot => slot.SlotId == "reference-input");
        Assert.Equal(CompiledInputArtifactClass.ReferenceImage, referenceSlot.ArtifactClass);
        _ = Assert.IsType<CompiledExactResolvedMapCapacityInputLengthRequirement>(referenceSlot.LengthRequirement);
        _ = Assert.IsType<CompiledNoInputNormalization>(referenceSlot.Normalization);
        AddressSpace reference = Assert.Single(
            composition.Plan.AddressSpaces,
            static space => space.AddressSpaceId == "reference-source");
        AddressSpace dp = Assert.Single(
            composition.Plan.AddressSpaces,
            static space => space.AddressSpaceId == "dp-source");
        Assert.Equal(16, reference.Length);
        Assert.Null(reference.InputPaddingByte);
        Assert.Empty(reference.AllowedInputLengths);
        Assert.Equal(16, dp.Length);
        Assert.Equal((byte)0, dp.InputPaddingByte);
        Assert.Empty(dp.AllowedInputLengths);
        Assert.Equal(InputOversizePolicy.Reject, dp.InputOversizePolicy);
        Assert.Equal(CompositionOperationKind.ReplaceRange, Assert.Single(composition.Plan.OrderedOperations).Kind);

        byte[] referenceBytes = [.. Enumerable.Repeat((byte)0xCC, 16)];
        CompositionExecutionResult success = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["reference-source"] = referenceBytes,
                ["dp-source"] = [0xA0, 0xB0],
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, success.Status);
        Assert.Equal(
            [0xA0, 0xB0, 0x00, 0x00, .. Enumerable.Repeat((byte)0xCC, 12)],
            success.OutputBytes.ToArray());
        CompositionExecutionResult oversized = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["reference-source"] = referenceBytes,
                ["dp-source"] = new byte[17],
            }));
        Assert.Equal(CompositionExecutionStatus.Failed, oversized.Status);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, Assert.Single(oversized.Issues).Code);
    }

    /// <summary>Verifies DP Replace cannot reinterpret a rejected copy as its initial replacement write.</summary>
    [Fact]
    public void DpReplaceLoweringRejectsCopyRangeInsteadOfReplaceRange()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(PrepareSupportedDpReplace(profile =>
        {
            JsonObject operation = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(profile["operations"])));
            operation["kind"] = "copy-range";
        }));

        Assert.Null(compilation.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(compilation.Issues).Code);
    }

    /// <summary>Verifies an auxiliary DP Replace payload cannot write a canonical DP-owned region.</summary>
    [Fact]
    public void DpReplaceLoweringRejectsAuxiliaryPayloadForDpRegion()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(PrepareSupportedDpReplace(profile =>
        {
            JsonObject dpSlot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[1]);
            dpSlot["artifactClass"] = "auxiliary";
            Assert.IsType<JsonObject>(dpSlot["acceptance"])["normalization"] = new JsonObject { ["kind"] = "none" };
        }));

        Assert.Null(compilation.CompiledComposition);
        Assert.Equal("profile.v2.plan.region-access-denied", Assert.Single(compilation.Issues).Code);
    }

    /// <summary>Verifies a declared auxiliary LDC payload can replace only one whole canonical LDC region.</summary>
    [Fact]
    public void DpReplaceLoweringAcceptsAuxiliaryPayloadForDeclaredLdcRegion()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(PrepareSupportedDpReplace(
            profile =>
            {
                JsonArray requiredRegions = Assert.IsType<JsonArray>(
                    Assert.IsType<JsonObject>(profile["mapBinding"])["requiredRegionIds"]);
                requiredRegions.Add("ldc-code");

                Assert.IsType<JsonArray>(profile["inputSlots"]).Add(new JsonObject
                {
                    ["slotId"] = "ldc-input",
                    ["role"] = "ldc",
                    ["artifactClass"] = "auxiliary",
                    ["required"] = true,
                    ["cardinality"] = "exactly-one",
                    ["acceptedExtensions"] = new JsonArray(".bin"),
                    ["acceptance"] = new JsonObject
                    {
                        ["lengthRule"] = new JsonObject { ["kind"] = "exact-resolved-map-capacity" },
                        ["normalization"] = new JsonObject { ["kind"] = "none" },
                    },
                });
                Assert.IsType<JsonArray>(profile["spaces"]).Add(new JsonObject
                {
                    ["spaceId"] = "ldc-source",
                    ["kind"] = "input-artifact",
                    ["slotId"] = "ldc-input",
                    ["instancePolicy"] = "singleton",
                });
                JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
                views.Add(new JsonObject
                {
                    ["viewId"] = "ldc-source-view",
                    ["spaceId"] = "ldc-source",
                    ["selector"] = new JsonObject { ["kind"] = "map-region", ["regionId"] = "ldc-code" },
                });
                views.Add(new JsonObject
                {
                    ["viewId"] = "ldc-output-view",
                    ["spaceId"] = "output",
                    ["selector"] = new JsonObject { ["kind"] = "map-region", ["regionId"] = "ldc-code" },
                });
                Assert.IsType<JsonArray>(profile["regionAccessRules"]).Add(new JsonObject
                {
                    ["regionId"] = "ldc-code",
                    ["access"] = "whole",
                    ["reason"] = "Synthetic profile-declared LDC replacement access.",
                });
                Assert.IsType<JsonArray>(profile["operations"]).Add(new JsonObject
                {
                    ["operationId"] = "replace-ldc",
                    ["sequence"] = 1,
                    ["overlapPolicy"] = "reject",
                    ["reason"] = "Replace the complete declared LDC region.",
                    ["kind"] = "replace-range",
                    ["sourceViewId"] = "ldc-source-view",
                    ["targetViewId"] = "ldc-output-view",
                });
            },
            configureFamily: family =>
            {
                JsonArray regions = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(
                    Assert.IsType<JsonArray>(family["regionSets"])[0])["regions"]);
                regions.Add(new JsonObject
                {
                    ["regionId"] = "dp-code",
                    ["parentRegionId"] = "root",
                    ["owner"] = "dp",
                    ["kind"] = "code",
                    ["range"] = new JsonObject { ["start"] = 0, ["length"] = 4 },
                    ["writeConstraint"] = "whole-region",
                    ["alignment"] = 1,
                });
                regions.Add(new JsonObject
                {
                    ["regionId"] = "ldc-code",
                    ["parentRegionId"] = "root",
                    ["owner"] = "ldc",
                    ["kind"] = "code",
                    ["range"] = new JsonObject { ["start"] = 4, ["length"] = 4 },
                    ["writeConstraint"] = "whole-region",
                    ["alignment"] = 1,
                });
                regions.Add(new JsonObject
                {
                    ["regionId"] = "reserved-tail",
                    ["parentRegionId"] = "root",
                    ["owner"] = "unknown",
                    ["kind"] = "unmapped",
                    ["range"] = new JsonObject { ["start"] = 8, ["length"] = 8 },
                    ["writeConstraint"] = "forbidden",
                    ["alignment"] = 1,
                });
            }));

        Assert.Empty(compilation.Issues);
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(
            ["copy-code", "replace-ldc"],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));

        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["reference-source"] = [.. Enumerable.Repeat((byte)0xCC, 16)],
                ["dp-source"] = [0x10, 0x11, 0x12, 0x13],
                ["ldc-source"] = [0, 0, 0, 0, 0xA4, 0xA5, 0xA6, 0xA7, 0, 0, 0, 0, 0, 0, 0, 0],
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(
            [0x10, 0x11, 0x12, 0x13, 0xA4, 0xA5, 0xA6, 0xA7, .. Enumerable.Repeat((byte)0xCC, 8)],
            execution.OutputBytes.ToArray());
    }

    /// <summary>Verifies no other Replace experience can enter the DP runtime lowering subset.</summary>
    [Fact]
    public void DpReplaceLoweringRejectsNonDpReplaceExperience()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(PrepareSupportedDpReplace(profile =>
        {
            JsonObject dpSlot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[1]);
            Assert.IsType<JsonObject>(dpSlot["acceptance"])["normalization"] = new JsonObject { ["kind"] = "none" };
        }, modeId: "general-replace"));

        Assert.Null(compilation.CompiledComposition);
        Assert.Equal(2, compilation.Issues.Count);
        Assert.All(
            compilation.Issues,
            issue => Assert.Equal("profile.v2.plan.unsupported-declaration", issue.Code));
    }

    /// <summary>Verifies CtrlRAM Replace cannot admit a DP Replace range without its processor contract.</summary>
    [Fact]
    public void CtrlRamReplaceLoweringRejectsDpReplaceRange()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(
            PrepareSupportedDpReplace(profile =>
            {
                JsonObject dpSlot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[1]);
                Assert.IsType<JsonObject>(dpSlot["acceptance"])["normalization"] =
                    new JsonObject { ["kind"] = "none" };
            }, modeId: "ctrlram-replace"));

        Assert.Null(compilation.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(compilation.Issues).Code);
    }

    /// <summary>Verifies DP Replace may restore a fully covered range only from the exact input cloned into output.</summary>
    [Fact]
    public void DpReplaceLoweringAcceptsReferenceRestoreAfterReplaceRange()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(PrepareSupportedDpReplace(profile =>
        {
            JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
            Assert.IsType<JsonObject>(views[0])["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject
                {
                    ["start"] = 0,
                    ["length"] = 4,
                },
            };
            Assert.IsType<JsonArray>(profile["operations"]).Add(new JsonObject
            {
                ["operationId"] = "restore-reference",
                ["sequence"] = 1,
                ["overlapPolicy"] = "replace-existing",
                ["reason"] = "Restore the declared reference range after DP replacement.",
                ["kind"] = "copy-range",
                ["sourceViewId"] = "reference-code",
                ["targetViewId"] = "output-code",
            });
        }));

        Assert.Empty(compilation.Issues);
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(
            [CompositionOperationKind.ReplaceRange, CompositionOperationKind.CopyRange],
            composition.Plan.OrderedOperations.Select(static operation => operation.Kind));

        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["reference-source"] = [.. Enumerable.Repeat((byte)0xCC, 16)],
                ["dp-source"] = [0xA0, 0xB0, 0xC0, 0xD0],
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal([.. Enumerable.Repeat((byte)0xCC, 16)], execution.OutputBytes.ToArray());
    }

    /// <summary>Verifies reference restore cannot relocate bytes from one reference offset into another output offset.</summary>
    [Fact]
    public void DpReplaceLoweringRejectsCrossOffsetReferenceRestore()
    {
        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(PrepareSupportedDpReplace(profile =>
        {
            JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
            Assert.IsType<JsonObject>(views[0])["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject
                {
                    ["start"] = 0,
                    ["length"] = 4,
                },
            };
            Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(views[1])["selector"])["range"] = new JsonObject
            {
                ["start"] = 4,
                ["length"] = 4,
            };
            Assert.IsType<JsonArray>(profile["operations"]).Add(new JsonObject
            {
                ["operationId"] = "restore-reference-at-a-different-offset",
                ["sequence"] = 1,
                ["overlapPolicy"] = "replace-existing",
                ["reason"] = "Synthetic cross-offset relocation attempt.",
                ["kind"] = "copy-range",
                ["sourceViewId"] = "reference-code",
                ["targetViewId"] = "output-code",
            });
        }));

        Assert.Null(compilation.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(compilation.Issues).Code);
    }

    private static V2CompositionPreparationResult PrepareSupportedDpReplace(
        Action<JsonObject>? configureProfile = null,
        string modeId = "dp-replace",
        Action<JsonObject>? configureFamily = null)
    {
        JsonObject family = Assert.IsType<JsonObject>(JsonNode.Parse(FamilyJsonWithRootWriteConstraint("explicit-range")));
        JsonObject rootRegion = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(family["regionSets"])[0])["regions"])[0]);
        rootRegion["owner"] = "dp";
        JsonObject applicability = Assert.IsType<JsonObject>(
            Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(family["imageMaps"])))["applicability"]);
        Assert.IsType<JsonArray>(applicability["modeIds"])[0] = modeId;
        configureFamily?.Invoke(family);
        string familyJson = family.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        string familyHash = Hash(familyJson);
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(SupportedProfileJson(familyHash, access: "explicit-range")));
        profile["compositionKind"] = "replace";
        profile["icNumberInputMode"] = "single-selector";
        Assert.IsType<JsonObject>(profile["experience"])["experienceId"] = modeId;

        JsonArray inputSlots = Assert.IsType<JsonArray>(profile["inputSlots"]);
        JsonObject referenceSlot = Assert.IsType<JsonObject>(inputSlots[0]);
        referenceSlot["slotId"] = "reference-input";
        referenceSlot["role"] = "reference";
        inputSlots.Add(new JsonObject
        {
            ["slotId"] = "dp-input",
            ["role"] = "dp",
            ["artifactClass"] = "dp-firmware",
            ["required"] = true,
            ["cardinality"] = "exactly-one",
            ["acceptedExtensions"] = new JsonArray(".bin"),
            ["acceptance"] = new JsonObject
            {
                ["lengthRule"] = new JsonObject { ["kind"] = "exact-resolved-map-capacity" },
                ["normalization"] = new JsonObject
                {
                    ["kind"] = "pad-shorter",
                    ["fillByte"] = 0,
                    ["evidenceRef"] = "synthetic-dp-padding",
                },
            },
        });

        JsonArray spaces = Assert.IsType<JsonArray>(profile["spaces"]);
        Assert.IsType<JsonObject>(spaces[0])["spaceId"] = "reference-source";
        Assert.IsType<JsonObject>(spaces[0])["slotId"] = "reference-input";
        Assert.IsType<JsonObject>(spaces[1])["initializer"] = new JsonObject
        {
            ["kind"] = "clone",
            ["sourceSlotId"] = "reference-input",
        };
        spaces.Add(new JsonObject
        {
            ["spaceId"] = "dp-source",
            ["kind"] = "input-artifact",
            ["slotId"] = "dp-input",
            ["instancePolicy"] = "singleton",
        });

        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        Assert.IsType<JsonObject>(views[0])["viewId"] = "reference-code";
        Assert.IsType<JsonObject>(views[0])["spaceId"] = "reference-source";
        Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(views[1])["selector"])["range"] = new JsonObject
        {
            ["start"] = 0,
            ["length"] = 4,
        };
        views.Add(new JsonObject
        {
            ["viewId"] = "dp-code",
            ["spaceId"] = "dp-source",
            ["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject
                {
                    ["start"] = 0,
                    ["length"] = 4,
                },
            },
        });

        JsonObject operation = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(profile["operations"])));
        operation["kind"] = "replace-range";
        operation["sourceViewId"] = "dp-code";
        configureProfile?.Invoke(profile);
        string profileJson = profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);
        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            selection,
            Inputs(modeId: modeId));
        Assert.Empty(preparation.Issues);
        Assert.Equal(V2CompositionPreparationStatus.Admitted, preparation.Status);
        Assert.True(preparation.IsAdmitted);
        return preparation;
    }
}
