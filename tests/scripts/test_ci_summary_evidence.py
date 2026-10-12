"""Offline public-artifact privacy and fail-closed summary acceptance."""

from contextlib import redirect_stderr, redirect_stdout
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from unittest.mock import patch

import pytest

from tests.scripts import test_ci_dotnet_retry as retry_fixtures
from tests.scripts import test_verify_orchestration as fixtures

MODULE = fixtures.MODULE
MARKER = "SYNTHETIC_CONFIDENTIAL_SENTINEL_84AC"


@pytest.fixture
def test_area_tmp_path():
    with tempfile.TemporaryDirectory() as temporary:
        yield Path(temporary)


def test_upload_contains_only_public_projection():
    original = fixtures.VerifyOrchestrationTests.write_ci_trx
    discovery_writer = fixtures.VerifyOrchestrationTests.write_vstest_discovery

    def marked_discovery(path, total):
        names = (
            tuple(f"Probe.Tests.Case{i}" for i in range(total))
            if isinstance(total, int)
            else total
        )
        discovery_writer(
            path, tuple(name.split("(", 1)[0] + f"(marker: {MARKER})" for name in names)
        )

    def marked_trx(path, **kwargs):
        kwargs["identities"] = tuple(
            identity[:-1] + f", marker: {MARKER})"
            if "(" in identity
            else identity + f"(marker: {MARKER})"
            for identity in kwargs["identities"]
        )
        original(path, **kwargs)
        tree = MODULE.ET.parse(path)
        for result in tree.findall(".//{*}UnitTestResult"):
            output = MODULE.ET.SubElement(result, "Output")
            MODULE.ET.SubElement(output, "StdOut").text = MARKER
            info = MODULE.ET.SubElement(output, "ErrorInfo")
            MODULE.ET.SubElement(info, "Message").text = MARKER
        tree.write(path, encoding="utf-8", xml_declaration=True)
        with (path.parent / "attempt.log").open("a", encoding="utf-8") as log:
            log.write(MARKER)

    errors = io.StringIO()
    with (
        patch.object(
            fixtures.VerifyOrchestrationTests, "write_ci_trx", staticmethod(marked_trx)
        ),
        patch.object(
            fixtures.VerifyOrchestrationTests,
            "write_vstest_discovery",
            staticmethod(marked_discovery),
        ),
        redirect_stderr(errors),
        retry_fixtures.CiDotnetRetryTests().shard("green") as result,
    ):
        root, _, _, attempts, manifest, error = result
        assert error is None
        assert len(attempts) == 1
        staged = list((root / "upload").rglob("*"))
        for path in staged:
            if path.is_file():
                assert MARKER not in path.read_text(encoding="utf-8")
        assert MARKER not in (root / "job.log").read_text() + errors.getvalue()
        assert not list((root / "upload").rglob("*.trx"))
        expected = {"shards/core/manifest.json", "shards/core/shard-summary.json"}
        for row in manifest["projects"]:
            project = Path(row["relativePath"]).stem
            expected.update(
                {
                    f"shards/core/coverage/{project}/coverage.json",
                    f"shards/core/coverage/{project}/coverage.cobertura.xml",
                }
            )
        assert {
            p.relative_to(root / "upload").as_posix() for p in staged if p.is_file()
        } == expected
        assert set(manifest["files"]) == expected - {"shards/core/manifest.json"}
        for attempt in attempts:
            for name in ("test-results.trx", "discovered-tests.txt", "attempt.log"):
                assert MARKER in (attempt / name).read_text()
        assert set(manifest["projects"][0]) == {
            "relativePath",
            "testAssemblySha256",
            "total",
            "passed",
            "failed",
            "skipped",
            "retry",
            "failedTests",
            "flakyTests",
        }


@pytest.mark.parametrize(
    "extra", [MARKER + ".trx", MARKER + ".log", "Sequence_" + "a" * 32 + ".xml"]
)
@pytest.mark.parametrize("injected_after_copy", [False, True])
def test_publication_clears_stale_files_but_refuses_files_added_during_copy(
    extra, injected_after_copy
):
    with retry_fixtures.CiDotnetRetryTests().shard("green") as result:
        root, _, _, _, manifest, error = result
        assert error is None
        upload = root / "upload"
        source = root / "public-source"
        shutil.copytree(upload, source)
        original_copy = MODULE.shutil.copyfile

        def copy_then_inject(source, destination, **kwargs):
            value = original_copy(source, destination, **kwargs)
            (upload / extra).write_text(MARKER)
            return value

        if not injected_after_copy:
            (upload / extra).write_text(MARKER)
        with patch.object(
            MODULE.shutil,
            "copyfile",
            side_effect=copy_then_inject if injected_after_copy else original_copy,
        ):
            if injected_after_copy:
                with pytest.raises(RuntimeError) as caught:
                    MODULE.publish_ci_dotnet_artifact(
                        source, upload, "shards/core/manifest.json", manifest["files"]
                    )
                assert (
                    str(caught.value)
                    == "Public evidence contains unexpected files (count: 1)."
                )
                assert MARKER not in str(caught.value)
                assert not any(p.is_file() for p in upload.rglob("*"))
            else:
                MODULE.publish_ci_dotnet_artifact(
                    source,
                    upload,
                    "shards/core/manifest.json",
                    manifest["files"],
                )
                assert not (upload / extra).exists()
                assert {
                    p.relative_to(upload).as_posix()
                    for p in upload.rglob("*")
                    if p.is_file()
                } == {"shards/core/manifest.json", *manifest["files"]}
        assert list((root / "work").rglob("*.trx"))


def _documents(downloads, shard="core", attempt=1):
    artifact = fixtures.VerifyOrchestrationTests.ci_artifact_root(
        downloads, shard, attempt
    )
    manifest_path = artifact / f"shards/{shard}/manifest.json"
    summary_path = artifact / f"shards/{shard}/shard-summary.json"
    return (
        artifact,
        manifest_path,
        summary_path,
        json.loads(manifest_path.read_text()),
        json.loads(summary_path.read_text()),
    )


def _save(manifest_path, summary_path, manifest, summary):
    fixtures.VerifyOrchestrationTests.write_ci_manifest(summary_path, summary)
    manifest["files"][f"shards/{manifest['shard']}/shard-summary.json"] = (
        MODULE.sha256_file(summary_path)
    )
    fixtures.VerifyOrchestrationTests.write_ci_manifest(manifest_path, manifest)


@pytest.mark.parametrize("path", ["producer", "finalizer"])
@pytest.mark.parametrize("scrub", ["0", "1"])
def test_successful_shard_with_empty_golden_project_is_refused(path, scrub, capsys):
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture() as (root, downloads, coverage):
        artifact, manifest_path, summary_path, manifest, summary = _documents(downloads)
        golden = next(
            row
            for row in manifest["projects"]
            if Path(row["relativePath"]).stem == "NvtFwCombiner.GoldenRegression.Tests"
        )
        for name in MODULE.summary_evidence.COUNTERS:
            summary[name] -= golden[name]
            golden[name] = 0
        _save(manifest_path, summary_path, manifest, summary)
        baseline = json.loads((root / "scripts/ci/test-baseline.json").read_text())
        assert summary["total"] >= baseline["shards"]["core"]["minTotal"]
        assert manifest["success"] and summary["outcome"] == "passed"
        assert all(
            row["total"] > 0 for row in manifest["projects"] if row is not golden
        )
        assert all(
            MODULE.sha256_file(artifact / relative) == digest
            for relative, digest in manifest["files"].items()
        )
        with (
            patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": scrub}),
            pytest.raises(RuntimeError) as caught,
        ):
            if path == "producer":
                MODULE.publish_ci_dotnet_artifact(
                    artifact,
                    root / "upload",
                    "shards/core/manifest.json",
                    manifest["files"],
                )
            else:
                MODULE.finalize_ci_dotnet_evidence(downloads)
        assert str(caught.value) == "Public evidence counters are inconsistent."
        coverage.assert_not_called()
        if path == "producer":
            assert not any(p.is_file() for p in (root / "upload").rglob("*"))
        output = capsys.readouterr()
        assert output.out + output.err == ""


@pytest.mark.parametrize(
    "mutation,message",
    [
        ("extra", "Public evidence contains unexpected files (count: 1)."),
        ("missing-shard", "Required producer evidence is missing."),
        ("source", "Public evidence identity does not match."),
        ("run-id", "Public evidence provenance is not current."),
        ("stale-attempt", "Public evidence provenance is not current."),
        ("hash", "Public evidence hash does not match."),
        ("counters", "Public evidence counters are inconsistent."),
        ("floor", "Public evidence does not meet the required floor."),
        ("failed", "Public evidence reports an unsuccessful shard."),
        ("skipped", "Public evidence reports an unsuccessful shard."),
        ("old-schema", "Previous public evidence schema is not supported."),
        ("manifest-field", "Public evidence summary is invalid."),
        ("summary-field", "Public evidence summary is invalid."),
        ("project-field", "Public evidence summary is invalid."),
        ("retry-field", "Public evidence summary is invalid."),
        ("check-field", "Public evidence summary is invalid."),
        ("theory-name", "Public evidence summary is invalid."),
        ("boolean-counter", "Public evidence counters are inconsistent."),
        ("string-counter", "Public evidence counters are inconsistent."),
        ("float-counter", "Public evidence counters are inconsistent."),
        ("duplicate-key", "Public evidence summary is invalid."),
        ("non-string-path", "Public evidence summary is invalid."),
        ("non-string-run-id", "Public evidence provenance is not current."),
        ("summary-commit", "Public evidence identity does not match."),
        ("missing-schema", "Public evidence summary is invalid."),
        ("string-schema", "Public evidence summary is invalid."),
        ("float-schema", "Public evidence summary is invalid."),
        ("boolean-schema", "Public evidence summary is invalid."),
        ("future-schema", "Public evidence summary is invalid."),
        ("boolean-shard-attempt", "Public evidence provenance is not current."),
        ("boolean-recovered", "Public evidence summary is invalid."),
        ("boolean-example-executed", "Public evidence summary is invalid."),
        ("summary-duplicate-key", "Public evidence summary is invalid."),
        ("non-boolean-success", "Public evidence summary is invalid."),
        ("retry-without-recovery", "Public evidence summary is invalid."),
        ("retry-without-flaky-method", "Public evidence summary is invalid."),
        ("golden-retry", "Public evidence summary is invalid."),
        ("sdk-version", "Public evidence identity does not match."),
        ("missing-row", "Public evidence summary is invalid."),
        ("unknown-row-path", "Public evidence summary is invalid."),
        ("row-order", "Public evidence summary is invalid."),
        ("summary-total", "Public evidence counters are inconsistent."),
        ("summary-failed-union", "Public evidence counters are inconsistent."),
        ("coverage-hash", "Public evidence hash does not match."),
        ("forged-floor", "Public evidence summary is invalid."),
        ("forged-success-failed", "Public evidence reports an unsuccessful shard."),
        ("forged-success-skipped", "Public evidence summary is invalid."),
    ],
)
def test_finalizer_refusals_are_fixed_and_private_values_never_escape(
    mutation, message, capsys
):
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture() as (root, downloads, coverage):
        artifact, manifest_path, summary_path, manifest, summary = _documents(downloads)
        if mutation == "extra":
            (artifact / (MARKER + ".trx")).write_text(MARKER)
        elif mutation == "missing-shard":
            shutil.rmtree(artifact)
        elif mutation == "source":
            manifest["sourceSha"] = "b" * 40
        elif mutation == "run-id":
            manifest["runId"] = MARKER
        elif mutation == "stale-attempt":
            manifest["runAttempt"] = 2
        elif mutation == "boolean-shard-attempt":
            manifest["runAttempt"] = True
        elif mutation == "boolean-recovered":
            manifest["projects"][0]["retry"]["recoveredTests"] = False
        elif mutation == "boolean-example-executed":
            summary["exampleData"]["executed"] = False
        elif mutation == "non-boolean-success":
            manifest["success"] = MARKER
        elif mutation in {
            "retry-without-recovery",
            "retry-without-flaky-method",
            "golden-retry",
        }:
            row = next(
                row
                for row in manifest["projects"]
                if (
                    Path(row["relativePath"]).stem
                    == "NvtFwCombiner.GoldenRegression.Tests"
                )
                == (mutation == "golden-retry")
            )
            row["retry"] = {
                "retried": True,
                "recoveredTests": 0 if mutation == "retry-without-recovery" else 1,
            }
            row["flakyTests"] = (
                [] if mutation != "golden-retry" else ["Probe.Tests.Method"]
            )
        elif mutation == "future-schema":
            manifest["schemaVersion"] = 99
        elif mutation == "sdk-version":
            manifest["sdkVersion"] = "0.0.0"
        elif mutation == "missing-row":
            manifest["projects"].pop()
        elif mutation == "unknown-row-path":
            manifest["projects"][0]["relativePath"] = "tests/Unknown/Unknown.csproj"
        elif mutation == "row-order":
            manifest["projects"].reverse()
        elif mutation == "summary-total":
            summary["total"] += 1
        elif mutation == "summary-failed-union":
            summary["failedTests"] = ["Probe.Tests.Method"]
        elif mutation == "forged-floor":
            baseline_path = root / "scripts/ci/test-baseline.json"
            baseline = json.loads(baseline_path.read_text())
            baseline["shards"]["core"]["minTotal"] = summary["total"] + 1
            fixture.write_ci_manifest(baseline_path, baseline)
        elif mutation in {"forged-success-failed", "forged-success-skipped"}:
            key = "failed" if mutation == "forged-success-failed" else "skipped"
            row = manifest["projects"][0]
            row["passed"] -= 1
            row[key] += 1
            summary["passed"] -= 1
            summary[key] += 1
        elif mutation == "coverage-hash":
            key = next(name for name in manifest["files"] if name.endswith("coverage.json"))
            manifest["files"][key] = "0" * 64
        elif mutation == "hash":
            manifest["files"]["shards/core/shard-summary.json"] = "0" * 64
        elif mutation == "counters":
            manifest["projects"][0]["passed"] += 1
        elif mutation == "boolean-counter":
            manifest["projects"][0]["passed"] = True
        elif mutation in {"string-counter", "float-counter"}:
            manifest["projects"][0]["passed"] = (
                MARKER if mutation == "string-counter" else 1.5
            )
        elif mutation in {"duplicate-key", "summary-duplicate-key"}:
            pass
        elif mutation == "non-string-path":
            manifest["projects"][0]["relativePath"] = {MARKER: MARKER}
        elif mutation == "non-string-run-id":
            manifest["runId"] = {MARKER: MARKER}
        elif mutation == "summary-commit":
            summary["commit"] = MARKER
        elif mutation == "missing-schema":
            del manifest["schemaVersion"]
        elif mutation in {"string-schema", "float-schema", "boolean-schema"}:
            manifest["schemaVersion"] = {
                "string-schema": MARKER,
                "float-schema": 5.0,
                "boolean-schema": True,
            }[mutation]
        elif mutation == "floor":
            baseline_path = root / "scripts/ci/test-baseline.json"
            baseline = json.loads(baseline_path.read_text())
            baseline["shards"]["core"]["minTotal"] = summary["total"] + 1
            fixture.write_ci_manifest(baseline_path, baseline)
            summary["checks"]["belowFloor"] = True
            summary["outcome"] = "failed"
            manifest["success"] = False
        elif mutation in {"failed", "skipped"}:
            key = mutation
            row = manifest["projects"][0]
            row["passed"] -= 1
            row[key] += 1
            summary["passed"] -= 1
            summary[key] += 1
            summary["outcome"] = "failed"
            manifest["success"] = False
            if key == "failed":
                row["failedTests"] = summary["failedTests"] = ["Probe.Tests.Method"]
            else:
                summary["checks"]["skippedNotZero"] = True
        elif mutation == "old-schema":
            manifest["schemaVersion"] = 4
        elif mutation == "manifest-field":
            manifest[MARKER] = MARKER
        elif mutation == "summary-field":
            summary[MARKER] = MARKER
        elif mutation == "project-field":
            manifest["projects"][0][MARKER] = MARKER
        elif mutation == "retry-field":
            manifest["projects"][0]["retry"][MARKER] = MARKER
        elif mutation == "check-field":
            summary["checks"][MARKER] = True
        else:
            manifest["projects"][0]["flakyTests"] = [f"Probe.Tests.Method({MARKER})"]
        if mutation != "missing-shard":
            if mutation != "hash":
                _save(manifest_path, summary_path, manifest, summary)
            else:
                fixture.write_ci_manifest(manifest_path, manifest)
            if mutation == "duplicate-key":
                text = manifest_path.read_text()
                manifest_path.write_text(
                    text.replace('"runId":', f'"runId": "{MARKER}", "runId":', 1)
                )
            elif mutation == "summary-duplicate-key":
                summary_path.write_text(
                    summary_path.read_text().replace(
                        '"commit":', f'"commit": "{MARKER}", "commit":', 1
                    )
                )
                manifest["files"]["shards/core/shard-summary.json"] = (
                    MODULE.sha256_file(summary_path)
                )
                fixture.write_ci_manifest(manifest_path, manifest)
        with pytest.raises(RuntimeError) as caught:
            MODULE.finalize_ci_dotnet_evidence(downloads)
        assert str(caught.value) == message
        assert MARKER not in str(caught.value)
        coverage.assert_not_called()
        output = capsys.readouterr()
        assert MARKER not in output.out + output.err


def test_finalizer_refuses_stale_download_even_when_its_manifest_claims_current():
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture(attempt=2) as (_root, downloads, coverage):
        artifact, *_ = _documents(downloads, attempt=2)
        artifact.rename(fixture.ci_artifact_root(downloads, "core", 1))
        with pytest.raises(RuntimeError) as caught:
            MODULE.finalize_ci_dotnet_evidence(downloads)
        assert str(caught.value) == "Public evidence provenance is not current."
        coverage.assert_not_called()


def test_superseded_downloads_cannot_hide_raw_files():
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture(attempt=2) as (_root, downloads, coverage):
        old = fixture.ci_artifact_root(downloads, "core", 1)
        old.mkdir()
        (old / (MARKER + ".log")).write_text(MARKER)
        with pytest.raises(RuntimeError) as caught:
            MODULE.finalize_ci_dotnet_evidence(downloads)
        assert (
            str(caught.value) == "Public evidence contains unexpected files (count: 1)."
        )
        assert MARKER not in str(caught.value)
        coverage.assert_not_called()


@pytest.mark.parametrize("nested", [False, True])
def test_unowned_downloaded_files_are_refused_with_only_the_count(nested):
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture() as (_root, downloads, coverage):
        extra_root = downloads / MARKER if nested else downloads
        extra_root.mkdir(exist_ok=True)
        for suffix in (".trx", ".log"):
            (extra_root / (MARKER + suffix)).write_text(MARKER)
        with pytest.raises(RuntimeError) as caught:
            MODULE.finalize_ci_dotnet_evidence(downloads)
        assert (
            str(caught.value) == "Public evidence contains unexpected files (count: 2)."
        )
        assert MARKER not in str(caught.value)
        coverage.assert_not_called()


def test_missing_baseline_is_a_fixed_refusal_without_the_source_path():
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture() as (root, downloads, coverage):
        with patch.object(MODULE, "ROOT", root / MARKER):
            with pytest.raises(RuntimeError) as caught:
                MODULE.finalize_ci_dotnet_evidence(downloads)
        assert str(caught.value) == "Public evidence summary is invalid."
        assert MARKER not in str(caught.value)
        coverage.assert_not_called()


@pytest.mark.parametrize(
    "kind", ["test-source", "message-field", "xml-message", "xml-comment"]
)
def test_coverage_that_can_carry_private_data_is_refused(kind):
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture() as (_root, downloads, coverage):
        artifact, manifest_path, summary_path, manifest, summary = _documents(downloads)
        project = MODULE.CI_DOTNET_SHARDS["core"][0]
        json_relative, xml_relative = MODULE.summary_evidence.coverage_paths(
            "core", project.name
        )
        if kind == "test-source":
            payload = {"NvtFwCombiner.Domain.dll": {f"tests/{MARKER}.cs": {}}}
        elif kind == "message-field":
            payload = {
                "NvtFwCombiner.Domain.dll": {
                    "src/NvtFwCombiner.Domain/Probe.cs": {
                        "Probe": {
                            "System.Void Probe::Run()": {
                                "Lines": {},
                                "Branches": [],
                                "Output": MARKER,
                            }
                        }
                    }
                }
            }
        else:
            payload = {}
            (artifact / xml_relative).write_text(
                f"<coverage><Output>{MARKER}</Output></coverage>"
                if kind == "xml-message"
                else f"<coverage><!--{MARKER}--></coverage>"
            )
        (artifact / json_relative).write_text(json.dumps(payload))
        manifest["files"][json_relative] = MODULE.sha256_file(artifact / json_relative)
        manifest["files"][xml_relative] = MODULE.sha256_file(artifact / xml_relative)
        _save(manifest_path, summary_path, manifest, summary)
        with pytest.raises(RuntimeError) as caught:
            MODULE.finalize_ci_dotnet_evidence(downloads)
        assert str(caught.value) == "Public coverage evidence is invalid."
        assert MARKER not in str(caught.value)
        coverage.assert_not_called()


@pytest.mark.parametrize("local_function", [False, True])
def test_passing_summary_run_preserves_the_complete_coverage_aggregate(local_function):
    fixture = fixtures.VerifyOrchestrationTests()
    with fixture.public_finalizer_fixture() as (root, downloads, coverage):
        artifact, manifest_path, summary_path, manifest, summary = _documents(downloads)
        project = MODULE.CI_DOTNET_SHARDS["core"][0]
        json_relative, xml_relative = MODULE.summary_evidence.coverage_paths(
            "core", project.name
        )
        data = {
            "NvtFwCombiner.Domain.dll": {
                "src/NvtFwCombiner.Domain/Probe.cs": {
                    "Probe": {
                        "System.Void Probe::Run()": {
                            "Lines": {"1": 3},
                            "Branches": [
                                {
                                    "Line": 1,
                                    "Offset": 0,
                                    "EndOffset": 1,
                                    "Path": 0,
                                    "Ordinal": 0,
                                    "Hits": 3,
                                }
                            ],
                        }
                    }
                }
            }
        }
        (artifact / json_relative).write_text(json.dumps(data))
        (artifact / xml_relative).write_text(
            '<coverage version="1.9"><sources><source>.</source></sources><packages>'
            '<package name="NvtFwCombiner.Domain"><classes><class name="Probe" '
            'filename="src/NvtFwCombiner.Domain/Probe.cs"><methods><method name="Run" '
            'signature="()"><lines><line number="1" hits="3" branch="true" '
            'condition-coverage="100% (1/1)" /></lines></method></methods></class></classes>'
            "</package></packages></coverage>"
        )
        if local_function:
            # C# local-function metadata from an existing public product pattern.
            module = "NvtFwCombiner.Application"
            source = "src/NvtFwCombiner.Application/Configuration/EventBufferFormatConfigurationSession.cs"
            cls = "NvtFwCombiner.Application.Configuration.EventBufferFormatConfigurationSession"
            generated = "<BuiltInDefaultsHash>g__Append|20_0"
            counts = data["NvtFwCombiner.Domain.dll"][
                "src/NvtFwCombiner.Domain/Probe.cs"
            ]["Probe"]["System.Void Probe::Run()"]
            (artifact / json_relative).write_text(
                json.dumps(
                    {
                        module + ".dll": {
                            source: {
                                cls: {
                                    "System.Void "
                                    + cls
                                    + "::"
                                    + generated
                                    + "(System.String)": counts,
                                }
                            }
                        },
                    }
                ),
                encoding="utf-8",
            )
            tree = MODULE.ET.parse(artifact / xml_relative)
            tree.find(".//package").set("name", module)
            node = tree.find(".//class")
            node.set("name", cls)
            node.set("filename", source)
            method = tree.find(".//method")
            method.set("name", generated)
            method.set("signature", "(System.String)")
            tree.write(artifact / xml_relative, encoding="utf-8", xml_declaration=True)
        for relative in (json_relative, xml_relative):
            manifest["files"][relative] = MODULE.sha256_file(artifact / relative)
        _save(manifest_path, summary_path, manifest, summary)
        expected = {}
        for shard, projects in MODULE.CI_DOTNET_SHARDS.items():
            source = fixture.ci_artifact_root(downloads, shard)
            for item in projects:
                for relative in MODULE.summary_evidence.coverage_paths(
                    shard, item.name
                ):
                    expected[f"{item.name}/{Path(relative).name}"] = (
                        source / relative
                    ).read_bytes()
        with (
            patch.object(
                MODULE, "parse_trx_counters", side_effect=AssertionError(MARKER)
            ),
            patch.object(
                MODULE,
                "require_discovered_test_results",
                side_effect=AssertionError(MARKER),
            ),
            patch.object(
                MODULE, "ci_retry_selection", side_effect=AssertionError(MARKER)
            ),
            patch.object(
                MODULE, "_load_trx_reader", side_effect=AssertionError(MARKER)
            ),
        ):
            MODULE.finalize_ci_dotnet_evidence(downloads)
        output = root / "coverage/dotnet"
        actual = {
            p.relative_to(output).as_posix(): p.read_bytes()
            for p in output.rglob("*")
            if p.is_file()
        }
        assert actual == expected
        coverage.assert_called_once_with("dotnet", output)


@pytest.mark.parametrize("scrub", ["0", "1"])
def test_failure_reports_never_expose_theory_arguments_or_messages(scrub):
    with retry_fixtures.CiDotnetRetryTests().shard("green") as result:
        root, _, _, attempts, _, _ = result
        trx = attempts[0] / "test-results.trx"
        fixtures.VerifyOrchestrationTests.write_ci_trx(
            trx,
            total=1,
            skipped=0,
            identities=(f"Probe.Tests.Method({MARKER})",),
            outcomes=("Failed",),
            error_messages={f"Probe.Tests.Method({MARKER})": MARKER},
        )
        public_out, public_err = io.StringIO(), io.StringIO()
        with (
            redirect_stdout(public_out),
            redirect_stderr(public_err),
            patch.dict(
                MODULE.os.environ,
                {
                    "NFC_CI_SCRUB_TEST_OUTPUT": scrub,
                    "GITHUB_STEP_SUMMARY": str(root / "safe-summary.md"),
                },
            ),
        ):
            MODULE.report_ci_test_failure_details("core", [(MARKER, MARKER, trx)])
            MODULE.report_ci_failed_tests(
                "core",
                MARKER,
                [
                    (
                        MARKER,
                        MODULE.CiFailedProjectEvidence(
                            (trx,), (f"Probe.Tests.Method({MARKER})",)
                        ),
                    )
                ],
            )
            MODULE.report_ci_flaky_tests(
                "core",
                [
                    {
                        "project": MARKER,
                        "fullyQualifiedName": f"Probe.Tests.Flaky({MARKER})",
                    }
                ],
            )
        assert MARKER not in public_out.getvalue() + public_err.getvalue()
        assert MARKER not in (root / "safe-summary.md").read_text()
        assert "::warning title=Flaky test::Probe.Tests.Flaky" in public_out.getvalue()
        if scrub == "1":
            assert "Probe.Tests.Method" not in public_out.getvalue()


def test_summary_write_failure_uses_a_fixed_public_sentence():
    with retry_fixtures.CiDotnetRetryTests().shard("green") as result:
        root, _, _, _, _, _ = result
        blocker = root / MARKER
        blocker.write_text(MARKER)
        output, errors = io.StringIO(), io.StringIO()
        with (
            redirect_stdout(output),
            redirect_stderr(errors),
            patch.dict(
                MODULE.os.environ, {"GITHUB_STEP_SUMMARY": str(blocker / MARKER)}
            ),
        ):
            MODULE.report_ci_flaky_tests(
                "core", [{"project": MARKER, "fullyQualifiedName": "Probe.Tests.Flaky"}]
            )
        assert errors.getvalue() == "CI step summary could not be written.\n"
        assert MARKER not in output.getvalue() + errors.getvalue()


@pytest.mark.parametrize("operation", ["mkdir", "copy"])
@pytest.mark.parametrize("scrub", ["0", "1"])
def test_public_projection_io_failure_is_fixed_and_private_values_never_escape(
    operation, scrub
):
    with retry_fixtures.CiDotnetRetryTests().shard("green") as result:
        root, _, _, _, manifest, error = result
        assert error is None
        output = root / "work/shards/core"
        public_root = root / "work/public-projection"
        original_mkdir = Path.mkdir
        original_copy = MODULE.shutil.copyfile

        def denied_mkdir(path, *args, **kwargs):
            if operation == "mkdir" and path.is_relative_to(public_root):
                raise PermissionError(13, "synthetic denied", MARKER)
            return original_mkdir(path, *args, **kwargs)

        def denied_copy(source, destination, **kwargs):
            if operation == "copy" and Path(destination).is_relative_to(public_root):
                raise PermissionError(13, "synthetic denied", MARKER)
            return original_copy(source, destination, **kwargs)

        public_out, public_err = io.StringIO(), io.StringIO()
        with (
            patch.object(Path, "mkdir", denied_mkdir),
            patch.object(MODULE.shutil, "copyfile", side_effect=denied_copy),
            patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": scrub}),
            redirect_stdout(public_out),
            redirect_stderr(public_err),
            pytest.raises(RuntimeError) as caught,
        ):
            MODULE.collect_ci_public_shard_evidence(
                "core",
                output,
                tuple(output.rglob("*")),
                {
                    Path(row["relativePath"]).stem: row["testAssemblySha256"]
                    for row in manifest["projects"]
                },
                {},
                set(),
                public_root=public_root,
                source_sha=manifest["sourceSha"],
                test_run_failed=False,
            )
        assert str(caught.value) == "Public evidence projection could not be created."
        assert caught.value.__suppress_context__
        assert (
            MARKER
            not in str(caught.value) + public_out.getvalue() + public_err.getvalue()
        )
        assert list((output / "results").rglob("*.trx"))


@pytest.mark.parametrize(
    "source,scenario",
    [
        ("require_discovered_test_results", "green"),
        ("normalize_ci_dotnet_coverage_reports", "green"),
        ("require_ci_retry_results", "recover"),
    ],
)
def test_producer_failure_diagnostics_stay_only_in_runner_shard_log(source, scenario):
    errors = io.StringIO()
    with (
        patch.object(MODULE, source, side_effect=RuntimeError(MARKER)),
        redirect_stderr(errors),
        retry_fixtures.CiDotnetRetryTests().shard(scenario) as result,
    ):
        root, project, _, _, _, error = result
        assert error is not None
        assert f"{project.name}: project failed." in str(error)
        assert MARKER in (root / "work/shards/core/shard.log").read_text()
        public = str(error) + errors.getvalue() + (root / "job.log").read_text()
        if (root / "summary.md").exists():
            public += (root / "summary.md").read_text()
        for path in (root / "upload").rglob("*"):
            if path.is_file():
                public += path.read_text()
        assert MARKER not in public


def test_local_run_preserves_the_previous_raw_manifest_exactly():
    with retry_fixtures.CiDotnetRetryTests().shard("green") as result:
        root, _, _, _, public, error = result
        assert error is None
        raw_root = root / "work"
        raw_rows, paths = [], ["shards/core/shard.log"]
        for row in public["projects"]:
            name = Path(row["relativePath"]).stem
            attempt = f"shards/core/results/{name}/attempt-1"
            raw_row = {
                "relativePath": row["relativePath"],
                "testAssemblySha256": row["testAssemblySha256"],
                "total": 3,
                "passed": 3,
                "failed": 0,
                "skipped": 0,
                "retry": None,
            }
            for key, filename in {
                "discovery": "discovered-tests.txt",
                "trx": "test-results.trx",
                "attemptLog": "attempt.log",
                "coverageJson": "collector/coverage.json",
                "coverageCobertura": "collector/coverage.cobertura.xml",
            }.items():
                raw_row[key] = f"{attempt}/{filename}"
                paths.append(raw_row[key])
            raw_rows.append(raw_row)
        expected = {
            key: public[key]
            for key in (
                "kind",
                "sourceSha",
                "runId",
                "runAttempt",
                "sdkVersion",
                "success",
                "shard",
                "producerPlatform",
            )
        }
        expected.update(
            schemaVersion=4,
            projects=raw_rows,
            flakyTests=[],
            files={path: MODULE.sha256_file(raw_root / path) for path in paths},
        )
        assert (
            json.loads((raw_root / "shards/core/manifest.json").read_text()) == expected
        )
        assert not (raw_root / "shards/core/shard-summary.json").exists()
        assert not (raw_root / "shards/core/coverage").exists()


@pytest.mark.parametrize("failure", ["projection", "flaky-report"])
def test_raw_manifest_hashes_match_after_late_diagnostics(failure):
    owner, name = (
        (MODULE.check_test_shard, "check_test_shard")
        if failure == "projection"
        else (MODULE, "report_ci_flaky_tests")
    )
    with (
        patch.object(owner, name, side_effect=ValueError(MARKER)),
        retry_fixtures.CiDotnetRetryTests().shard("green") as result,
    ):
        root, _, _, _, _, error = result
        assert error is not None
        raw_root = root / "work"
        raw = json.loads((raw_root / "shards/core/manifest.json").read_text())
        assert raw["schemaVersion"] == 4 and raw["success"] is True
        assert MARKER in (raw_root / "shards/core/shard.log").read_text()
        assert all(
            MODULE.sha256_file(raw_root / path) == digest
            for path, digest in raw["files"].items()
        )


@pytest.mark.parametrize("operation", ["hash", "write"])
@pytest.mark.parametrize(
    "terminal_type",
    [RuntimeError, KeyboardInterrupt, SystemExit, subprocess.TimeoutExpired],
)
def test_late_raw_receipt_io_failure_preserves_primary_failure(
    operation, terminal_type, capsys
):
    terminal = (
        subprocess.TimeoutExpired(MARKER, 30, output=MARKER, stderr=MARKER)
        if terminal_type is subprocess.TimeoutExpired
        else terminal_type(MARKER)
    )
    original_hashes = MODULE.ci_file_hashes
    original_write = MODULE.write_ci_manifest
    raw_writes = 0

    def hashes(paths, root):
        if operation == "hash" and (root / "shards/core/manifest.json").exists():
            raise OSError(MARKER)
        return original_hashes(paths, root)

    def write(path, document):
        nonlocal raw_writes
        if document["schemaVersion"] == 4:
            raw_writes += 1
            if operation == "write" and raw_writes == 2:
                raise OSError(MARKER)
        return original_write(path, document)

    with (
        patch.object(MODULE, "ci_file_hashes", side_effect=hashes),
        patch.object(MODULE, "write_ci_manifest", side_effect=write),
        retry_fixtures.CiDotnetRetryTests().shard(
            "green", terminal_phase="cleanup", terminal=terminal
        ) as result,
    ):
        root, _, _, _, _, error = result
        assert type(error) is terminal_type
        if terminal_type is RuntimeError:
            assert "Shard cleanup failed." in str(error)
            assert "Runner-local evidence receipt could not be refreshed." in str(error)
        else:
            assert error is terminal
        assert MARKER in (root / "work/shards/core/shard.log").read_text()
        assert MARKER not in str(error) + (root / "job.log").read_text()
        assert not list((root / "upload").rglob("*"))
        captured = capsys.readouterr()
        assert MARKER not in captured.out + captured.err
        assert "Runner-local evidence receipt could not be refreshed." in captured.err


@pytest.mark.parametrize(
    "terminal_type", [KeyboardInterrupt, SystemExit, subprocess.TimeoutExpired]
)
@pytest.mark.parametrize("phase", ["execution", "cleanup"])
def test_shard_preserves_terminal_exception_types_and_local_reasons(
    terminal_type, phase
):
    terminal = (
        subprocess.TimeoutExpired(MARKER, 30, output=MARKER, stderr=MARKER)
        if terminal_type is subprocess.TimeoutExpired
        else terminal_type(MARKER)
    )
    with (
        patch.object(
            MODULE.check_test_shard, "check_test_shard", side_effect=ValueError(MARKER)
        ),
        retry_fixtures.CiDotnetRetryTests().shard(
            "green", terminal_phase=phase, terminal=terminal
        ) as result,
    ):
        root, _, _, _, _, error = result
        assert type(error) is terminal_type
        assert error is terminal
        assert MARKER in (root / "work/shards/core/shard.log").read_text()
        assert MARKER not in str(error) + (root / "job.log").read_text()
        if terminal_type is subprocess.TimeoutExpired:
            assert error.output is None and error.stderr is None


def test_projection_reset_failure_keeps_original_failure_and_fixed_diagnostics():
    original_reset = MODULE.reset_ci_dotnet_evidence_directory
    calls = 0

    def reset(path, **kwargs):
        nonlocal calls
        if path == MODULE.CI_DOTNET_UPLOAD_ROOT:
            calls += 1
            if calls > 1:
                raise OSError(MARKER)
        return original_reset(path, **kwargs)

    with (
        patch.object(MODULE, "reset_ci_dotnet_evidence_directory", side_effect=reset),
        patch.object(
            MODULE.check_test_shard, "check_test_shard", side_effect=ValueError(MARKER)
        ),
        retry_fixtures.CiDotnetRetryTests().shard("twice") as result,
    ):
        root, project, _, _, _, error = result
        assert f"{project.name}: project failed." in str(error)
        assert "Public evidence projection could not be created." in str(error)
        assert "Public evidence staging could not be cleared." in str(error)
        assert MARKER not in str(error) + (root / "job.log").read_text()


@pytest.mark.parametrize("failed", [False, True])
@pytest.mark.parametrize("unsafe_name", ["Probe.Generic`1", "Probe.測試"])
def test_non_public_test_names_keep_project_rows_and_counters(failed, unsafe_name):
    with retry_fixtures.CiDotnetRetryTests().shard("green") as result:
        root, project, _, attempts, manifest, error = result
        assert error is None
        first = attempts[0] / "test-results.trx"
        tree = MODULE.ET.parse(first)
        definition = tree.find(".//{*}TestMethod")
        definition.set("className", unsafe_name)
        definition.set("name", "方法")
        if failed:
            tree.find(".//{*}UnitTestResult").set("outcome", "Failed")
            counters = tree.find(".//{*}Counters")
            counters.set("passed", "2")
            counters.set("failed", "1")
            tree.find(".//{*}ResultSummary").set("outcome", "Failed")
        tree.write(first, encoding="utf-8")
        # A sibling TRX must not join this project's selected first result.
        (first.parent / "unselected.trx").write_text(MARKER)
        rows, summary, _ = MODULE.collect_ci_public_shard_evidence(
            "core",
            root / "work/shards/core",
            tuple((root / "work").rglob("*")),
            {
                Path(row["relativePath"]).stem: row["testAssemblySha256"]
                for row in manifest["projects"]
            },
            {},
            set(),
            public_root=root / "work/projection",
            source_sha=manifest["sourceSha"],
            test_run_failed=failed,
        )
        row = next(row for row in rows if row["relativePath"] == project.relative_path)
        assert (row["total"], row["passed"], row["failed"]) == (
            3,
            2 if failed else 3,
            int(failed),
        )
        assert row["failedTests"] == []
        assert summary["outcome"] == ("failed" if failed else "passed")
        assert summary["total"] == sum(row["total"] for row in rows)
        MODULE.summary_evidence.validate_documents(
            {**manifest, "success": not failed, "projects": rows},
            summary,
            shard="core",
            project_paths=[p.relative_path for p in MODULE.CI_DOTNET_SHARDS["core"]],
            source_sha=manifest["sourceSha"],
            run_id=manifest["runId"],
            run_attempt=manifest["runAttempt"],
            sdk_version=manifest["sdkVersion"],
            platform=manifest["producerPlatform"],
            floor=json.loads((root / "scripts/ci/test-baseline.json").read_text())[
                "shards"
            ]["core"],
        )


def test_unsafe_recovered_method_is_omitted_without_losing_recovery_counts():
    with retry_fixtures.CiDotnetRetryTests().shard("recover") as result:
        root, project, _, attempts, manifest, _ = result
        output = root / "work/shards/core"
        rows, summary, _ = MODULE.collect_ci_public_shard_evidence(
            "core",
            output,
            tuple(output.rglob("*")),
            {
                Path(row["relativePath"]).stem: row["testAssemblySha256"]
                for row in manifest["projects"]
            },
            {
                project.name: (
                    attempts[1] / "test-results.trx",
                    ("Probe.Tests.Generic`1.Method",),
                )
            },
            {project.name},
            public_root=root / "work/public-projection",
            source_sha=manifest["sourceSha"],
            test_run_failed=True,
        )
        assert summary["outcome"] == "failed"
        row = next(row for row in rows if row["relativePath"] == project.relative_path)
        assert row["retry"] == {"retried": True, "recoveredTests": 1}
        assert row["flakyTests"] == []
        assert row["total"] == row["passed"] == 3


def test_shard_checker_value_error_keeps_original_failure_and_clears_upload():
    with (
        patch.object(
            MODULE.check_test_shard, "check_test_shard", side_effect=ValueError(MARKER)
        ),
        retry_fixtures.CiDotnetRetryTests().shard("twice") as result,
    ):
        root, project, _, _, _, error = result
        assert f"{project.name}: project failed." in str(error)
        assert "Public evidence projection could not be created." in str(error)
        assert MARKER not in str(error) + (root / "job.log").read_text()
        assert not any(p.is_file() for p in (root / "upload").rglob("*"))
        assert (root / "work/shards/core/manifest.json").is_file()


@pytest.mark.parametrize("scrub", ["0", "1"])
def test_failed_project_without_readable_results_prints_fixed_diagnosis(
    scrub, capsys, test_area_tmp_path, monkeypatch
):
    tmp_path = test_area_tmp_path
    summary = tmp_path / "summary.md"
    monkeypatch.setenv("GITHUB_STEP_SUMMARY", str(summary))
    monkeypatch.setenv("NFC_CI_SCRUB_TEST_OUTPUT", scrub)
    MODULE.report_ci_failed_tests(
        "core",
        "unused",
        [("NvtFwCombiner.Domain.Tests", MODULE.CiFailedProjectEvidence((), None))],
    )
    sentence = "NvtFwCombiner.Domain.Tests: no readable results.\n"
    assert capsys.readouterr().out == sentence
    assert sentence in summary.read_text()


@pytest.mark.parametrize(
    "character,location",
    [
        ("$", "pair"),
        ("@", "json"),
        ("@", "xml"),
        ("#", "json"),
        ("#", "xml"),
        ("^", "json"),
        ("^", "xml"),
    ],
)
def test_coverage_identity_character_contract(character, location, test_area_tmp_path):
    tmp_path = test_area_tmp_path
    # Observed Coverlet pair: retained Infrastructure.Tests collector,
    # f01a2c25699b43fc8db3f0832e94300c; JSON fba8ef2d23583a2681386f95e74e2e9c449932dd8b89ab165cb61178fb81ad84,
    # Cobertura 935b59d371e220303acd9d3e52167003aaa22db5a57884428042f8377465b896.
    module = "NvtFwCombiner.PrebuiltProfileCatalogGenerator"
    source = "eng/prebuilt-profile-catalog/Program.cs"
    json_method = f"<Main>{character if location != 'xml' else '$'}"
    method = f"<Main>{character if location != 'json' else '$'}"
    report, xml = tmp_path / "coverage.json", tmp_path / "coverage.cobertura.xml"
    report.write_text(
        json.dumps(
            {
                module + ".dll": {
                    source: {
                        "Program": {
                            f"System.Int32 Program::{json_method}(System.String[])": {
                                "Lines": {"3": 1},
                                "Branches": [],
                            }
                        }
                    }
                }
            }
        )
    )
    root = MODULE.ET.Element("coverage")
    package = MODULE.ET.SubElement(
        MODULE.ET.SubElement(root, "packages"), "package", name=module
    )
    cls = MODULE.ET.SubElement(
        MODULE.ET.SubElement(package, "classes"),
        "class",
        name="Program",
        filename=source,
    )
    MODULE.ET.SubElement(
        MODULE.ET.SubElement(cls, "methods"),
        "method",
        name=method,
        signature="(System.String[])",
    )
    MODULE.ET.ElementTree(root).write(xml, encoding="utf-8")
    if character == "$":
        MODULE.summary_evidence.validate_coverage(
            report, xml, {"eng/prebuilt-profile-catalog", "src/" + module}
        )
    else:
        with pytest.raises(
            RuntimeError, match="^Public coverage evidence is invalid\\.$"
        ):
            MODULE.summary_evidence.validate_coverage(
                report, xml, {"eng/prebuilt-profile-catalog", "src/" + module}
            )


@pytest.mark.parametrize("malformed_production_line", [False, True])
def test_real_infrastructure_coverage_projection_keeps_only_production(
    malformed_production_line, test_area_tmp_path
):
    source = Path(__file__).parent / "fixtures/infrastructure-coverage"
    report = test_area_tmp_path / "coverage.json"
    xml = test_area_tmp_path / "coverage.cobertura.xml"
    shutil.copyfile(source / report.name, report)
    shutil.copyfile(source / xml.name, xml)
    original = json.loads(report.read_text())
    assert "NvtFwCombiner.TestSupport.dll" in original
    assert "NvtFwCombiner.PrebuiltProfileCatalogGenerator.dll" in original
    assert any("/obj/" in path for sources in original.values() for path in sources)
    MODULE.normalize_ci_dotnet_coverage_reports(report, xml)
    directories = {
        path.as_posix()
        for roots in MODULE.PRODUCTION_MODULES.values()
        for path in roots
    }
    if malformed_production_line:
        document = json.loads(report.read_text())
        classes = next(
            classes
            for filename, classes in document["NvtFwCombiner.Domain.dll"].items()
            if "/obj/" not in filename
        )
        next(iter(next(iter(classes.values())).values()))["Lines"] = {"1": True}
        report.write_text(json.dumps(document))
        with pytest.raises(
            RuntimeError, match="^Public coverage evidence is invalid\\.$"
        ):
            MODULE.summary_evidence.project_coverage(report, xml, directories)
        return
    MODULE.summary_evidence.project_coverage(report, xml, directories)
    document = json.loads(report.read_text())
    assert document
    assert all(
        module.removesuffix(".dll") in {Path(root).name for root in directories}
        for module in document
    )
    assert all(
        any(Path(filename).is_relative_to(root) for root in directories)
        for sources in document.values()
        for filename in sources
    )
    assert not any(
        "/obj/" in filename for sources in document.values() for filename in sources
    )
    for text in (report.read_text(), xml.read_text()):
        assert "tests/" not in text and "eng/" not in text
    tree = MODULE.ET.parse(xml)
    assert all(
        package.findall("./classes/class")
        for package in tree.findall("./packages/package")
    )
    MODULE.summary_evidence.validate_coverage(report, xml, directories)


@pytest.mark.parametrize("malformed_complexity", [False, True])
def test_coverage_projection_recomputes_parent_metrics_from_retained_classes(
    malformed_complexity,
    test_area_tmp_path,
):
    report = test_area_tmp_path / "coverage.json"
    xml = test_area_tmp_path / "coverage.cobertura.xml"
    module = "NvtFwCombiner.Domain"
    source = "src/" + module + "/Code.cs"
    generated = "src/" + module + "/obj/Release/Generated.cs"
    branch = dict(Line=1, Offset=0, EndOffset=1, Path=0, Ordinal=0, Hits=1)
    counts = {
        "Lines": {"1": 1, "2": 0},
        "Branches": [branch, {**branch, "Path": 1, "Hits": 0}],
    }
    report.write_text(
        json.dumps(
            {
                module + ".dll": {
                    source: {
                        "Product.Code": {"System.Void Product.Code::Run()": counts}
                    },
                    generated: {"Generated": {"System.Void Generated::Run()": counts}},
                }
            }
        )
    )
    xml.write_text(
        '<coverage lines-covered="3" lines-valid="4" line-rate="0.75" '
        'branches-covered="3" branches-valid="4" branch-rate="0.75">'
        f'<packages><package name="{module}" line-rate="0.75" branch-rate="0.75" complexity="9">'
        f'<classes><class name="Product.Code" filename="{source}" complexity="2">'
        '<lines><line number="1" hits="1" branch="true" condition-coverage="50% (1/2)" />'
        '<line number="2" hits="0" branch="false" /></lines></class>'
        f'<class name="Generated" filename="{generated}" complexity="7">'
        '<lines><line number="1" hits="1" branch="true" condition-coverage="100% (2/2)" />'
        '<line number="2" hits="1" branch="false" /></lines></class>'
        "</classes></package></packages></coverage>"
    )
    if malformed_complexity:
        tree = MODULE.ET.parse(xml)
        tree.find("./packages/package/classes/class").set("complexity", "1%")
        tree.write(xml, encoding="utf-8")
        with pytest.raises(
            RuntimeError, match="^Public coverage evidence is invalid\\.$"
        ):
            MODULE.summary_evidence.project_coverage(report, xml, {"src/" + module})
        return
    MODULE.summary_evidence.project_coverage(report, xml, {"src/" + module})
    root = MODULE.ET.parse(xml).getroot()
    assert {
        name: root.get(name)
        for name in (
            "lines-covered",
            "lines-valid",
            "line-rate",
            "branches-covered",
            "branches-valid",
            "branch-rate",
        )
    } == {
        "lines-covered": "1",
        "lines-valid": "2",
        "line-rate": "0.5000",
        "branches-covered": "1",
        "branches-valid": "2",
        "branch-rate": "0.5000",
    }
    package = root.find("./packages/package")
    assert package.get("line-rate") == package.get("branch-rate") == "0.5000"
    assert package.get("complexity") == "2"
    MODULE.summary_evidence.validate_coverage(report, xml, {"src/" + module})


@pytest.mark.parametrize("condition_type", ["jump", "switch", MARKER])
def test_coverlet_condition_types_are_closed(condition_type, test_area_tmp_path):
    report, xml = (
        test_area_tmp_path / "coverage.json",
        test_area_tmp_path / "coverage.cobertura.xml",
    )
    report.write_text("{}")
    xml.write_text(
        '<coverage><packages><package name="NvtFwCombiner.Domain"><classes>'
        '<class name="Product.Code" filename="src/NvtFwCombiner.Domain/Code.cs">'
        '<lines><line number="1" hits="1" branch="true" condition-coverage="100% (1/1)">'
        f'<conditions><condition number="0" type="{condition_type}" coverage="100%" /></conditions>'
        "</line></lines></class></classes></package></packages></coverage>"
    )
    if condition_type == MARKER:
        with pytest.raises(
            RuntimeError, match="^Public coverage evidence is invalid\\.$"
        ):
            MODULE.summary_evidence.validate_coverage(
                report, xml, {"src/NvtFwCombiner.Domain"}
            )
    else:
        MODULE.summary_evidence.validate_coverage(
            report, xml, {"src/NvtFwCombiner.Domain"}
        )


@pytest.mark.parametrize(
    "cardinality", [None, "50%", "50% (2/1)", "0% (0/0)", "50% (1/2)"]
)
@pytest.mark.parametrize("operation", ["validate_coverage", "project_coverage"])
def test_retained_branch_cardinality_uses_existing_policy_before_publication(
    cardinality,
    operation,
    test_area_tmp_path,
):
    report = test_area_tmp_path / "coverage.json"
    xml = test_area_tmp_path / "coverage.cobertura.xml"
    report.write_text("{}")
    xml.write_text(
        '<coverage><packages><package name="NvtFwCombiner.Domain" branch-rate="0.5">'
        '<classes><class name="Product.Code" filename="src/NvtFwCombiner.Domain/Code.cs">'
        '<lines><line number="1" hits="1" branch="true" /></lines>'
        "</class></classes></package></packages></coverage>"
    )
    if cardinality is not None:
        tree = MODULE.ET.parse(xml)
        tree.find(".//line").set("condition-coverage", cardinality)
        tree.write(xml, encoding="utf-8")
    validate = getattr(MODULE.summary_evidence, operation)
    if cardinality == "50% (1/2)":
        validate(report, xml, {"src/NvtFwCombiner.Domain"})
        if operation == "project_coverage":
            assert (
                MODULE.ET.parse(xml).find("./packages/package").get("branch-rate")
                == "0.5000"
            )
    else:
        with pytest.raises(
            RuntimeError, match="^Public coverage evidence is invalid\\.$"
        ):
            validate(report, xml, {"src/NvtFwCombiner.Domain"})


def test_public_coverage_validation_failure_prints_fixed_project_diagnosis():
    with (
        patch.object(
            MODULE.summary_evidence,
            "validate_coverage",
            side_effect=RuntimeError(MARKER),
        ),
        retry_fixtures.CiDotnetRetryTests().shard("green") as result,
    ):
        root, project, _, _, _, error = result
        assert error is not None
        sentence = f"{project.name}: coverage refused."
        assert sentence in (root / "job.log").read_text()
        assert sentence in (root / "summary.md").read_text()
        assert (
            MARKER
            not in str(error)
            + (root / "job.log").read_text()
            + (root / "summary.md").read_text()
        )


def test_rewritten_trx_copies_stay_inside_runner_evidence_root():
    destinations = []
    original = MODULE.ET.ElementTree.write

    def tracked_write(tree, destination, *args, **kwargs):
        if any(part.startswith("nfc-ci-summary-") for part in Path(destination).parts):
            destinations.append(Path(destination))
        return original(tree, destination, *args, **kwargs)

    with (
        patch.object(MODULE.ET.ElementTree, "write", tracked_write),
        retry_fixtures.CiDotnetRetryTests().shard("recover") as result,
    ):
        root, _, _, _, _, error = result
        assert error is None
        assert len(destinations) == 2
        assert all(
            path.is_relative_to(root / "work/shards/core") for path in destinations
        )
        assert all(not path.exists() for path in destinations)


def test_unsafe_recovered_method_keeps_original_producer_failure():
    unsafe = f"Probe.Tests.Generic`1.{MARKER}"
    with (
        patch.object(MODULE, "require_ci_retry_results", return_value=(unsafe,)),
        retry_fixtures.CiDotnetRetryTests().shard("partial-recovery") as result,
    ):
        root, project, _, _, manifest, error = result
        assert f"{project.name}: project failed." in str(error)
        assert "flaky reporting also failed" not in str(error)
        assert not manifest["success"]
        assert (
            MARKER
            not in str(error)
            + (root / "job.log").read_text()
            + (root / "summary.md").read_text()
        )
        assert MARKER in (root / "work/shards/core/manifest.json").read_text()
        for path in (root / "upload").rglob("*"):
            if path.is_file():
                assert MARKER not in path.read_text()


def test_projection_keeps_the_production_aggregate_of_the_raw_pair(test_area_tmp_path):
    from scripts import coverage_policy

    source = Path(__file__).parent / "fixtures/infrastructure-coverage"
    root = Path(__file__).resolve().parents[2]
    raw = coverage_policy.parse_dotnet_cobertura_reports(source, root).as_document()
    report = test_area_tmp_path / "coverage.json"
    xml = test_area_tmp_path / "coverage.cobertura.xml"
    shutil.copyfile(source / report.name, report)
    shutil.copyfile(source / xml.name, xml)
    MODULE.normalize_ci_dotnet_coverage_reports(report, xml)
    directories = {
        path.as_posix()
        for roots in MODULE.PRODUCTION_MODULES.values()
        for path in roots
    }
    MODULE.summary_evidence.project_coverage(report, xml, directories)
    MODULE.summary_evidence.validate_coverage(report, xml, directories)
    projected = coverage_policy.parse_dotnet_cobertura_reports(
        test_area_tmp_path, root
    ).as_document()
    assert projected == raw
    assert raw["overall"]["lines"]["total"] > 0


def test_unsafe_flaky_name_without_open_bug_record_fails_the_shard():
    unsafe = "Probe.Tests.Generic`1.Method"
    with (
        patch.object(MODULE, "require_ci_retry_results", return_value=(unsafe,)),
        retry_fixtures.CiDotnetRetryTests().shard("recover") as result,
    ):
        root, _, _, _, _, error = result
        assert error is not None
        assert "Flaky test has no open bug record." in str(error)
        assert "Generic" not in str(error) + (root / "job.log").read_text()


def test_unsafe_flaky_name_with_open_bug_record_still_fails_closed():
    # The public name list is empty while a retry recovered a test, so the closed
    # receipt is refused. The bug-record gate itself no longer fires.
    unsafe = "Probe.Tests.Generic`1.Method"
    with (
        patch.object(MODULE, "require_ci_retry_results", return_value=(unsafe,)),
        retry_fixtures.CiDotnetRetryTests().shard(
            "recover", bug_text=f"Status: open\n{unsafe}\n"
        ) as result,
    ):
        _, _, _, _, _, error = result
        assert error is not None
        assert "Flaky test has no open bug record." not in str(error)


@pytest.mark.parametrize("code", [None, 0, 7])
def test_system_exit_keeps_a_numeric_exit_code(code):
    error = SystemExit(code)
    assert MODULE.ci_public_shard_failure(None, error, "Shard execution failed.") is error
    assert error.code == code


def test_system_exit_text_code_is_replaced_by_the_fixed_sentence():
    error = SystemExit(MARKER)
    MODULE.ci_public_shard_failure(None, error, "Shard execution failed.")
    assert error.code == "Shard execution failed."
    assert MARKER not in str(error.args) + str(error.code)
