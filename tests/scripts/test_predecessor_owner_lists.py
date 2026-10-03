"""Owner lists project recorded results, never approve or rerun firmware."""

import copy
import hashlib
import json
from pathlib import Path
import tempfile
from unittest.mock import patch

import pytest

from scripts import predecessor_comparison as execution
from scripts import predecessor_report_reader as reader
from scripts import predecessor_validation as validation


FIXTURES = Path(__file__).parent / "fixtures/predecessor-comparison"


def result(name="rolling-equal"):
    return json.loads((FIXTURES / f"{name}.json").read_bytes())


def seal(report):
    report["deterministicSha256"] = validation.deterministic_report_sha256(report)
    return report


def test_equal_diagnostic_list_keeps_scenario_identity_and_does_not_claim_record():
    report = result()
    text = reader.render_owner_list(report)
    assert "diagnostic rehearsal, not a report of record" in text
    assert report["scenarios"][0]["scenarioId"] in text
    assert "equal（完整輸出相同）" in text
    assert "certification: none; terminal: false" in text
    assert "未重新比較 bytes" in text


@pytest.mark.parametrize("name,word", [
    ("rolling-declared", "已引用宣告 RP-1.2.2-01"),
    ("rolling-undeclared", "未宣告差異"),
])
def test_difference_list_preserves_recorded_disposition_and_exact_observed_bounds(name, word):
    text = reader.render_owner_list(result(name))
    assert word in text
    assert "output-image [0, 1)" in text
    assert "不同 bytes：1；範圍數：1" in text
    assert "ec2b005e0231cae92f436be7599103db0550d17b40d64cd93d91a70c0056b0e2" in text


def test_invalid_cause_is_kept_without_local_paths_or_unknown_payloads():
    report = result()
    row = report["scenarios"][0]
    row.update(outcome="invalid", failureCode="PREDECESSOR_REPORT_INVALID")
    row["firmwarePreview"] = "PRIVATE-BYTE-PREVIEW"
    report["gate"] = {"result": "blocked", "failures": [{
        "code": "PREDECESSOR_REPORT_INVALID", "subject": row["scenarioId"],
        "detail": 'range audit refused "C:\\private folder\\output.bin" and /private/tmp/output.bin',
    }]}
    text = reader.render_owner_list(seal(report))
    assert "invalid（執行或證據無效）" in text
    assert "range audit refused" in text
    assert "PREDECESSOR_REPORT_INVALID" in text
    assert "private" not in text
    assert "PRIVATE-BYTE-PREVIEW" not in text
    assert "[local path omitted]" in text


def test_gaps_are_not_equal_and_keep_version_specific_approval_reason():
    report = result()
    report["coverage"].update(universe=4, debtSetInUniverse=1, acceptedGaps=1, pendingAcceptedGaps=1,
        notCovered=[{"routeId": "route-debt", "reason": "debt-set", "evidenceKind": "contract-only"},
                    {"routeId": "route-accepted", "reason": "accepted-gap", "evidenceKind": "missing"},
                    {"routeId": "route-pending", "reason": "pending-gap", "evidenceKind": "missing"}])
    text = reader.render_owner_list(seal(report))
    assert "無 canonical 認證案例" in text
    assert "待本版 owner 核准" in text
    assert "本版已列為 accepted gap" in text
    for route in ("route-debt", "route-accepted", "route-pending"):
        assert route in text


def test_both_reject_is_an_acceptance_difference_with_each_sides_cause():
    report = result()
    row = report["scenarios"][0]
    row.update(outcome="both-reject", declarationEntryId="RP-1.2.2-02")
    for name in ("baseline", "candidate"):
        row[name].update(status="rejected", output=None, stoppedAt="preview",
                         issues=[{"severity": "error", "code": f"input.{name}.invalid", "source": "report"}])
    text = reader.render_owner_list(seal(report))
    assert "both-reject（兩側均拒絕輸入）" in text
    assert "input.baseline.invalid" in text and "input.candidate.invalid" in text
    assert "preview" in text
    assert "已引用宣告 RP-1.2.2-02" in text
    assert "拒絕輸入沒有可比較的完整輸出" in text


def test_historical_list_keeps_proof_kinds_corrections_and_not_covered_routes():
    text = reader.render_owner_list(result("v0916-consistent"))
    assert "plan/approvedSemanticCorrections" in text
    assert "amendment/approvedSemanticCorrections" in text
    assert "tp-prefix-transitive" in text
    assert "route-unbound-a" in text and "route-unbound-b" in text
    assert "not-covered（未比較）" in text
    assert "無 canonical 認證案例" in text
    assert "canonical-binding-not-applicable-to-v0916" in text


def test_candidate_policy_lists_published_routes_outside_historical_mode_without_renaming():
    report = result("v0916-consistent")
    def route(identifier, publication="candidate"):
        return {"routeId": identifier, "authoring": {"value": "available"},
                "publication": {"value": publication}}
    policy = {"routes": [route("route-exact"), route("route-new"), route("route-hidden", "internal")]}
    text = reader.render_owner_list(report, candidate_policy=policy)
    assert "route-new | — | outside-mode（不在本模式" in text
    assert "route-hidden" not in text
    assert "補充目錄" in text and "不推定 renamed route" in text


def test_precursor_only_and_truncated_ranges_are_visible_without_inventing_full_bounds():
    report = result("rolling-declared")
    row = report["scenarios"][0]
    row["precursorComparison"], row["comparison"] = row["comparison"], None
    row["precursorComparison"].update(rangeCount=33, rangesTruncated=True)
    text = reader.render_owner_list(seal(report))
    assert "precursor output-image [0, 1)" in text
    assert "僅列出結果保留的前段範圍" in text
    assert "完整宣告邊界須讀取" in text


def test_sorting_is_stable_and_markdown_text_cannot_add_rows():
    report = result()
    row = report["scenarios"][0]
    second = copy.deepcopy(row)
    second.update(routeId="route-aaa", scenarioId="NT51950:standard-merge:selector-free:synthetic:aaa")
    report["scenarios"].append(second)
    report["coverage"].update(universe=2, coveredRoutes=2, scenarios=2)
    first = reader.render_owner_list(seal(report))
    report["scenarios"].reverse()
    second_text = reader.render_owner_list(seal(report))
    # The source digest binds original array order; the human tables are sorted.
    assert first.split("## Coverage", 1)[1] == second_text.split("## Coverage", 1)[1]
    assert first.index("route-aaa") < first.index("route-synthetic")
    report["scenarios"][0]["informational"] = [{
        "field": "map-id", "baseline": "old|map\n<script>`", "candidate": "new-map"}]
    text = reader.render_owner_list(seal(report))
    assert "old&#124;map &lt;script&gt;&#96;" in text


def test_formal_projection_never_promotes_a_blocked_result_to_report_of_record():
    report = result("rolling-undeclared")
    report["formal"] = True
    text = reader.render_owner_list(seal(report))
    assert "formal result" in text
    assert "blocked" in text
    assert "此清單不核發 report of record" in text


def test_full_declared_bounds_require_the_exact_result_bound_document():
    report = result("rolling-declared")
    observed = report["scenarios"][0]["comparison"]
    declaration = {"entries": [{"id": "RP-1.2.2-01", "kind": "byte-difference",
        "expected": {name: {"result": "output", "output": report["scenarios"][0][name]["output"],
                            "precursor": None, "stage": None, "issueCodes": []}
                     for name in ("baseline", "candidate")},
        "differences": {"output": {**observed, "ranges": [{"start": 0, "endExclusive": 1},
                                                            {"start": 100, "endExclusive": 101}]},
                        "precursor": None}}]}
    raw = json.dumps(declaration).encode()
    report["declarationSha256"] = hashlib.sha256(raw).hexdigest()
    text = reader.render_owner_list(seal(report), bound_documents={"declaration": raw})
    assert "完整宣告邊界" in text
    assert "output output-image [100, 101)" in text
    assert "expected baseline: output" in text
    with pytest.raises(reader.ReportReaderError, match="binding"):
        reader.render_owner_list(report, bound_documents={"declaration": raw + b" "})


def test_bound_historical_rows_preserve_correction_and_rejection_limits():
    report = result("v0916-consistent")
    plan = {"approvedSemanticCorrections": [{"routeId": "route-plan-correction",
        "differentByteCount": 1, "differentRanges": [{"start": 0, "endExclusive": 1}]}]}
    amendment = {"approvedSemanticCorrections": [{"routeId": "route-amendment-correction",
        "differentByteCount": 1, "differentRanges": [{"start": 0, "endExclusive": 1}]}],
        "baselineNotApplicable": [{"routeId": "route-not-applicable", "binding": {"inputCaseId": "case-synthetic"},
            "scope": "this-canonical-binding-only",
            "expectedBaseline": {"rejectingStage": "preview", "issueCodes": ["map.invalid"],
                                 "precursorOutput": {"size": 160, "sha256": "a" * 64}},
            "expectedCandidate": {"output": {"size": 160, "sha256": "b" * 64}}}]}
    raw_plan, raw_amendment = json.dumps(plan).encode(), json.dumps(amendment).encode()
    for row in report["comparator"]["contracts"]:
        if row["path"] == "docs/contracts/v0916-parity-certification-v1.json":
            row["sha256"] = hashlib.sha256(raw_plan).hexdigest()
    report["amendmentSha256"] = hashlib.sha256(raw_amendment).hexdigest()
    text = reader.render_owner_list(seal(report), bound_documents={"plan": raw_plan, "amendment": raw_amendment})
    assert "baseline rejection: preview" in text
    assert "this-canonical-binding-only" in text
    assert "case-synthetic" in text
    assert "expectedBaseline precursorOutput: size 160" in text


@pytest.mark.parametrize("mutation", ["digest", "mode", "missing-row", "duplicate-row"])
def test_malformed_or_incomplete_results_are_refused(mutation):
    report = result()
    if mutation == "digest":
        report["deterministicSha256"] = "f" * 64
    elif mutation == "mode":
        report["mode"] = "unknown"
        seal(report)
    elif mutation == "missing-row":
        report["scenarios"].clear()
        seal(report)
    else:
        report["scenarios"].append(copy.deepcopy(report["scenarios"][0]))
        seal(report)
    with pytest.raises(reader.ReportReaderError):
        reader.render_owner_list(report)


@pytest.mark.parametrize("name", ["rolling-equal", "v0916-consistent"])
def test_empty_units_refuse_even_with_zero_counts_and_resealed_digest(name):
    report = result(name)
    assert reader.render_owner_list(report)
    if report["mode"] == "rolling":
        report["scenarios"] = []
        report["coverage"].update(universe=0, coveredRoutes=0, scenarios=0)
        report["coverage"]["notCovered"] = []
    else:
        report["routes"] = []
        report["summary"] = validation.v0916_summary([])
    with pytest.raises(reader.ReportReaderError):
        reader.render_owner_list(seal(report))


@pytest.mark.parametrize("shape", ["rolling-invalid", "rolling-failure", "rolling-undeclared",
                                  "historical-inconsistent", "historical-invalid", "historical-failure",
                                  "historical-wrong-invalid", "historical-wrong-inconsistent"])
def test_top_verdict_cannot_contradict_recorded_units_or_failures(shape):
    historical = shape.startswith("historical")
    report = result("v0916-consistent" if historical else "rolling-equal")
    assert reader.render_owner_list(report)
    failure = {"code": "PREDECESSOR_REPORT_INVALID", "subject": "synthetic", "detail": "invalid evidence"}
    if historical:
        if shape.endswith("failure"):
            report["failures"] = [failure]
        elif "wrong" in shape:
            report["result"] = shape.rsplit("-", 1)[1]
            report["failures"] = [failure]
        else:
            report["routes"][0]["result"] = shape.rsplit("-", 1)[1]
            report["routes"][0]["failureCode"] = failure["code"]
            report["summary"] = validation.v0916_summary([row["result"] for row in report["routes"]])
    elif shape.endswith("failure"):
        report["gate"]["failures"] = [failure]
    else:
        report["scenarios"][0]["outcome"] = "invalid" if shape.endswith("invalid") else "different"
        report["scenarios"][0]["failureCode"] = failure["code"] if shape.endswith("invalid") else None
    with pytest.raises(reader.ReportReaderError):
        reader.render_owner_list(seal(report))


@pytest.mark.parametrize("name", ["rolling-undeclared", "v0916-inconsistent"])
def test_blocked_and_inconsistent_recorded_results_remain_displayable(name):
    report = result(name)
    assert reader.render_owner_list(report)


@pytest.fixture
def private_directory():
    with tempfile.TemporaryDirectory() as directory:
        yield Path(directory)


def test_command_reads_result_only_and_refuses_overwrite_or_duplicate_json(private_directory):
    tmp_path = private_directory
    source, output = tmp_path / "result.json", tmp_path / "list.md"
    source.write_text(json.dumps(result()), encoding="utf-8")
    with patch.object(execution, "local_settings_folder", side_effect=AssertionError("no execution")):
        assert execution.main(["owner-list", "--result", str(source), "--output", str(output)]) == 0
    assert "route-synthetic" in output.read_text(encoding="utf-8")
    original = output.read_bytes()
    assert execution.main(["owner-list", "--result", str(source), "--output", str(output)]) == 1
    assert output.read_bytes() == original
    source.write_text('{"mode":"rolling","mode":"rolling"}', encoding="utf-8")
    output.unlink()
    assert execution.main(["owner-list", "--result", str(source), "--output", str(output)]) == 1
    assert not output.exists()
