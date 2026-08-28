using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Compiled visibility guards for the Application-to-Infrastructure adapter boundary.</summary>
public sealed class InfrastructureAdapterBoundaryTests
{
    /// <summary>Only focused ports are public; semantic implementations remain assembly-owned.</summary>
    [Fact]
    public void FocusedPortsDoNotExposeApplicationImplementations()
    {
        Assert.True(typeof(IStandardMergeCompilationPort).IsPublic);
        Assert.True(typeof(ICompiledInputSlotInspector<>).IsPublic);
        Assert.False(typeof(CanonicalCapabilityCompilerAdapter).IsPublic);
        Assert.False(typeof(StandardMergeAuthoringExperience).IsPublic);
        Assert.False(typeof(AbMergeAuthoringExperience).IsPublic);
        Assert.False(typeof(DpReplaceAuthoringExperience).IsPublic);
        Assert.False(typeof(CtrlRamAuthoringExperience).IsPublic);
    }
}
