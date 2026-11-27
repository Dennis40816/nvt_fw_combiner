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
        })
        self.assertEqual([r["route_id"] for r in result["routes"]], ["route-a", "route-b", "route-c"])
        self.assertEqual(result["routes"][0], {
            "route_id": "route-a", "status": "ok", "inputs": [self.input], "outputs": [self.output],
            "input_source": "route-evidence",
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
                    and (workflow is None or self.routes[route_id]["workflowId"] == workflow))

    def test_recipe_inventory_has_exact_phase_a_scope_and_fingerprints(self):
        recipes = self.catalog.recipes
        self.assertEqual(len(recipes), 27)
        self.assertEqual(list(recipes), sorted(recipes))
        self.assertEqual(sum(r["kind"] == "repo-case" for r in recipes.values()), 12)
        self.assertEqual(sum(r["kind"] == "synthetic" for r in recipes.values()), 15)
        for route_id, recipe in recipes.items():
            with self.subTest(route=route_id):
                self.assertEqual(recipe["capabilityFingerprint"], self.routes[route_id]["capabilityFingerprint"])
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
        route = self.recipe_route("repo-case", "standard-merge")
        inputs = []
        request, missing = self.catalog.prepare(route, inputs)
        self.assertEqual(missing, [])
        self.assertEqual(request["input_source"], "recipe-repo-case")
        self.assertEqual([row["name"] for row in inputs], ["dp-input", "tp-input", "ldc-input"])
        for row in inputs:
            self.assertEqual(row["sha256"], hashlib.sha256((baseline.REPO / row["path"]).read_bytes()).hexdigest())

    def test_repo_recipe_refuses_actual_manifest_hash_or_size_mismatch(self):
        route = self.recipe_route("repo-case", "standard-merge")
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
        route = self.recipe_route("repo-case")
        self.catalog.recipes[route["routeId"]]["caseId"] = "unlisted-case"
        with self.assertRaisesRegex(ValueError, "canonical manifest"):
            self.catalog.prepare(route, [])

    def test_all_repo_recipes_prepare_without_generating_a_base_or_using_expected_output(self):
        for route_id, recipe in self.catalog.recipes.items():
            if recipe["kind"] != "repo-case":
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
            if recipe["kind"] == "synthetic":
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
                ([{"routeId": "a"}, {"routeId": "a"}], 1, "Duplicate route"),
                ([{"routeId": "b"}, {"routeId": "a"}], 1, "sorted"),
                ([], 2, "schema version")]:
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

    def test_collect_projects_each_available_input_source_and_null_for_unavailable(self):
        routes = [self.route("standard-merge"), self.recipe_route("repo-case"),
                  self.recipe_route("synthetic")]
        unavailable = next(row for row in self.routes.values()
                           if row["routeId"] not in self.catalog.recipes
                           and "caseId" not in self.catalog.evidence[row["routeId"]])
        result = baseline.collect([*routes, unavailable], self.catalog.prepare,
                                  lambda *_: {"outputs": []}, "a" * 40, "version")
        self.assertEqual({row["input_source"] for row in result["routes"]},
                         {"route-evidence", "recipe-repo-case", "recipe-synthetic", None})


if __name__ == "__main__":
    unittest.main()
