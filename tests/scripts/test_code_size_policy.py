"""ADR 0080 dynamic hotspot enrollment and physical aggregate regressions."""

from pathlib import Path

import pytest

from scripts import code_size_policy as policy


@pytest.fixture
def root(tmp_path):
    return tmp_path


def write(root, path, text):
    target = root / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")


def sized_type(root, lines, *, name="Hot", declaration="class", file="Hot.cs"):
    write(root, "src/Product/" + file,
          f"namespace Product;\npublic {declaration} {name} {{\n"
          + "// body\n" * (lines - 3) + "}\n")


def test_unenrolled_type_at_threshold_requires_owner_approved_enrollment(root):
    sized_type(root, 2000)
    errors = policy.validate_code_size_policy(root, {})
    assert len(errors) == 1
    assert "Product.Hot" in errors[0] and "enroll" in errors[0]
    assert "owner" in errors[0]


@pytest.mark.parametrize("lines,action", [(2001, "raise"), (1999, "lower"), (1499, "remove")])
def test_baseline_tracks_measurement_and_exit(root, lines, action):
    sized_type(root, lines)
    errors = policy.validate_code_size_policy(root, {"Product.Hot": 2000})
    assert len(errors) == 1 and action in errors[0]
    assert ("owner approval" in errors[0]) == (action == "raise")


@pytest.mark.parametrize("lines", [1500, 1750, 1999, 2000, 2200])
def test_enrolled_exact_baseline_is_valid_including_retention_band(root, lines):
    sized_type(root, lines)
    assert policy.validate_code_size_policy(root, {"Product.Hot": lines}) == []


@pytest.mark.parametrize("lines", [1499, 1500, 1999])
def test_unenrolled_type_below_entry_threshold_is_advisory(root, lines):
    sized_type(root, lines)
    assert policy.validate_code_size_policy(root, {}) == []


def test_removed_type_must_be_removed_from_enrollment(root):
    assert "remove" in policy.validate_code_size_policy(root, {"Product.Gone": 2000})[0]


def test_reentry_requires_enrollment_after_exit(root):
    sized_type(root, 1499)
    assert "remove" in policy.validate_code_size_policy(root, {"Product.Hot": 2000})[0]
    assert policy.validate_code_size_policy(root, {}) == []
    sized_type(root, 2000)
    assert "enroll" in policy.validate_code_size_policy(root, {})[0]


@pytest.mark.parametrize("declaration", ["class", "partial class", "struct", "record", "record struct", "interface", "enum"])
def test_single_file_types_are_measured(root, declaration):
    sized_type(root, 2000, declaration=declaration)
    aggregate, = policy.measure_code_size(root).type_aggregates
    assert (aggregate.name, aggregate.file_count, aggregate.nonblank_lines) == ("Product.Hot", 1, 2000)
    assert "enroll" in policy.validate_code_size_policy(root, {})[0]


def test_split_and_two_type_files_count_whole_file_once_for_each_type(root):
    write(root, "src/Product/One.cs", "namespace Product;\npartial class A {}\npartial class A {}\nclass B {}\n\n")
    write(root, "src/Product/Two.cs", "namespace Product;\npartial class A {}\n")
    aggregates = {a.name: (a.file_count, a.nonblank_lines) for a in policy.measure_code_size(root).type_aggregates}
    assert aggregates == {"Product.A": (2, 6), "Product.B": (1, 4)}


def test_namespace_nested_and_generic_identities_do_not_collide(root):
    write(root, "src/Product/One.cs", "namespace First { class Outer { class Item {} } class Item {} }\nnamespace Second { class Item<T> {} class Item {} }\n")
    names = {a.name for a in policy.measure_code_size(root).type_aggregates}
    assert names == {"First.Outer", "First.Outer.Item", "First.Item", "Second.Item`1", "Second.Item"}


def test_comments_and_literals_do_not_declare_types(root):
    write(root, "src/Product/One.cs", 'namespace Product;\nclass Real { string x = "class Fake {}"; }\n// class Comment {}\n/* record Another {} */\n')
    assert [a.name for a in policy.measure_code_size(root).type_aggregates] == ["Product.Real"]


def test_global_types_and_delegates_are_measured(root):
    write(root, "src/Product/One.cs", "class Global {}\npublic delegate void Callback(int x);\n")
    assert {a.name for a in policy.measure_code_size(root).type_aggregates} == {"Global", "Callback"}


@pytest.mark.parametrize("declaration,name", [
    ("public delegate (int X, int Y) Callback();", "Product.Callback"),
    ("public delegate System.Func<(int X, int Y)> Callback();", "Product.Callback"),
    ("public delegate System.Func<(T X, T Y)> Callback<T>();", "Product.Callback`1"),
    ("public delegate ref readonly (int X, int Y) Callback();", "Product.Callback"),
    ("public delegate ref readonly (T X, T Y) Callback<T>();", "Product.Callback`1"),
    ("public delegate ref (int X, int Y) Callback();", "Product.Callback"),
    ("public delegate ref readonly int Callback();", "Product.Callback"),
])
def test_tuple_return_delegate_is_enrolled_by_its_declared_name(root, declaration, name):
    write(root, "src/Product/Delegate.cs", "namespace Product;\n" + declaration + "\n" + "// body\n" * 1998)
    aggregate, = policy.measure_code_size(root).type_aggregates
    assert (aggregate.name, aggregate.nonblank_lines) == (name, 2000)
    errors = policy.validate_code_size_policy(root, {})
    assert len(errors) == 1 and name in errors[0] and "enroll" in errors[0]


def test_anonymous_delegate_is_not_a_type(root):
    write(root, "src/Product/Worker.cs", "namespace Product;\nclass Worker { System.Action<int> Run = delegate(int x) { }; }\n")
    assert [a.name for a in policy.measure_code_size(root).type_aggregates] == ["Product.Worker"]


def test_generated_and_build_directories_are_excluded(root):
    sized_type(root, 2000)
    for directory in ("obj", "bin", "generated", "Generated", "artifacts"):
        write(root, f"src/Product/{directory}/Hidden.cs", "namespace Product;\nclass Hidden {}\n")
    write(root, "src/Product/Hidden.g.cs", "namespace Product;\nclass Generated {}\n")
    write(root, "tests/Outside.cs", "class TestOnly {}\n")
    assert [a.name for a in policy.measure_code_size(root).type_aggregates] == ["Product.Hot"]


def test_every_other_measurement_has_one_advisory_block(root):
    sized_type(root, 1999)
    write(root, "src/Product/View.axaml", "<View/>\n")
    write(root, "tools/crc-worker/src/worker.py", "x = 1\n")
    write(root, "profiles/a.json", "{}\n")
    write(root, "docs/contracts/b.json", "{}\n")
    findings = policy.review_code_size_policy(root)
    assert len(findings) == 1
    assert "2000 nonblank" in findings[0]
    assert "runtime" in findings[0] and "duplicate JSON" in findings[0]
    assert policy.validate_code_size_policy(root, {}) == []


def test_repository_enrollment_matches_current_measurement():
    repository = Path(__file__).resolve().parents[2]
    assert policy.validate_code_size_policy(repository) == []
