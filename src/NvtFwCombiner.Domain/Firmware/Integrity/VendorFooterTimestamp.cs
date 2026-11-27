// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Domain.Firmware.Integrity;

/// <summary>
/// Caller-supplied local Gregorian calendar fields, admitted by the footer builder.
/// Year 0000 is representable here although it cannot be represented by DateTime.
/// </summary>
internal readonly record struct VendorFooterTimestamp(int Year, int Month, int Day, int Hour, int Minute, int Second);
