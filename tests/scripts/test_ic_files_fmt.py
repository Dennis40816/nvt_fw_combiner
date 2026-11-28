"""Behavioral tests for schema-directed layout and generated IC file hashes."""

from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys

import pytest


REPOSITORY = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY / "scripts/ic_files.py"
FIXTURE = Path(__file__).parent / "fixtures/ic-files"
SPEC = importlib.util.spec_from_file_location("ic_files", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
ic_files = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = ic_files
SPEC.loader.exec_module(ic_files)


@pytest.fixture
def tree(tmp_path: Path) -> Path:
    return Path(shutil.copytree(FIXTURE, tmp_path / "ic-files"))


def run_fmt(tree: Path, *arguments: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "fmt", "--root", str(tree), *arguments],
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )


def snapshot(tree: Path) -> dict[str, bytes]:
    return {p.relative_to(tree).as_posix(): p.read_bytes() for p in tree.rglob("*") if p.is_file()}


def read_yaml(path: Path):
    return ic_files.YamlDocument(path, path.read_bytes()).root.plain()


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False) + "\n", encoding="utf-8")


def canonicalize(tree: Path) -> None:
    result = run_fmt(tree)
    assert result.returncode == 0, result.stderr


def base_hash(tree: Path) -> str:
    return read_yaml(tree / "overrides/demo.yaml")["base"][0]["sha256"]


def test_repository_ic_files_have_current_layout_and_lock_without_writes() -> None:
    root = REPOSITORY / "profiles/ic-files"
    before = snapshot(root)
    result = run_fmt(root, "--check")
    assert result.returncode == 0, result.stdout + result.stderr
    assert result.stdout == ""
    assert snapshot(root) == before
    entries = read_yaml(root / "ic-files.lock.yaml")
    assert len(entries) == 60
    for entry in entries:
        assert entry["sha256"] == hashlib.sha256((root / entry["path"]).read_bytes()).hexdigest()


def test_layout_hashes_and_second_run_are_idempotent(tree: Path) -> None:
    path = tree / "definitions/word.yaml"
    path.write_bytes(
        b'\xef\xbb\xbf# A reusable synthetic field.\r\ndecoder: "u8le"\r\nkind: "scalar"\r\ndefinition: "word"\r\n'
    )
    result = run_fmt(tree)
    assert result.returncode == 0, result.stderr
    assert path.read_bytes() == (FIXTURE / "definitions/word.yaml").read_bytes()
    before = snapshot(tree)
    assert run_fmt(tree).returncode == 0
    assert run_fmt(tree, "--check").returncode == 0
    assert snapshot(tree) == before


def test_check_reports_initial_drift_without_creating_any_file(tree: Path) -> None:
    before = snapshot(tree)
    times = {p: (tree / p).stat().st_mtime_ns for p in before}
    result = run_fmt(tree, "--check")
    assert result.returncode == 1
    assert "missing lock entry" in result.stdout
    assert "stale override base" in result.stdout
    assert snapshot(tree) == before
    assert {p: (tree / p).stat().st_mtime_ns for p in before} == times


def test_nested_schema_refs_items_and_union_branches_order_keys(tree: Path) -> None:
    path = tree / "parts/common.yaml"
    path.write_text(
        "fact-sets:\n"
        "    - integrity: []\n"
        "      fields:\n"
        "        - required: true\n"
        "          at:\n"
        '            end: "0X000a"\n'
        '            start: "0x000"\n'
        '            space: "$self"\n'
        '          definition: "word"\n'
        '          id: "sample"\n'
        '      id: "facts"\n'
        'part: "common"',
        encoding="utf-8",
    )
    canonicalize(tree)
    assert path.read_text(encoding="utf-8") == (
        'part: "common"\nfact-sets:\n  -\n    id: "facts"\n    fields:\n'
        '      -\n        id: "sample"\n        definition: "word"\n        at:\n'
        '          space: "$self"\n          start: "0x0"\n          end: "0xA"\n'
        "        required: true\n    integrity: []\n"
    )
    assert run_fmt(tree, "--check").returncode == 0


def test_full_line_comments_follow_entries_and_file_boundaries(tree: Path) -> None:
    path = tree / "definitions/word.yaml"
    path.write_text(
        '# Header\nkind: "scalar"\n# Decoder note\ndecoder: "u8le"\n'
        '# Identity note\ndefinition: "word"\n# Trailer\n',
        encoding="utf-8",
    )
    canonicalize(tree)
    assert path.read_text(encoding="utf-8") == (
        '# Header\n# Identity note\ndefinition: "word"\nkind: "scalar"\n'
        '# Decoder note\ndecoder: "u8le"\n# Trailer\n'
    )


def test_comments_inside_scalar_lists_are_retained(tree: Path) -> None:
    path = tree / "ic/demo.yaml"
    path.write_text(
        path.read_text(encoding="utf-8").replace('ic: ["DEMO"]', 'ic:\n  # Name note\n  - "DEMO"'),
        encoding="utf-8",
    )
    canonicalize(tree)
    assert 'ic:\n  # Name note\n  - "DEMO"\n' in path.read_text(encoding="utf-8")
    assert run_fmt(tree, "--check").returncode == 0


@pytest.mark.parametrize("escape", [r"\u0085", r"\u2028", r"\u2029"])
def test_unicode_line_separators_remain_escaped_and_idempotent(tree: Path, escape: str) -> None:
    path = tree / "definitions/word.yaml"
    path.write_text(
        f'definition: "word"\nkind: "scalar"\ndecoder: "text{escape}value"\n', encoding="utf-8"
    )
    canonicalize(tree)
    assert escape in path.read_text(encoding="utf-8")
    before = snapshot(tree)
    canonicalize(tree)
    assert run_fmt(tree, "--check").returncode == 0
    assert snapshot(tree) == before


@pytest.mark.parametrize(
    "hexadecimal,canonical", [("0X000a", "0xA"), ("0x00ff", "0xFF"), ("0X0000", "0x0")]
)
def test_hexadecimal_spelling_is_canonical(tree: Path, hexadecimal: str, canonical: str) -> None:
    path = tree / "ic/demo.yaml"
    path.write_text(
        path.read_text(encoding="utf-8").replace('"0xA"', f'"{hexadecimal}"'), encoding="utf-8"
    )
    canonicalize(tree)
    assert f'sizes: ["{canonical}"]' in path.read_text(encoding="utf-8")
    assert run_fmt(tree, "--check").returncode == 0


def test_lock_contains_exact_raw_byte_hashes_and_direct_sorted_uses(tree: Path) -> None:
    canonicalize(tree)
    entries = read_yaml(tree / "ic-files.lock.yaml")
    assert [entry["path"] for entry in entries] == sorted(
        [
            "definitions/word.yaml",
            "ic/demo.yaml",
            "overrides/demo.yaml",
            "parts/common.yaml",
            "rules/merge.yaml",
            "tools/calc.yaml",
        ]
    )
    uses = {entry["path"]: entry["uses"] for entry in entries}
    assert uses == {
        "definitions/word.yaml": [],
        "ic/demo.yaml": ["parts/common.yaml", "rules/merge.yaml"],
        "overrides/demo.yaml": ["ic/demo.yaml"],
        "parts/common.yaml": ["definitions/word.yaml", "tools/calc.yaml"],
        "rules/merge.yaml": ["tools/calc.yaml"],
        "tools/calc.yaml": [],
    }
    for entry in entries:
        assert list(entry) == ["path", "sha256", "uses"]
        assert entry["sha256"] == hashlib.sha256((tree / entry["path"]).read_bytes()).hexdigest()


def test_override_h3_matches_an_independently_enumerated_closure(tree: Path) -> None:
    canonicalize(tree)
    closure = [
        "definitions/word.yaml",
        "ic/demo.yaml",
        "parts/common.yaml",
        "rules/merge.yaml",
        "tools/calc.yaml",
    ]
    payload = (
        "".join(
            f"{path} {hashlib.sha256((tree / path).read_bytes()).hexdigest()}\n" for path in closure
        )
        + "1\n"
    )
    assert base_hash(tree) == hashlib.sha256(payload.encode("utf-8")).hexdigest()


def test_h3_changes_for_used_bytes_but_not_an_unrelated_source(tree: Path) -> None:
    canonicalize(tree)
    original = base_hash(tree)
    unrelated = tree / "definitions/unrelated.yaml"
    unrelated.write_text(
        'definition: "unrelated"\nkind: "scalar"\ndecoder: "u8le"\n', encoding="utf-8"
    )
    canonicalize(tree)
    assert base_hash(tree) == original
    used = tree / "definitions/word.yaml"
    used.write_bytes(used.read_bytes() + b"# Changed comment is part of H1.\n")
    result = run_fmt(tree, "--check")
    assert result.returncode == 1
    assert "stale override base" in result.stdout
    canonicalize(tree)
    assert base_hash(tree) != original


def test_loader_semantic_version_is_part_of_h3(tree: Path) -> None:
    canonicalize(tree)
    original = base_hash(tree)
    config = json.loads(SCRIPT.with_name("ic_files_refs.json").read_text(encoding="utf-8"))
    config["loaderSemanticVersion"] = "2"
    config_path = tree / "config.json"
    write_json(config_path, config)
    assert run_fmt(tree, "--config", str(config_path)).returncode == 0
    assert base_hash(tree) != original


@pytest.mark.parametrize(
    "stale,reason",
    [
        ("layout", "layout"),
        ("missing", "missing lock entry"),
        ("hash", "stale hash"),
        ("uses", "stale uses"),
        ("base", "stale override base"),
        ("lock-layout", "layout"),
        ("removed", "obsolete lock entry"),
    ],
)
def test_check_exit_one_for_each_stale_case_and_zero_after_repair(
    tree: Path, stale: str, reason: str
) -> None:
    canonicalize(tree)
    lock_path = tree / "ic-files.lock.yaml"
    entries = read_yaml(lock_path)
    if stale == "layout":
        path = tree / "definitions/word.yaml"
        path.write_bytes(b"\xef\xbb\xbf" + path.read_bytes().replace(b"\n", b"\r\n"))
    elif stale == "base":
        path = tree / "overrides/demo.yaml"
        path.write_text(
            path.read_text(encoding="utf-8").replace(base_hash(tree), "0" * 64), encoding="utf-8"
        )
    else:
        if stale == "missing":
            entries.pop(0)
        elif stale == "hash":
            entries[0]["sha256"] = "0" * 64
        elif stale == "uses":
            entries[0]["uses"] = ["tools/calc.yaml"]
        elif stale == "removed":
            entries.append({"path": "definitions/removed.yaml", "sha256": "0" * 64, "uses": []})
        write_json(lock_path, entries)
    before = snapshot(tree)
    result = run_fmt(tree, "--check")
    assert result.returncode == 1, result.stderr
    assert reason in result.stdout
    assert snapshot(tree) == before
    canonicalize(tree)
    assert run_fmt(tree, "--check").returncode == 0


@pytest.mark.parametrize(
    "body,reason,line",
    [
        ('definition: "word"\nkind: "scalar"\nextra: 1\n', "unknown key", 3),
        ('definition: "word"\ndefinition: "other"\n', "duplicate key", 2),
        ('definition: &name "word"\n', "anchors", 1),
        ("definition: *name\n", "aliases", 1),
        ('definition: !custom "word"\n', "tags", 1),
        ('definition: !!str "word"\n', "tags", 1),
        ('definition: "word"\n---\nkind: "scalar"\n', "multiple documents", 2),
        ('definition: "word"\n\tkind: "scalar"\n', "tabs in indentation", 2),
        ('definition: "word"\n<<: {"kind": "scalar"}\n', "merge keys", 2),
        ('definition: "word" # inline\n', "invalid JSON", 1),
        ("definition: |\n  word\n", "multiline", 1),
        ('definition: ["word", &x "bad"]\n', "invalid JSON", 1),
        ('definition: "word"\nkind:\n', "missing indented value", 2),
        ('definition: "word"\n  kind: "scalar"\n', "indentation", 2),
        ('definition: {"x": 1, "x": 2}\n', "duplicate key", 1),
        ('%YAML 1.2\ndefinition: "word"\n', "directives", 1),
        ('"\\q": "word"\n', "invalid JSON mapping key", 1),
        ("definition: 1e999\n", "non-finite", 1),
        ('definition: "\\ud800"\n', "invalid JSON", 1),
        ('"\\ud800": "word"\n', "invalid JSON mapping key", 1),
    ],
)
@pytest.mark.parametrize("check", [False, True])
def test_invalid_yaml_or_unknown_keys_report_file_and_line_without_writes(
    tree: Path, body: str, reason: str, line: int, check: bool
) -> None:
    path = tree / "definitions/word.yaml"
    path.write_text(body, encoding="utf-8")
    before = snapshot(tree)
    result = run_fmt(tree, *(["--check"] if check else []))
    assert result.returncode == 2, (result.stdout, result.stderr)
    assert f"definitions/word.yaml:{line}:" in result.stderr
    assert reason in result.stderr
    assert snapshot(tree) == before


def test_duplicate_global_identity_is_an_error(tree: Path) -> None:
    (tree / "definitions/duplicate.yaml").write_bytes((tree / "definitions/word.yaml").read_bytes())
    result = run_fmt(tree)
    assert result.returncode == 2
    assert "duplicate id 'word'" in result.stderr


def test_tool_with_no_optional_invocations_is_valid(tree: Path) -> None:
    path = tree / "tools/metadata-only.yaml"
    path.write_text('tool: "metadata-only"\ninvocations: []\n', encoding="utf-8")
    canonicalize(tree)
    assert run_fmt(tree, "--check").returncode == 0


def test_duplicate_local_identity_is_an_error(tree: Path) -> None:
    path = tree / "ic/demo.yaml"
    path.write_text(
        path.read_text(encoding="utf-8") + '  -\n    id: "merge-binding"\n    rule: "merge"\n',
        encoding="utf-8",
    )
    result = run_fmt(tree)
    assert result.returncode == 2
    assert "duplicate id 'merge-binding'" in result.stderr


@pytest.mark.parametrize(
    "relative,old,new",
    [
        ("parts/common.yaml", 'definition: "word"', 'definition: "missing"'),
        ("parts/common.yaml", 'run: "finalize"', 'run: "missing"'),
        ("ic/demo.yaml", 'rule: "merge"', 'rule: "missing"'),
        ("ic/demo.yaml", 'file: "parts/common.yaml"', 'file: "../escape.yaml"'),
        ("tools/calc.yaml", 'tool: "calc"\n', 'tool: "missing"\n'),
        ("overrides/demo.yaml", 'ic: "DEMO"', 'ic: "missing"'),
    ],
)
def test_dangling_references_have_locations_and_no_partial_write(
    tree: Path, relative: str, old: str, new: str
) -> None:
    path = tree / relative
    text = path.read_text(encoding="utf-8")
    # For tool metadata, change the nested reference and retain the declaration.
    if relative.startswith("tools/"):
        text = text[: text.rindex(old)] + text[text.rindex(old) :].replace(old, new, 1)
    else:
        text = text.replace(old, new)
    path.write_text(text, encoding="utf-8")
    before = snapshot(tree)
    result = run_fmt(tree)
    assert result.returncode == 2
    assert "dangling reference" in result.stderr
    assert relative + ":" in result.stderr
    assert snapshot(tree) == before


def test_unknown_key_in_schema_ref_reports_its_own_line(tree: Path) -> None:
    path = tree / "parts/common.yaml"
    path.write_text(
        path.read_text(encoding="utf-8").replace(
            '          end: "0xA"', '          surprise: "0xA"'
        ),
        encoding="utf-8",
    )
    result = run_fmt(tree)
    assert result.returncode == 2
    assert "parts/common.yaml:12: unknown key 'surprise'" in result.stderr


def test_optional_override_folder_can_be_absent(tree: Path) -> None:
    shutil.rmtree(tree / "overrides")
    canonicalize(tree)
    assert len(read_yaml(tree / "ic-files.lock.yaml")) == 5
    assert run_fmt(tree, "--check").returncode == 0


def test_missing_override_hash_is_filled_without_a_handwritten_placeholder(tree: Path) -> None:
    path = tree / "overrides/demo.yaml"
    path.write_text('base:\n  -\n    ic: "DEMO"\n', encoding="utf-8")
    canonicalize(tree)
    assert len(base_hash(tree)) == 64
    assert run_fmt(tree, "--check").returncode == 0


def test_field_renames_need_only_schema_and_config_changes(tree: Path) -> None:
    config = json.loads(SCRIPT.with_name("ic_files_refs.json").read_text(encoding="utf-8"))
    config["kinds"]["definition"]["identities"]["definition"] = ["/name"]
    config["kinds"]["part"]["references"]["definition"] = ["/**/source"]
    config_path = tree / "config.json"
    write_json(config_path, config)
    schema_path = tree / "schema/definition.schema.json"
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    schema["properties"] = {
        ("name" if k == "definition" else k): v for k, v in schema["properties"].items()
    }
    schema["required"] = ["name", "kind"]
    write_json(schema_path, schema)
    part_schema = tree / "schema/part.schema.json"
    schema = json.loads(part_schema.read_text(encoding="utf-8"))
    props = schema["$defs"]["facts"]["properties"]["fields"]["items"]["properties"]
    schema["$defs"]["facts"]["properties"]["fields"]["items"]["properties"] = {
        ("source" if k == "definition" else k): v for k, v in props.items()
    }
    write_json(part_schema, schema)
    for relative, old, new in [
        ("definitions/word.yaml", "definition:", "name:"),
        ("parts/common.yaml", "definition:", "source:"),
    ]:
        path = tree / relative
        path.write_text(path.read_text(encoding="utf-8").replace(old, new), encoding="utf-8")
    result = run_fmt(tree, "--config", str(config_path))
    assert result.returncode == 0, result.stderr
    lock = {entry["path"]: entry for entry in read_yaml(tree / "ic-files.lock.yaml")}
    assert "definitions/word.yaml" in lock["parts/common.yaml"]["uses"]
    assert run_fmt(tree, "--config", str(config_path), "--check").returncode == 0


def test_external_local_schema_refs_determine_order(tree: Path) -> None:
    schema_path = tree / "schema/definition.schema.json"
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    common = {"type": "object", "properties": dict(reversed(list(schema["properties"].items())))}
    write_json(tree / "schema/common.json", common)
    write_json(schema_path, {"$ref": "common.json"})
    canonicalize(tree)
    assert (tree / "definitions/word.yaml").read_text(encoding="utf-8").splitlines()[1:] == [
        'decoder: "u8le"',
        'kind: "scalar"',
        'definition: "word"',
    ]


def test_all_of_properties_are_ordered_after_the_base_properties(tree: Path) -> None:
    schema_path = tree / "schema/definition.schema.json"
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    decoder = schema["properties"].pop("decoder")
    schema["allOf"] = [{"properties": {"decoder": decoder}}]
    write_json(schema_path, schema)
    canonicalize(tree)
    assert run_fmt(tree, "--check").returncode == 0


def test_error_late_in_tree_does_not_publish_earlier_layouts_or_lock(tree: Path) -> None:
    earlier = tree / "definitions/word.yaml"
    earlier.write_text('kind: "scalar"\ndefinition: "word"\n', encoding="utf-8")
    later = tree / "tools/calc.yaml"
    later.write_text(later.read_text(encoding="utf-8") + "unknown: true\n", encoding="utf-8")
    before = snapshot(tree)
    assert run_fmt(tree).returncode == 2
    assert snapshot(tree) == before


def test_replacement_failure_rolls_back_prior_files_and_removes_temporaries(
    tree: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    before = snapshot(tree)
    replace = ic_files.os.replace
    count = 0

    def fail_second(source, destination):
        nonlocal count
        count += 1
        if count == 2:
            raise OSError("synthetic replacement failure")
        return replace(source, destination)

    monkeypatch.setattr(ic_files.os, "replace", fail_second)
    with pytest.raises(OSError, match="synthetic replacement failure"):
        ic_files.format_tree(tree, SCRIPT.with_name("ic_files_refs.json"), False)
    assert snapshot(tree) == before


def test_staging_failure_does_not_publish_or_leave_temporaries(
    tree: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    before = snapshot(tree)
    stage = ic_files.stage_bytes
    count = 0

    def fail_second(path, payload):
        nonlocal count
        count += 1
        if count == 2:
            raise OSError("synthetic staging failure")
        return stage(path, payload)

    monkeypatch.setattr(ic_files, "stage_bytes", fail_second)
    with pytest.raises(OSError, match="synthetic staging failure"):
        ic_files.format_tree(tree, SCRIPT.with_name("ic_files_refs.json"), False)
    assert snapshot(tree) == before


def test_recovery_failure_preserves_original_backup_and_reports_its_path(
    tree: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    original = (tree / "overrides/demo.yaml").read_bytes()
    replace = ic_files.os.replace
    count = 0

    def fail_publish_and_recovery(source, destination):
        nonlocal count
        count += 1
        if count in (2, 3):
            raise OSError("synthetic persistent replacement failure")
        return replace(source, destination)

    monkeypatch.setattr(ic_files.os, "replace", fail_publish_and_recovery)
    with pytest.raises(
        ic_files.FormatError, match="recovery incomplete;.*original backup:"
    ) as failure:
        ic_files.format_tree(tree, SCRIPT.with_name("ic_files_refs.json"), False)
    backups = list(tree.rglob(".ic-files-*.tmp"))
    assert len(backups) == 1
    assert backups[0].read_bytes() == original
    assert str(backups[0]) in str(failure.value)
