namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Serializes real ready-probe processes that inherit process-wide test environment.</summary>
[CollectionDefinition(nameof(ReadyProbeProcessSerialGroup), DisableParallelization = true)]
public sealed class ReadyProbeProcessSerialGroup;
