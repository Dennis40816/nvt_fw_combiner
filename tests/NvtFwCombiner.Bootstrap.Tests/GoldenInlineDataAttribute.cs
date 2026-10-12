// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Reflection;
using NvtFwCombiner.TestSupport;
using Xunit.Sdk;
using Xunit.v3;

namespace NvtFwCombiner.Bootstrap.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class GoldenInlineDataAttribute(params object?[] data) : DataAttribute
{
    private readonly Xunit.InlineDataAttribute _data = new(data);

    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker)
    {
        IReadOnlyCollection<ITheoryDataRow> rows = await _data.GetData(testMethod, disposalTracker)
            .ConfigureAwait(false);
        ParameterInfo[] parameters = testMethod.GetParameters();
        return rows.Select(row => Wrap(row, parameters)).ToArray();
    }

    public override bool SupportsDiscoveryEnumeration() => _data.SupportsDiscoveryEnumeration();

    private static TheoryDataRow Wrap(ITheoryDataRow row, ParameterInfo[] parameters)
    {
        object?[] values = row.GetData().ToArray();
        for (int index = 0; index < parameters.Length && index < values.Length; index++)
        {
            if (parameters[index].ParameterType == typeof(GoldenCaseId) && values[index] is string caseId)
            {
                values[index] = new GoldenCaseId(caseId);
            }
        }

        return new TheoryDataRow(values)
        {
            Explicit = row.Explicit,
            Label = row.Label,
            Skip = row.Skip,
            SkipType = row.SkipType,
            SkipUnless = row.SkipUnless,
            SkipWhen = row.SkipWhen,
            TestDisplayName = row.TestDisplayName,
            Timeout = row.Timeout,
            Traits = row.Traits ?? [],
        };
    }
}
