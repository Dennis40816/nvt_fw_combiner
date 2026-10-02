using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Provides shared helpers for trusted profile bundle catalog tests.</summary>
internal static class TrustedProfileBundleCatalogFactoryTestSupport
{
    internal const string FirmwareFamilySchemaId =
        "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";

    internal const string CompositionProfileSchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    internal static TrustedProfileBundleCatalog CreateCatalogFromSources(
        IEnumerable<(TrustedProfileBundleCatalogEntryIdentity Identity, JsonElement Document)> families,
        IEnumerable<(TrustedProfileBundleCatalogEntryIdentity Identity, JsonElement Document)> profiles,
        string bundleContentHash = BundleHash)
    {
        return TrustedProfileBundleCatalogFactory.Create(
            ManifestHash,
            new ProfileBundleIdentity("bundle", "1.0.0", bundleContentHash, "release-binding"),
            families,
            profiles);
    }

    internal static (TrustedProfileBundleCatalogEntryIdentity Identity, JsonElement Document) Family(
        string entryId,
        string contentHash,
        JsonElement document)
    {
        return (
            new TrustedProfileBundleCatalogEntryIdentity(
                entryId,
                $"families/{entryId}.json",
                FirmwareFamilySchemaId,
                contentHash),
            document);
    }

    internal static (TrustedProfileBundleCatalogEntryIdentity Identity, JsonElement Document) Profile(
        string entryId,
        string contentHash,
        JsonElement document)
    {
        return (
            new TrustedProfileBundleCatalogEntryIdentity(
                entryId,
                $"profiles/{entryId}.json",
                CompositionProfileSchemaId,
                contentHash),
            document);
    }

    internal static string Hash(string json)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    internal const string ManifestHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal const string BundleHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    internal static TrustedProfileBundleCatalog CreateCatalog(
        string? familyJson = null,
        string? profileJson = null,
        string bundleContentHash = BundleHash)
    {
        familyJson ??= TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        profileJson ??= TrustedV2BundleTestDocuments.ProfileJson(familyHash);
        return CreateCatalogFromSources(
            [Family("family-entry", familyHash, Parse(familyJson))],
            [Profile("profile-entry", Hash(profileJson), Parse(profileJson))],
            bundleContentHash);
    }

    internal static TrustedCompositionProfileCatalogEntry Select(TrustedProfileBundleCatalog catalog)
    {
        TrustedCompositionProfileCatalogEntry? selection = catalog.SelectProfile(
            "profile",
            "1.0.0",
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return Assert.IsType<TrustedCompositionProfileCatalogEntry>(selection);
    }

    internal static PreparedProfile PrepareAdmitted(
        TrustedProfileBundleCatalog catalog,
        TrustedCompositionProfileCatalogEntry selection,
        FirmwareMapResolutionInputs inputs)
    {
        bool admitted = V2CompositionPreparationService.PreparedCompilation.TryCreate(
            catalog,
            selection,
            inputs,
            out V2CompositionPreparationService.PreparedCompilation? preparation,
            out _,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(
            admitted,
            string.Join(
                Environment.NewLine,
                issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        return new PreparedProfile(
            Assert.IsType<V2CompositionPreparationService.PreparedCompilation>(preparation));
    }

    internal static V2CompositionPlanCompileResult Compile(
        PreparedProfile preparation,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        return V2CompositionPlanCompiler.CompilePrepared(
            preparation.Compilation,
            selectedInputSlotIds);
    }

    internal sealed record PreparedProfile(
        V2CompositionPreparationService.PreparedCompilation Compilation)
    {
        internal IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> CapabilityAdmissions =>
            Compilation.CapabilityAdmissions;
    }

    internal static FirmwareMapResolutionInputs Inputs(
        long capacityBytes = 16,
        string modeId = "standard")
    {
        return new FirmwareMapResolutionInputs(
            "NT00001",
            modeId,
            capacityBytes,
            requestedTopology: null,
            []);
    }

    internal static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    internal static PreparedProfile PrepareSupportedBlankCopy(
        Func<string, string>? profileJsonFactory = null,
        string? familyJson = null,
        long capacityBytes = 16)
    {
        familyJson ??= FamilyJsonWithRootWriteConstraint("whole-region");
        string familyHash = Hash(familyJson);
        string profileJson = (profileJsonFactory ?? (hash => SupportedProfileJson(hash)))(familyHash);
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);
        return PrepareAdmitted(catalog, selection, Inputs(capacityBytes));
    }

    internal static string SupportedProfileJson(string familyHash, string? access = "whole")
    {
        string profile = TrustedV2BundleTestDocuments.ProfileJson(familyHash)
            .Replace("\"stage\": \"known\"", "\"stage\": \"compilable\"", StringComparison.Ordinal)
            .Replace("\"artifactClass\": \"tp-firmware\"", "\"artifactClass\": \"reference-image\"", StringComparison.Ordinal)
            .Replace(
                "\"lengthRule\": { \"kind\": \"tp-maximum-256k\", \"maximumBytes\": 262144 }",
                "\"lengthRule\": { \"kind\": \"exact-resolved-map-capacity\" }",
                StringComparison.Ordinal);
        JsonObject profileNode = Assert.IsType<JsonObject>(JsonNode.Parse(profile));
        JsonArray rules = Assert.IsType<JsonArray>(profileNode["regionAccessRules"]);
        if (access is null)
        {
            rules.Clear();
        }
        else
        {
            Assert.IsType<JsonObject>(rules[0])["access"] = access;
        }

        return profileNode.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    internal static string RuntimeSupportedProfileJson(
        string familyHash,
        string stage = "supported",
        bool tokenizedOutput = false,
        bool allowOutputOverride = false,
        string invalidCharacterPolicy = "reject")
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(SupportedProfileJson(familyHash)));
        JsonObject promotion = Assert.IsType<JsonObject>(profile["promotion"]);
        promotion["stage"] = stage;
        promotion["blockers"] = new JsonArray();
        JsonObject output = Assert.IsType<JsonObject>(profile["output"]);
        output["fileNameTemplate"] = tokenizedOutput ? "{original-name}.bin" : "v2-output.bin";
        output["allowOverride"] = allowOutputOverride;
        output["invalidCharacterPolicy"] = invalidCharacterPolicy;
        output["requiredTokenIds"] = tokenizedOutput
            ? new JsonArray("original-name")
            : [];
        return profile.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    internal static string ProfileRequiringCapability(string profileJson)
    {
        return profileJson.Replace(
            "\"requiredCapabilityIds\": []",
            "\"requiredCapabilityIds\": [\"ab-code\"]",
            StringComparison.Ordinal);
    }

    internal static string FamilyJsonWithRootWriteConstraint(
        string writeConstraint,
        int alignment = 1,
        long capacity = 16)
    {
        JsonObject family = ParseFamily();
        JsonObject root = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["regionSets"])[0])["regions"])[0]);
        root["writeConstraint"] = writeConstraint;
        root["alignment"] = alignment;
        root["range"] = new JsonObject { ["start"] = 0, ["length"] = capacity };
        JsonObject map = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["imageMaps"])[0]);
        JsonObject applicability = Assert.IsType<JsonObject>(map["applicability"]);
        applicability["capacityBytes"] = capacity;
        return family.ToJsonString();
    }

    internal static JsonObject ParseFamily(string? familyJson = null)
    {
        return Assert.IsType<JsonObject>(JsonNode.Parse(familyJson ?? TrustedV2BundleTestDocuments.FamilyJson()));
    }

    internal static JsonObject Capability(
        string capabilityFactId,
        string capabilityId,
        string memberId,
        string mapId,
        string reason)
    {
        return new JsonObject
        {
            ["capabilityFactId"] = capabilityFactId,
            ["capabilityId"] = capabilityId,
            ["memberId"] = memberId,
            ["mapId"] = mapId,
            ["applicability"] = Applicability(),
            ["state"] = "confirmed-present",
            ["reason"] = reason,
            ["evidenceRefs"] = new JsonArray("source-capability-evidence"),
        };
    }

    internal static JsonObject Applicability()
    {
        return new JsonObject
        {
            ["modeIds"] = new JsonArray("standard"),
            ["topologyRequirement"] = new JsonObject { ["kind"] = "none" },
            ["capacityBytes"] = 16,
        };
    }

    internal static string ProfileWithTpMaximumInput(
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

    internal static string ProfileWithInactiveOptionalBranch(
        string profileJson,
        Action<JsonObject>? mutateSlot = null,
        Action<JsonObject>? mutateOperation = null)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        profile["schemaVersion"] = "2.13";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        var slot = new JsonObject
        {
            ["slotId"] = "optional-input",
            ["role"] = "auxiliary",
            ["artifactClass"] = "auxiliary",
            ["required"] = false,
            ["cardinality"] = "zero-or-one",
            ["acceptedExtensions"] = new JsonArray(".bin"),
            ["acceptance"] = new JsonObject
            {
                ["lengthRule"] = new JsonObject { ["kind"] = "source-view-coverage" },
                ["normalization"] = new JsonObject { ["kind"] = "none" },
            },
        };
        Assert.IsType<JsonArray>(profile["inputSlots"]).Add(slot);
        profile["inputSelectionGroups"] = new JsonArray
        {
            new JsonObject
            {
                ["groupId"] = "optional-selection",
                ["memberSlotIds"] = new JsonArray("optional-input"),
                ["minimumSelected"] = 0,
                ["maximumSelected"] = 1,
            },
        };
        Assert.IsType<JsonArray>(profile["spaces"]).Add(new JsonObject
        {
            ["spaceId"] = "optional-source",
            ["kind"] = "input-artifact",
            ["slotId"] = "optional-input",
            ["instancePolicy"] = "singleton",
        });
        Assert.IsType<JsonArray>(profile["views"]).Add(new JsonObject
        {
            ["viewId"] = "optional-view",
            ["spaceId"] = "optional-source",
            ["selector"] = new JsonObject { ["kind"] = "map-region", ["regionId"] = "root" },
        });
        var operation = new JsonObject
        {
            ["operationId"] = "copy-optional",
            ["sequence"] = 1,
            ["overlapPolicy"] = "reject",
            ["reason"] = "Copy the selected optional source view.",
            ["kind"] = "copy-range",
            ["sourceViewId"] = "optional-view",
            ["targetViewId"] = "output-code",
        };
        Assert.IsType<JsonArray>(profile["operations"]).Add(operation);
        mutateSlot?.Invoke(slot);
        mutateOperation?.Invoke(operation);
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    internal static string ProfileWithDeclaredPrefix(
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

    internal static string ProfileWithWorkBuffer(
        string profileJson,
        string? cloneSourceSlotId = null,
        int? fixedCapacityBytes = null)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray spaces = Assert.IsType<JsonArray>(profile["spaces"]);
        spaces.Add(new JsonObject
        {
            ["spaceId"] = "scratch",
            ["kind"] = "work-buffer",
            ["capacity"] = fixedCapacityBytes is null
                ? new JsonObject { ["kind"] = "resolved-map" }
                : new JsonObject { ["kind"] = "fixed", ["bytes"] = fixedCapacityBytes },
            ["initializer"] = cloneSourceSlotId is null
                ? new JsonObject
                {
                    ["kind"] = "blank",
                    ["fillByte"] = 0,
                }
                : new JsonObject
                {
                    ["kind"] = "clone",
                    ["sourceSlotId"] = cloneSourceSlotId,
                },
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    internal static string ProfileWithWorkBufferCopyFlow(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(ProfileWithWorkBuffer(profileJson)));
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        views.Add(new JsonObject
        {
            ["viewId"] = "scratch-code",
            ["spaceId"] = "scratch",
            ["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject { ["start"] = 0, ["length"] = 16 },
            },
        });
        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        JsonObject first = Assert.IsType<JsonObject>(operations[0]);
        first["operationId"] = "copy-to-scratch";
        first["targetViewId"] = "scratch-code";
        operations.Add(new JsonObject
        {
            ["operationId"] = "copy-to-output",
            ["sequence"] = 1,
            ["overlapPolicy"] = "reject",
            ["reason"] = "Copy the engine-owned scratch buffer into the physical output view.",
            ["kind"] = "copy-range",
            ["sourceViewId"] = "scratch-code",
            ["targetViewId"] = "output-code",
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    internal static string ProfileWithTemplateRangeCopyFlow(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        profile["schemaVersion"] = "2.14";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        JsonObject input = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        input["artifactClass"] = "tp-firmware";
        Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(input["acceptance"])["lengthRule"])
            ["kind"] = "source-view-coverage";
        JsonArray requiredRegions = Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(profile["mapBinding"])["requiredRegionIds"]);
        requiredRegions.Clear();
        requiredRegions.Add("a-code");
        requiredRegions.Add("b-code");

        Assert.IsType<JsonArray>(profile["spaces"]).Add(new JsonObject
        {
            ["spaceId"] = "scratch",
            ["kind"] = "work-buffer",
            ["capacity"] = new JsonObject { ["kind"] = "fixed", ["bytes"] = 16 },
            ["initializer"] = new JsonObject { ["kind"] = "clone", ["sourceSlotId"] = "tp-input" },
        });

        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        views.Clear();
        views.Add(TemplateRangeView("a-source", "tp-source", "a-bank"));
        views.Add(MapRegionView("a-output", "output", "a-code"));
        views.Add(TemplateRangeView("b-source", "scratch", "b-bank"));
        views.Add(MapRegionView("b-output", "output", "b-code"));

        JsonArray accessRules = Assert.IsType<JsonArray>(profile["regionAccessRules"]);
        accessRules.Clear();
        accessRules.Add(WholeRegionAccess("a-code"));
        accessRules.Add(WholeRegionAccess("b-code"));

        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        operations.Clear();
        operations.Add(Copy("copy-a", 0, "a-source", "a-output"));
        operations.Add(Copy("copy-b", 1, "b-source", "b-output"));
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        static JsonObject TemplateRangeView(
            string viewId,
            string spaceId,
            string regionInstanceId)
        {
            return new JsonObject
            {
                ["viewId"] = viewId,
                ["spaceId"] = spaceId,
                ["selector"] = new JsonObject
                {
                    ["kind"] = "region-template-range",
                    ["regionInstanceId"] = regionInstanceId,
                    ["templateRegionId"] = "tp-code",
                },
            };
        }

        static JsonObject MapRegionView(string viewId, string spaceId, string regionId)
        {
            return new JsonObject
            {
                ["viewId"] = viewId,
                ["spaceId"] = spaceId,
                ["selector"] = new JsonObject
                {
                    ["kind"] = "map-region",
                    ["regionId"] = regionId,
                },
            };
        }

        static JsonObject WholeRegionAccess(string regionId)
        {
            return new JsonObject
            {
                ["regionId"] = regionId,
                ["access"] = "whole",
                ["reason"] = "Synthetic template projection is writable only as one complete region.",
            };
        }

        static JsonObject Copy(
            string operationId,
            int sequence,
            string sourceViewId,
            string targetViewId)
        {
            return new JsonObject
            {
                ["operationId"] = operationId,
                ["sequence"] = sequence,
                ["overlapPolicy"] = "reject",
                ["reason"] = "Copy one canonical template-relative source range.",
                ["kind"] = "copy-range",
                ["sourceViewId"] = sourceViewId,
                ["targetViewId"] = targetViewId,
            };
        }
    }

    internal static string FamilyWithTwoBankInstances()
    {
        JsonObject family = ParseFamily(FamilyJsonWithRootWriteConstraint(
            "explicit-range",
            capacity: 32));
        family["schemaVersion"] = "1.2";
        JsonObject regionSet = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["regionSets"])[0]);
        regionSet["regionTemplates"] = new JsonArray
        {
            new JsonObject
            {
                ["templateId"] = "ab-bank",
                ["capacityBytes"] = 16,
                ["regions"] = new JsonArray
                {
                    RelativeRegion("bank", null, 0, 16, "image"),
                    RelativeRegion("dp-before", "bank", 0, 4, "code"),
                    RelativeRegion("tp-code", "bank", 4, 8, "code"),
                    RelativeRegion("dp-after", "bank", 12, 4, "code"),
                },
            },
        };
        regionSet["regionInstances"] = new JsonArray
        {
            Instance("a-bank", 0, "a-bank", "a-code"),
            Instance("b-bank", 16, "b-bank", "b-code"),
        };
        return family.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        static JsonObject RelativeRegion(
            string regionId,
            string? parentRegionId,
            int start,
            int length,
            string kind)
        {
            var region = new JsonObject
            {
                ["regionId"] = regionId,
                ["owner"] = regionId == "tp-code" ? "tp" : "system",
                ["kind"] = kind,
                ["range"] = new JsonObject { ["start"] = start, ["length"] = length },
                ["writeConstraint"] = regionId == "tp-code" ? "whole-region" : "explicit-range",
                ["alignment"] = 1,
            };
            if (parentRegionId is not null)
            {
                region["parentRegionId"] = parentRegionId;
            }

            return region;
        }

        static JsonObject Instance(
            string instanceId,
            int baseOffset,
            string bankRegionId,
            string codeRegionId)
        {
            return new JsonObject
            {
                ["instanceId"] = instanceId,
                ["templateId"] = "ab-bank",
                ["baseOffset"] = baseOffset,
                ["parentRegionId"] = "root",
                ["resolvedRegionIds"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["templateRegionId"] = "bank",
                        ["resolvedRegionId"] = bankRegionId,
                    },
                    new JsonObject
                    {
                        ["templateRegionId"] = "dp-before",
                        ["resolvedRegionId"] = $"{instanceId}-before",
                    },
                    new JsonObject
                    {
                        ["templateRegionId"] = "tp-code",
                        ["resolvedRegionId"] = codeRegionId,
                    },
                    new JsonObject
                    {
                        ["templateRegionId"] = "dp-after",
                        ["resolvedRegionId"] = $"{instanceId}-after",
                    },
                },
            };
        }
    }

    internal const string LogicalTestMemberId = "NT00001";

    internal static V2RuntimeReferenceReplaceCompileRequest RuntimeReferenceReplaceRequest(
        long referenceLength = 16,
        long sourceLength = 4,
        params ExplicitMapping[] mappings)
    {
        return new V2RuntimeReferenceReplaceCompileRequest(
            [
                new V2ExplicitMappingInputBinding("base", "reference", referenceLength),
                new V2ExplicitMappingInputBinding("source-a", "source", sourceLength),
            ],
            mappings.Length == 0
                ? [RuntimeReferenceReplaceMapping("replace-source", 10, new ByteRange(2, 2), new ByteRange(8, 2))]
                : mappings);
    }

    internal static ExplicitMapping RuntimeReferenceReplaceMapping(
        string mappingId,
        int sequence,
        ByteRange sourceRange,
        ByteRange targetRange,
        string sourceBindingId = "source-a",
        ExplicitMappingOperationKind operationKind = ExplicitMappingOperationKind.ReplaceRange,
        OverlapPolicy overlapPolicy = OverlapPolicy.Reject,
        int alignment = 1)
    {
        return new ExplicitMapping(
            mappingId,
            sequence,
            operationKind,
            sourceBindingId,
            sourceRange,
            "output-image",
            targetRange,
            overlapPolicy,
            alignment,
            reason: "Synthetic runtime General Replace mapping");
    }


    internal static void AssertEquivalentRuntimeExecutionSemantics(
        CompiledComposition expected,
        CompiledComposition actual)
    {
        Assert.Equal(expected.Eligibility, actual.Eligibility);
        Assert.Equal(expected.V2Details.CompositionKind, actual.V2Details.CompositionKind);
        Assert.Equal(
            expected.V2Details.IcNumberInputMode,
            actual.V2Details.IcNumberInputMode);
        Assert.Equal(expected.Plan.OutputSpaceId, actual.Plan.OutputSpaceId);
        Assert.Equal(
            expected.Plan.AddressSpaces.OrderBy(static space => space.AddressSpaceId).Select(static space =>
                (space.AddressSpaceId, space.Length, space.Mutability, space.InputPaddingByte,
                    space.InputOversizePolicy, Allowed: string.Join(",", space.AllowedInputLengths),
                    Expected: string.Join(",", space.ExpectedInputLengths), space.UnexpectedInputLengthIssueCode)),
            actual.Plan.AddressSpaces.OrderBy(static space => space.AddressSpaceId).Select(static space =>
                (space.AddressSpaceId, space.Length, space.Mutability, space.InputPaddingByte,
                    space.InputOversizePolicy, Allowed: string.Join(",", space.AllowedInputLengths),
                    Expected: string.Join(",", space.ExpectedInputLengths), space.UnexpectedInputLengthIssueCode)));
        Assert.Equal(
            expected.Plan.Initializations.Select(static initialization =>
                (initialization.Kind, initialization.TargetSpaceId, initialization.Capacity,
                    initialization.FillByte, initialization.ReferenceSpaceId)),
            actual.Plan.Initializations.Select(static initialization =>
                (initialization.Kind, initialization.TargetSpaceId, initialization.Capacity,
                    initialization.FillByte, initialization.ReferenceSpaceId)));
        Assert.Equal(expected.Plan.OrderedOperations.Count, actual.Plan.OrderedOperations.Count);
        for (int index = 0; index < expected.Plan.OrderedOperations.Count; index++)
        {
            AssertEquivalentOperation(
                expected.Plan.OrderedOperations[index],
                actual.Plan.OrderedOperations[index]);
        }

        V2CompiledCompositionDetails expectedDetails = Assert.IsType<V2CompiledCompositionDetails>(expected.V2Details);
        V2CompiledCompositionDetails actualDetails = Assert.IsType<V2CompiledCompositionDetails>(actual.V2Details);
        Assert.Equal(
            expectedDetails.InputContract.Slots.Select(static slot =>
                (slot.SlotId, slot.Role, slot.ArtifactClass, slot.Required, slot.Cardinality,
                    Extensions: string.Join("|", slot.AcceptedExtensions), slot.LengthRequirement, slot.Normalization)),
            actualDetails.InputContract.Slots.Select(static slot =>
                (slot.SlotId, slot.Role, slot.ArtifactClass, slot.Required, slot.Cardinality,
                    Extensions: string.Join("|", slot.AcceptedExtensions), slot.LengthRequirement, slot.Normalization)));
        Assert.Equal(
            expectedDetails.InputContract.SpaceBindings.Select(static binding =>
                (binding.AddressSpaceId, binding.SlotId, binding.InstancePolicy)),
            actualDetails.InputContract.SpaceBindings.Select(static binding =>
                (binding.AddressSpaceId, binding.SlotId, binding.InstancePolicy)));
        Assert.Empty(expectedDetails.InputContract.SelectionGroups);
        Assert.Empty(actualDetails.InputContract.SelectionGroups);
        Assert.Equal(
            expectedDetails.RegionAccessContract.Requirements.Select(static requirement =>
                (requirement.RegionId, requirement.Access, requirement.Reason,
                    Subregions: string.Join("|", requirement.AllowedSubregionIds),
                    Chain: RegionChain(requirement.GoverningRegionChain))),
            actualDetails.RegionAccessContract.Requirements.Select(static requirement =>
                (requirement.RegionId, requirement.Access, requirement.Reason,
                    Subregions: string.Join("|", requirement.AllowedSubregionIds),
                    Chain: RegionChain(requirement.GoverningRegionChain))));
        Assert.Equal(
            expectedDetails.RegionAccessContract.ResolvedViews.Select(static view =>
                (view.ViewId, view.AddressSpaceId, view.Range, Chain: RegionChain(view.GoverningRegionChain))),
            actualDetails.RegionAccessContract.ResolvedViews.Select(static view =>
                (view.ViewId, view.AddressSpaceId, view.Range, Chain: RegionChain(view.GoverningRegionChain))));
        Assert.Empty(expectedDetails.Provenance.ValidationRequirements);
        Assert.Empty(actualDetails.Provenance.ValidationRequirements);
        Assert.Empty(expectedDetails.Provenance.RequiredCapabilities);
        Assert.Empty(actualDetails.Provenance.RequiredCapabilities);

        RuntimeReferenceReplaceV2CompilationContext expectedContext = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            expectedDetails.Provenance.Context);
        RuntimeReferenceReplaceV2CompilationContext actualContext = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            actualDetails.Provenance.Context);
        Assert.Equal(expectedContext.AllowsConditionalProcessor, actualContext.AllowsConditionalProcessor);
        Assert.Equal(expectedContext.ProcessorWriteViewIds, actualContext.ProcessorWriteViewIds);
        Assert.Equal(expectedContext.ResolvedMap.ImageMap.MapId, actualContext.ResolvedMap.ImageMap.MapId);
        Assert.Equal(expectedContext.ResolvedMap.CapacityBytes, actualContext.ResolvedMap.CapacityBytes);
        Assert.Equal(expectedContext.ResolvedMap.TopologySelection, actualContext.ResolvedMap.TopologySelection);
        Assert.Equal("cascade-map", expectedContext.ResolvedMap.ImageMap.MapId);
    }

    internal static void AssertEquivalentOperation(CompositionOperation expected, CompositionOperation actual)
    {
        Assert.Equal(expected.OperationId, actual.OperationId);
        Assert.Equal(expected.Sequence, actual.Sequence);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.SourceSpaceId, actual.SourceSpaceId);
        Assert.Equal(expected.SourceRange, actual.SourceRange);
        Assert.Equal(expected.TargetSpaceId, actual.TargetSpaceId);
        Assert.Equal(expected.TargetRange, actual.TargetRange);
        Assert.Equal(expected.OverlapPolicy, actual.OverlapPolicy);
        Assert.Equal(expected.FillByte, actual.FillByte);
        Assert.Equal(expected.PatchBytes.ToArray(), actual.PatchBytes.ToArray());
        Assert.Equal(expected.Reason, actual.Reason);
        Assert.Equal(expected.Provenance, actual.Provenance);
        Assert.Equal(expected.ScalarTransform is null, actual.ScalarTransform is null);
        if (expected.ScalarTransform is { } expectedScalar && actual.ScalarTransform is { } actualScalar)
        {
            Assert.Equal(expectedScalar.Width, actualScalar.Width);
            Assert.Equal(expectedScalar.ByteOrder, actualScalar.ByteOrder);
            Assert.Equal(expectedScalar.Addend, actualScalar.Addend);
            Assert.Equal(expectedScalar.AddendSource, actualScalar.AddendSource);
            Assert.Equal(expectedScalar.ExpectedBefore, actualScalar.ExpectedBefore);
            Assert.Equal(expectedScalar.OverflowPolicy, actualScalar.OverflowPolicy);
        }

        Assert.Equal(expected.ExternalProcessorInvocation is null, actual.ExternalProcessorInvocation is null);
        if (expected.ExternalProcessorInvocation is not { } expectedProcessor ||
            actual.ExternalProcessorInvocation is not { } actualProcessor)
        {
            return;
        }

        Assert.Equal(expectedProcessor.ProcessorId, actualProcessor.ProcessorId);
        Assert.Equal(expectedProcessor.ToolBindingId, actualProcessor.ToolBindingId);
        Assert.Equal(expectedProcessor.AllowedReadRanges, actualProcessor.AllowedReadRanges);
        Assert.Equal(expectedProcessor.AllowedWriteRanges, actualProcessor.AllowedWriteRanges);
        Assert.Equal(
            expectedProcessor.AllowedWriteRangeSections.Select(static section =>
                (section.SectionId, section.Range, section.SourceRange)),
            actualProcessor.AllowedWriteRangeSections.Select(static section =>
                (section.SectionId, section.Range, section.SourceRange)));
        Assert.Equal(
            expectedProcessor.StagedSourceBindings.Select(static binding =>
                (binding.SourceSpaceId, binding.SourceRange, binding.FirmwareRange)),
            actualProcessor.StagedSourceBindings.Select(static binding =>
                (binding.SourceSpaceId, binding.SourceRange, binding.FirmwareRange)));
        Assert.Equal(
            expectedProcessor.StagedArtifactBindings.Select(static binding =>
                (binding.ArtifactId, binding.SourceSpaceId, binding.SourceRange)),
            actualProcessor.StagedArtifactBindings.Select(static binding =>
                (binding.ArtifactId, binding.SourceSpaceId, binding.SourceRange)));
        Assert.Equal(
            expectedProcessor.OutputAssertions.Select(static assertion =>
                (assertion.Range, Bytes: Convert.ToHexString(assertion.ExpectedBytes.Span))),
            actualProcessor.OutputAssertions.Select(static assertion =>
                (assertion.Range, Bytes: Convert.ToHexString(assertion.ExpectedBytes.Span))));
    }

    internal static string RegionChain(IEnumerable<FirmwareRegion> chain)
    {
        return string.Join("|", chain.Select(static region =>
            $"{region.RegionId}:{region.WriteConstraint}:{region.Alignment}"));
    }
}
