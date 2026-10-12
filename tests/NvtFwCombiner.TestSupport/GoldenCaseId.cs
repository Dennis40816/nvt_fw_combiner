// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Xunit.Sdk;

namespace NvtFwCombiner.TestSupport;

/// <summary>Preserves a theory's case identity while showing only its diagnostic label.</summary>
public record struct GoldenCaseId : IXunitSerializable
{
    /// <summary>Wraps an existing case identity without changing it.</summary>
    public GoldenCaseId(string value) => Value = value;

    /// <summary>Gets the original identity for fixture lookup.</summary>
    public string Value { get; private set; }

    /// <summary>Returns the same anonymous label used by golden diagnostics.</summary>
    public override readonly string ToString() => GoldenDiagnostics.Label(Value);

    /// <inheritdoc/>
    public readonly void Serialize(IXunitSerializationInfo info) => info.AddValue(nameof(Value), Value);

    /// <inheritdoc/>
    public void Deserialize(IXunitSerializationInfo info) => Value = info.GetValue<string>(nameof(Value))!;
}
