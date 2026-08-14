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
    return " ".join(ANSI_ESCAPE.sub("", value).split())


@unittest.skipUnless(POWERSHELL, "PowerShell is required")
class StartupMeasurementContractTests(unittest.TestCase):
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

    def test_powershell_error_rendering_is_normalized(self) -> None:
        rendered = "\x1b[31;1mfive scored\x1b[0m\n\x1b[31;1mlaunches\x1b[0m"
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
        self.assertEqual(11.125, candidate_sample["processToWindowMilliseconds"])
        self.assertEqual(303, candidate_sample["peakWorkingSetBytes"])
        self.assertEqual(606, candidate_sample["peakPrivateBytes"])
        self.assertEqual(
            5, candidate_sample["uiThreadWork"]["firstFrame"]["totalMilliseconds"]
        )
        self.assertEqual(
            3, candidate_sample["uiThreadWork"]["background"]["totalMilliseconds"]
        )

    def test_release_lifecycle_rejects_missing_nonterminal_and_invalid_work(
        self,
    ) -> None:
        source = json.loads((FIXTURES / "trace-v3.json").read_text(encoding="utf-8"))
        variants = {
            "missing-stage": lambda trace: trace["preloadStages"].pop(3),
            "nonterminal-stage": lambda trace: trace["preloadStages"][2].update(
                state="Running"
            ),
            "incomplete-success": lambda trace: trace["preloadStages"][4].update(
                completedWork=4
            ),
            "duplicate-stage": lambda trace: trace["preloadStages"].insert(
                1, trace["preloadStages"][0].copy()
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


if __name__ == "__main__":
    unittest.main()
