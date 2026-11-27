"""Deterministic route inventory, failure isolation and output comparison."""

from __future__ import annotations

import contextlib
import copy
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
        return {"arguments": ["standard-merge", "build"], "input_source": "route-evidence"}, []

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
            "input_kind_counts": {"native": 3, "derived": 0, "synthetic": 0, "private-native": 0},
        })
        self.assertEqual([r["route_id"] for r in result["routes"]], ["route-a", "route-b", "route-c"])
        self.assertEqual(result["routes"][0], {
            "route_id": "route-a", "status": "ok", "inputs": [self.input], "outputs": [self.output],
            "input_source": "route-evidence", "input_kind": "native",
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
        self.assertEqual(baseline.first_error("error: cannot read 'C:\\Users\\example\\file.bin'\nstack"),
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
            self.assertEqual(command[command.index("--output") + 1],
                             str(baseline.REPO.parent / "fake-work/final/outputs/output.bin"))
            self.assertEqual(run.call_args.kwargs["env"]["TMPDIR"], str(executor.temp))
            report["Capability"]["RouteId"] = "another-route"
            with mock.patch.object(Path, "read_bytes", return_value=json.dumps(report).encode()):
                with self.assertRaisesRegex(ValueError, "different route"):
                    executor(route, request, [])

    def test_forbidden_filename_override_uses_cli_name_and_records_it(self):
        routes = [r for r in baseline.load_routes()
                  if r["icId"] == "NT51925" and r["workflowId"] == "ctrlram-replace"
                  and r["icCountVariant"] == "1-ic"]
        self.assertEqual(len(routes), 4)
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        work = baseline.REPO.parent / "fake-work"
        actual = dict(self.output, name="nt51925-ctrlram-replace_UNVERIFIED.bin")
        output_file = mock.Mock()
        output_file.open.side_effect = lambda *_: io.BytesIO(b"output")
        output_file.relative_to.return_value = Path(actual["name"])
        output_file.stat.return_value = SimpleNamespace(st_size=6)
        for route in routes:
            report = {"Capability": {"RouteId": route["routeId"],
                                     "CapabilityFingerprint": route["capabilityFingerprint"]},
                      "Output": {"Committed": True, "FileName": actual["name"],
                                 "Sha256": actual["sha256"], "Size": actual["size"]}}
            with (self.subTest(route=route["routeId"]), mock.patch.object(Path, "mkdir"),
                  mock.patch.object(Path, "read_bytes", return_value=json.dumps(report).encode()),
                  mock.patch.object(Path, "rglob", return_value=[output_file]),
                  mock.patch.object(baseline.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as run):
                self.assertEqual(executor.build(route, ["ctrlram-replace", "build"], work), [actual])
                self.assertNotIn("--output", run.call_args.args[0])
                self.assertNotIn("output.bin", run.call_args.args[0])
                self.assertEqual(run.call_args.kwargs["cwd"], work / "outputs")

    def test_denied_test_area_fails_before_tempfile_can_retry_windows_names(self):
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        with (mock.patch.object(Path, "mkdir", side_effect=PermissionError(13, "Permission denied")),
              mock.patch.object(baseline.tempfile, "TemporaryDirectory") as temporary):
            with self.assertRaises(PermissionError):
                executor({}, [], [])
            temporary.assert_not_called()

    def test_other_executable_routes_keep_the_exact_explicit_output_command(self):
        recipes = baseline.load_recipes()
        routes = [r for r in baseline.load_routes()
                  if not recipes[r["routeId"]].get("missingInputs")
                  and not (r["icId"] == "NT51925" and r["workflowId"] == "ctrlram-replace")]
        self.assertEqual(len(routes), 82)
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        work = baseline.REPO.parent / "fake-work"
        output_file = mock.Mock()
        output_file.open.side_effect = lambda *_: io.BytesIO(b"output")
        output_file.relative_to.return_value = Path("output.bin")
        output_file.stat.return_value = SimpleNamespace(st_size=6)
        for route in routes:
            args = [route["workflowId"], "build", "--profile", route["icId"]]
            report = {"Capability": {"RouteId": route["routeId"],
                                     "CapabilityFingerprint": route["capabilityFingerprint"]},
                      "Output": {"Committed": True, "FileName": "output.bin",
                                 "Sha256": self.output["sha256"], "Size": self.output["size"]}}
            with (self.subTest(route=route["routeId"]), mock.patch.object(Path, "mkdir"),
                  mock.patch.object(Path, "read_bytes", return_value=json.dumps(report).encode()),
                  mock.patch.object(Path, "rglob", return_value=[output_file]),
                  mock.patch.object(baseline.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as run):
                self.assertEqual(executor.build(route, args, work), [self.output])
                self.assertEqual(run.call_args.args[0], ["dotnet", str(executor.cli), *args,
                                 "--output", str(work / "outputs/output.bin"),
                                 "--report", str(work / "report.json")])
                self.assertEqual(run.call_args.kwargs["cwd"], work)

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
        self.assertEqual(request["input_kind"], "native")
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
        catalog.recipes = {}
        route = next(r for r in baseline.load_routes() if r["workflowId"] == "general-merge")
        execute = mock.Mock()
        result = baseline.collect([route], catalog.prepare, execute, "a" * 40, "version")
        self.assertEqual(result["routes"][0]["status"], "input-unavailable")
        self.assertEqual(result["routes"][0]["missing_inputs"], ["saved-rule", "slot-inputs"])
        execute.assert_not_called()


class BaselineRecipeTests(unittest.TestCase):
    def setUp(self):
        self.catalog = baseline.CanonicalInputs(lambda _: ("1", "2", "3", "single", "cascade"))
        self.routes = {row["routeId"]: row for row in baseline.load_routes()}

    def route(self, workflow, ic="NT51926", map_variant=None):
        return next(row for row in self.routes.values()
                    if row["workflowId"] == workflow and row["icId"] == ic
                    and (map_variant is None or row["mapVariant"] == map_variant))

    def recipe_route(self, kind, workflow=None):
        return next(self.routes[route_id] for route_id, recipe in self.catalog.recipes.items()
                    if recipe["kind"] == kind
                    and recipe.get("source") != "route-evidence"
                    and (workflow is None or self.routes[route_id]["workflowId"] == workflow))

    def test_every_route_declares_one_input_kind_and_inventory_disposition(self):
        recipes = self.catalog.recipes
        self.assertEqual(set(recipes), set(self.routes))
        self.assertEqual({kind: sum(r["kind"] == kind for r in recipes.values())
                          for kind in baseline.INPUT_KINDS},
                         {"native": 45, "derived": 22, "synthetic": 22, "private-native": 5})
        for recipe in recipes.values():
            self.assertTrue(recipe["inventory"]["reason"])
            self.assertTrue(recipe["inventory"]["nativeReplacement"])

    def test_derived_byte_operations_copy_exact_half_open_views_in_declared_order(self):
        spec = {"size": 8, "operations": [
            {"kind": "copy", "source": {"caseId": "example", "artifactId": "source"},
             "sourceRange": {"addressSpace": "repository-artifact", "start": 1, "endExclusive": 4},
             "copyCount": 2, "targetOrder": [{"bank": "a", "start": 0}, {"bank": "b", "start": 4}]},
            {"kind": "fill", "length": 1, "value": 90, "copyCount": 2,
             "targetOrder": [{"bank": "a", "start": 3}, {"bank": "b", "start": 7}]},
            {"kind": "literal", "hex": "FF", "copyCount": 1,
             "targetOrder": [{"bank": "b", "start": 5}]},
        ]}
        self.assertEqual(baseline.derived_bytes(spec, lambda _: b"01234"), b"123Z1\xff3Z")
        for start, end in [(-1, 3), (1, 6), (2, 2), (True, 3)]:
            with self.subTest(start=start, end=end):
                spec["operations"][0]["sourceRange"].update(start=start, endExclusive=end)
                with self.assertRaisesRegex(ValueError, "source bounds"):
                    baseline.derived_bytes(spec, lambda _: b"01234")

    def test_input_kinds_markdown_matches_generator(self):
        self.assertEqual(baseline.INPUT_KINDS_MARKDOWN.read_bytes(),
                         baseline.render_input_kinds(self.catalog).encode("utf-8"))

    def test_all_route_inputs_prepare_with_d1_and_missing_same_ic_base_unavailable_without_cli(self):
        executed = []

        def capture(route, request, inputs):
            kind = self.catalog.recipes[route["routeId"]]["kind"]
            self.assertEqual(request["input_kind"], kind)
            self.assertTrue(inputs)
            if kind == "native":
                self.assertNotIn("synthetic", request)
                self.assertNotIn("derived", request)
                for row in inputs:
                    self.assertTrue(row["path"].startswith("testdata/golden/canonical/"))
                    self.assertEqual(hashlib.sha256((baseline.REPO / row["path"]).read_bytes()).hexdigest(), row["sha256"])
            else:
                self.assertTrue(request[kind])
                self.assertTrue(any(row["path"].startswith(kind + "/") for row in inputs))
                if kind == "synthetic":
                    self.assertTrue(all(row["path"].startswith("synthetic/") for row in inputs))
                else:
                    self.assertTrue(all(spec["operations"] for spec in request["derived"]))
            executed.append(route["routeId"])
            return {"outputs": []}

        result = baseline.collect(list(self.routes.values()), self.catalog.prepare, capture, "a" * 40, "version")
        self.assertEqual(result["header"]["counts"], {"ok": 82, "input-unavailable": 12, "failed": 0})
        self.assertEqual(len(executed), 82)
        private = [row for row in result["routes"] if row["input_kind"] == "private-native"]
        self.assertTrue(all(row["input_kind"] == "private-native" and row["inputs"] == [] for row in private))
        self.assertTrue(all(self.routes[row["route_id"]]["icId"] == "NT51925" for row in private))
        unavailable = [row for row in result["routes"] if row["input_kind"] != "private-native"
                       and row["status"] == "input-unavailable"]
        self.assertEqual(sorted(self.routes[row["route_id"]]["icId"] for row in unavailable),
                         ["NT51919", "NT51925", "NT51925", "NT51925", "NT51925", "NT51929", "NT51932"])
        for row in unavailable:
            self.assertEqual(row["inputs"], [])
            self.assertTrue(row["missing_inputs"][0].strip())
        nt51919 = next(row for row in unavailable if self.routes[row["route_id"]]["icId"] == "NT51919")
        self.assertIn("same-IC", nt51919["missing_inputs"][0])

    def test_round2_crc_bases_preserve_same_ic_source_structure_and_declared_views(self):
        round2 = [r for r in self.catalog.recipes.values() if r.get("decision") == "D4-R2"]
        self.assertEqual(len(round2), 6)
        # The NT51929 and NT51932 bases were still refused by the CRC processor, so they wait for real data.
        refused = [r for r in round2 if r.get("missingInputs")]
        self.assertEqual(sorted(self.routes[r["routeId"]]["icId"] for r in refused), ["NT51929", "NT51932"])
        self.assertTrue(all(r["kind"] == "derived" and "inputs" not in r for r in refused))
        converted = [r for r in round2 if not r.get("missingInputs")]
        self.assertEqual(len(converted), 4)
        for recipe in converted:
            route = self.routes[recipe["routeId"]]
            with self.subTest(route=route["routeId"]):
                self.assertEqual(recipe["kind"], "derived")
                request, missing = self.catalog.prepare(route, [])
                self.assertEqual(missing, [])
                spec = request["derived"][0]
                data = baseline.derived_bytes(spec, self.catalog.read_derived_source)
                for operation in spec["operations"]:
                    if operation["kind"] != "copy":
                        continue
                    source = operation["source"]
                    self.assertEqual(self.catalog.case(source["caseId"])["ic"], route["icId"])
                    view = operation["sourceRange"]
                    self.assertGreaterEqual(view["start"], 0)
                    self.assertLessEqual(view["endExclusive"], source["size"])
                original = self.catalog.read_derived_source(spec["operations"][0]["source"])
                expected = bytearray(original[:spec["size"]])
                if route["icId"] == "NT51928":
                    expected[0x34017] = recipe["icCount"]
                    expected[0x3401A:0x3401D] = (b"\x01\x04\x01" if recipe["icCount"] == 1
                                                       else b"\x01\x04\x00")
                    # In particular retain the nonzero CRC lengths and header structure.
                    self.assertEqual(data[:0x16000], original[:0x16000])
                elif route["icId"] == "NT51929":
                    expected[0x2F000:0x30000] = original[0x2E000:0x2F000]
                    expected[0x2EFFC:0x2F000] = b"\0" * 4
                    expected[0x2F017] = 2
                else:
                    self.assertEqual(route["icId"], "NT51932")
                    expected[0x2E000:0x2F000] = original[0x33000:0x34000]
                    expected[0x33FFC:0x34000] = b"\0" * 4
                    expected[0x2E017] = 1
                self.assertEqual(data, bytes(expected))
                self.assertEqual(data.count(b"\0NVT"), 1)

    def test_unavailable_generated_recipe_keeps_kind_and_never_generates_or_executes(self):
        route = next(r for r in self.routes.values() if r["icId"] == "NT51919"
                     and r["mapVariant"] == "nt51929-ctrlram-fw1x-cascade-full-flash")
        execute = mock.Mock()
        with mock.patch.object(baseline, "synthetic_bytes") as generate:
            result = baseline.collect([route], self.catalog.prepare, execute, "a" * 40, "version")
        record = result["routes"][0]
        self.assertEqual(record["status"], "input-unavailable")
        self.assertEqual(record["input_kind"], "synthetic")
        self.assertEqual(record["input_source"], "recipe-synthetic")
        self.assertEqual(record["missing_inputs"], self.catalog.recipes[route["routeId"]]["missingInputs"])
        execute.assert_not_called()
        generate.assert_not_called()

    def test_each_derived_source_view_is_bounded_and_refuses_one_byte_past_its_artifact(self):
        for recipe in self.catalog.recipes.values():
            if recipe["kind"] != "derived" or recipe.get("missingInputs"):
                continue
            for spec in recipe["inputs"]:
                resolved = self.catalog.resolve_derived(spec, [])
                original = baseline.derived_bytes(resolved, self.catalog.read_derived_source)
                self.assertEqual(len(original), spec["size"])
                for index, operation in enumerate(resolved["operations"]):
                    if operation["kind"] != "copy":
                        continue
                    with self.subTest(route=recipe["routeId"], operation=index):
                        changed = copy.deepcopy(resolved)
                        changed["operations"][index]["sourceRange"]["endExclusive"] = operation["source"]["size"] + 1
                        with self.assertRaisesRegex(ValueError, "source bounds"):
                            baseline.derived_bytes(changed, self.catalog.read_derived_source)

    def test_derived_operations_refuse_wrong_spaces_counts_banks_targets_and_undeclared_bytes(self):
        original = {"size": 2, "operations": [{
            "kind": "copy", "source": {},
            "sourceRange": {"addressSpace": "repository-artifact", "start": 0, "endExclusive": 2},
            "copyCount": 1, "targetOrder": [{"bank": "none", "start": 0}]}]}
        mutations = [lambda s: s["operations"][0]["sourceRange"].update(addressSpace="flash"),
                     lambda s: s["operations"][0].update(copyCount=2),
                     lambda s: s["operations"][0].update(copyCount=True),
                     lambda s: s["operations"][0]["targetOrder"][0].update(start=-1),
                     lambda s: s["operations"][0]["targetOrder"][0].update(start=1),
                     lambda s: s["operations"][0]["targetOrder"][0].update(bank="unknown"),
                     lambda s: s.update(size=3),
                     lambda s: s["operations"][0].update(kind="firmware-merge")]
        for mutate in mutations:
            spec = copy.deepcopy(original)
            mutate(spec)
            with self.subTest(spec=spec), self.assertRaises(ValueError):
                baseline.derived_bytes(spec, lambda _: b"ab")

    def test_kind_loader_rejects_input_source_disguised_as_another_kind(self):
        invalid = [{"kind": "repo-case"}, {"kind": "private-native", "inputs": []},
                   {"kind": "private-native", "caseId": "example"},
                   {"kind": "native", "caseId": "example", "inputs": [{"size": 1, "fill": 0}]},
                   {"kind": "derived", "inputs": [{"size": 1, "fill": 0}]},
                   {"kind": "synthetic", "inputs": [{"operations": []}]},
                   {"kind": "synthetic", "inputs": [{"source": "testdata/example.bin"}]}]
        for row in invalid:
            document = json.dumps({"schemaVersion": 2, "recipes": [dict(row, routeId="example")]}).encode()
            with self.subTest(row=row), mock.patch.object(Path, "read_bytes", return_value=document), self.assertRaises(ValueError):
                baseline.load_recipes()

    def test_unavailable_generated_recipe_requires_nonempty_reasons_without_inputs(self):
        for kind in ("derived", "synthetic"):
            valid = {"routeId": "example", "kind": kind, "missingInputs": ["No usable same-IC base"]}
            document = {"schemaVersion": 2, "recipes": [valid]}
            with mock.patch.object(Path, "read_bytes", return_value=json.dumps(document).encode()):
                self.assertEqual(baseline.load_recipes()["example"], valid)
            for fields in ({"missingInputs": []}, {"missingInputs": [""]},
                           {"missingInputs": "reason"}, {"missingInputs": [1]},
                           {"inputs": []}, {"bindings": []}, {"caseId": "example"},
                           {"source": "route-evidence"}):
                invalid = dict(valid, **fields)
                document = {"schemaVersion": 2, "recipes": [invalid]}
                with (self.subTest(kind=kind, fields=fields),
                      mock.patch.object(Path, "read_bytes", return_value=json.dumps(document).encode()),
                      self.assertRaisesRegex(ValueError, "Unavailable generated recipe")):
                    baseline.load_recipes()

    def test_d2_tp_views_and_nt51928_own_reference_use_exact_original_source_prefixes(self):
        for rid, recipe in self.catalog.recipes.items():
            if recipe.get("decision") != "D2":
                continue
            with self.subTest(route=rid):
                request, missing = self.catalog.prepare(self.routes[rid], [])
                self.assertEqual(missing, [])
                spec = request["derived"][0]
                op = spec["operations"][0]
                source = self.catalog.read_derived_source(op["source"])
                expected_length = 0x80000 if self.routes[rid]["icId"] == "NT51928" and "full-flash" in rid else 0x35000
                self.assertEqual(baseline.derived_bytes(spec, self.catalog.read_derived_source), source[:expected_length])
                if self.routes[rid]["icId"] == "NT51928":
                    self.assertEqual(op["source"]["caseId"], "nt51928-gen-flash")
                    self.assertEqual(op["source"]["artifactId"], "expected-output")
                    self.assertEqual({row["caseId"] for row in recipe["bindings"] if "caseId" in row},
                                     {"nt51927-2chip-self-20260705"})

    def test_d3_references_match_independent_bank_construction_and_declared_member_count(self):
        for rid, recipe in self.catalog.recipes.items():
            if recipe.get("decision") != "D3":
                continue
            route = self.routes[rid]
            with self.subTest(route=rid):
                request, missing = self.catalog.prepare(route, [])
                self.assertEqual(missing, [])
                self.assertEqual(request["arguments"][request["arguments"].index("--bank") + 1], "b")
                self.assertEqual(recipe["members"]["targetIc"], route["icId"])
                source_case = self.catalog.case(recipe["members"]["referenceCaseId"])
                source = baseline.examples.artifact(source_case, "expected-output").read_bytes()
                perfect = route["icId"] in {"NT51919", "NT51929", "NT51932"}
                single = route["icCountVariant"] == "1-ic"
                if single and (perfect or route["icId"] == "NT51950"):
                    expected = source
                else:
                    bank_size = 0x40000 if perfect else 0x80000
                    local_size = 0x40000 if perfect or route["icId"] == "NT51950" else 0x80000
                    bank = source[:local_size] + b"\x5A" * (bank_size - local_size)
                    expected = bytearray(bank * 2)
                    for field in ([0x7164, 0x7168, 0x716C] if perfect else [0xA100, 0xA110, 0xA120]):
                        value = int.from_bytes(bank[field:field + 4], "little") + bank_size
                        expected[bank_size + field:bank_size + field + 4] = value.to_bytes(4, "little")
                self.assertEqual(recipe["icCount"], 1 if single else 3 if perfect else 2)
                self.assertEqual(baseline.derived_bytes(request["derived"][0], self.catalog.read_derived_source), expected)

    def test_kinds_command_writes_the_same_document_without_test_area_or_cli(self):
        with (mock.patch.object(Path, "write_bytes") as write,
              mock.patch.object(baseline, "test_area", side_effect=AssertionError("not needed")),
              mock.patch.object(baseline.subprocess, "run", side_effect=AssertionError("no CLI"))):
            self.assertEqual(baseline.main(["kinds", "--output", "example.md"]), 0)
        write.assert_called_once_with(baseline.render_input_kinds(self.catalog).encode("utf-8"))

    def test_derived_source_hash_size_and_canonical_root_are_rechecked(self):
        route = self.recipe_route("derived")
        request, _ = self.catalog.prepare(route, [])
        source = request["derived"][0]["operations"][0]["source"]
        for field, value in [("sha256", "0" * 64), ("size", source["size"] + 1),
                             ("path", "../example.bin")]:
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "changed|escapes"):
                self.catalog.read_derived_source(dict(source, **{field: value}))

    def test_derived_executor_uses_test_area_and_detects_prepared_or_source_mutation(self):
        route = self.recipe_route("derived")
        inputs = []
        request, _ = self.catalog.prepare(route, inputs)
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        work = executor.temp / "fake-work"
        spec = request["derived"][0]
        expected = baseline.derived_bytes(spec, self.catalog.read_derived_source)
        for mutation in (None, "prepared", "source"):
            files = {baseline.REPO / row["path"]: (baseline.REPO / row["path"]).read_bytes()
                     for row in inputs if row["path"].startswith("testdata/")}

            def write(path, payload):
                self.assertEqual(path, work / "reference.bin")
                self.assertEqual(payload, expected)
                files[path] = payload
                return len(payload)

            def build(actual_route, arguments, final):
                self.assertEqual(actual_route, route)
                self.assertEqual(final, work / "final")
                self.assertEqual(arguments[arguments.index("--base") + 1], str(work / "reference.bin"))
                self.assertFalse(any("<derived:" in arg for arg in arguments))
                if mutation:
                    path = work / "reference.bin" if mutation == "prepared" else baseline.REPO / inputs[0]["path"]
                    files[path] = b"changed"
                return []

            with (self.subTest(mutation=mutation),
                  mock.patch.object(baseline.tempfile, "TemporaryDirectory", return_value=contextlib.nullcontext(str(work))) as temporary,
                  mock.patch.object(Path, "mkdir"), mock.patch.object(Path, "rmdir"),
                  mock.patch.object(Path, "write_bytes", autospec=True, side_effect=write),
                  mock.patch.object(Path, "open", autospec=True, side_effect=lambda path, *_, **__: io.BytesIO(files[path])),
                  mock.patch.object(executor, "build", side_effect=build)):
                if mutation:
                    with self.assertRaisesRegex(ValueError, "input changed"):
                        executor(route, request, inputs)
                else:
                    self.assertEqual(executor(route, request, inputs), {"outputs": []})
                temporary.assert_called_once_with(prefix="g0-baseline-", dir=executor.temp)
            self.assertIn("<derived:reference.bin>", request["arguments"])

    def test_d4_synthetic_inputs_declare_counts_versions_and_marker_envelopes(self):
        for rid, recipe in self.catalog.recipes.items():
            if recipe.get("decision") != "D4" or recipe.get("missingInputs"):
                continue
            with self.subTest(route=rid):
                route = self.routes[rid]
                request, missing = self.catalog.prepare(route, [])
                self.assertEqual(missing, [])
                data = {spec["name"]: baseline.synthetic_bytes(spec) for spec in request["synthetic"]}
                if route["workflowId"] == "ab-merge":
                    self.assertEqual(recipe["icCount"], 2 if route["icId"] == "NT51950" else 1)
                    for name in ("tp-a.bin", "tp-b.bin"):
                        self.assertEqual(data[name][0x36017], recipe["icCount"])
                        self.assertEqual(data[name][0x36FFC:0x37000], b"\0NVT")
                    continue
                expected_count = 3 if route["icCountVariant"] == "3-ic" else 2 if route["icCountVariant"] == "2-8-ic" else 1
                reference = data["reference.bin"]
                marker = reference.index(b"\0NVT")
                self.assertEqual(reference.count(b"\0NVT"), 1)
                backup = marker - 0xFFC
                self.assertEqual(reference[backup + 0x17], expected_count)
                expected_fw = b"\x01\x04\x01" if "fw141" in rid else b"\x01\x04\x00" if "fw140" in rid else b"\x02\0\0"
                self.assertEqual(reference[backup + 0x1A:backup + 0x1D], expected_fw)
                self.assertEqual(recipe["icCount"], expected_count)

    def test_recipe_inventory_has_exact_policy_fingerprints_and_honest_evidence_bindings(self):
        recipes = self.catalog.recipes
        self.assertEqual(len(recipes), 94)
        self.assertEqual(list(recipes), sorted(recipes))
        for route_id, recipe in recipes.items():
            with self.subTest(route=route_id):
                self.assertEqual(recipe["capabilityFingerprint"], self.routes[route_id]["capabilityFingerprint"])
                if recipe.get("source") == "route-evidence":
                    self.assertEqual(recipe["kind"], "native")
                    self.assertIn("caseId", self.catalog.evidence[route_id])
                else:
                    self.assertNotIn("caseId", self.catalog.evidence[route_id])

    def test_route_evidence_takes_precedence_even_over_invalid_recipe(self):
        route = self.route("standard-merge")
        self.catalog.recipes[route["routeId"]] = {"kind": "invalid", "capabilityFingerprint": "wrong"}
        request, missing = self.catalog.prepare(route, [])
        self.assertEqual(missing, [])
        self.assertEqual(request["input_source"], "route-evidence")

    def test_case_bearing_evidence_with_missing_case_does_not_fall_back_to_recipe(self):
        route = self.recipe_route("synthetic")
        self.catalog.evidence[route["routeId"]] = {
            "caseId": "missing-case", "capabilityFingerprint": route["capabilityFingerprint"]}
        request, missing = self.catalog.prepare(route, [])
        self.assertTrue(missing)
        self.assertEqual(request["input_source"], "route-evidence")

    def test_recipe_fingerprint_mismatch_fails_without_executing_or_generating(self):
        route = self.recipe_route("synthetic")
        self.catalog.recipes[route["routeId"]]["capabilityFingerprint"] = "0" * 64
        execute = mock.Mock()
        with mock.patch.object(baseline, "synthetic_bytes") as generate:
            result = baseline.collect([route], self.catalog.prepare, execute, "a" * 40, "version")
        self.assertEqual(result["routes"][0]["status"], "failed")
        self.assertIn("fingerprint", result["routes"][0]["error"])
        execute.assert_not_called()
        generate.assert_not_called()

    def test_repo_recipe_uses_validated_artifacts_and_records_source(self):
        route = self.recipe_route("native", "standard-merge")
        inputs = []
        request, missing = self.catalog.prepare(route, inputs)
        self.assertEqual(missing, [])
        self.assertEqual(request["input_source"], "recipe-native")
        self.assertEqual([row["name"] for row in inputs], ["dp-input", "tp-input", "ldc-input"])
        for row in inputs:
            self.assertEqual(row["sha256"], hashlib.sha256((baseline.REPO / row["path"]).read_bytes()).hexdigest())

    def test_repo_recipe_refuses_actual_manifest_hash_or_size_mismatch(self):
        route = self.recipe_route("native", "standard-merge")
        recipe = self.catalog.recipes[route["routeId"]]
        artifact = self.catalog.case(recipe["caseId"])["artifacts"][0]
        for field, value, diagnostic in [("sha256", "0" * 64, "SHA-256"), ("size", 1, "size")]:
            with self.subTest(field=field), mock.patch.dict(artifact, {field: value}):
                execute = mock.Mock()
                result = baseline.collect([route], self.catalog.prepare, execute, "a" * 40, "version")
                self.assertEqual(result["routes"][0]["status"], "failed")
                self.assertIn(diagnostic, result["routes"][0]["error"])
                execute.assert_not_called()

    def test_repo_recipe_requires_case_in_canonical_manifest(self):
        route = self.recipe_route("native")
        self.catalog.recipes[route["routeId"]]["caseId"] = "unlisted-case"
        with self.assertRaisesRegex(ValueError, "canonical manifest"):
            self.catalog.prepare(route, [])

    def test_all_repo_recipes_prepare_without_generating_a_base_or_using_expected_output(self):
        for route_id, recipe in self.catalog.recipes.items():
            if recipe["kind"] != "native" or recipe.get("source") == "route-evidence":
                continue
            with self.subTest(route=route_id):
                inputs = []
                request, missing = self.catalog.prepare(self.routes[route_id], inputs)
                self.assertEqual(missing, [])
                self.assertNotIn("base_build", request)
                self.assertNotIn("expected-output", {row["name"] for row in inputs})
                if "tp-work" in route_id:
                    base_index = request["arguments"].index("--base") + 1
                    self.assertEqual(Path(request["arguments"][base_index]).stat().st_size, 0x37000)

    def test_synthetic_bytes_are_deterministic_with_independent_small_pattern_oracles(self):
        self.assertEqual(baseline.synthetic_bytes({"size": 3, "fill": 0x5A}), b"\x5A" * 3)
        spec = {"size": 5, "pattern": {"seed": 254, "step": 3},
                "overlays": [{"offset": 2, "hex": "A55A"}]}
        self.assertEqual(baseline.synthetic_bytes(spec), b"\xFE\x01\xA5\x5A\x0A")
        for recipe in self.catalog.recipes.values():
            if recipe["kind"] == "synthetic" and not recipe.get("missingInputs"):
                for spec in recipe["inputs"]:
                    self.assertEqual(baseline.synthetic_bytes(spec), baseline.synthetic_bytes(spec))

    def test_synthetic_input_paths_and_hashes_repeat_without_host_or_run_identity(self):
        route = self.recipe_route("synthetic", "general-merge")
        first, second = [], []
        request, missing = self.catalog.prepare(route, first)
        self.catalog.prepare(route, second)
        self.assertEqual(missing, [])
        self.assertEqual(first, second)
        self.assertEqual(request["input_source"], "recipe-synthetic")
        self.assertEqual(first[0], {
            "name": "source.bin", "path": f"synthetic/{route['routeId']}/source.bin",
            "size": 2, "sha256": hashlib.sha256(b"\x10\x11").hexdigest()})

    def test_synthetic_executor_materializes_in_test_area_and_detects_input_mutation(self):
        route = self.recipe_route("synthetic", "general-replace")
        inputs = []
        request, _ = self.catalog.prepare(route, inputs)
        executor = baseline.CliExecutor(Path("current.dll"), baseline.REPO.parent)
        work = executor.temp / "fake-work"
        for mutate in (False, True):
            with self.subTest(mutate=mutate):
                files = {}

                def write(path, payload):
                    self.assertEqual(path.parent, work)
                    files[path] = payload
                    return len(payload)

                def build(actual_route, arguments, final):
                    self.assertEqual(actual_route, route)
                    self.assertEqual(final, work / "final")
                    self.assertEqual(arguments, [
                        "general-replace", "build", "--profile", "NT51926", "--ic-num", "single",
                        "--base", str(work / "reference.bin"),
                        "--mapping", f"0x3e020+0x2={work / 'source.bin'}"])
                    self.assertEqual(files[work / "source.bin"], b"\xA5\x5A")
                    self.assertEqual(len(files[work / "reference.bin"]), 0x40000)
                    if mutate:
                        files[work / "source.bin"] = b"changed"
                    return []

                with (mock.patch.object(baseline.tempfile, "TemporaryDirectory",
                                        return_value=contextlib.nullcontext(str(work))) as temporary,
                      mock.patch.object(Path, "mkdir"), mock.patch.object(Path, "rmdir"),
                      mock.patch.object(Path, "write_bytes", autospec=True, side_effect=write),
                      mock.patch.object(Path, "open", autospec=True,
                                        side_effect=lambda path, *_: io.BytesIO(files[path])),
                      mock.patch.object(executor, "build", side_effect=build)):
                    if mutate:
                        with self.assertRaisesRegex(ValueError, "input changed"):
                            executor(route, request, inputs)
                    else:
                        self.assertEqual(executor(route, request, inputs), {"outputs": []})
                    temporary.assert_called_once_with(prefix="g0-baseline-", dir=executor.temp)
                self.assertIn("<synthetic:reference.bin>", request["arguments"])
                self.assertTrue(all(row["path"].startswith("synthetic/") for row in inputs))

    def test_unsafe_or_duplicate_synthetic_names_fail_before_materialization(self):
        route = self.recipe_route("synthetic")
        original = self.catalog.recipes[route["routeId"]]["inputs"]
        for specs in ([dict(original[0], name="../escape.bin")], [original[0], original[0]]):
            with self.subTest(specs=specs):
                self.catalog.recipes[route["routeId"]]["inputs"] = specs
                with self.assertRaisesRegex(ValueError, "unique plain BIN names"):
                    self.catalog.prepare(route, [])

    def test_recipe_loader_refuses_duplicate_ids_unsorted_entries_and_unknown_schema(self):
        for rows, version, diagnostic in [
                ([{"routeId": "a"}, {"routeId": "a"}], 2, "Duplicate route"),
                ([{"routeId": "b"}, {"routeId": "a"}], 2, "sorted"),
                ([], 1, "schema version")]:
            document = json.dumps({"schemaVersion": version, "recipes": rows}).encode()
            with (self.subTest(rows=rows, version=version),
                  mock.patch.object(Path, "read_bytes", return_value=document),
                  self.assertRaisesRegex(ValueError, diagnostic)):
                baseline.load_recipes()

    def test_general_merge_constructs_existing_manual_cli_options(self):
        route = self.recipe_route("synthetic", "general-merge")
        request, missing = self.catalog.prepare(route, [])
        self.assertEqual(missing, [])
        self.assertEqual(request["arguments"], [
            "general-merge", "build", "--profile", route["icId"], "--size", "0x4", "--fill", "0x00",
            "--mapping", f"0x0+0x1+0x2=<synthetic:source.bin>"])

    def test_general_replace_constructs_immutable_base_and_dp_mapping_options(self):
        route = self.recipe_route("synthetic", "general-replace")
        request, missing = self.catalog.prepare(route, [])
        self.assertEqual(missing, [])
        self.assertEqual(request["arguments"], [
            "general-replace", "build", "--profile", "NT51926", "--ic-num", "single",
            "--base", "<synthetic:reference.bin>", "--mapping", "0x3e020+0x2=<synthetic:source.bin>"])
        self.assertNotIn("--size", request["arguments"])
        self.assertNotIn("--fill", request["arguments"])

    def test_synthetic_standard_declares_capacity_pattern_and_cli_metadata(self):
        route = self.recipe_route("synthetic", "standard-merge")
        request, missing = self.catalog.prepare(route, [])
        self.assertEqual(missing, [])
        self.assertEqual(request["arguments"], ["standard-merge", "build", "--profile", route["icId"],
                                              "--dp", "<synthetic:dp.bin>", "--tp", "<synthetic:tp.bin>"])
        specs = {row["name"]: row for row in request["synthetic"]}
        self.assertEqual(len(baseline.synthetic_bytes(specs["dp.bin"])), 0x80000)
        tp = baseline.synthetic_bytes(specs["tp.bin"])
        self.assertEqual(tp[0x36000:0x36002], b"\x81\x7E")
        self.assertEqual(tp[0x36017], 1)
        self.assertEqual(tp[0x36FFC:0x37000], b"\0NVT")

    def test_invalid_synthetic_sizes_bytes_or_overlays_are_refused(self):
        specs = [{"size": -1, "fill": 0}, {"size": 2, "fill": 256},
                 {"size": 2, "fill": 0, "pattern": {"seed": 0, "step": 1}},
                 {"size": 2, "pattern": {"seed": -1, "step": 1}},
                 {"size": 2, "fill": 0, "overlays": [{"offset": 2, "hex": "FF"}]}]
        for spec in specs:
            with self.subTest(spec=spec), self.assertRaises(ValueError):
                baseline.synthetic_bytes(spec)

    def test_collect_projects_each_kind_and_source_including_private_unavailable(self):
        routes = [self.route("standard-merge"), self.recipe_route("native"),
                  self.recipe_route("synthetic"), self.recipe_route("derived")]
        unavailable = self.recipe_route("private-native")
        result = baseline.collect([*routes, unavailable], self.catalog.prepare,
                                  lambda *_: {"outputs": []}, "a" * 40, "version")
        self.assertEqual({row["input_source"] for row in result["routes"]},
                         {"route-evidence", "recipe-native", "recipe-synthetic", "recipe-derived", "recipe-private-native"})
        self.assertEqual(result["header"]["input_kind_counts"],
                         {"native": 2, "derived": 1, "synthetic": 1, "private-native": 1})


if __name__ == "__main__":
    unittest.main()
