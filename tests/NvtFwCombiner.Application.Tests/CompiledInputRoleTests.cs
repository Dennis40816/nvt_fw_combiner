using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Compiled role vocabulary is projected once by its Application owner.</summary>
public sealed class CompiledInputRoleTests
{
    /// <summary>Every known role reaches authoring clients as its typed identity.</summary>
    [Theory]
    [InlineData("dp-ab", CompiledInputRole.DpAb)]
    [InlineData("reference-base", CompiledInputRole.ReferenceBase)]
    [InlineData("tp-a", CompiledInputRole.TpA)]
    [InlineData("tp-b", CompiledInputRole.TpB)]
    public void KnownCompiledRolesHaveTypedAuthoringIdentities(string role, CompiledInputRole expected)
    {
        var input = new CompiledAuthoringInputBinding("slot", "space", role);

        Assert.Equal(expected, CompiledInputArtifactObservationService.GetRole(role));
        Assert.Equal(expected, input.RoleKind);
        Assert.Equal(role, input.Role);
    }

    /// <summary>Unknown ids are neither normalized nor inferred from other identities.</summary>
    [Theory]
    [InlineData("unrecognized")]
    [InlineData("")]
    [InlineData("DP-AB")]
    [InlineData(" dp-ab")]
    [InlineData("dp-input")]
    public void UnknownCompiledRolesRetainTheirRawIdentity(string role)
    {
        var known = new CompiledAuthoringInputBinding("slot", "space", "tp-a");
        CompiledAuthoringInputBinding input = known with { Role = role };

        Assert.Equal(CompiledInputRole.Unknown, CompiledInputArtifactObservationService.GetRole(role));
        Assert.Equal(CompiledInputRole.Unknown, input.RoleKind);
        Assert.Equal(role, input.Role);
        Assert.Equal(CompiledInputRole.TpA, known.RoleKind);
    }

    /// <summary>An absent declaration cannot acquire a known role.</summary>
    [Fact]
    public void AbsentCompiledRoleRemainsUnknown()
    {
        Assert.Equal(CompiledInputRole.Unknown, CompiledInputArtifactObservationService.GetRole(null));
        Assert.Equal(CompiledInputRole.Unknown, new CompiledAuthoringInputBinding("slot", "space").RoleKind);
    }
}
