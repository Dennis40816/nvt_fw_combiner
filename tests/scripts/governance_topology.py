"""Explicit commit graphs for the governance retirement's three merge shapes."""
from __future__ import annotations

import json
from pathlib import Path
from typing import Callable

from tests.scripts.test_frozen_evidence_pins import CHECKPOINT, DIRECTORIES, git, write_pins


def commit(root: Path, message: str) -> str:
    git(root, "add", "-A")
    git(root, "commit", "-qm", message)
    return git(root, "rev-parse", "HEAD")


def product(root: Path, name: str) -> str:
    path = root / "docs" / (name + ".md")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(name + "\n", encoding="utf-8", newline="\n")
    return commit(root, name)


def seal(root: Path, task: str) -> tuple[str, str]:
    """Two real commits; only legacy runs inject the old record admission helper."""
    path = root / DIRECTORIES[0] / (task + ".json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"taskId": task, "state": "design-active"}) + "\n", encoding="utf-8", newline="\n")
    implementation = product(root, task)
    path.write_text(json.dumps({"taskId": task, "state": "final-complete"}) + "\n", encoding="utf-8", newline="\n")
    evidence = commit(root, "Seal " + task)
    assert git(root, "rev-parse", evidence + "^") == implementation
    return implementation, evidence


def tree(root: Path, revision: str) -> str:
    return git(root, "rev-parse", revision + "^{tree}")


def merge(root: Path, branch: str) -> str:
    git(root, "merge", "--no-ff", "-qm", "Topology merge " + branch, branch)
    return git(root, "rev-parse", "HEAD")


def ancestor(root: Path, before: str, after: str) -> bool:
    import subprocess
    try:
        git(root, "merge-base", "--is-ancestor", before, after)
    except subprocess.CalledProcessError as error:
        if error.returncode != 1:
            raise
        return False
    return True


def build_shape(root: Path, shape: str,
                seal_task: Callable[[Path, str], tuple[str, str]] = seal) -> dict[str, str]:
    """Build the pre-freeze graph. A commander may inject the base legacy harness."""
    c0 = git(root, "rev-parse", "HEAD")
    refs = {"C0": c0}
    git(root, "branch", "topo-main", c0)
    if shape in {"a", "b"}:
        git(root, "checkout", "-qb", "topo-rel", c0)
        refs["I1"], refs["E1"] = seal_task(root, "REL-01")
        git(root, "checkout", "-q", "topo-main")
        refs["M1"] = merge(root, "topo-rel")
        assert tree(root, refs["M1"]) == tree(root, refs["E1"])
        if shape == "a":
            git(root, "checkout", "-q", "topo-rel")
            refs["I2"], refs["E2"] = seal_task(root, "FIX-01")
            assert not ancestor(root, refs["M1"], refs["E2"])
            refs["R5"] = merge(root, "topo-main")
            assert tree(root, refs["R5"]) == tree(root, refs["E2"])
            assert tree(root, refs["R5"]) != tree(root, refs["M1"])
            refs["pre_base"] = refs["M1"]
        else:
            git(root, "checkout", "-qb", "topo-trunk", c0)
            refs["I2"], refs["E2"] = seal_task(root, "TRUNK-01")
            assert not ancestor(root, refs["M1"], refs["E2"])
            assert not ancestor(root, refs["E2"], refs["M1"])
            refs["T3"] = merge(root, "topo-main")
            assert tree(root, refs["T3"]) not in {tree(root, refs["E2"]), tree(root, refs["M1"])}
            refs["pre_base"] = refs["E2"]
    elif shape == "c":
        git(root, "checkout", "-qb", "topo-wave2", c0)
        refs["W1"] = product(root, "notes")
        git(root, "checkout", "-qb", "topo-trunk", c0)
        refs["I1"], refs["E1"] = seal_task(root, "SEAL-01")
        git(root, "checkout", "-q", "topo-wave2")
        refs["W2"] = merge(root, "topo-trunk")
        assert tree(root, refs["W2"]) not in {tree(root, refs["W1"]), tree(root, refs["E1"])}
        refs["pre_base"] = refs["E1"]
    else:
        raise ValueError(shape)
    refs["pre_head"] = git(root, "rev-parse", "HEAD")
    assert ancestor(root, refs["pre_base"], refs["pre_head"])
    parents = git(root, "show", "-s", "--format=%P", "HEAD").split()
    assert len(parents) == 2
    return refs


def freeze_and_replay(root: Path, shape: str, refs: dict[str, str], mutation: str = "none") -> dict[str, str]:
    for directory in DIRECTORIES:
        folder = root / directory
        folder.mkdir(parents=True, exist_ok=True)
        (folder / "README.md").write_text("Historical frozen evidence.\n", encoding="utf-8", newline="\n")
    checkpoint = root / CHECKPOINT
    if not checkpoint.exists():
        checkpoint.write_bytes(b'{}\n')
    (root / "docs/freeze-product.md").write_bytes(b'freeze-product\n')
    git(root, "add", "-A")
    write_pins(root)
    refs["F"] = commit(root, "Freeze evidence")
    working = git(root, "branch", "--show-current")
    # Both sides descend from F; shape a preserves the tree-transparent merge.
    other = "topo-main" if shape in {"a", "b"} else "topo-trunk"
    git(root, "checkout", "-q", other)
    if shape == "a":
        refs["M2"] = merge(root, working)
    else:
        merge(root, working)
        refs["M2" if shape == "b" else "P1"] = product(root, "post-freeze-other")
    refs["base"] = git(root, "rev-parse", "HEAD")
    git(root, "checkout", "-q", working)
    refs["P" if shape == "a" else "P1" if shape == "b" else "W3"] = product(root, "post-freeze-working")
    frozen_file = root / DIRECTORIES[0] / ("SEAL-01.json" if shape == "c" else "REL-01.json")
    original = frozen_file.read_bytes()
    if mutation != "none":
        frozen_file.write_bytes(b'{"changed":true}\n')
        commit(root, "Change frozen evidence")
        if mutation == "restored":
            frozen_file.write_bytes(original)
            commit(root, "Restore frozen evidence")
    prior_tree = tree(root, "HEAD")
    refs["head"] = merge(root, other)
    refs[{"a": "R6", "b": "T4", "c": "W4"}[shape]] = refs["head"]
    assert ancestor(root, refs["base"], refs["head"])
    if shape == "a":
        assert tree(root, refs["head"]) == prior_tree
    else:
        assert tree(root, refs["head"]) not in {prior_tree, tree(root, refs["base"])}
    refs["frozen_file"] = frozen_file.relative_to(root).as_posix()
    return refs
