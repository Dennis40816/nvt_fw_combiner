namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies every production implementation workflow uses the one fail-closed reuse gate.</summary>
    [Fact]
    public void ProductionImplementationUsesOneFailClosedCapabilityReuseGate()
    {
        string workflow = ReadText("docs/governance/development-execution-workflow.md");
        string implement = ReadText(".agents/skills/implement/SKILL.md");
        string architecture = ReadText(".agents/skills/nfc-architecture-change/SKILL.md");
        string polytail = ReadText(".agents/skills/polytail/SKILL.md");

        Assert.Equal(1, CountOccurrences(workflow, "## Capability-reuse gate (fail closed)"));
        Assert.Contains("Production edits must not begin", workflow, StringComparison.Ordinal);
        Assert.Contains("complete production caller path", workflow, StringComparison.Ordinal);
        Assert.Contains("`none-found` plus the exact", workflow, StringComparison.Ordinal);
        Assert.Contains("validator, parser, normalizer, formatter", workflow, StringComparison.Ordinal);
        Assert.Contains("`approved-migration-seam`", workflow, StringComparison.Ordinal);
        Assert.Contains("Unknown, unsearched, or conflicting ownership fails the gate", workflow, StringComparison.Ordinal);
        Assert.Contains("executable deletion milestone", workflow, StringComparison.Ordinal);

        foreach (string routedSkill in new[] { implement, architecture, polytail })
        {
            Assert.Contains(
                "development-execution-workflow.md#capability-reuse-gate-fail-closed",
                routedSkill,
                StringComparison.Ordinal);
        }

        Assert.Contains("before any production edit", implement, StringComparison.Ordinal);
        Assert.Contains("is a P1 finding", polytail, StringComparison.Ordinal);
    }
}
