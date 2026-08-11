using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Tests.Capabilities;

/// <summary>Canonical General execution-publication ownership tests.</summary>
public sealed class AcceptedGeneralExecutionPlanTests
{
    /// <summary>Accepted inline artifacts are immutable snapshots rather than caller-owned arrays.</summary>
    [Fact]
    public void VirtualArtifactsAreDefensivelyCopied()
    {
        var admission = new GeneralAuthoringAdmissionResult(
            new GeneralMappingDraftState([]),
            "parent",
            savedRuleId: null,
            new GeneralResourceLimits(1, 1, 1, 1),
            inputResources: [],
            occupancySegments: [],
            issues: []);
        byte[] supplied = [0xA5, 0x5A];
        var plan = new AcceptedGeneralExecutionPlan(
            admission,
            [new InputArtifactBinding("patch-input", "patch", "virtual:patch")],
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["virtual:patch"] = supplied,
            });

        supplied[0] = 0;

        Assert.Equal([0xA5, 0x5A], plan.VirtualArtifacts["virtual:patch"].ToArray());
    }
}
