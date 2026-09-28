"""Behavioral tests for the release startup-measurement contract."""

from __future__ import annotations

import json
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MEASUREMENT_SCRIPT = ROOT / "scripts" / "measure-startup.ps1"
FIXTURES = ROOT / "testdata" / "startup-measurement"
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")
ANSI_ESCAPE = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


def normalize_console_output(value: str) -> str:
    return " ".join(ANSI_ESCAPE.sub("", value).replace("|", " ").split())


@unittest.skipUnless(POWERSHELL, "PowerShell is required")
class StartupMeasurementContractTests(unittest.TestCase):
    def admission_trace(self, source="prebuilt"):
        trace = json.loads((FIXTURES / "trace-v3.json").read_text(encoding="utf-8"))
        marker = dict(trace["stages"][4])
        marker.update(name="startup-warmup.catalog-admission." + source,
                      deltaMilliseconds=0, allocationDeltaBytes=0)
        trace["stages"].insert(5, marker)
        return trace

    def run_admission_sample(self, trace, required="prebuilt"):
        with tempfile.TemporaryDirectory() as temp:
            fixture = Path(temp) / "trace.json"
            fixture.write_text(json.dumps(trace), encoding="utf-8")
            command = self.sample_command(fixture, required=False).replace(
                "-Trace $trace", f"-Trace $trace -RequiredAdmissionSource '{required}'")
            return self.run_contract(command)

    def test_admission_requirement_records_both_sources_and_implies_lifecycle(self):
        for source in ("prebuilt", "json"):
            with self.subTest(source=source):
                result = self.run_admission_sample(self.admission_trace(source), source)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                sample = json.loads(result.stdout)
                self.assertEqual(source, sample["requiredAdmissionSource"])
                self.assertEqual(source, sample["observedAdmissionSource"])
                self.assertTrue(sample["admissionSourceRequirementPassed"])
                self.assertEqual(1, sample["catalogReadyAfterWindowMilliseconds"])

    def test_admission_requirement_rejects_unproven_or_invalid_samples(self):
        mutations = {
            "absent": lambda t: t["stages"].pop(5),
            "unknown": lambda t: t["stages"][5].update(name="startup-warmup.catalog-admission.unknown"),
            "mismatch": lambda t: t["stages"][5].update(name="startup-warmup.catalog-admission.json"),
            "duplicate": lambda t: t["stages"].insert(5, t["stages"][5].copy()),
            "mixed": lambda t: t["stages"].insert(5, dict(t["stages"][5], name="startup-warmup.catalog-admission.json")),
            "wrong-process": lambda t: t.update(processId=8),
            "invalid-time": lambda t: t["stages"][5].update(elapsedMilliseconds="NaN"),
            "reversed-time": lambda t: t["stages"][5].update(elapsedMilliseconds=7, deltaMilliseconds=-1),
            "after-catalog": lambda t: t["stages"].insert(7, t["stages"].pop(5)),
            "failed-terminal": lambda t: t["stages"][-1].update(name="startup-warmup.failed"),
            "failed-lifecycle": lambda t: t["preloadStages"][0].update(state="Failed"),
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name):
                trace = self.admission_trace()
                mutate(trace)
                result = self.run_admission_sample(trace)
                self.assertNotEqual(0, result.returncode, name + result.stdout + result.stderr)
        for source in ("PREBUILT", "unknown"):
            result = self.run_admission_sample(self.admission_trace(), source)
            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)

    def test_admission_requirement_is_enforced_on_warmups_before_scored_launches(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            executable = root / "fixture.exe"
            executable.write_bytes(b"not executed")
            trace = root / "warmup.json"
            trace.write_text(json.dumps(self.admission_trace("json")), encoding="utf-8")
            script = str(MEASUREMENT_SCRIPT).replace("'", "''")
            body = f"""
$ApplicationPath = '{str(executable).replace("'", "''")}'
$OutputPath = '{str(root / 'result.json').replace("'", "''")}'
$RequireAdmissionSource = 'prebuilt'
$RequirePreloadLifecycle = [switch]$false
$WarmupRuns = 1
$Runs = 5
$Page = 'home'
$TimeoutSeconds = 5
$script:launches = 0
function Invoke-StartupSample {{
    param($Executable, $TracePath, $StartupPage, $Timeout, $RequireLifecycle, $RequiredAdmissionSource)
    $script:launches++
    $trace = Get-Content -Raw -LiteralPath '{str(trace).replace("'", "''")}' | ConvertFrom-Json
    if ($script:launches -gt 1) {{ $trace.stages[5].name = 'startup-warmup.catalog-admission.prebuilt' }}
    New-StartupSampleEvidence -Trace $trace -RequireLifecycle $RequireLifecycle -RequiredAdmissionSource $RequiredAdmissionSource `
        -ProcessId 7 -WindowMilliseconds 1 -TraceReadyMilliseconds 30 `
        -WorkingSetBytesAtWindow 1 -WorkingSetBytesAtTrace 1 -PeakWorkingSetBytes 1 `
        -PrivateBytesAtWindow 1 -PrivateBytesAtTrace 1 -PeakPrivateBytes 1
}}
$source = Get-Content -Raw -LiteralPath '{script}'
$entry = $source.Substring($source.IndexOf('$requireReleaseEvidence ='))
try {{ & ([scriptblock]::Create($entry)); throw 'Mismatched warmup was accepted.' }}
catch {{
    if ($_.Exception.Message -notlike '*exactly one matching admission source marker*' -or $script:launches -ne 1) {{ throw }}
}}
Write-Output 'Warmup mismatch rejected before scored launches.'
"""
            result = self.run_contract(body)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertFalse((root / "result.json").exists())

    def test_admission_source_option_keeps_release_count_and_page_guards(self):
        for extra, message in ((["-WarmupRuns", "0"], "at least one warm-up"),
                               (["-Page", "settings"], "exact lowercase 'home'"),
                               (["-RequireAdmissionSource", "PREBUILT"], "")):
            command = [str(POWERSHELL), "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
                       str(MEASUREMENT_SCRIPT), "-ApplicationPath", "must-not-launch.exe"]
            if "-RequireAdmissionSource" not in extra:
                command.extend(["-RequireAdmissionSource", "prebuilt"])
            result = subprocess.run(command + extra, cwd=ROOT, capture_output=True, text=True,
                                    encoding="utf-8", errors="replace", timeout=30)
            self.assertNotEqual(0, result.returncode)
            if message:
                self.assertIn(message, normalize_console_output(result.stdout + result.stderr))

    def run_contract(self, body: str) -> subprocess.CompletedProcess[str]:
        script_path = str(MEASUREMENT_SCRIPT).replace("'", "''")
        command = f". '{script_path}' -ApplicationPath 'fixture.exe'\n{body}"
        return subprocess.run(
            [
                str(POWERSHELL),
                "-NoLogo",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                command,
            ],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )

    def sample_command(self, fixture: Path, required: bool) -> str:
        fixture_path = str(fixture).replace("'", "''")
        required_literal = "$true" if required else "$false"
        return f"""
$trace = Get-Content -LiteralPath '{fixture_path}' -Raw | ConvertFrom-Json
$result = New-StartupSampleEvidence `
    -Trace $trace -RequireLifecycle {required_literal} -ProcessId 7 `
    -WindowMilliseconds 11.125 -TraceReadyMilliseconds 22.25 `
    -WorkingSetBytesAtWindow 101 -WorkingSetBytesAtTrace 202 -PeakWorkingSetBytes 303 `
    -PrivateBytesAtWindow 404 -PrivateBytesAtTrace 505 -PeakPrivateBytes 606
$result | ConvertTo-Json -Depth 12 -Compress
"""

    def test_release_mode_requires_one_warmup_and_five_scored_runs(self) -> None:
        valid = self.run_contract(
            "Assert-ReleaseSampleCounts -Required $true -Warmups 1 -ScoredRuns 5"
        )
        self.assertEqual(0, valid.returncode, valid.stdout + valid.stderr)

        for warmups, scored_runs in ((0, 5), (1, 4)):
            with self.subTest(warmups=warmups, scored_runs=scored_runs):
                invalid = self.run_contract(
                    "Assert-ReleaseSampleCounts "
                    f"-Required $true -Warmups {warmups} -ScoredRuns {scored_runs}"
                )
                self.assertNotEqual(0, invalid.returncode)
                self.assertIn(
                    "at least one warm-up and five scored launches",
                    normalize_console_output(invalid.stdout + invalid.stderr),
                )

    def test_release_mode_accepts_only_the_exact_home_workload(self) -> None:
        valid = self.run_contract(
            "Assert-ReleaseStartupPage -Required $true -StartupPage 'home'"
        )
        self.assertEqual(0, valid.returncode, valid.stdout + valid.stderr)

        for page in ("settings", "merge", "replace", "hex-editor", "HOME"):
            with self.subTest(page=page):
                invalid = self.run_contract(
                    f"Assert-ReleaseStartupPage -Required $true -StartupPage '{page}'"
                )
                self.assertNotEqual(0, invalid.returncode)
                self.assertIn(
                    "exact lowercase 'home' startup page",
                    normalize_console_output(invalid.stdout + invalid.stderr),
                )

    def test_powershell_error_rendering_is_normalized(self) -> None:
        rendered = "\x1b[31;1mfive scored\x1b[0m\n\x1b[31;1m | launches\x1b[0m"
        self.assertEqual("five scored launches", normalize_console_output(rendered))

    def test_v2_and_complete_v3_fixtures_use_the_same_sample_projection(self) -> None:
        predecessor = self.run_contract(
            self.sample_command(FIXTURES / "trace-v2.json", required=False)
        )
        candidate = self.run_contract(
            self.sample_command(FIXTURES / "trace-v3.json", required=True)
        )

        self.assertEqual(
            0, predecessor.returncode, predecessor.stdout + predecessor.stderr
        )
        self.assertEqual(0, candidate.returncode, candidate.stdout + candidate.stderr)
        predecessor_sample = json.loads(predecessor.stdout)
        candidate_sample = json.loads(candidate.stdout)
        self.assertIsNone(predecessor_sample["preloadLifecycle"])
        self.assertEqual(5, candidate_sample["preloadLifecycle"]["stageCount"])
        self.assertEqual(7, candidate_sample["processId"])
        self.assertEqual(11.125, candidate_sample["processToWindowMilliseconds"])
        self.assertEqual(303, candidate_sample["peakWorkingSetBytes"])
        self.assertEqual(606, candidate_sample["peakPrivateBytes"])
        self.assertEqual(
            5, candidate_sample["uiThreadWork"]["firstFrame"]["totalMilliseconds"]
        )
        self.assertEqual(
            5, candidate_sample["uiThreadWork"]["background"]["totalMilliseconds"]
        )
        self.assertEqual(1, candidate_sample["catalogReadyAfterWindowMilliseconds"])

    def test_release_lifecycle_rejects_missing_nonterminal_and_invalid_work(
        self,
    ) -> None:
        source = json.loads((FIXTURES / "trace-v3.json").read_text(encoding="utf-8"))
        variants = {
            "missing-stage": lambda trace: trace["preloadStages"].pop(3),
            "mis-cased-schema": lambda trace: trace.update(
                schemaVersion="NFC-STARTUP-TRACE-V3"
            ),
            "mis-cased-stage": lambda trace: trace["preloadStages"][0].update(
                id="CANONICAL-CATALOG"
            ),
            "joined-stage-impersonation": lambda trace: (
                trace["preloadStages"][0].update(id="canonical-catalog|report-history"),
                trace["preloadStages"].pop(1),
            ),
            "nonterminal-stage": lambda trace: trace["preloadStages"][2].update(
                state="Running"
            ),
            "mis-cased-state": lambda trace: trace["preloadStages"][2].update(
                state="succeeded"
            ),
            "incomplete-success": lambda trace: trace["preloadStages"][4].update(
                completedWork=4
            ),
            "fractional-work": lambda trace: trace["preloadStages"][4].update(
                completedWork=4.6
            ),
            "string-work": lambda trace: trace["preloadStages"][4].update(
                completedWork="5"
            ),
            "duplicate-stage": lambda trace: trace["preloadStages"].insert(
                1, trace["preloadStages"][0].copy()
            ),
            "extra-startup-report": lambda trace: trace["preloadStages"].insert(
                2,
                {
                    "id": "startup-report",
                    "state": "Succeeded",
                    "completedWork": None,
                    "totalWork": None,
                },
            ),
            "failed-stage": lambda trace: trace["preloadStages"][1].update(
                state="Failed"
            ),
            "skipped-stage": lambda trace: trace["preloadStages"][1].update(
                state="Skipped"
            ),
            "cancelled-stage": lambda trace: trace["preloadStages"][1].update(
                state="Cancelled"
            ),
            "dependency-blocked-stage": lambda trace: trace["preloadStages"][1].update(
                state="DependencyBlocked"
            ),
            "missing-deferred-work": lambda trace: trace["preloadStages"][4].update(
                completedWork=None, totalWork=None
            ),
            "zero-deferred-work": lambda trace: trace["preloadStages"][4].update(
                completedWork=0, totalWork=0
            ),
            "incorrect-deferred-work": lambda trace: trace["preloadStages"][4].update(
                completedWork=4, totalWork=4
            ),
            "missing-catalog-ready": lambda trace: trace["stages"].__setitem__(
                slice(None),
                [
                    stage
                    for stage in trace["stages"]
                    if stage["name"] != "startup-warmup.catalog-state.applied"
                ],
            ),
            "missing-deferred-pair": lambda trace: trace["stages"].__setitem__(
                slice(None),
                [
                    stage
                    for stage in trace["stages"]
                    if not stage["name"].startswith("startup-warmup.settings-view.")
                ],
            ),
            "duplicate-opened": lambda trace: trace["stages"].insert(
                5, trace["stages"][4].copy()
            ),
            "duplicate-completed": lambda trace: trace["stages"].append(
                trace["stages"][-1].copy()
            ),
            "duplicate-ordinary-stage": lambda trace: trace["stages"].insert(
                2, trace["stages"][1].copy()
            ),
            "null-ordinary-elapsed": lambda trace: trace["stages"][1].update(
                elapsedMilliseconds=None
            ),
            "string-ordinary-delta": lambda trace: trace["stages"][1].update(
                deltaMilliseconds="2"
            ),
            "inconsistent-ordinary-delta": lambda trace: trace["stages"][1].update(
                deltaMilliseconds=1
            ),
            "fractional-allocation": lambda trace: trace["stages"][1].update(
                allocatedBytesSinceManagedEntry=20.5
            ),
            "negative-allocation-delta": lambda trace: trace["stages"][1].update(
                allocationDeltaBytes=-1
            ),
            "inconsistent-allocation-delta": lambda trace: trace["stages"][1].update(
                allocationDeltaBytes=9
            ),
            "decreasing-allocation": lambda trace: trace["stages"][1].update(
                allocatedBytesSinceManagedEntry=5, allocationDeltaBytes=0
            ),
            "missing-process-id": lambda trace: trace.pop("processId"),
            "wrong-process-id": lambda trace: trace.update(processId=8),
            "string-process-id": lambda trace: trace.update(processId="7"),
            "conflicting-failed-terminal": lambda trace: trace["stages"].append(
                {
                    **trace["stages"][-1],
                    "name": "startup-warmup.failed",
                    "elapsedMilliseconds": 21,
                }
            ),
            "conflicting-cancelled-terminal": lambda trace: trace["stages"].append(
                {
                    **trace["stages"][-1],
                    "name": "startup-warmup.cancelled",
                    "elapsedMilliseconds": 21,
                }
            ),
            "completed-is-not-final": lambda trace: trace["stages"].append(
                {
                    **trace["stages"][-1],
                    "name": "startup-warmup.late",
                    "elapsedMilliseconds": 21,
                }
            ),
            "null-opened": lambda trace: trace["stages"][4].update(
                elapsedMilliseconds=None
            ),
            "boolean-catalog-ready": lambda trace: trace["stages"][5].update(
                elapsedMilliseconds=True
            ),
            "string-completed": lambda trace: trace["stages"][-1].update(
                elapsedMilliseconds="20"
            ),
            "overlapping-deferred-intervals": lambda trace: trace["stages"][8].update(
                elapsedMilliseconds=10.5
            ),
            "out-of-order-deferred-ready": lambda trace: trace["stages"].insert(
                9, trace["stages"].pop(7)
            ),
            "deferred-before-catalog-ready": lambda trace: trace["stages"][6].update(
                elapsedMilliseconds=8.5
            ),
            "deferred-after-completion": lambda trace: trace["stages"][15].update(
                elapsedMilliseconds=20.5
            ),
            "nonfinite-catalog-ready": lambda trace: trace["stages"][5].update(
                elapsedMilliseconds="NaN"
            ),
            "negative-opened": lambda trace: trace["stages"][4].update(
                elapsedMilliseconds=-1
            ),
            "reversed-catalog-ready": lambda trace: trace["stages"][5].update(
                elapsedMilliseconds=7
            ),
            "nonfinite-deferred-ready": lambda trace: trace["stages"][7].update(
                elapsedMilliseconds="Infinity"
            ),
        }
        with tempfile.TemporaryDirectory(prefix="nfc-startup-fixture-") as temporary:
            temporary_path = Path(temporary)
            for name, mutate in variants.items():
                with self.subTest(name=name):
                    trace = json.loads(json.dumps(source))
                    mutate(trace)
                    fixture = temporary_path / f"{name}.json"
                    fixture.write_text(json.dumps(trace), encoding="utf-8")
                    result = self.run_contract(
                        self.sample_command(fixture, required=True)
                    )
                    self.assertNotEqual(0, result.returncode)

    def test_predecessor_trace_cannot_satisfy_required_lifecycle_mode(self) -> None:
        result = self.run_contract(
            self.sample_command(FIXTURES / "trace-v2.json", required=True)
        )

        self.assertNotEqual(0, result.returncode)
        self.assertIn(
            "required preload lifecycle evidence", result.stdout + result.stderr
        )

    def test_measurement_output_records_whether_release_admission_ran(self) -> None:
        standard = self.run_contract(
            "New-StartupMeasurementValidation -Required $false | ConvertTo-Json -Compress"
        )
        release = self.run_contract(
            "New-StartupMeasurementValidation -Required $true | ConvertTo-Json -Compress"
        )

        self.assertEqual(0, standard.returncode, standard.stdout + standard.stderr)
        self.assertEqual(0, release.returncode, release.stdout + release.stderr)
        self.assertEqual(
            {"mode": "standard", "releaseAdmissionPassed": False,
             "requiredAdmissionSource": "", "admissionSourceRequirementPassed": False},
            json.loads(standard.stdout),
        )
        self.assertEqual(
            {"mode": "preload-release", "releaseAdmissionPassed": True,
             "requiredAdmissionSource": "", "admissionSourceRequirementPassed": False},
            json.loads(release.stdout),
        )
        self.assertIn(
            "validation = New-StartupMeasurementValidation",
            MEASUREMENT_SCRIPT.read_text(encoding="utf-8"),
        )


if __name__ == "__main__":
    unittest.main()
