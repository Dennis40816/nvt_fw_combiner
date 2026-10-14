// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.Json.Nodes;
using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Only the opted-in successor accepts canonical three-byte Common FW slot strings.</summary>
    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("2.0.0", true)]
    [InlineData("255.255.255", true)]
    [InlineData("1.0", false)]
    [InlineData("1.0.0.0", false)]
    [InlineData("01.0.0", false)]
    [InlineData("1.0.-1", false)]
    [InlineData("256.0.0", false)]
    public void CommonFirmwareVersionSuccessorEnforcesTypedSlot(string version, bool valid)
    {
        JsonObject family = LoadFirmwareFamilyWithRelationships();
        family["imageMaps"]![0]!["applicability"]!["commonFirmwareVersionFrom"] = version;
        EvaluationResults result = ParseContractSchema("firmware-family-v1.3-common-fw-version.schema.json")
            .Evaluate(JsonSerializer.SerializeToElement(family));
        Assert.Equal(valid, result.IsValid);
    }

    /// <summary>The field remains optional in the successor and forbidden by the existing schema.</summary>
    [Fact]
    public void ExistingFamilySchemaRejectsCommonFirmwareVersionField()
    {
        JsonObject family = LoadFirmwareFamilyWithRelationships();
        Assert.True(ParseContractSchema("firmware-family-v1.3-common-fw-version.schema.json").Evaluate(JsonSerializer.SerializeToElement(family)).IsValid);
        family["imageMaps"]![0]!["applicability"]!["commonFirmwareVersionFrom"] = "1.0.0";

        Assert.False(ParseContractSchema("firmware-family-v1.2-tp-header-subjects.schema.json").Evaluate(JsonSerializer.SerializeToElement(family)).IsValid);
    }
}
