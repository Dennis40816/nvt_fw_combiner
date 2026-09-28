"""Plan and synchronize explicitly selected, source-derived repository projections."""

from __future__ import annotations

import argparse
import difflib
import json
import os
import re
import stat
import tempfile
import tomllib
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from types import MappingProxyType
from typing import Callable, Mapping, Sequence


class SyncError(RuntimeError):
    """A local synchronization plan cannot safely be applied."""


@dataclass(frozen=True)
class Provider:
    name: str
    inputs: tuple[str, ...]
    outputs: tuple[str, ...]
    plan: Callable[[Mapping[str, bytes]], Mapping[str, bytes]]
    creatable_outputs: tuple[str, ...] = ()


def default_providers(
    root: Path | None = None, *, include_claude: bool = True
) -> tuple[Provider, ...]:
    try:
        from scripts import v0916_parity_certification as parity
        from scripts import release_source_pins as source_pins
        from scripts.render_release_notes import STABLE_VERSION
    except ModuleNotFoundError as error:
        if error.name != "scripts":
            raise
        import v0916_parity_certification as parity
        import release_source_pins as source_pins
        from render_release_notes import STABLE_VERSION
    source = ".github/workflows/ci.yml"
    target = "docs/ci/workflow-templates/ci.yml"
    providers = (
        Provider(
            "v0916-workflow-contract",
            parity.WORKFLOW_SYNC_INPUTS,
            parity.WORKFLOW_SYNC_INPUTS[1:],
            parity.plan_workflow_contract_sync,
        ),
        Provider(
            "ci-template-mirror",
            (source, target),
            (target,),
            lambda before: {target: before[source]},
        ),
        Provider(
            "reviewed-source-pins",
            source_pins.SOURCE_PIN_INPUTS,
            source_pins.SOURCE_PIN_OUTPUTS,
            source_pins.plan_reviewed_source_pins,
        ),
        Provider(
            "release-version-headers",
            ("VERSION", "SPEC.md", "docs/references/verification-report.md"),
            ("SPEC.md", "docs/references/verification-report.md"),
            lambda before: _plan_release_version_headers(before, STABLE_VERSION),
        ),
    )
    if include_claude:
        providers += (claude_projection_provider(root or Path(__file__).resolve().parents[1]),)
    return providers


def claude_projection_provider(root: Path) -> Provider:
    """Project canonical skill and role sources; never own source deletion."""
    root = root.resolve(strict=True)
    manifest = ".agents/skills/manifest.json"
    manifest_raw = _snapshot(root, [manifest])[manifest]
    entries = json.loads(manifest_raw)["skills"]
    names = [entry["name"] for entry in entries]
    if len(names) != len(set(names)) or any(
        re.fullmatch(r"nfc-[a-z0-9-]+", name) is None for name in names
    ):
        raise SyncError("invalid Claude projection skill inventory")
    _checked_path(root, ".codex/agents")
    agents = tuple(sorted(path.relative_to(root).as_posix()
                          for path in (root / ".codex/agents").glob("*.toml")))
    skills = tuple(f".agents/skills/{name}/SKILL.md" for name in names)
    outputs = tuple(f".claude/skills/{name}/SKILL.md" for name in names) + tuple(
        f".claude/agents/{Path(path).stem}.md" for path in agents
    )

    def plan(before: Mapping[str, bytes]) -> Mapping[str, bytes]:
        if before[manifest] != manifest_raw:
            raise SyncError("Claude manifest changed after inventory discovery")
        _checked_path(root, ".codex/agents")
        if tuple(sorted(path.relative_to(root).as_posix()
                        for path in (root / ".codex/agents").glob("*.toml"))) != agents:
            raise SyncError("Claude agent inventory changed after discovery")
        planned = {}
        for entry, source, target in zip(entries, skills, outputs):
            raw = before[source]
            if not raw.startswith(b"---\n") or b"\n---\n" not in raw[4:]:
                raise SyncError(f"invalid canonical skill frontmatter: {source}")
            if entry["invocation"] == "explicit":
                raw = raw.replace(b"\n---\n", b"\ndisable-model-invocation: true\n---\n", 1)
            elif entry["invocation"] != "implicit":
                raise SyncError(f"invalid invocation: {source}")
            planned[target] = raw
        for source, target in zip(agents, outputs[len(skills):]):
            role = tomllib.loads(before[source].decode("utf-8"))
            # JSON scalars are also YAML scalars, preserving quotes/newlines safely.
            header = "---\n" + "".join(
                f"{key}: {json.dumps(role[key], ensure_ascii=False)}\n"
                for key in ("name", "description")
            )
            if role["sandbox_mode"] == "read-only":
                header += "tools: Read, Grep, Glob\n"
            planned[target] = (header + "---\n\n" + role["developer_instructions"] + "\n").encode("utf-8")
        return planned

    return Provider("claude-projections", (manifest, *skills, *agents, *outputs),
                    outputs, plan, creatable_outputs=outputs)


def _plan_release_version_headers(
    before: Mapping[str, bytes], version_pattern: re.Pattern[str]
) -> Mapping[str, bytes]:
    version = before["VERSION"].decode("utf-8").strip()
    if version_pattern.fullmatch(version) is None:
        raise SyncError("release-version-headers: invalid stable VERSION")
    planned = {}
    for path, marker in (
        ("SPEC.md", "> 文件版本："),
        ("docs/references/verification-report.md", "Specification package version: "),
    ):
        raw = before[path]
        matches = list(
            re.finditer(rb"(?m)^" + re.escape(marker.encode()) + rb"([^\r\n]*)", raw)
        )
        if len(matches) != 1:
            raise SyncError(f"{path}: expected exactly one version header")
        match = matches[0]
        token = match.group(1)
        if (
            not token.startswith(b"`")
            or not token.endswith(b"`")
            or version_pattern.fullmatch(token[1:-1].decode("utf-8")) is None
        ):
            raise SyncError(f"{path}: malformed stable-version header")
        planned[path] = (
            raw[: match.start(1) + 1] + version.encode() + raw[match.end(1) - 1 :]
        )
    return planned


def _validate_relative(relative: str) -> None:
    path = PurePosixPath(relative)
    if (
        not relative
        or path.is_absolute()
        or ":" in relative
        or "\\" in relative
        or any(part in ("", ".", "..") for part in relative.split("/"))
    ):
        raise SyncError(f"unsafe synchronization path: {relative}")


def _checked_path(root: Path, relative: str) -> Path | None:
    """Inspect parents first, including parents of an absent creatable output."""
    _validate_relative(relative)
    path = root
    for part in PurePosixPath(relative).parts:
        path = path / part
        try:
            status = path.lstat()
        except FileNotFoundError:
            return None
        if path.is_symlink() or getattr(status, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
            raise SyncError(f"reparse synchronization path: {relative}")
    return path


def _snapshot(
    root: Path, paths: Sequence[str], creatable: Sequence[str] = ()
) -> dict[str, bytes | None]:
    captured = {}
    for relative in paths:
        path = _checked_path(root, relative)
        if path is None and relative in creatable:
            captured[relative] = None
            continue
        if path is None or not path.is_file():
            raise SyncError(f"missing synchronization input: {relative}")
        captured[relative] = path.read_bytes()
    return captured


def _plan(
    providers: Sequence[Provider], before: Mapping[str, bytes]
) -> dict[str, bytes]:
    expected = dict(before)
    for provider in providers:
        planned = provider.plan(
            MappingProxyType({path: before[path] for path in provider.inputs})
        )
        if set(planned) != set(provider.outputs) or any(
            not isinstance(raw, bytes) for raw in planned.values()
        ):
            raise SyncError(f"{provider.name}: missing or undeclared output")
        expected.update(planned)
    return expected


def synchronize(
    root: Path, providers: Sequence[Provider], *, write: bool = False
) -> int:
    if write and any(
        os.environ.get(key, "").strip().lower() not in ("", "false", "0")
        for key in ("CI", "GITHUB_ACTIONS")
    ):
        raise SyncError("derived synchronization cannot write in CI")
    root = root.resolve(strict=True)
    paths, outputs, names, creatable = set(), set(), set(), set()
    for provider in providers:
        if (
            provider.name in names
            or outputs.intersection(provider.outputs)
            or len(set(provider.outputs)) != len(provider.outputs)
        ):
            raise SyncError("duplicate provider or conflicting output owner")
        if not set(provider.outputs).issubset(provider.inputs):
            raise SyncError(f"{provider.name}: every output requires an input snapshot")
        if not set(provider.creatable_outputs).issubset(provider.outputs) or any(
            not path.startswith(".claude/") for path in provider.creatable_outputs
        ):
            raise SyncError(f"{provider.name}: creatable outputs must belong below .claude/")
        names.add(provider.name)
        paths.update(provider.inputs)
        outputs.update(provider.outputs)
        creatable.update(provider.creatable_outputs)
    if len({path.casefold() for path in paths}) != len(paths):
        raise SyncError("case-alias synchronization paths are not portable")
    before = _snapshot(root, sorted(paths), creatable)
    expected = _plan(providers, before)
    if _plan(providers, expected) != expected:
        raise SyncError(
            "providers do not produce a converged plan; no files were written"
        )
    changed = [path for path in sorted(outputs) if before[path] != expected[path]]
    for relative in changed:
        print(
            "".join(
                difflib.unified_diff(
                    (before[relative] or b"").decode("utf-8").splitlines(keepends=True),
                    expected[relative].decode("utf-8").splitlines(keepends=True),
                    fromfile=relative,
                    tofile=relative,
                )
            ),
            end="",
        )
    if _snapshot(root, sorted(paths), creatable) != before:
        raise SyncError("synchronization inputs changed while planning")
    if changed and not write:
        print("Derived-file drift: sync the approved providers before verification.")
        return 1
    for relative in changed:
        if _snapshot(root, [relative], creatable)[relative] != before[relative]:
            raise SyncError(f"concurrent edit: {relative}")
        path = root / relative
        if before[relative] is None:
            for parent in reversed(path.parents):
                if parent == root or root not in parent.parents:
                    continue
                parent_relative = parent.relative_to(root).as_posix()
                if _checked_path(root, parent_relative) is None:
                    parent.mkdir(exist_ok=True)
                _checked_path(root, parent_relative)
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(
                prefix=f".{path.name}.sync-", dir=path.parent, delete=False
            ) as stream:
                temporary = Path(stream.name)
                stream.write(expected[relative])
                stream.flush()
                os.fsync(stream.fileno())
            if _snapshot(root, [relative], creatable)[relative] != before[relative]:
                raise SyncError(f"concurrent edit: {relative}")
            os.chmod(temporary, 0o644 if before[relative] is None else stat.S_IMODE(path.stat().st_mode))
            if before[relative] is None:
                # Atomic no-clobber publication: a racing creator keeps its file.
                os.link(temporary, path)
            else:
                os.replace(temporary, path)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    after = _snapshot(root, sorted(paths))
    if after != expected or _plan(providers, after) != after:
        raise SyncError("synchronization did not converge; inspect the local diff")
    print(
        f"Derived files synchronized ({len(changed)} files changed); no staging or approval performed."
    )
    return 0


def main(argv: Sequence[str] | None = None) -> int:
    providers = default_providers(include_claude=False)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repository", default=str(Path(__file__).resolve().parents[1])
    )
    parser.add_argument(
        "--only", action="append", choices=[provider.name for provider in providers] + ["claude-projections"]
    )
    parser.add_argument(
        "--write",
        action="store_true",
        help="sync explicitly selected, already approved changes locally",
    )
    parser.add_argument(
        "--list", action="store_true", help="list the fixed provider/target inventory"
    )
    args = parser.parse_args(argv)
    if args.write and not args.only:
        raise SyncError(
            "--write requires --only for each approved provider; no implicit trust-pin refresh"
        )
    if not args.only or "claude-projections" in args.only:
        providers += (claude_projection_provider(Path(args.repository)),)
    selected = [
        provider
        for provider in providers
        if not args.only or provider.name in args.only
    ]
    if args.list:
        if args.write:
            raise SyncError("--list and --write cannot be combined")
        for provider in selected:
            print(f"{provider.name}: {', '.join(provider.outputs)}")
        return 0
    return synchronize(Path(args.repository), selected, write=args.write)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(
            f"Derived synchronization failed; inspect local diff: {error}",
            file=os.sys.stderr,
        )
        raise SystemExit(2)
