"""Plan and synchronize explicitly selected, source-derived repository projections."""

from __future__ import annotations

import argparse
import difflib
import os
import re
import stat
import tempfile
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


def default_providers() -> tuple[Provider, ...]:
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
    return (
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


def _snapshot(root: Path, paths: Sequence[str]) -> dict[str, bytes]:
    captured = {}
    for relative in paths:
        _validate_relative(relative)
        path = root / relative
        for component in (path, *path.parents):
            if component == root:
                break
            status = component.lstat()
            if (
                component.is_symlink()
                or getattr(status, "st_file_attributes", 0)
                & stat.FILE_ATTRIBUTE_REPARSE_POINT
            ):
                raise SyncError(f"reparse synchronization path: {relative}")
        if not path.is_file():
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
    paths, outputs, names = set(), set(), set()
    for provider in providers:
        if (
            provider.name in names
            or outputs.intersection(provider.outputs)
            or len(set(provider.outputs)) != len(provider.outputs)
        ):
            raise SyncError("duplicate provider or conflicting output owner")
        if not set(provider.outputs).issubset(provider.inputs):
            raise SyncError(f"{provider.name}: every output requires an input snapshot")
        names.add(provider.name)
        paths.update(provider.inputs)
        outputs.update(provider.outputs)
    if len({path.casefold() for path in paths}) != len(paths):
        raise SyncError("case-alias synchronization paths are not portable")
    before = _snapshot(root, sorted(paths))
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
                    before[relative].decode("utf-8").splitlines(keepends=True),
                    expected[relative].decode("utf-8").splitlines(keepends=True),
                    fromfile=relative,
                    tofile=relative,
                )
            ),
            end="",
        )
    if _snapshot(root, sorted(paths)) != before:
        raise SyncError("synchronization inputs changed while planning")
    if changed and not write:
        print("Derived-file drift: sync the approved providers before verification.")
        return 1
    for relative in changed:
        if _snapshot(root, [relative])[relative] != before[relative]:
            raise SyncError(f"concurrent edit: {relative}")
        path = root / relative
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(
                prefix=f".{path.name}.sync-", dir=path.parent, delete=False
            ) as stream:
                temporary = Path(stream.name)
                stream.write(expected[relative])
                stream.flush()
                os.fsync(stream.fileno())
            os.chmod(temporary, stat.S_IMODE(path.stat().st_mode))
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
    providers = default_providers()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repository", default=str(Path(__file__).resolve().parents[1])
    )
    parser.add_argument(
        "--only", action="append", choices=[provider.name for provider in providers]
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
