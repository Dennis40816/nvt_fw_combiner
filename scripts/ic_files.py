"""Format restricted IC YAML and generate H1/H2 and override H6.

No third-party packages are needed. Schema properties own mapping order. The
adjacent config owns identity/reference selectors and the loader semantic version.
Selectors are JSON-pointer paths with * (one component) and ** (any depth).

The YAML subset is block mappings/sequences and JSON flow values. Compact mapping
sequence entries and plain scalars are accepted and emitted in the draft's style.
Full-line comments stay with the following entry; headers and trailers stay at the
file boundaries. Inline comments, multiline scalars and YAML graph/tag constructs
are rejected. This is an authoring formatter, not a firmware/schema validator.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import hashlib
import json
import math
import os
from pathlib import Path
import re
import sys
import tempfile
from typing import Any


class FormatError(Exception):
    def __init__(self, path: Path | str, line: int, message: str):
        super().__init__(f"{path}:{line}: {message}")


@dataclass
class Node:
    value: Any
    line: int = 1
    before: list[str] = field(default_factory=list)
    key_lines: dict[str, int] = field(default_factory=dict)

    def plain(self) -> Any:
        if isinstance(self.value, dict):
            return {key: child.plain() for key, child in self.value.items()}
        if isinstance(self.value, list):
            return [child.plain() for child in self.value]
        return self.value


def node_from(value: Any, line: int = 1) -> Node:
    if isinstance(value, dict):
        return Node({key: node_from(child, line) for key, child in value.items()}, line)
    if isinstance(value, list):
        return Node([node_from(child, line) for child in value], line)
    if isinstance(value, str) and re.fullmatch(r"0[xX][0-9a-fA-F]+", value):
        value = f"0x{int(value, 16):X}"
    return Node(value, line)


def unique_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate key {key!r}")
        result[key] = value
    return result


def check_json_scalars(value: Any) -> None:
    if isinstance(value, str):
        value.encode("utf-8")
    elif isinstance(value, float) and not math.isfinite(value):
        raise ValueError("non-finite numbers are forbidden")
    elif isinstance(value, dict):
        for key, child in value.items():
            check_json_scalars(key)
            check_json_scalars(child)
    elif isinstance(value, list):
        for child in value:
            check_json_scalars(child)


@dataclass
class Row:
    indent: int
    text: str
    line: int
    comments: list[str]


KEY = r'(?:[A-Za-z0-9_.-]+|"(?:[^"\\]|\\.)*")'
ENTRY = re.compile(rf"({KEY}):(?:[ ]+(.*))?$")


class YamlDocument:
    def __init__(self, path: Path | str, raw: bytes):
        self.path = path
        self.raw = raw
        self.header: list[str] = []
        self.trailer: list[str] = []
        self.rows: list[Row] = []
        try:
            text = raw.decode("utf-8-sig")
        except UnicodeDecodeError as error:
            raise FormatError(path, raw[: error.start].count(b"\n") + 1, "invalid UTF-8") from error
        pending: list[str] = []
        for number, line in enumerate(text.splitlines(), 1):
            leading = line[: len(line) - len(line.lstrip())]
            if "\t" in leading:
                raise FormatError(path, number, "tabs in indentation are forbidden")
            stripped = line.strip()
            if not stripped:
                continue
            if stripped.startswith("#"):
                pending.append(stripped)
                continue
            if stripped in ("---", "...") or stripped.startswith("--- "):
                raise FormatError(
                    path, number, "document markers / multiple documents are forbidden"
                )
            if stripped.startswith("%"):
                raise FormatError(path, number, "YAML directives are forbidden")
            if not self.rows:
                self.header, pending = pending, []
            indent = len(leading)
            self.rows.append(Row(indent, stripped, number, pending))
            pending = []
            if stripped.startswith("- ") and ENTRY.fullmatch(stripped[2:]):
                self.rows[-1].text = "-"
                self.rows.append(Row(indent + 2, stripped[2:], number, []))
        self.trailer = pending
        if not self.rows:
            raise FormatError(path, 1, "empty YAML document")
        if len(self.rows) == 1 and self.rows[0].text.startswith(("[", "{")):
            self.root, end = self.scalar(self.rows[0].text, self.rows[0]), 1
        else:
            self.root, end = self.block(0, self.rows[0].indent)
        if end != len(self.rows):
            self.fail(self.rows[end], "unexpected indentation or trailing content")

    def fail(self, row: Row, message: str) -> None:
        raise FormatError(self.path, row.line, message)

    def scalar(self, text: str, row: Row) -> Node:
        if text.startswith(("&", "*", "!", "|", ">", "'")):
            self.fail(
                row, "anchors, aliases, tags and multiline/single-quoted scalars are forbidden"
            )
        try:
            if (
                text.startswith(('"', "[", "{"))
                or text in ("true", "false", "null")
                or re.fullmatch(r"-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?", text)
            ):
                value = json.loads(text, object_pairs_hook=unique_pairs)
            else:
                if (
                    " #" in text
                    or not text
                    or text.startswith(("?", ":", "@", "`"))
                    or ": " in text
                ):
                    self.fail(row, "unsupported YAML scalar or inline comment")
                value = text
            check_json_scalars(value)
        except (ValueError, TypeError) as error:
            self.fail(row, f"invalid JSON flow/scalar value: {error}")
        return node_from(value, row.line)

    def block(self, start: int, indent: int) -> tuple[Node, int]:
        first = self.rows[start]
        sequence = first.text == "-" or first.text.startswith("- ")
        result = Node([] if sequence else {}, first.line)
        index = start
        while index < len(self.rows) and self.rows[index].indent == indent:
            row = self.rows[index]
            is_item = row.text == "-" or row.text.startswith("- ")
            if is_item != sequence:
                self.fail(row, "mixed mapping and sequence")
            if sequence:
                tail = row.text[1:].strip()
                key = None
            else:
                match = ENTRY.fullmatch(row.text)
                if match is None:
                    self.fail(
                        row,
                        "unsupported YAML mapping (anchors, aliases, tags and merge keys are forbidden)",
                    )
                try:
                    key = json.loads(match[1]) if match[1].startswith('"') else match[1]
                    check_json_scalars(key)
                except ValueError as error:
                    self.fail(row, f"invalid JSON mapping key: {error}")
                if key in result.value:
                    self.fail(row, f"duplicate key {key!r}")
                tail = match[2] or ""
            index += 1
            if tail:
                value = self.scalar(tail, row)
            else:
                if index == len(self.rows) or self.rows[index].indent <= indent:
                    self.fail(row, "missing indented value (write null explicitly)")
                value, index = self.block(index, self.rows[index].indent)
            value.before = row.comments
            if sequence:
                result.value.append(value)
            else:
                result.value[key] = value
                result.key_lines[key] = row.line
        return result, index

    def render(self) -> bytes:
        lines = [*self.header]

        def emit(node: Node, indent: int) -> None:
            pad = " " * indent
            if isinstance(node.value, dict):
                for key, value in node.value.items():
                    lines.extend(pad + comment for comment in value.before)
                    spelling = key if re.fullmatch(r"[A-Za-z0-9_.-]+", key) else json_spelling(key)
                    if expanded(value):
                        lines.append(f"{pad}{spelling}:")
                        emit(value, indent + 2)
                    else:
                        lines.append(f"{pad}{spelling}: {flow(value)}")
            elif isinstance(node.value, list):
                for value in node.value:
                    lines.extend(pad + comment for comment in value.before)
                    if expanded(value):
                        lines.append(pad + "-")
                        emit(value, indent + 2)
                    else:
                        lines.append(pad + "- " + flow(value))

        emit(self.root, 0)
        lines.extend(self.trailer)
        return ("\n".join(lines) + "\n").encode("utf-8")


def expanded(node: Node) -> bool:
    return (isinstance(node.value, dict) and bool(node.value)) or (
        isinstance(node.value, list)
        and bool(node.value)
        and (
            any(isinstance(child.value, (dict, list)) for child in node.value)
            or any(child.before for child in node.value)
        )
    )


def flow(node: Node) -> str:
    return json_spelling(node.plain())


def json_spelling(value: Any) -> str:
    text = json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(", ", ": "))
    return text.translate({0x85: r"\u0085", 0x2028: r"\u2028", 0x2029: r"\u2029"})


def walk(node: Node, path: tuple[str, ...] = ()):
    yield path, node
    if isinstance(node.value, dict):
        for key, child in node.value.items():
            yield from walk(child, (*path, key))
    elif isinstance(node.value, list):
        for index, child in enumerate(node.value):
            yield from walk(child, (*path, str(index)))


def selects(selector: str, path: tuple[str, ...]) -> bool:
    parts = tuple(
        part.replace("~1", "/").replace("~0", "~") for part in selector.lstrip("/").split("/")
    )

    def match(pattern: tuple[str, ...], remaining: tuple[str, ...]) -> bool:
        if not pattern:
            return not remaining
        if pattern[0] == "**":
            return match(pattern[1:], remaining) or bool(
                remaining and match(pattern, remaining[1:])
            )
        return bool(
            remaining and pattern[0] in ("*", remaining[0]) and match(pattern[1:], remaining[1:])
        )

    return match(parts, path)


def read_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_pairs)
    except json.JSONDecodeError as error:
        raise FormatError(path, error.lineno, str(error)) from error
    except ValueError as error:
        raise FormatError(path, 1, str(error)) from error


class Schemas:
    def __init__(self, root: Path):
        self.root = root.resolve()
        self.cache: dict[Path, dict[str, Any]] = {}

    def load(self, path: Path) -> dict[str, Any]:
        if path in self.cache:
            return self.cache[path]
        path = path.resolve()
        if not path.is_relative_to(self.root):
            raise FormatError(path, 1, "schema path escapes schema directory")
        if path not in self.cache:
            self.cache[path] = read_json(path)
        return self.cache[path]

    def resolve(self, schema: dict[str, Any], file: Path) -> tuple[dict[str, Any], Path]:
        seen: set[tuple[Path, str]] = set()
        while "$ref" in schema:
            reference = schema["$ref"]
            if (file, reference) in seen:
                raise FormatError(file, 1, "cyclic schema reference without a data boundary")
            seen.add((file, reference))
            name, _, pointer = reference.partition("#")
            if ":" in name or name.startswith("/"):
                raise FormatError(file, 1, "only local schema references are supported")
            file = (file.parent / name).resolve() if name else file
            target = self.load(file)
            try:
                for part in pointer.lstrip("/").split("/") if pointer else []:
                    component = part.replace("~1", "/").replace("~0", "~")
                    target = (
                        target[int(component)] if isinstance(target, list) else target[component]
                    )
            except (KeyError, TypeError, ValueError, IndexError) as error:
                raise FormatError(file, 1, f"dangling schema reference {reference}") from error
            schema = {**target, **{key: value for key, value in schema.items() if key != "$ref"}}
        return schema, file

    def matches(
        self, value: Any, schema: dict[str, Any], file: Path, *, ignore_unknown: bool = False
    ) -> bool:
        schema, file = self.resolve(schema, file)
        types = schema.get("type", [])
        types = [types] if isinstance(types, str) else types
        observed = (
            "object"
            if isinstance(value, dict)
            else "array"
            if isinstance(value, list)
            else "boolean"
            if isinstance(value, bool)
            else "integer"
            if isinstance(value, int)
            else "number"
            if isinstance(value, float)
            else "null"
            if value is None
            else "string"
        )
        if types and observed not in types and not (observed == "integer" and "number" in types):
            return False
        if "const" in schema and value != schema["const"]:
            return False
        if "enum" in schema and value not in schema["enum"]:
            return False
        if isinstance(value, dict):
            properties = schema.get("properties", {})
            if not set(schema.get("required", [])) <= value.keys():
                return False
            if (
                not ignore_unknown
                and schema.get("additionalProperties") is False
                and not value.keys() <= properties.keys()
            ):
                return False
            if any(
                not self.matches(child, properties[key], file, ignore_unknown=ignore_unknown)
                for key, child in value.items()
                if key in properties
            ):
                return False
        if isinstance(value, list) and "items" in schema:
            if any(
                not self.matches(child, schema["items"], file, ignore_unknown=ignore_unknown)
                for child in value
            ):
                return False
        for keyword in ("oneOf", "anyOf"):
            if keyword in schema:
                count = sum(
                    self.matches(value, branch, file, ignore_unknown=ignore_unknown)
                    for branch in schema[keyword]
                )
                if (keyword == "oneOf" and count != 1) or count == 0:
                    return False
        return True

    def views(self, node: Node, schema: dict[str, Any], file: Path, source: Path | str):
        schema, file = self.resolve(schema, file)
        yield schema, file
        for keyword in ("oneOf", "anyOf"):
            if keyword in schema:
                candidates = [
                    branch for branch in schema[keyword] if self.matches(node.plain(), branch, file)
                ]
                if not candidates:
                    candidates = [
                        branch
                        for branch in schema[keyword]
                        if self.matches(node.plain(), branch, file, ignore_unknown=True)
                    ]
                if len(candidates) != 1:
                    raise FormatError(
                        source, node.line, f"expected one unambiguous {keyword} schema branch"
                    )
                yield from self.views(node, candidates[0], file, source)
        for branch in schema.get("allOf", []):
            yield from self.views(node, branch, file, source)
        if "if" in schema:
            branch = "then" if self.matches(node.plain(), schema["if"], file) else "else"
            if branch in schema:
                yield from self.views(node, schema[branch], file, source)

    def order(self, node: Node, schema: dict[str, Any], file: Path, source: Path | str) -> None:
        views = list(self.views(node, schema, file, source))
        if isinstance(node.value, dict):
            properties: dict[str, tuple[dict[str, Any], Path]] = {}
            for view, owner in views:
                for key, child_schema in view.get("properties", {}).items():
                    properties.setdefault(key, (child_schema, owner))
            for key in node.value:
                if key not in properties:
                    raise FormatError(
                        source, node.key_lines.get(key, node.line), f"unknown key {key!r}"
                    )
            node.value = {key: node.value[key] for key in properties if key in node.value}
            for key, child in node.value.items():
                self.order(child, *properties[key], source)
        elif isinstance(node.value, list):
            items = [(view["items"], owner) for view, owner in views if "items" in view]
            if node.value and not items:
                raise FormatError(source, node.line, "schema does not declare items")
            for child in node.value:
                self.order(child, *items[0], source)


@dataclass
class Source:
    path: Path
    relative: str
    kind: str
    config: dict[str, Any]
    document: YamlDocument
    uses: set[str] = field(default_factory=set)


def selected(source: Source, selectors: list[str]):
    for path, node in walk(source.document.root):
        if any(selects(selector, path) for selector in selectors):
            yield node


def source_index(sources: list[Source], local_fields: list[str]) -> dict[tuple[str, str], Source]:
    identities: dict[tuple[str, str], Source] = {}
    for source in sources:
        for _, node in walk(source.document.root):
            if isinstance(node.value, list):
                for name in local_fields:
                    seen: set[str] = set()
                    for child in node.value:
                        if isinstance(child.value, dict) and name in child.value:
                            identity = child.value[name]
                            if not isinstance(identity.value, str):
                                raise FormatError(
                                    source.relative, identity.line, "id must be a string"
                                )
                            if identity.value in seen:
                                raise FormatError(
                                    source.relative,
                                    identity.line,
                                    f"duplicate id {identity.value!r}",
                                )
                            seen.add(identity.value)
        for namespace, selectors in source.config.get("identities", {}).items():
            found = list(selected(source, selectors))
            if not found and namespace in source.config.get("requiredIdentities", []):
                raise FormatError(source.relative, 1, f"missing configured {namespace} identity")
            for node in found:
                if not isinstance(node.value, str) or not node.value:
                    raise FormatError(source.relative, node.line, "id must be a nonempty string")
                key = namespace, node.value
                if key in identities:
                    raise FormatError(
                        source.relative,
                        node.line,
                        f"duplicate id {node.value!r} in {namespace}; first in {identities[key].relative}",
                    )
                identities[key] = source
    return identities


def find_uses(sources: list[Source], identities: dict[tuple[str, str], Source]) -> None:
    paths = {source.relative: source for source in sources}
    for source in sources:
        for target, selectors in source.config.get("references", {}).items():
            for node in selected(source, selectors):
                if not isinstance(node.value, str):
                    raise FormatError(source.relative, node.line, "reference must be a string")
                if target.startswith("path:"):
                    destination = paths.get(node.value)
                    if destination is not None and destination.kind != target[5:]:
                        destination = None
                else:
                    destination = identities.get((target, node.value))
                if destination is None:
                    raise FormatError(
                        source.relative, node.line, f"dangling reference {node.value!r} ({target})"
                    )
                if destination is not source:
                    source.uses.add(destination.relative)


def ic_unit_sha256(
    start: str, sources: dict[str, Source], hashes: dict[str, str], version: str
) -> str:
    """H3 wire format: sorted UTF-8 'path H1\\n' rows, then 'version\\n'."""
    closure: set[str] = set()
    pending = [start]
    while pending:
        path = pending.pop()
        if path not in closure:
            closure.add(path)
            pending.extend(sources[path].uses)
    payload = "".join(f"{path} {hashes[path]}\n" for path in sorted(closure)) + version + "\n"
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def stage_bytes(path: Path, payload: bytes) -> Path:
    handle, name = tempfile.mkstemp(prefix=".ic-files-", suffix=".tmp", dir=path.parent)
    temporary = Path(name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise
    return temporary


def replace_all(changes: dict[Path, tuple[bytes | None, bytes]]) -> None:
    """Validate first, stage every replacement/backup, then publish. Roll back I/O failures."""
    staged: dict[Path, tuple[Path, Path | None]] = {}
    replaced: list[Path] = []
    retained: set[Path] = set()
    try:
        for path, (old, new) in changes.items():
            temporary = stage_bytes(path, new)
            staged[path] = temporary, None
            backup = stage_bytes(path, old) if old is not None else None
            staged[path] = temporary, backup
        for path, (old, _) in changes.items():
            observed = path.read_bytes() if path.exists() else None
            if observed != old:
                raise FormatError(
                    path, 1, "file changed during formatting; retry with stable inputs"
                )
        for path, (temporary, _) in staged.items():
            os.replace(temporary, path)
            replaced.append(path)
    except BaseException as failure:
        recovery_errors: list[str] = []
        for path in reversed(replaced):
            backup = staged[path][1]
            try:
                if backup is None:
                    path.unlink(missing_ok=True)
                else:
                    os.replace(backup, path)
            except OSError as error:
                if backup is not None:
                    retained.add(backup)
                recovery_errors.append(f"{path}: {error}; original backup: {backup}")
        if recovery_errors:
            raise FormatError(
                replaced[0], 1, "write failed; recovery incomplete; " + "; ".join(recovery_errors)
            ) from failure
        raise
    finally:
        for temporary, backup in staged.values():
            temporary.unlink(missing_ok=True)
            if backup is not None and backup not in retained:
                backup.unlink(missing_ok=True)


def format_tree(root: Path, config_path: Path, check: bool) -> int:
    root = root.resolve()
    if not root.is_dir():
        raise FormatError(root, 1, "root must be an existing directory")
    config = read_json(config_path)
    version = config["loaderSemanticVersion"]
    if not isinstance(version, str) or not version or "\n" in version or "\r" in version:
        raise FormatError(config_path, 1, "loaderSemanticVersion must be one nonempty line")
    schemas = Schemas(root / "schema")
    sources: list[Source] = []
    override = config["override"]
    for kind, policy in [*config["kinds"].items(), ("override", override)]:
        folder = root / policy["folder"]
        if not folder.resolve().is_relative_to(root):
            raise FormatError(folder, 1, "source folder escapes root")
        files = sorted([*folder.rglob("*.yaml"), *folder.rglob("*.yml")])
        if not files:
            continue
        schema_path = (root / "schema" / policy["schema"]).resolve()
        schema = (
            override["fallbackSchema"]
            if kind == "override" and not schema_path.exists()
            else schemas.load(schema_path)
        )
        for path in files:
            if path.is_symlink() or not path.resolve().is_relative_to(root):
                raise FormatError(path, 1, "source symlinks / escaping paths are forbidden")
            relative = path.relative_to(root).as_posix()
            document = YamlDocument(relative, path.read_bytes())
            if not isinstance(document.root.value, dict):
                raise FormatError(relative, 1, "source document must be a mapping")
            schemas.order(document.root, schema, schema_path, relative)
            sources.append(Source(path, relative, kind, policy, document))
    if not any(source.kind != "override" for source in sources):
        raise FormatError(root, 1, "no IC source files found")
    sources.sort(key=lambda source: source.relative)
    identities = source_index(sources, config["localIdFields"])
    find_uses(sources, identities)
    by_path = {source.relative: source for source in sources}
    canonical = {source.relative: source.document.render() for source in sources}
    hashes = {path: hashlib.sha256(raw).hexdigest() for path, raw in canonical.items()}
    reasons: dict[str, list[str]] = {}
    h3_cache: dict[str, str] = {}
    for source in sources:
        if canonical[source.relative] != source.document.raw:
            reasons[source.relative] = ["layout"]
        if source.kind == "override":
            base = source.document.root.value.get(override["baseField"])
            if base is None or not isinstance(base.value, list):
                raise FormatError(source.relative, 1, "override must declare a base list")
            seen: set[str] = set()
            for entry in base.value:
                if not isinstance(entry.value, dict) or override["icField"] not in entry.value:
                    raise FormatError(source.relative, entry.line, "override base must name an IC")
                ic = entry.value[override["icField"]]
                target = identities.get((override["icNamespace"], ic.value))
                if target is None:
                    raise FormatError(
                        source.relative, ic.line, f"dangling override IC {ic.value!r}"
                    )
                if ic.value in seen:
                    raise FormatError(
                        source.relative, ic.line, f"duplicate override base IC {ic.value!r}"
                    )
                seen.add(ic.value)
                if target.relative not in h3_cache:
                    h3_cache[target.relative] = ic_unit_sha256(
                        target.relative, by_path, hashes, version
                    )
                expected = h3_cache[target.relative]
                current = entry.value.get(override["hashField"])
                if current is None or current.value != expected:
                    if current is None:
                        entry.value[override["hashField"]] = Node(expected, entry.line)
                    else:
                        current.value = expected
                    reasons.setdefault(source.relative, []).append("stale override base")
            schemas.order(
                source.document.root,
                schema=schemas.load(root / "schema" / override["schema"])
                if (root / "schema" / override["schema"]).exists()
                else override["fallbackSchema"],
                file=root / "schema" / override["schema"],
                source=source.relative,
            )
            canonical[source.relative] = source.document.render()
            hashes[source.relative] = hashlib.sha256(canonical[source.relative]).hexdigest()
    entries = [
        {"path": source.relative, "sha256": hashes[source.relative], "uses": sorted(source.uses)}
        for source in sources
    ]
    lock_path = root / "ic-files.lock.yaml"
    old_lock = lock_path.read_bytes() if lock_path.exists() else None
    lock = YamlDocument(lock_path.name, old_lock) if old_lock is not None else None
    old_entries: dict[str, dict[str, Any]] = {}
    if lock is not None:
        if not isinstance(lock.root.value, list):
            raise FormatError(lock_path.name, 1, "lock must be a list")
        for node in lock.root.value:
            entry = node.plain()
            if (
                not isinstance(entry, dict)
                or set(entry) - {"path", "sha256", "uses"}
                or not isinstance(entry.get("path"), str)
            ):
                raise FormatError(lock_path.name, node.line, "invalid lock entry")
            if entry["path"] in old_entries:
                raise FormatError(lock_path.name, node.line, "duplicate lock path")
            old_entries[entry["path"]] = entry
    lock_reasons: list[str] = []
    for entry in entries:
        previous = old_entries.pop(entry["path"], None)
        if previous is None:
            lock_reasons.append(f"missing lock entry: {entry['path']}")
        else:
            if previous.get("sha256") != entry["sha256"]:
                lock_reasons.append(f"stale hash: {entry['path']}")
            if previous.get("uses") != entry["uses"]:
                lock_reasons.append(f"stale uses: {entry['path']}")
    lock_reasons.extend(f"obsolete lock entry: {path}" for path in sorted(old_entries))
    new_lock = YamlDocument(lock_path.name, b"[]\n")
    new_lock.root = node_from(entries)
    lock_bytes = new_lock.render()
    if old_lock != lock_bytes:
        reasons[lock_path.name] = lock_reasons or ["layout"]
    for path, messages in sorted(reasons.items()):
        print(f"{path}: {'; '.join(dict.fromkeys(messages))}")
    if check:
        return 1 if reasons else 0
    changes = {
        source.path: (source.document.raw, canonical[source.relative])
        for source in sources
        if source.document.raw != canonical[source.relative]
    }
    if old_lock != lock_bytes:
        changes[lock_path] = old_lock, lock_bytes
    replace_all(changes)
    print(f"Formatted {len(sources)} files; changed {len(changes)} files.")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    fmt = commands.add_parser("fmt", help="format IC YAML and regenerate hashes")
    fmt.add_argument("--root", type=Path, required=True)
    fmt.add_argument("--check", action="store_true", help="report drift without writing")
    fmt.add_argument("--config", type=Path, default=Path(__file__).with_name("ic_files_refs.json"))
    args = parser.parse_args(argv)
    try:
        return format_tree(args.root, args.config, args.check)
    except FormatError as error:
        print(error, file=sys.stderr)
        return 2
    except OSError as error:
        print(f"{error.filename or args.root}:1: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
