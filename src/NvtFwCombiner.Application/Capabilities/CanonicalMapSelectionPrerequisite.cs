// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Canonical artifact binding required before selecting a fixed-capacity map.</summary>
/// <param name="SlotId">The profile's artifact slot, never an inferred filename.</param>
/// <param name="MapCapacity">The declared output capacity, independent of the prerequisite's length.</param>
public sealed record CanonicalMapSelectionPrerequisite(string SlotId, long MapCapacity);
