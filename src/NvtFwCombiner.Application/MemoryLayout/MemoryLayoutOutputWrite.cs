// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>One content step's declared write at a proven output placement, never an observed byte change.</summary>
public sealed record MemoryLayoutOutputWrite
{
    internal MemoryLayoutOutputWrite(string operationId, FirmwareAddressedRange outputRange,
        string? sourceSlotId, ByteRange? sourceRange, string? bankId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if ((sourceSlotId is null) != (sourceRange is null) ||
            (sourceRange is { } source && source.Length != outputRange.Range.Length))
        {
            throw new ArgumentException("An input source requires a slot and a matching byte slice.", nameof(sourceRange));
        }
        OperationId = operationId;
        OutputRange = outputRange;
        SourceSlotId = sourceSlotId;
        SourceRange = sourceRange;
        BankId = bankId;
    }

    /// <summary>Exact content operation identity, joined to <see cref="MemoryLayoutSnapshot.Steps"/>.</summary>
    public string OperationId { get; }
    /// <summary>Half-open final placement in the snapshot's output address space.</summary>
    public FirmwareAddressedRange OutputRange { get; }
    /// <summary>Bound authoring input slot for an input copy, including an input-cloned work buffer; otherwise null.</summary>
    public string? SourceSlotId { get; }
    /// <summary>Corresponding input-file slice; null for fills, scalar edits, processors and transport.</summary>
    public ByteRange? SourceRange { get; }
    /// <summary>Containing canonical bank locator's identity, or null outside declared bank locators.</summary>
    public string? BankId { get; }
}
