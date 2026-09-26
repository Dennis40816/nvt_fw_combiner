using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>
/// Runs the predecessor-comparison contracts through the repository's real Draft 2020-12 engine: every
/// schema against the meta-schema, the committed documents against their closed schemas, and one
/// counterexample for each relation the proposed report and declaration schemas must hold.
/// </summary>
public sealed class PredecessorComparisonSchemaContractTests
{
    private const string ReportSchema = "predecessor-comparison-report-v1.schema.json";
    private const string DeclarationSchema = "predecessor-comparison-declaration-v1.schema.json";
    private const string Remove = "<remove>";

    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.Flag,
        RequireFormatValidation = true,
    };

    private static readonly string ShaA = new('a', 64);
    private static readonly string ShaB = new('b', 64);
    private static readonly string ShaC = new('c', 64);
    private static readonly string ShaD = new('d', 64);
    private static readonly string ShaE = new('e', 64);
    private static readonly string ShaF = new('f', 64);
    private static readonly string Sha0 = new('0', 64);
    private static readonly string Sha1 = new('1', 64);
    private static readonly string TagObject = "\"" + new string('7', 40) + "\"";

    private static readonly Dictionary<string, Example> Examples = new(StringComparer.Ordinal)
    {
        ["rolling-formal-clear"] = new(ReportSchema, RollingFormalClear()),
        ["rolling-diagnostic-clear-with-settings"] = new(ReportSchema, RollingDiagnosticClear()),
        ["rolling-blocked"] = new(ReportSchema, RollingBlocked()),
        ["v0916-formal-consistent"] = new(ReportSchema, V0916FormalConsistent()),
        ["v0916-inconsistent"] = new(ReportSchema, V0916Inconsistent()),
        ["declaration-every-kind"] = new(DeclarationSchema, DeclarationEveryKind()),
        ["declaration-empty"] = new(DeclarationSchema, Declaration(string.Empty)),
        ["declaration-precursor-only"] = new(DeclarationSchema, DeclarationPrecursorOnly()),
        ["declaration-output-and-precursor"] = new(DeclarationSchema, DeclarationOutputAndPrecursor()),
        ["declaration-33-output-and-precursor-ranges"] = new(DeclarationSchema, DeclarationManyRanges()),
    };

    private static readonly Dictionary<string, Contradiction> Contradictions = new(StringComparer.Ordinal)
    {
        ["rolling-formal-clear-without-declaration"] = new("rolling-formal-clear", [new("/declarationSha256", "null")]),
        ["rolling-formal-with-settings-present-before"] = new(
            "rolling-formal-clear",
            [new("/environment/perUserSettings/event-buffer-format.v1.json/sha256Before", Quoted(ShaA))]),
        ["rolling-formal-with-settings-present-after"] = new(
            "rolling-formal-clear",
            [new("/environment/perUserSettings/toolchain-runtime.v1.json/sha256After", Quoted(ShaA))]),
        ["rolling-settings-file-unrecorded"] = new(
            "rolling-formal-clear",
            [new("/environment/perUserSettings/toolchain-runtime.v1.json", Remove)]),
        ["rolling-temporary-root-too-long"] = new("rolling-formal-clear", [new("/environment/temporaryRootLength", "65")]),
        ["rolling-clear-with-pending-gap-count"] = new("rolling-formal-clear", [new("/coverage/pendingAcceptedGaps", "1")]),
        ["rolling-clear-with-pending-gap-route"] = new(
            "rolling-formal-clear",
            [new("/coverage/notCovered/0/reason", "\"pending-gap\"")]),
        ["rolling-clear-with-undeclared-coverage-change"] = new(
            "rolling-formal-clear",
            [new("/coverage/changesSinceBaseline/1/declarationEntryId", "null")]),
        ["rolling-clear-with-failure"] = new(
            "rolling-formal-clear",
            [new("/gate/failures", """[{ "code": "PREDECESSOR_UNDECLARED_CHANGE", "subject": "example", "detail": "" }]""")]),
        ["rolling-blocked-without-failure"] = new("rolling-blocked", [new("/gate/failures", "[]")]),
        ["rolling-clear-with-invalid-scenario"] = new(
            "rolling-formal-clear",
            [new("/scenarios/0/outcome", "\"invalid\""), new("/scenarios/0/failureCode", "\"PREDECESSOR_PROCESS_FAILED\"")]),
        ["rolling-clear-with-undeclared-difference"] = new(
            "rolling-formal-clear",
            [new("/scenarios/1/declarationEntryId", "null")]),
        ["rolling-clear-with-declared-equal-outcome"] = new(
            "rolling-formal-clear",
            [new("/scenarios/0/declarationEntryId", "\"RP-1.1.13-09\"")]),
        ["equal-without-candidate-output"] = new("rolling-formal-clear", [new("/scenarios/0/candidate", "null")]),
        ["equal-with-rejected-candidate"] = new("rolling-formal-clear", [new("/scenarios/0/candidate", RejectedSide())]),
        ["equal-with-comparison"] = new("rolling-formal-clear", [new("/scenarios/0/comparison", Comparison())]),
        ["different-without-comparison"] = new("rolling-formal-clear", [new("/scenarios/1/comparison", "null")]),
        ["different-with-empty-ranges"] = new("rolling-formal-clear", [new("/scenarios/1/comparison/ranges", "[]")]),
        ["different-with-truncated-short-range-list"] = new(
            "rolling-formal-clear",
            [new("/scenarios/1/comparison/rangesTruncated", "true")]),
        ["baseline-rejects-without-stage"] = new("rolling-formal-clear", [new("/scenarios/2/baseline/stoppedAt", "null")]),
        ["baseline-rejects-without-issue"] = new("rolling-formal-clear", [new("/scenarios/2/baseline/issues", "[]")]),
        ["rejection-without-error-issue"] = new(
            "rolling-formal-clear",
            [new("/scenarios/2/baseline/issues/0/severity", "\"warning\"")]),
        ["rejection-from-stderr-only"] = new(
            "rolling-formal-clear",
            [new("/scenarios/2/baseline/issues/0/source", "\"stderr\"")]),
        ["rejection-by-external-tool-failure"] = new(
            "rolling-formal-clear",
            [new("/scenarios/2/baseline/issues/0/code", "\"external-tool.process.failed\"")]),
        ["rejection-with-extra-timed-out-process"] = new(
            "rolling-formal-clear",
            [new("/scenarios/2/baseline/processes", "[" + Process("preview", "1", ShaF) + "," + TimedOutProcess() + "]")]),
        ["rejection-with-extra-reportless-process"] = new(
            "rolling-formal-clear",
            [new("/scenarios/2/baseline/processes", "[" + Process("preview", "1", ShaF) + "," + ReportlessProcess() + "]")]),
        ["precursor-comparison-without-precursor-identity"] = new(
            "rolling-formal-clear",
            [new("/scenarios/5/baseline/precursor", "null")]),
        ["rejection-without-report"] = new(
            "rolling-formal-clear",
            [new("/scenarios/2/baseline/processes/0/report", "null")]),
        ["candidate-rejects-with-output"] = new(
            "rolling-formal-clear",
            [new("/scenarios/3/candidate/output", Artifact(ShaA))]),
        ["both-reject-with-output-side"] = new("rolling-formal-clear", [new("/scenarios/4/candidate", OutputSide(ShaA))]),
        ["output-side-with-nonzero-exit"] = new(
            "rolling-formal-clear",
            [new("/scenarios/0/baseline/processes/1/exitCode", "1")]),
        ["output-side-with-process-failure-warning"] = new(
            "rolling-formal-clear",
            [new("/scenarios/0/candidate/issues/0/code", "\"external-tool.process.failed\"")]),
        ["output-side-with-process-failure-info"] = new(
            "rolling-formal-clear",
            [new(
                "/scenarios/0/baseline/issues/0",
                """{ "code": "external-tool.process.failed", "severity": "info", "source": "report" }""")]),
        ["v0916-output-side-with-process-failure-warning"] = new(
            "v0916-formal-consistent",
            [new("/routes/0/candidate/issues/0/code", "\"external-tool.process.failed\"")]),
        ["output-side-with-error-issue"] = new(
            "rolling-formal-clear",
            [new("/scenarios/0/candidate/issues/0/severity", "\"error\"")]),
        ["output-side-with-changed-inputs"] = new(
            "rolling-formal-clear",
            [new("/scenarios/0/baseline/processes/0/inputsUnchanged", "false")]),
        ["timed-out-process-with-exit-code"] = new(
            "rolling-formal-clear",
            [new("/scenarios/0/baseline/processes/0/timedOut", "true")]),
        ["invalid-without-failure-code"] = new("rolling-blocked", [new("/scenarios/0/failureCode", "null")]),
        ["invalid-scenario-declared"] = new("rolling-blocked", [new("/scenarios/0/declarationEntryId", "\"RP-1.1.13-09\"")]),
        ["rolling-baseline-without-tag-object"] = new("rolling-formal-clear", [new("/baseline/executor/tagObject", "null")]),
        ["rolling-report-with-v0916-routes"] = new("rolling-formal-clear", [new("/routes", "[]", AddsMember: true)]),
        ["rolling-report-with-v0916-failure-code"] = new(
            "rolling-blocked",
            [new("/gate/failures/0/code", "\"PREDECESSOR_UNAPPROVED_DIFFERENCE\"")]),
        ["v0916-consistent-with-inconsistent-route"] = new(
            "v0916-formal-consistent",
            [new("/routes/0/result", "\"inconsistent\""), new("/routes/0/failureCode", "\"PREDECESSOR_UNAPPROVED_DIFFERENCE\"")]),
        ["v0916-exact-output-consistent-with-comparison"] = new(
            "v0916-formal-consistent",
            [new("/routes/0/comparison", Comparison())]),
        ["v0916-correction-without-row"] = new("v0916-formal-consistent", [new("/routes/1/dispositionRow", "null")]),
        ["v0916-correction-row-of-other-member"] = new(
            "v0916-formal-consistent",
            [new("/routes/1/dispositionRow/member", "\"baselineNotApplicable\"")]),
        ["v0916-correction-consistent-without-comparison"] = new(
            "v0916-formal-consistent",
            [new("/routes/1/comparison", "null")]),
        ["v0916-transitive-without-proof"] = new("v0916-formal-consistent", [new("/routes/2/transitive", "null")]),
        ["v0916-transitive-consistent-with-failed-check"] = new(
            "v0916-formal-consistent",
            [new("/routes/2/transitive/candidateTpEqualsBaselineFullPrefix", "false")]),
        ["v0916-transitive-with-baseline-run"] = new("v0916-formal-consistent", [new("/routes/2/baseline", OutputSide(ShaA))]),
        ["v0916-not-applicable-with-output-baseline"] = new(
            "v0916-formal-consistent",
            [new("/routes/3/baseline", OutputSide(ShaA))]),
        ["v0916-not-applicable-row-from-plan"] = new(
            "v0916-formal-consistent",
            [new("/routes/3/dispositionRow/source", "\"plan\"")]),
        ["v0916-not-covered-route-with-result"] = new("v0916-formal-consistent", [new("/routes/4/result", "\"consistent\"")]),
        ["v0916-not-covered-route-with-evidence"] = new(
            "v0916-formal-consistent",
            [new("/routes/4/candidate", OutputSide(ShaA))]),
        ["v0916-run-route-reported-not-covered"] = new(
            "v0916-formal-consistent",
            [new("/routes/0/result", "\"not-covered\"")]),
        ["v0916-report-with-declaration"] = new(
            "v0916-formal-consistent",
            [new("/declarationSha256", Quoted(ShaA), AddsMember: true)]),
        ["v0916-result-pass"] = new("v0916-formal-consistent", [new("/result", "\"pass\"")]),
        ["v0916-certification-claimed"] = new("v0916-formal-consistent", [new("/certification", "\"v0916-parity\"")]),
        ["v0916-formal-without-milestone"] = new("v0916-formal-consistent", [new("/milestone", "null")]),
        ["v0916-formal-with-settings-present"] = new(
            "v0916-formal-consistent",
            [new("/environment/perUserSettings/event-buffer-format.v1.json/sha256After", Quoted(ShaA))]),
        ["v0916-inconsistent-without-failure"] = new("v0916-inconsistent", [new("/failures", "[]")]),
        ["v0916-inconsistent-without-inconsistent-route"] = new(
            "v0916-inconsistent",
            [new("/routes/0/result", "\"invalid\"")]),
        ["v0916-inconsistent-route-without-failure-code"] = new(
            "v0916-inconsistent",
            [new("/routes/0/failureCode", "null")]),
        ["v0916-report-with-rolling-failure-code"] = new(
            "v0916-inconsistent",
            [new("/failures/0/code", "\"PREDECESSOR_UNDECLARED_CHANGE\"")]),
        ["byte-difference-without-expected"] = new("declaration-every-kind", [new("/entries/0/expected", "null")]),
        ["byte-difference-without-differences"] = new("declaration-every-kind", [new("/entries/0/differences", "null")]),
        ["byte-difference-with-neither-difference"] = new(
            "declaration-every-kind",
            [new("/entries/0/differences/output", "null")]),
        ["byte-difference-without-attribution"] = new(
            "declaration-every-kind",
            [new("/entries/0/differences/output/attribution", "[]")]),
        ["byte-difference-with-rejected-candidate"] = new(
            "declaration-every-kind",
            [new("/entries/0/expected/candidate", RejectedOutcome())]),
        ["byte-difference-without-scenario"] = new("declaration-every-kind", [new("/entries/0/scenarioIds", "[]")]),
        ["byte-difference-with-route-withdrawal"] = new("declaration-every-kind", [new("/entries/0/routeWithdrawal", "true")]),
        ["supported-attribution-without-evidence"] = new(
            "declaration-every-kind",
            [new("/entries/0/differences/output/attribution/0/evidence", "[]")]),
        ["baseline-rejects-without-stage-declared"] = new(
            "declaration-every-kind",
            [new("/entries/1/expected/baseline/stage", "null")]),
        ["baseline-rejects-without-issue-codes-declared"] = new(
            "declaration-every-kind",
            [new("/entries/1/expected/baseline/issueCodes", "[]")]),
        ["rejected-outcome-with-output"] = new(
            "declaration-every-kind",
            [new("/entries/1/expected/baseline/output", Artifact(ShaA))]),
        ["candidate-rejects-without-disposition"] = new("declaration-every-kind", [new("/entries/2/knownIssue", "null")]),
        ["candidate-rejects-with-both-dispositions"] = new(
            "declaration-every-kind",
            [new("/entries/2/routeWithdrawal", "true")]),
        ["both-reject-without-known-issue"] = new("declaration-every-kind", [new("/entries/3/knownIssue", "null")]),
        ["scenario-retired-without-scenario"] = new("declaration-every-kind", [new("/entries/4/scenarioIds", "[]")]),
        ["scenario-retired-with-expected-outcome"] = new(
            "declaration-every-kind",
            [new("/entries/4/expected", Expected(OutputOutcome(ShaA), OutputOutcome(ShaB)))]),
        ["input-revision-with-differences"] = new(
            "declaration-every-kind",
            [new("/entries/5/differences", Differences(Difference(), "null"))]),
        ["baseline-rejects-with-output-difference"] = new(
            "declaration-every-kind",
            [new("/entries/1/differences", Differences(Difference(), "null"))]),
        ["output-outcome-with-issue-codes"] = new(
            "declaration-every-kind",
            [new("/entries/0/expected/baseline/issueCodes", $"[\"{CompositionIssueCodes.InputAddressSpaceTruncated}\"]")]),
        ["precursor-difference-without-precursor-identity"] = new(
            "declaration-precursor-only",
            [new("/entries/0/expected/candidate/precursor", "null")]),
        ["precursor-difference-with-malformed-hash"] = new(
            "declaration-precursor-only",
            [new("/entries/0/expected/baseline/precursor/sha256", "\"not-a-sha256\"")]),
        ["precursor-difference-without-attribution"] = new(
            "declaration-precursor-only",
            [new("/entries/0/differences/precursor/attribution", "[]")]),
        ["precursor-difference-with-empty-ranges"] = new(
            "declaration-output-and-precursor",
            [new("/entries/0/differences/precursor/ranges", "[]")]),
        ["accepted-gap-with-scenario"] = new(
            "declaration-every-kind",
            [new("/entries/6/scenarioIds", "[\"example-gap\"]")]),
        ["approval-by-other-role"] = new(
            "declaration-every-kind",
            [new("/entries/0/approval/role", "\"release-owner\"")]),
        ["approval-without-board-decision"] = new(
            "declaration-every-kind",
            [new("/entries/0/approval/boardDecision", "\"owner said so\"")]),
    };

    /// <summary>Every predecessor-comparison schema that must satisfy the Draft 2020-12 meta-schema.</summary>
    public static TheoryData<string> Schemas =>
    [
        "predecessor-comparison-v1.schema.json",
        "predecessor-comparison-scenarios-v1.schema.json",
        DeclarationSchema,
        ReportSchema,
    ];

    /// <summary>Committed predecessor-comparison documents and their exact schemas.</summary>
    public static TheoryData<string, string> Instances()
    {
        TheoryData<string, string> data = [];
        data.Add("predecessor-comparison-v1.schema.json", "predecessor-comparison-v1.json");
        data.Add("predecessor-comparison-scenarios-v1.schema.json", "predecessor-comparison-scenarios-v1.json");
        return data;
    }

    /// <summary>Internally consistent reports and declarations of both modes.</summary>
    public static TheoryData<string> ExampleNames()
    {
        TheoryData<string> data = [];
        foreach (string name in Examples.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>Documents that each break exactly one relation of their example.</summary>
    public static TheoryData<string> ContradictionNames()
    {
        TheoryData<string> data = [];
        foreach (string name in Contradictions.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>Rejects a predecessor-comparison schema that is not a valid Draft 2020-12 schema.</summary>
    [Theory]
    [MemberData(nameof(Schemas))]
    public void SchemaSatisfiesDraft202012MetaSchema(string schemaName)
    {
        _ = LoadSchema(schemaName);
    }

    /// <summary>Rejects a committed predecessor-comparison document that drifts from its closed schema.</summary>
    [Theory]
    [MemberData(nameof(Instances))]
    public void RepositoryInstanceSatisfiesItsClosedSchema(string schemaName, string instanceName)
    {
        JsonNode instance = JsonNode.Parse(File.ReadAllText(ContractPath(instanceName)))!;

        Assert.True(IsValid(schemaName, instance), $"{instanceName} must satisfy {schemaName}.");
    }

    /// <summary>Accepts every example whose members agree with each other.</summary>
    [Theory]
    [MemberData(nameof(ExampleNames))]
    public void ConsistentExampleSatisfiesItsSchema(string exampleName)
    {
        Example example = Examples[exampleName];

        Assert.True(IsValid(example.Schema, JsonNode.Parse(example.Json)!), $"{exampleName} must satisfy {example.Schema}.");
    }

    /// <summary>Rejects a document whose members contradict one relation the schema holds.</summary>
    [Theory]
    [MemberData(nameof(ContradictionNames))]
    public void ContradictoryDocumentIsRejected(string contradictionName)
    {
        Contradiction contradiction = Contradictions[contradictionName];
        Example example = Examples[contradiction.Example];
        JsonNode document = JsonNode.Parse(example.Json)!;
        foreach (Edit edit in contradiction.Edits)
        {
            Apply(document, edit);
        }

        Assert.False(IsValid(example.Schema, document), $"{contradictionName} must be rejected by {example.Schema}.");
    }

    private static bool IsValid(string schemaName, JsonNode document)
    {
        using JsonDocument instance = JsonDocument.Parse(document.ToJsonString());
        return LoadSchema(schemaName).Evaluate(instance.RootElement, EvaluationOptions).IsValid;
    }

    private static void Apply(JsonNode document, Edit edit)
    {
        string[] segments = edit.Path.Split('/')[1..];
        JsonNode parent = document;
        foreach (string segment in segments[..^1])
        {
            parent = parent is JsonArray array
                ? array[int.Parse(segment, CultureInfo.InvariantCulture)]!
                : parent.AsObject()[segment]!;
        }

        string member = segments[^1];
        if (parent is JsonArray items)
        {
            int index = int.Parse(member, CultureInfo.InvariantCulture);
            Assert.InRange(index, 0, items.Count - 1);
            items[index] = JsonNode.Parse(edit.Json);
            return;
        }

        JsonObject target = parent.AsObject();
        Assert.True(
            target.ContainsKey(member) != edit.AddsMember,
            $"{edit.Path} must {(edit.AddsMember ? "not " : string.Empty)}exist before the edit.");
        if (edit.Json == Remove)
        {
            _ = target.Remove(member);
            return;
        }

        target[member] = JsonNode.Parse(edit.Json);
    }

    private static JsonSchema LoadSchema(string schemaName)
    {
        string path = ContractPath(schemaName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(
            MetaSchemas.Draft202012.Evaluate(document.RootElement, EvaluationOptions).IsValid,
            $"{schemaName} must satisfy the Draft 2020-12 meta-schema.");
        return JsonSchema.FromText(document.RootElement.GetRawText(), new BuildOptions
        {
            SchemaRegistry = new SchemaRegistry(),
        });
    }

    private static string ContractPath(string fileName)
    {
        return RepositoryPaths.FromRepositoryRoot("docs", "contracts", fileName);
    }

    private static string Quoted(string value)
    {
        return "\"" + value + "\"";
    }

    private static string Artifact(string sha256)
    {
        return $$"""{ "size": 4, "sha256": "{{sha256}}" }""";
    }

    private static string Executor(string tagObject)
    {
        return $$"""
            {
              "authorityTrees": {
                "external-tools": "{{new string('1', 40)}}",
                "profiles": "{{new string('2', 40)}}",
                "src": "{{new string('3', 40)}}",
                "tools/crc-worker": "{{new string('4', 40)}}"
              },
              "cliSha256": "{{ShaA}}",
              "commit": "{{new string('5', 40)}}",
              "lockFileSetSha256": "{{ShaB}}",
              "resolvedSdkVersion": "10.0.303",
              "runtimeClosureSha256": "{{ShaC}}",
              "tagObject": {{tagObject}},
              "tree": "{{new string('6', 40)}}"
            }
            """;
    }

    private static string Comparator()
    {
        return $$"""
            {
              "scriptSha256": "{{ShaA}}",
              "contracts": [{ "path": "docs/contracts/predecessor-comparison-v1.json", "sha256": "{{ShaB}}" }]
            }
            """;
    }

    private static string EnvironmentSection(string eventBufferSettings)
    {
        return $$"""
            {
              "policy": "per-user-settings-absent-before-and-after",
              "temporaryRootLength": 40,
              "perUserSettings": {
                "event-buffer-format.v1.json": { "sha256Before": {{eventBufferSettings}}, "sha256After": {{eventBufferSettings}} },
                "toolchain-runtime.v1.json": { "sha256Before": null, "sha256After": null }
              }
            }
            """;
    }

    private static string Process(string stage, string exitCode, string reportSha256)
    {
        return $$"""
            {
              "stage": "{{stage}}",
              "exitCode": {{exitCode}},
              "timedOut": false,
              "stdoutSha256": "{{ShaD}}",
              "stderrSha256": "{{ShaE}}",
              "inputsUnchanged": true,
              "report": { "size": 128, "sha256": "{{reportSha256}}", "readerVersion": "1x-1", "unknownMembers": ["/Diagnostics"] }
            }
            """;
    }

    private static string TimedOutProcess()
    {
        return $$"""
            {
              "stage": "build",
              "exitCode": null,
              "timedOut": true,
              "stdoutSha256": "{{ShaD}}",
              "stderrSha256": "{{ShaE}}",
              "inputsUnchanged": true,
              "report": null
            }
            """;
    }

    private static string ReportlessProcess()
    {
        return $$"""
            {
              "stage": "build",
              "exitCode": 1,
              "timedOut": false,
              "stdoutSha256": "{{ShaD}}",
              "stderrSha256": "{{ShaE}}",
              "inputsUnchanged": true,
              "report": null
            }
            """;
    }

    private static string OutputSide(string outputSha256, string precursor = "null")
    {
        return $$"""
            {
              "status": "output",
              "stoppedAt": null,
              "issues": [{ "code": "{{CompositionIssueCodes.InputAddressSpaceTruncated}}", "severity": "warning", "source": "report" }],
              "precursor": {{precursor}},
              "output": {{Artifact(outputSha256)}},
              "processes": [{{Process("preview", "0", ShaF)}}, {{Process("build", "0", Sha0)}}]
            }
            """;
    }

    private static string RejectedSide()
    {
        return $$"""
            {
              "status": "rejected",
              "stoppedAt": "preview",
              "issues": [{ "code": "profile.v2.compile.map-selection-invalid", "severity": "error", "source": "report" }],
              "precursor": {{Artifact(ShaB)}},
              "output": null,
              "processes": [{{Process("preview", "1", ShaF)}}]
            }
            """;
    }

    private static string Comparison()
    {
        return $$"""
            {
              "differentByteCount": 1,
              "rangeCount": 1,
              "rangeListSha256": "{{ShaC}}",
              "ranges": [{ "start": 0, "endExclusive": 1 }],
              "rangesTruncated": false
            }
            """;
    }

    private static string Scenario(
        string name,
        string outcome,
        string baseline,
        string candidate,
        string comparison,
        string failureCode,
        string declarationEntryId,
        string precursorComparison = "null")
    {
        return $$"""
            {
              "scenarioId": "example-{{name}}",
              "routeId": "route-example-{{name}}",
              "inputRevision": 1,
              "outcome": "{{outcome}}",
              "baseline": {{baseline}},
              "candidate": {{candidate}},
              "comparison": {{comparison}},
              "precursorComparison": {{precursorComparison}},
              "failureCode": {{failureCode}},
              "declarationEntryId": {{declarationEntryId}},
              "informational": [{ "field": "map-id", "baseline": "example-map", "candidate": "example-map" }]
            }
            """;
    }

    private static string Coverage(string pendingAcceptedGaps, string pendingGapRoutes)
    {
        return $$"""
            {
              "universe": 4,
              "coveredRoutes": 1,
              "scenarios": 8,
              "debtSetInUniverse": 1,
              "acceptedGaps": 1,
              "pendingAcceptedGaps": {{pendingAcceptedGaps}},
              "notCovered": [
                { "routeId": "route-example-owed", "reason": "debt-set", "evidenceKind": "contract-only" },
                { "routeId": "route-example-gap", "reason": "accepted-gap", "evidenceKind": "missing" }{{pendingGapRoutes}}
              ],
              "changesSinceBaseline": [
                { "kind": "scenario-added", "subject": "example-added", "declarationEntryId": null },
                { "kind": "input-revision", "subject": "example-revised", "declarationEntryId": "RP-1.1.13-06" }
              ]
            }
            """;
    }

    private static string RollingReport(
        string formal,
        string declarationSha256,
        string environment,
        string scenarios,
        string coverage,
        string gate)
    {
        return $$"""
            {
              "schemaVersion": "1.0",
              "kind": "predecessor-comparison-report",
              "certification": "none",
              "terminal": false,
              "mode": "rolling",
              "formal": {{formal}},
              "comparator": {{Comparator()}},
              "candidate": { "version": "1.1.13", "executor": {{Executor("null")}} },
              "baseline": { "kind": "previous-release", "tag": "v1.1.12", "executor": {{Executor(TagObject)}} },
              "ledgerSha256": "{{ShaC}}",
              "declarationSha256": {{declarationSha256}},
              "environment": {{environment}},
              "scenarios": [{{scenarios}}],
              "coverage": {{coverage}},
              "gate": {{gate}},
              "deterministicSha256": "{{ShaD}}"
            }
            """;
    }

    private static string RollingFormalClear()
    {
        string scenarios = string.Join(
            ",",
            Scenario("equal", "equal", OutputSide(ShaA), OutputSide(ShaA), "null", "null", "null"),
            Scenario("different", "different", OutputSide(ShaA), OutputSide(ShaB), Comparison(), "null", "\"RP-1.1.13-01\""),
            Scenario("baseline-rejects", "baseline-rejects", RejectedSide(), OutputSide(ShaA), "null", "null", "\"RP-1.1.13-02\""),
            Scenario("candidate-rejects", "candidate-rejects", OutputSide(ShaA), RejectedSide(), "null", "null", "\"RP-1.1.13-03\""),
            Scenario("both-reject", "both-reject", RejectedSide(), RejectedSide(), "null", "null", "\"RP-1.1.13-04\""),
            Scenario(
                "precursor-only",
                "different",
                OutputSide(ShaA, Artifact(ShaB)),
                OutputSide(ShaA, Artifact(ShaC)),
                "null",
                "null",
                "\"RP-1.1.13-09\"",
                Comparison()),
            Scenario(
                "output-and-precursor",
                "different",
                OutputSide(ShaA, Artifact(ShaB)),
                OutputSide(ShaD, Artifact(ShaC)),
                Comparison(),
                "null",
                "\"RP-1.1.13-10\"",
                Comparison()),
            Scenario(
                "many-ranges",
                "different",
                OutputSide(ShaA, Artifact(ShaB)),
                OutputSide(ShaD, Artifact(ShaC)),
                TruncatedComparison(0),
                "null",
                "\"RP-1.1.13-11\"",
                TruncatedComparison(8192)));
        return RollingReport(
            "true",
            Quoted(ShaE),
            EnvironmentSection("null"),
            scenarios,
            Coverage("0", string.Empty),
            """{ "result": "clear", "failures": [] }""");
    }

    private static string RollingDiagnosticClear()
    {
        return RollingReport(
            "false",
            "null",
            EnvironmentSection(Quoted(Sha1)),
            Scenario("equal", "equal", OutputSide(ShaA), OutputSide(ShaA), "null", "null", "null"),
            Coverage("0", string.Empty),
            """{ "result": "clear", "failures": [] }""");
    }

    private static string RollingBlocked()
    {
        string scenarios = string.Join(
            ",",
            Scenario("invalid", "invalid", "null", RejectedSide(), "null", "\"PREDECESSOR_PROCESS_FAILED\"", "null"),
            Scenario("different", "different", OutputSide(ShaA), OutputSide(ShaB), Comparison(), "null", "null"));
        return RollingReport(
            "false",
            "null",
            EnvironmentSection(Quoted(Sha1)),
            scenarios,
            Coverage(
                "1",
                """, { "routeId": "route-example-pending", "reason": "pending-gap", "evidenceKind": "missing" }"""),
            """
            {
              "result": "blocked",
              "failures": [
                { "code": "PREDECESSOR_PROCESS_FAILED", "subject": "example-invalid", "detail": "" },
                { "code": "PREDECESSOR_UNDECLARED_CHANGE", "subject": "example-different", "detail": "" },
                { "code": "PREDECESSOR_COVERAGE_UNDISPOSED", "subject": "route-example-pending", "detail": "" }
              ]
            }
            """);
    }

    private static string Route(
        string name,
        string proofKind,
        string result,
        string dispositionRow,
        string baseline,
        string candidate,
        string comparison,
        string transitive,
        string failureCode)
    {
        return $$"""
            {
              "planRouteId": "route-example-{{name}}",
              "planCapabilityFingerprint": "{{ShaE}}",
              "proofKind": "{{proofKind}}",
              "result": "{{result}}",
              "dispositionRow": {{dispositionRow}},
              "baseline": {{baseline}},
              "candidate": {{candidate}},
              "comparison": {{comparison}},
              "transitive": {{transitive}},
              "failureCode": {{failureCode}},
              "informational": []
            }
            """;
    }

    private static string DispositionRow(string source, string member, string name)
    {
        return $$"""{ "source": "{{source}}", "member": "{{member}}", "routeId": "route-example-{{name}}" }""";
    }

    private static string V0916Report(
        string formal,
        string milestone,
        string routes,
        string summary,
        string result,
        string failures)
    {
        return $$"""
            {
              "schemaVersion": "1.0",
              "kind": "predecessor-comparison-report",
              "certification": "none",
              "terminal": false,
              "mode": "v0916-1x",
              "formal": {{formal}},
              "milestone": {{milestone}},
              "comparator": {{Comparator()}},
              "candidate": { "version": "1.1.13", "executor": {{Executor("null")}} },
              "baseline": { "kind": "v0916", "tag": "v0.9.16", "executor": {{Executor(TagObject)}} },
              "planBinding": {
                "path": "docs/contracts/v0916-parity-certification-v1.json",
                "withoutCandidateAuthorityJcsSha256": "{{ShaA}}",
                "canonicalInputAuthorityJcsSha256": "{{ShaB}}"
              },
              "amendmentSha256": "{{ShaC}}",
              "environment": {{EnvironmentSection("null")}},
              "routes": [{{routes}}],
              "summary": {{summary}},
              "result": "{{result}}",
              "failures": {{failures}},
              "deterministicSha256": "{{ShaD}}"
            }
            """;
    }

    private static string V0916FormalConsistent()
    {
        string routes = string.Join(
            ",",
            Route("exact", "exact-output", "consistent", "null", OutputSide(ShaA), OutputSide(ShaA), "null", "null", "null"),
            Route(
                "correction",
                "exact-output-with-approved-semantic-correction",
                "consistent",
                DispositionRow("amendment", "approvedSemanticCorrections", "correction"),
                OutputSide(ShaA),
                OutputSide(ShaB),
                Comparison(),
                "null",
                "null"),
            Route(
                "transitive",
                "tp-prefix-transitive",
                "consistent",
                "null",
                "null",
                OutputSide(ShaA),
                "null",
                """
                {
                  "fullRouteId": "route-example-exact",
                  "tpLength": 4,
                  "candidateTpEqualsCandidateFullPrefix": true,
                  "candidateTpEqualsBaselineFullPrefix": true,
                  "candidateFullTailImmutable": true
                }
                """,
                "null"),
            Route(
                "not-applicable",
                "canonical-binding-not-applicable-to-v0916",
                "consistent",
                DispositionRow("amendment", "baselineNotApplicable", "not-applicable"),
                RejectedSide(),
                OutputSide(ShaA),
                "null",
                "null",
                "null"),
            Route("owed", "not-covered", "not-covered", "null", "null", "null", "null", "null", "null"));
        return V0916Report(
            "true",
            "\"1.1.13-final-candidate\"",
            routes,
            """{ "consistent": 4, "inconsistent": 0, "invalid": 0, "notCovered": 1 }""",
            "consistent",
            "[]");
    }

    private static string V0916Inconsistent()
    {
        return V0916Report(
            "false",
            "null",
            Route(
                "exact",
                "exact-output",
                "inconsistent",
                "null",
                OutputSide(ShaA),
                OutputSide(ShaB),
                Comparison(),
                "null",
                "\"PREDECESSOR_UNAPPROVED_DIFFERENCE\""),
            """{ "consistent": 0, "inconsistent": 1, "invalid": 0, "notCovered": 0 }""",
            "inconsistent",
            """[{ "code": "PREDECESSOR_UNAPPROVED_DIFFERENCE", "subject": "route-example-exact", "detail": "" }]""");
    }

    private static string OutputOutcome(string sha256, string precursor = "null")
    {
        return $$"""
            { "result": "output", "output": {{Artifact(sha256)}}, "precursor": {{precursor}}, "stage": null, "issueCodes": [] }
            """;
    }

    private static string RejectedOutcome(string precursor = "null")
    {
        return $$"""
            {
              "result": "rejected",
              "output": null,
              "precursor": {{precursor}},
              "stage": "preview",
              "issueCodes": ["profile.v2.compile.map-selection-invalid"]
            }
            """;
    }

    private static string Difference()
    {
        return $$"""
            {
              "differentByteCount": 1,
              "rangeCount": 1,
              "rangeListSha256": "{{ShaC}}",
              "ranges": [{ "start": 0, "endExclusive": 1 }],
              "attribution": [{{Attribution()}}]
            }
            """;
    }

    private static string Differences(string output, string precursor)
    {
        return $$"""{ "output": {{output}}, "precursor": {{precursor}} }""";
    }

    private static string Expected(string baseline, string candidate)
    {
        return $$"""{ "baseline": {{baseline}}, "candidate": {{candidate}} }""";
    }

    private static string Attribution()
    {
        return """
            {
              "start": 0,
              "endExclusive": 1,
              "mechanism": "writes-different-bytes",
              "cause": "example change",
              "causeVerification": "supported-by-cited-evidence",
              "evidence": ["docs/contracts/predecessor-comparison-v1.md"]
            }
            """;
    }

    private static string Entry(
        string number,
        string kind,
        string scenarioIds,
        string expected,
        string differences,
        string knownIssue,
        string routeWithdrawal)
    {
        return $$"""
            {
              "id": "RP-1.1.13-{{number}}",
              "kind": "{{kind}}",
              "scenarioIds": {{scenarioIds}},
              "routeIds": ["route-example-{{number}}"],
              "expected": {{expected}},
              "differences": {{differences}},
              "knownIssue": {{knownIssue}},
              "routeWithdrawal": {{routeWithdrawal}},
              "approval": { "boardDecision": "1.1.13 board decision 1", "role": "firmware-owner", "date": "2026-09-26" }
            }
            """;
    }

    private static string Declaration(string entries)
    {
        return $$"""
            {
              "schemaVersion": "1.0",
              "kind": "predecessor-comparison-declaration",
              "candidateVersion": "1.1.13",
              "baseline": { "tag": "v1.1.12", "tagObject": {{TagObject}} },
              "ledgerSha256": "{{ShaA}}",
              "entries": [{{entries}}]
            }
            """;
    }

    private static string DeclarationEveryKind()
    {
        const string KnownIssue = """{ "bugId": "BUG-20260926-example-known-issue" }""";
        return Declaration(string.Join(
            ",",
            Entry(
                "01",
                "byte-difference",
                "[\"example-different\"]",
                Expected(OutputOutcome(ShaA), OutputOutcome(ShaB)),
                Differences(Difference(), "null"),
                "null",
                "false"),
            Entry(
                "02",
                "baseline-rejects",
                "[\"example-baseline-rejects\"]",
                Expected(RejectedOutcome(), OutputOutcome(ShaA)),
                "null",
                "null",
                "false"),
            Entry(
                "03",
                "candidate-rejects",
                "[\"example-candidate-rejects\"]",
                Expected(OutputOutcome(ShaA), RejectedOutcome()),
                "null",
                KnownIssue,
                "false"),
            Entry(
                "04",
                "both-reject",
                "[\"example-both-reject\"]",
                Expected(RejectedOutcome(), RejectedOutcome()),
                "null",
                KnownIssue,
                "false"),
            Entry("05", "scenario-retired", "[\"example-retired\"]", "null", "null", "null", "false"),
            Entry("06", "input-revision", "[\"example-revised\"]", "null", "null", "null", "false"),
            Entry("07", "accepted-gap", "[]", "null", "null", "null", "false"),
            Entry(
                "08",
                "candidate-rejects",
                "[\"example-withdrawn\"]",
                Expected(OutputOutcome(ShaA), RejectedOutcome()),
                "null",
                "null",
                "true")));
    }

    private static string DeclarationPrecursorOnly()
    {
        return Declaration(Entry(
            "09",
            "byte-difference",
            "[\"example-precursor-only\"]",
            Expected(OutputOutcome(ShaA, Artifact(ShaB)), OutputOutcome(ShaA, Artifact(ShaC))),
            Differences("null", Difference()),
            "null",
            "false"));
    }

    private static string DeclarationOutputAndPrecursor()
    {
        return Declaration(Entry(
            "10",
            "byte-difference",
            "[\"example-output-and-precursor\"]",
            Expected(OutputOutcome(ShaA, Artifact(ShaB)), OutputOutcome(ShaD, Artifact(ShaC))),
            Differences(Difference(), Difference()),
            "null",
            "false"));
    }

    private static string DeclarationManyRanges()
    {
        return Declaration(Entry(
            "11",
            "byte-difference",
            "[\"example-many-ranges\"]",
            Expected(OutputOutcome(ShaA, Artifact(ShaB)), OutputOutcome(ShaD, Artifact(ShaC))),
            Differences(CompleteDifference(0), CompleteDifference(8192)),
            "null",
            "false"));
    }

    private static string RangeAt(int start)
    {
        return "{ \"start\": " + start.ToString(CultureInfo.InvariantCulture)
            + ", \"endExclusive\": " + (start + 2).ToString(CultureInfo.InvariantCulture) + " }";
    }

    private static string AttributionAt(int start)
    {
        return "{ \"start\": " + start.ToString(CultureInfo.InvariantCulture)
            + ", \"endExclusive\": " + (start + 2).ToString(CultureInfo.InvariantCulture)
            + ", \"mechanism\": \"writes-different-bytes\", \"cause\": \"example change\""
            + ", \"causeVerification\": \"not-independently-verified\", \"evidence\": [] }";
    }

    private static string RangeList(int count, int offset, Func<int, string> item)
    {
        return "[" + string.Join(",", Enumerable.Range(0, count).Select(index => item(offset + (index * 4)))) + "]";
    }

    private static string CompleteDifference(int offset)
    {
        return $$"""
            {
              "differentByteCount": 66,
              "rangeCount": 33,
              "rangeListSha256": "{{ShaC}}",
              "ranges": {{RangeList(33, offset, RangeAt)}},
              "attribution": {{RangeList(33, offset, AttributionAt)}}
            }
            """;
    }

    private static string TruncatedComparison(int offset)
    {
        return $$"""
            {
              "differentByteCount": 66,
              "rangeCount": 33,
              "rangeListSha256": "{{ShaC}}",
              "ranges": {{RangeList(32, offset, RangeAt)}},
              "rangesTruncated": true
            }
            """;
    }

    private sealed record Example(string Schema, string Json);

    private sealed record Edit(string Path, string Json, bool AddsMember = false);

    private sealed record Contradiction(string Example, Edit[] Edits);
}
