// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Reads the recompute confirmation facts for one IC, IC number token and base firmware image.</summary>
public interface IIntegrityRecomputeFactsQuery
{
    /// <summary>
    /// Returns the facts of the IC's declared postbuild route for an output as long as the base image. The route
    /// passes the same IC number, capacity and write-range checks as General Replace planning. A route that fails
    /// them returns <see cref="IntegrityRecomputeAvailability.Unknown"/> with the planning issue, never a missing rule.
    /// </summary>
    IntegrityRecomputeFacts Resolve(string icId, string icNumberToken, ReadOnlyMemory<byte> baseFirmware);
}
