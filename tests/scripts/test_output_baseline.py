"""Deterministic route inventory, failure isolation and output comparison."""

from __future__ import annotations

import contextlib
import hashlib
import io
import json
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

from scripts import output_baseline as baseline


class OutputBaselineTests(unittest.TestCase):
    def setUp(self) -> None:
        self.routes = [{"routeId": name, "icId": "NT51926", "workflowId": "standard-merge"}
                       for name in ("route-c", "route-a", "route-b")]
        self.input = {"name": "dp-input", "path": "testdata/example.bin", "sha256": "1" * 64}
        self.output = {"name": "output.bin", "sha256": hashlib.sha256(b"output").hexdigest(), "size": 6}

    def prepare(self, route, inputs):
        inputs.append(dict(self.input))
        return {"arguments": ["standard-merge", "build"]}, []

    def generate(self, prepare=None, execute=None):
        return baseline.collect(self.routes, prepare or self.prepare,
                                execute or (lambda *_: {"outputs": [dict(self.output)]}), "a" * 40, "1.3.0-g.0")

    def test_records_and_header_have_stable_order_and_complete_hashes(self):
        result = self.generate()
        self.assertEqual(list(result), ["header", "routes"])
        self.assertEqual(result["header"], {
            "schema_version": 1, "commit": "a" * 40, "tool_version": baseline.TOOL_VERSION,
            "product_version": "1.3.0-g.0", "route_count": 3,
            "counts": {"ok": 3, "input-unavailable": 0, "failed": 0},
        })
        self.assertEqual([r["route_id"] for r in result["routes"]], ["route-a", "route-b", "route-c"])
        self.assertEqual(result["routes"][0], {
            "route_id": "route-a", "status": "ok", "inputs": [self.input], "outputs": [self.output],
        })

    def test_same_fixture_twice_produces_identical_utf8_lf_bytes(self):
        first = baseline.encode(self.generate())
        self.routes.reverse()
        second = baseline.encode(self.generate())
        self.assertEqual(first, second)
        self.assertEqual(json.loads(first), self.generate())
        self.assertNotIn(b"\r", first)
        self.assertTrue(first.endswith(b"\n"))

    def test_missing_and_failed_routes_do_not_stop_the_next_route(self):
        def prepare(route, inputs):
            if route["routeId"] == "route-a":
                return [], ["tp-input"]
            return self.prepare(route, inputs)

        def execute(route, *_):
            if route["routeId"] == "route-b":
                raise RuntimeError("first error\nsecond error")
            return {"outputs": [self.output]}

        result = self.generate(prepare, execute)
        missing, failed, ok = result["routes"]
        self.assertEqual(missing["missing_inputs"], ["tp-input"])
        self.assertEqual(missing["status"], "input-unavailable")
        self.assertEqual(failed["error"], "first error")
        self.assertEqual(failed["status"], "failed")
        self.assertEqual(ok["status"], "ok")
        self.assertEqual(result["header"]["counts"], {"ok": 1, "input-unavailable": 1, "failed": 1})

    def test_compare_reports_changed_added_and_removed_routes(self):
        before = self.generate()
        after = json.loads(baseline.encode(before))
        after["routes"][0]["outputs"][0]["sha256"] = "2" * 64
        after["routes"].pop(1)
        after["routes"].append({"route_id": "route-d", "status": "ok", "inputs": [], "outputs": []})
        with contextlib.redirect_stdout(io.StringIO()) as output:
            code = baseline.compare(before, after)
        self.assertEqual(code, 1)
        self.assertEqual(output.getvalue().splitlines(), ["changed route-a", "removed route-b", "added route-d"])
        self.assertEqual(baseline.compare(before, before), 0)

    def test_compare_ignores_commit_and_input_metadata_but_detects_status_changes(self):
        before = self.generate()
        after = json.loads(baseline.encode(before))
        after["header"]["commit"] = "b" * 40
        after["routes"][0]["inputs"] = []
        self.assertEqual(baseline.compare(before, after), 0)
        after["routes"][0]["status"] = "failed"
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(baseline.compare(before, after), 1)

    def test_duplicate_route_ids_are_rejected(self):
        self.routes.append(self.routes[0])
        with self.assertRaisesRegex(ValueError, "Duplicate route"):
            self.generate()

    def test_error_redacts_host_paths_and_keeps_only_first_line(self):
        self.assertEqual(baseline.first_error("error: cannot read 'C:\\Users\\alice\\file.bin'\nstack"),
                         "error: cannot read '<path>'")
        self.assertEqual(baseline.first_error(PermissionError(13, "Permission denied", "/private/user/file")),
                         "Permission denied")

    def test_inventory_uses_every_declared_matrix_route_without_support_filter(self):
        routes = baseline.load_routes()
        policy = json.loads((baseline.REPO / baseline.POLICY).read_bytes())
        self.assertEqual(routes, sorted(policy["routes"], key=lambda r: r["routeId"]))
        self.assertEqual(len(routes), 94)
        self.assertEqual(len({r["routeId"] for r in routes}), 94)

    def test_canonical_standard_inputs_are_bound_by_route_evidence(self):
        catalog = baseline.CanonicalInputs()
        route = next(r for r in baseline.load_routes()
                     if r["icId"] == "NT51926" and r["workflowId"] == "standard-merge")
        inputs = []
        request, missing = catalog.prepare(route, inputs)
        self.assertEqual(missing, [])
        self.assertEqual(request["arguments"][:4], ["standard-merge", "build", "--profile", "NT51926"])
        self.assertEqual([r["name"] for r in inputs], ["dp-input", "tp-input"])
        for row in inputs:
            self.assertTrue(row["path"].startswith("testdata/"))
            self.assertEqual(row["sha256"], hashlib.sha256((baseline.REPO / row["path"]).read_bytes()).hexdigest())

    def test_missing_declared_file_is_unavailable_and_hash_mismatch_is_failed(self):
        catalog = baseline.CanonicalInputs()
        route = next(r for r in baseline.load_routes()
                     if r["icId"] == "NT51926" and r["workflowId"] == "standard-merge")
        with mock.patch.object(baseline.examples, "artifact", side_effect=FileNotFoundError("missing")):
            _, missing = catalog.prepare(route, [])
            self.assertEqual(missing, ["dp-input", "tp-input"])
        with mock.patch.object(baseline.examples, "artifact", side_effect=ValueError("hash mismatch")):
            result = baseline.collect([route], catalog.prepare, mock.Mock(), "a" * 40, "version")
            self.assertEqual(result["routes"][0]["status"], "failed")

    def test_executor_checks_actual_route_and_hashes_every_output_file(self):
        route = {"routeId": "route-target", "capabilityFingerprint": "a" * 64}
        report = {"Capability": {"RouteId": route["routeId"],
                                 "CapabilityFingerprint": route["capabilityFingerprint"]},
                  "Output": {"Committed": True, "FileName": self.output["name"],
                             "Sha256": self.output["sha256"], "Size": self.output["size"]}}
        output_file = mock.Mock()
        output_file.open.return_value = contextlib.nullcontext(io.BytesIO(b"output"))
        output_file.relative_to.return_value = Path("output.bin")
        output_file.stat.return_value = SimpleNamespace(st_size=6)
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        with (mock.patch.object(baseline.tempfile, "TemporaryDirectory",
                                return_value=contextlib.nullcontext(str(baseline.REPO.parent / "fake-work"))),
              mock.patch.object(Path, "mkdir"),
              mock.patch.object(Path, "rmdir"),
              mock.patch.object(Path, "read_bytes", return_value=json.dumps(report).encode()),
              mock.patch.object(Path, "rglob", return_value=[output_file]),
              mock.patch.object(baseline.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as run):
            request = {"arguments": ["standard-merge", "build"]}
            self.assertEqual(executor(route, request, []), {"outputs": [self.output]})
            command = run.call_args.args[0]
            self.assertEqual(command[:4], ["dotnet", str(executor.cli), "standard-merge", "build"])
            self.assertIn("--report", command)
            self.assertEqual(run.call_args.kwargs["env"]["TMPDIR"], str(executor.temp))
            report["Capability"]["RouteId"] = "another-route"
            with mock.patch.object(Path, "read_bytes", return_value=json.dumps(report).encode()):
                with self.assertRaisesRegex(ValueError, "different route"):
                    executor(route, request, [])

    def test_denied_test_area_fails_before_tempfile_can_retry_windows_names(self):
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        with (mock.patch.object(Path, "mkdir", side_effect=PermissionError(13, "Permission denied")),
              mock.patch.object(baseline.tempfile, "TemporaryDirectory") as temporary):
            with self.assertRaises(PermissionError):
                executor({}, [], [])
            temporary.assert_not_called()

    def capture_ctrlram(self, choices, variant="2-plus-ic", count=2, profile="NT51926"):
        catalog = baseline.CanonicalInputs()
        catalog.number_choices = mock.Mock(return_value=tuple(choices))
        route = {"routeId": "ctrlram-fixture", "capabilityFingerprint": "f" * 64,
                 "icId": profile, "workflowId": "ctrlram-replace", "icCountVariant": variant}
        case = {"caseId": "ctrlram-case", "icCount": count, "artifacts": [
            {"artifactId": "reference-base", "sha256": "1" * 64},
            {"artifactId": "ctrlram-input", "sha256": "2" * 64, "slotId": "replace-ctrlram-nf"},
        ]}
        catalog.evidence = {route["routeId"]: {"caseId": case["caseId"],
                                              "capabilityFingerprint": route["capabilityFingerprint"]}}
        catalog.cases = {case["caseId"]: case}
        catalog.bases = {}
        execute = mock.Mock(return_value={"outputs": [self.output]})
        with mock.patch.object(baseline.examples, "artifact", return_value=baseline.REPO / "testdata/example.bin"):
            result = baseline.collect([route], catalog.prepare, execute, "a" * 40, "version")
        return result["routes"][0], execute

    def assert_ic_number(self, execute, expected):
        arguments = execute.call_args.args[1]["arguments"]
        self.assertEqual(arguments[arguments.index("--ic-num") + 1], expected)

    def test_multi_ic_ctrlram_uses_published_cascade_token(self):
        record, execute = self.capture_ctrlram(["cascade", "single"])
        self.assertEqual(record["status"], "ok")
        self.assert_ic_number(execute, "cascade")

    def test_ctrlram_range_profile_uses_its_published_cascade_token(self):
        record, execute = self.capture_ctrlram(["single", "cascade_2to8"], "2-8-ic", 3, "NT51932")
        self.assertEqual(record["status"], "ok")
        self.assert_ic_number(execute, "cascade_2to8")

    def test_single_ic_ctrlram_uses_published_single_token(self):
        record, execute = self.capture_ctrlram(["cascade", "single"], "1-ic", 1)
        self.assertEqual(record["status"], "ok")
        self.assert_ic_number(execute, "single")

    def test_ctrlram_prefers_published_numeric_token_matching_declared_count(self):
        for variant, count in [("1-ic", 1), ("2-ic", 2), ("3-ic", 3)]:
            with self.subTest(variant=variant):
                record, execute = self.capture_ctrlram(["single", "cascade", "1", "2", "3"], variant, count)
                self.assertEqual(record["status"], "ok")
                self.assert_ic_number(execute, str(count))

    def test_ambiguous_ctrlram_choices_fail_without_calling_executor(self):
        record, execute = self.capture_ctrlram(["single", "cascade_2to8", "cascade"])
        self.assertEqual(record["status"], "failed")
        self.assertIn("IC number", record["error"])
        self.assertIn("cascade, cascade_2to8, single", record["error"])
        execute.assert_not_called()
        reordered, execute = self.capture_ctrlram(["cascade", "single", "cascade_2to8"])
        self.assertEqual(record, reordered)
        execute.assert_not_called()

    def test_undeclared_ctrlram_selection_fails_without_calling_executor(self):
        scenarios = [([], "2-ic", 2), (["single"], "2-ic", 2),
                     (["cascade"], "1-ic", 1), (["single", "cascade"], "unknown", 2),
                     (["single", "cascade"], "2-ic", None)]
        for choices, variant, count in scenarios:
            with self.subTest(choices=choices, variant=variant, count=count):
                record, execute = self.capture_ctrlram(choices, variant, count)
                self.assertEqual(record["status"], "failed")
                self.assertIn("IC number", record["error"])
                execute.assert_not_called()

    def test_cli_number_choices_deduplicate_slot_rows_and_list_once_per_profile(self):
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        first = "\n".join(["profile=NT51926 ic-num=single", "profile=NT51926 ic-num=cascade",
                           "profile=NT51926 ic-num=cascade slot=replace-ctrlram-nf"])
        second = "\n".join(["profile=NT51932 ic-num=single", "profile=NT51932 ic-num=cascade_2to8"])
        with mock.patch.object(baseline.subprocess, "run", side_effect=[
                SimpleNamespace(returncode=0, stdout=first), SimpleNamespace(returncode=0, stdout=second)]) as run:
            for _ in range(2):
                self.assertEqual(executor.number_choices("NT51926"), ("cascade", "single"))
                self.assertEqual(executor.number_choices("NT51932"), ("cascade_2to8", "single"))
        self.assertEqual(run.call_count, 2)
        for call, profile in zip(run.call_args_list, ["NT51926", "NT51932"]):
            self.assertEqual(call.args[0], ["dotnet", str(executor.cli), "workflows", "list",
                                           "--workflow", "ctrlram-replace", "--profile", profile])
            self.assertEqual(call.kwargs["cwd"], baseline.REPO)
            for name in ("TEMP", "TMP", "TMPDIR"):
                self.assertEqual(call.kwargs["env"][name], str(executor.temp))

    def test_failed_or_invalid_cli_number_listing_is_cached_without_retry(self):
        results = [SimpleNamespace(returncode=1, stderr="listing failed\nsecond error"),
                   SimpleNamespace(returncode=0, stdout="profile=NT51926"),
                   SimpleNamespace(returncode=0, stdout="profile=NT51932 ic-num=cascade"),
                   SimpleNamespace(returncode=0, stdout="unexpected output"),
                   PermissionError(13, "Permission denied")]
        for result in results:
            with self.subTest(result=result):
                executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
                options = {"side_effect": result} if isinstance(result, Exception) else {"return_value": result}
                with mock.patch.object(baseline.subprocess, "run", **options) as run:
                    errors = []
                    for _ in range(2):
                        with self.assertRaisesRegex(ValueError, "Cannot list CtrlRAM IC number choices") as raised:
                            executor.number_choices("NT51926")
                        errors.append(str(raised.exception))
                self.assertEqual(errors[0], errors[1])
                self.assertNotIn("\n", errors[0])
                run.assert_called_once()

    def test_ctrlram_full_base_uses_declared_standard_build_and_original_dp_tp(self):
        catalog = baseline.CanonicalInputs(lambda _: ("cascade", "single"))
        route = next(r for r in baseline.load_routes()
                     if r["icId"] == "NT51926" and r["workflowId"] == "ctrlram-replace"
                     and r["mapVariant"] == "nt51926-ctrlram-fw141-full-flash-256k"
                     and r["icCountVariant"] == "1-ic")
        inputs = []
        request, missing = catalog.prepare(route, inputs)
        self.assertEqual(missing, [])
        self.assertIn("<generated-base>", request["arguments"])
        self.assertEqual(request["base_build"]["route"]["routeId"],
                         catalog.bases[route["routeId"]]["standardMergeRouteId"])
        self.assertEqual(request["base_build"]["arguments"][:4],
                         ["standard-merge", "build", "--profile", "NT51926"])
        self.assertTrue({"dp-input", "tp-input"}.issubset({row["name"] for row in inputs}))
        self.assertNotIn("expected-output", {row["name"] for row in inputs})

    def test_ctrlram_tp_base_uses_original_tp_without_a_standard_build(self):
        catalog = baseline.CanonicalInputs(lambda _: ("cascade", "single"))
        route = next(r for r in baseline.load_routes()
                     if r["icId"] == "NT51926" and r["workflowId"] == "ctrlram-replace"
                     and r["mapVariant"] == "nt51926-ctrlram-fw141-tp-work-240k"
                     and r["icCountVariant"] == "1-ic")
        inputs = []
        request, missing = catalog.prepare(route, inputs)
        self.assertEqual(missing, [])
        self.assertNotIn("base_build", request)
        self.assertIn("tp-input", {row["name"] for row in inputs})
        self.assertNotIn("expected-output", {row["name"] for row in inputs})

    def test_executor_builds_base_before_ctrlram_and_records_base_provenance(self):
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        base_route = {"routeId": "standard-route"}
        request = {"arguments": ["ctrlram-replace", "build", "--base", "<generated-base>"],
                   "base_build": {"route": base_route, "arguments": ["standard-merge", "build"]}}
        with (mock.patch.object(baseline.tempfile, "TemporaryDirectory",
                                return_value=contextlib.nullcontext(str(baseline.REPO.parent / "fake-work"))),
              mock.patch.object(Path, "mkdir"), mock.patch.object(Path, "rmdir"),
              mock.patch.object(executor, "build", return_value=[self.output]) as build):
            captured = executor({"routeId": "ctrlram-route"}, request, [])
        self.assertEqual(build.call_count, 2)
        self.assertEqual(build.call_args_list[0].args[0], base_route)
        self.assertEqual(build.call_args_list[1].args[0], {"routeId": "ctrlram-route"})
        self.assertNotIn("<generated-base>", build.call_args_list[1].args[1])
        self.assertEqual(Path(build.call_args_list[1].args[1][-1]),
                         baseline.REPO.parent / "fake-work/base/outputs/output.bin")
        self.assertEqual(captured["base_build"], {"route_id": "standard-route", "outputs": [self.output]})

    def test_subset_cannot_overwrite_default_full_inventory_baseline(self):
        with (mock.patch.object(Path, "write_bytes") as write,
              contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as raised):
            baseline.main(["run", "--route", "any-route"])
        self.assertEqual(raised.exception.code, 2)
        write.assert_not_called()

    def test_compare_command_returns_nonzero_for_changed_output(self):
        before = self.generate()
        after = json.loads(baseline.encode(before))
        after["routes"][0]["outputs"][0]["size"] += 1
        with (mock.patch.object(Path, "read_bytes", side_effect=[baseline.encode(before), baseline.encode(after)]),
              contextlib.redirect_stdout(io.StringIO()) as output):
            self.assertEqual(baseline.main(["compare", "old.json", "new.json"]), 1)
        self.assertEqual(output.getvalue(), "changed route-a\n")

    def test_input_only_missing_binding_is_unavailable_without_calling_executor(self):
        catalog = baseline.CanonicalInputs()
        route = next(r for r in baseline.load_routes() if r["workflowId"] == "general-merge")
        execute = mock.Mock()
        result = baseline.collect([route], catalog.prepare, execute, "a" * 40, "version")
        self.assertEqual(result["routes"][0]["status"], "input-unavailable")
        self.assertEqual(result["routes"][0]["missing_inputs"], ["saved-rule", "slot-inputs"])
        execute.assert_not_called()


if __name__ == "__main__":
    unittest.main()
