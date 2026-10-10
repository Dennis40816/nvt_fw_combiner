// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Executes every assigned Standard Merge route for the NT51917 through NT51932 IC group.</summary>
public sealed class CanonicalFormalRouteRuntimeStandardMergeNt51917ToNt51932Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeStandardMergeNt51917ToNt51932Tests));
}

/// <summary>Executes every assigned standard-merge route for NT51950, NT51951.</summary>
public sealed class CanonicalFormalRouteRuntimeStandardMergeNt51950AndNt51951Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeStandardMergeNt51950AndNt51951Tests));
}

/// <summary>Executes every assigned ab-merge route for NT51919, NT51929, NT51932, NT51950, NT51951.</summary>
public sealed class CanonicalFormalRouteRuntimeAbMergeTests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeAbMergeTests));
}

/// <summary>Executes every assigned ctrlram-replace route for NT51917, NT51919.</summary>
public sealed class CanonicalFormalRouteRuntimeCtrlRamReplaceNt51917AndNt51919Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51917AndNt51919Tests));
}

/// <summary>Executes every assigned ctrlram-replace route for NT51923, NT51928.</summary>
public sealed class CanonicalFormalRouteRuntimeCtrlRamReplaceNt51923AndNt51928Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51923AndNt51928Tests));
}

/// <summary>Executes every assigned ctrlram-replace route for NT51926.</summary>
public sealed class CanonicalFormalRouteRuntimeCtrlRamReplaceNt51926Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51926Tests));
}

/// <summary>Executes every assigned ctrlram-replace route for NT51927, NT51929.</summary>
public sealed class CanonicalFormalRouteRuntimeCtrlRamReplaceNt51927AndNt51929Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51927AndNt51929Tests));
}

/// <summary>Executes every assigned ctrlram-replace route for NT51932, NT51950.</summary>
public sealed class CanonicalFormalRouteRuntimeCtrlRamReplaceNt51932AndNt51950Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51932AndNt51950Tests));
}

/// <summary>Executes every assigned ctrlram-replace route for NT51951.</summary>
public sealed class CanonicalFormalRouteRuntimeCtrlRamReplaceNt51951Tests
{
    /// <summary>Preparation, preview, build, and runtime identity remain exact for this shard.</summary>
    [Fact(Timeout = 180_000)]
    public Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync()
        => CanonicalFormalRouteRuntimeExecutor.ExecuteShardAsync(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51951Tests));
}
