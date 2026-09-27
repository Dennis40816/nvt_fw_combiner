"""Retired history constraints must not reject unchanged frozen merge snapshots."""
import json
import pytest
import tempfile
from pathlib import Path

from tests.scripts.governance_topology import build_shape, freeze_and_replay
from tests.scripts.test_frozen_evidence_pins import git, validator


@pytest.fixture
def repository(tmp_path):
    git(tmp_path, "init", "-q")
    git(tmp_path, "config", "user.name", "Fixture")
    git(tmp_path, "config", "user.email", "fixture@example.invalid")
    git(tmp_path, "config", "core.autocrlf", "false")
    (tmp_path / ".gitattributes").write_bytes(b'*.json text eol=lf\n*.md text eol=lf\n')
    git(tmp_path, "add", ".")
    git(tmp_path, "commit", "-qm", "C0")
    return tmp_path


@pytest.mark.parametrize("shape", ["a", "b", "c"])
@pytest.mark.parametrize("mutation", ["none", "kept", "restored"])
def test_frozen_snapshot_survives_merge_shapes(repository, shape, mutation):
    refs = freeze_and_replay(repository, shape, build_shape(repository, shape), mutation)
    errors = []
    validator.validate_frozen_evidence_pins(repository, errors)
    if mutation == "kept":
        path = "docs/governance/change-records"
        pins = json.loads((repository / "docs/governance/frozen-evidence-pins.json").read_text())
        pinned = next(pin["id"] for pin in pins["pins"] if pin["path"] == path)
        actual = git(repository, "--no-replace-objects", "rev-parse", f"HEAD:{path}")
        assert pinned != actual
        assert errors == [f"frozen evidence pins: {path}: HEAD differs from frozen pin: "
                          f"pinned=040000 tree {pinned}; actual=040000 tree {actual}"]
    else:
        assert errors == []


@pytest.fixture(scope="module", params=["a", "b", "c"], ids=["topo-a", "topo-b", "topo-c"])
def entry_topology(request):
    from tests.scripts.structure_entry_audit import create_entry_checkout, entry_path
    with tempfile.TemporaryDirectory(prefix="gt-", dir=entry_path(tempfile.gettempdir())) as directory:
        root = create_entry_checkout(Path(__file__).resolve().parents[2], Path(directory) / "r")
        git(root, "config", "user.name", "Fixture")
        git(root, "config", "user.email", "fixture@example.invalid")
        refs = freeze_and_replay(root, request.param, build_shape(root, request.param))
        yield root, refs


def test_real_entry_accepts_post_freeze_topology(entry_topology):
    from tests.scripts.structure_entry_audit import run_audited
    root, refs = entry_topology
    result = run_audited(root)
    assert result.returncode == 0, result.stderr
    assert git(root, "merge-base", "--is-ancestor", refs["base"], "HEAD") == ""


@pytest.mark.parametrize("control", ["unbounded", "system", "python", "import", "foreign", "repeated"])
def test_entry_controls_remain_fail_closed_on_topologies(entry_topology, control):
    from tests.scripts.test_structure_entry_audit import test_caught_launch_regressions_remain_visible_to_parent
    root, _ = entry_topology
    test_caught_launch_regressions_remain_visible_to_parent(root, control)


@pytest.mark.parametrize("broken", ["runpy", "path", "argv"])
def test_broken_bootstraps_fail_on_topology_heads(entry_topology, tmp_path, broken):
    from tests.scripts.test_structure_entry_audit import test_launch_control_rejects_broken_bootstrap
    root, _ = entry_topology
    test_launch_control_rejects_broken_bootstrap(root, tmp_path, broken)
