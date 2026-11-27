// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Retains the legacy exception contract after extracting graph validation.</summary>
public sealed partial class FirmwareImageMapTests
{
    /// <summary>The existing unknown-parent case retains its exception type, parameter and complete message.</summary>
    [Fact]
    public void UnknownParentRetainsTheLegacyExceptionMessage()
    {
        FirmwareRegionSet regionSet = Set("regions",
            [Region("root", 0, 16), Region("child", 0, 4, parentRegionId: "missing")]);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));

        Assert.Equal("regions", exception.ParamName);
        Assert.Equal(new ArgumentException(
            "Firmware region 'child' references unknown parent 'missing'.", "regions").Message, exception.Message);
    }
}
