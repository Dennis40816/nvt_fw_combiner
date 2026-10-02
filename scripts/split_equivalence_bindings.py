"""Conservative declaration inventory and shared E3 name-collision policy.

This is not a C# binder. Type moves use the approved lookup-scope boundary;
other moves retain the nested method/non-invocable exception. Unknowns block.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

try:
    from scripts.split_equivalence_source import class_name, code_lines, declared_names, namespace, support_source
    from scripts.split_equivalence_moves import member_end, member_spans
    from scripts.split_equivalence_imports import name_imports, primary_parameters
except ModuleNotFoundError:
    from split_equivalence_source import class_name, code_lines, declared_names, namespace, support_source
    from split_equivalence_moves import member_end, member_spans
    from split_equivalence_imports import name_imports, primary_parameters


MODIFIERS = r"(?:(?:public|private|protected|internal|static|readonly|const|volatile|async|virtual|override|abstract|sealed|partial|new|unsafe|extern|required|ref)\s+)*"
TYPE = re.compile(r"\b(record(?:\s+(?:class|struct))?|class|struct|interface|enum)\s+(@?\w+)")
DELEGATE = re.compile(r"\bdelegate\s+[\w.<>,?\[\] :\n]+?\b(@?\w+)\s*(?:<[^<>]+>)?\s*\(")
MEMBER = re.compile(MODIFIERS + r"(?P<type>\([^)]*\)|[\w.<>,?\[\] :]+?)\s+(?P<name>@?\w+)"
                    r"(?:<[^<>]+>)?\s*(?P<end>\(|=>|\{|=|;)")
FUNCTION_POINTER = re.compile(MODIFIERS + r"(?P<type>delegate\*\s*(?:unmanaged\s*(?:\[[^\]]*\])?\s*)?<[^;=]+>)"
                              r"\s+(?P<name>@?\w+)\s*(?P<end>=|;)")
BUILTINS = frozenset("bool byte sbyte char decimal double float int uint nint nuint long ulong short ushort string object".split())
DELEGATES = frozenset({"Action", "Func", "Predicate", "Comparison", "Converter", "EventHandler", "ThreadStart", "ParameterizedThreadStart"})


@dataclass(frozen=True)
class Declaration:
    name: str
    declaring_type: str
    line: int
    kind: str
    type_name: str = ""
    nested: bool = False
    in_support: bool = False


def declaration_lines(clean):
    # code_lines retains summaries for E3 attribution; they are never code.
    return tuple(re.sub(r"///[^\n]*", lambda match: " " * len(match[0]), line) for line in clean)


def declarations(clean: tuple[str, ...], support: frozenset[str] = frozenset(),
                 excluded: frozenset[str] = frozenset()) -> tuple[Declaration, ...]:
    """Keep declaring scopes/lines while retaining the legacy guard's names."""
    legacy_clean, clean = clean, declaration_lines(clean)
    result: list[Declaration] = []
    depth = 0
    scopes: list[tuple[int, str]] = []
    pending = None
    signature = ""
    first = 0
    continuation = False
    for number, line in enumerate(clean, 1):
        value = line.strip()
        at_member = bool(scopes and depth == scopes[-1][0])
        target = TYPE.search(value) if not scopes or at_member else None
        delegate = DELEGATE.search(value) if not scopes or at_member else None
        owner = ".".join(name for _, name in scopes)
        in_support = any(name in support for _, name in scopes)
        if target or delegate:
            name = (target[2] if target else delegate[1]).removeprefix("@")
            kind = "delegate_type" if delegate else "type"
            result.append(Declaration(name, owner or "<namespace>", number, kind,
                                      type_name=target[1] if target else "", nested=bool(scopes), in_support=in_support))
            if target:
                pending = name
                if name not in excluded:
                    # Type headers may span lines; keep parameter names even
                    # when there is no record/class body to enter.
                    header_end = line.find(value) + target.end()
                    for parameter, type_name, parameter_line in primary_parameters(clean, number, header_end):
                        result.append(Declaration(parameter, ".".join(part for part in (owner, name) if part),
                                                  parameter_line, "unknown", type_name, True, in_support))
            signature = ""
        elif at_member and scopes[-1][1] not in excluded:
            if value and not value.startswith("///"):
                value = re.sub(r"^(?:\[[^\]]*\]\s*)+", "", value)
                if value and not continuation:
                    if not signature:
                        first = number
                    signature = (signature + " " + value).strip()
                    if match := FUNCTION_POINTER.match(signature) or MEMBER.match(signature):
                        kind = "method" if match["end"] == "(" else "value"
                        if member_end(clean, first - 1, match["end"] in {"=", ";"}) is None:
                            kind = "unknown"
                        result.append(Declaration(match["name"].removeprefix("@"), owner, first, kind,
                                                  match["type"], len(scopes) > 1, in_support))
                        signature = ""
                        continuation = True
                if any(token in value for token in ("{", "=>", ";")):
                    signature = ""
                    continuation = False
        else:
            signature = ""
            continuation = False
        if pending and "{" in line:
            scopes.append((depth + 1, pending))
            pending = None
        elif pending and ";" in line:
            pending = None
        depth += line.count("{") - line.count("}")
        while scopes and depth < scopes[-1][0]:
            scopes.pop()
    # Unrecognized old declaration syntax cannot become a newly accepted move.
    missing = declared_names(legacy_clean, excluded | support) - {item.name for item in result}
    for name in sorted(missing):
        number = next(i for i, line in enumerate(legacy_clean, 1) if re.search(r"\b" + re.escape(name) + r"\b", line))
        result.append(Declaration(name, "<unknown>", number, "unknown"))
    return tuple(result)


def type_facts(sources: dict[str, tuple[str, ...]]) -> dict[str, set[str]]:
    facts: dict[str, set[str]] = {}
    for lines in sources.values():
        scope = namespace(lines)
        if scope is None:
            scope = "<unknown>" if any(re.search(r"\bnamespace\b", line) for line in code_lines(lines)) else ""
        for item in declarations(code_lines(lines)):
            if item.kind in {"type", "delegate_type"}:
                owner = "" if item.declaring_type == "<namespace>" else item.declaring_type
                identity = ".".join(part for part in (scope, owner, item.name) if part)
                facts.setdefault(identity, set()).add("interface_type" if item.type_name == "interface" else item.kind)
        for match in DELEGATE.finditer("\n".join(declaration_lines(code_lines(lines)))):
            # A multiline delegate with unproven scope can only tighten a fact.
            facts.setdefault("<possible-delegate>." + match[1].removeprefix("@"), set()).add("delegate_type")
    return facts


def imported_namespaces(sources, *, global_only=False):
    pattern = r"global using (?:global::)?([\w.]+);" if global_only else r"(?:global )?using (?:global::)?([\w.]+);"
    return {match[1] for lines in sources.values() for line in code_lines(lines)
            if (match := re.fullmatch(pattern, line))}


def visible_facts(facts, lines, declaring_type="", global_imports=frozenset()):
    """Only a declared type visible from this scope can prove non-delegateness.

    Ambiguous kinds and unsupported namespaces/imports remain unknown. This
    deliberately does not resolve aliases, using-static types or inheritance.
    """
    scope = namespace(lines)
    if scope is None and any(re.search(r"\bnamespace\b", line) for line in code_lines(lines)):
        return {}
    parts = [part for part in (scope or "").split(".") if part]
    prefixes = {".".join(parts[:i]) for i in range(len(parts) + 1)}
    owners = declaring_type.split(".") if declaring_type and not declaring_type.startswith("<") else []
    prefixes.update(".".join([*parts, *owners[:i]]) for i in range(1, len(owners) + 1))
    prefixes.update(imported_namespaces({"source": lines}) | set(global_imports))
    result: dict[str, set[str]] = {}
    for identity, kinds in facts.items():
        owner, _, name = identity.rpartition(".")
        if owner in prefixes or owner == "<possible-delegate>":
            result.setdefault(name, set()).update(kinds)
    return result


def declaring_bases(sources):
    """Partial declarations also participate in inherited type-name lookup."""
    result = {}
    for lines in sources.values():
        scope = namespace(lines) or ""
        clean = declaration_lines(code_lines(lines))
        for item in declarations(clean):
            if item.kind != "type":
                continue
            owner = "" if item.declaring_type == "<namespace>" else item.declaring_type
            identity = ".".join(part for part in (scope, owner, item.name) if part)
            header = " ".join(clean[item.line - 1:]).split("{", 1)[0]
            match = re.search(r"(?<!:):(?!:)", header)
            if not match:
                continue
            bases = header[match.end():].strip()
            result.setdefault(identity, []).append((item.type_name, bases, lines))
    return result


def inherited_lookup_known(facts, bases, lines, declaring_type, global_imports, aliases):
    scope = namespace(lines) or ""
    owners = declaring_type.split(".")
    for length in range(1, len(owners) + 1):
        identity = ".".join(part for part in (scope, ".".join(owners[:length])) if part)
        for kind, value, source in bases.get(identity, []):
            # Classes implementing proven interfaces do not inherit nested type
            # names from them. Base classes and unproven bases remain unknown.
            visible = visible_facts(facts, source, ".".join(owners[:length]), global_imports)
            if kind not in {"class", "record", "record class"} or "where" in value:
                return False
            for base in value.split(","):
                name = base.strip()
                if name.startswith("global::"):
                    proven = facts.get(name.removeprefix("global::")) == {"interface_type"}
                else:
                    proven = bool(name not in aliases and re.fullmatch(r"\w+", name)
                                  and visible.get(name) == {"interface_type"})
                if not proven:
                    return False
    return True


def alias_names(sources):
    names = set()
    for lines in sources.values():
        for line in declaration_lines(code_lines(lines)):
            if match := re.search(r"\busing\s+(@?\w+)\s*=", line):
                names.add(match[1].removeprefix("@"))
            if (match := TYPE.search(line)) and (parameters := re.match(r"\s*<([^<>]+)>", line[match.end():])):
                names.update(re.findall(r"\b\w+\b", parameters[1]))
    return names


def value_kind(type_name: str, facts: dict[str, set[str]], aliases: set[str]) -> str:
    """Prove non-delegate types; unresolved names and aliases are unknown."""
    value = re.sub(MODIFIERS, "", type_name, count=1).strip()
    name = value.removesuffix("?").removeprefix("global::").split("<", 1)[0].rsplit(".", 1)[-1]
    if name in aliases or not re.fullmatch(r"[\w.:]+(?:<[^<>]+>)?\??(?:\[\])?", value):
        return "unknown"
    if "." in value and not value.startswith("System."):
        return "unknown"
    if value.endswith("[]") or name in BUILTINS:
        return "non_invocable"
    if name in DELEGATES or facts.get(name) == {"delegate_type"}:
        return "delegate"
    if facts.get(name) in ({"type"}, {"interface_type"}):
        return "non_invocable"
    return "unknown"


def moved_kind(member, lines: tuple[str, ...], facts, aliases) -> str:
    if member.kind == "support_type_move":
        return "type"
    signature = " ".join(code_lines(lines)[member.start - 1:member.end])
    match = MEMBER.match(signature.strip())
    if not match:
        return "unknown"
    if match["end"] == "(":
        return "method"
    kind = value_kind(match["type"], facts, aliases)
    modifiers = re.match(MODIFIERS, signature.strip())[0].split()
    return "constant" if kind == "non_invocable" and "const" in modifiers else kind


def type_identity(lines, item):
    scope = namespace(lines)
    if scope is None and any(re.search(r"\bnamespace\b", line) for line in code_lines(lines)):
        return None
    owner = "" if item.declaring_type == "<namespace>" else item.declaring_type
    return scope or "", ".".join(part for part in (owner, item.name) if part)


def base_names(header):
    """Erase balanced generic/primary-constructor arguments before base names.

    An unresolved external/generic base is not itself a collision. A matching
    declaring type in the project is still evidence of a possible nested name.
    """
    text = re.split(r"\bwhere\b", header, maxsplit=1)[0]
    for opening, closing in (("(", ")"), ("<", ">")):
        while re.search(re.escape(opening) + "[^" + re.escape(opening + closing) + "]*" + re.escape(closing), text):
            text = re.sub(re.escape(opening) + "[^" + re.escape(opening + closing) + "]*" + re.escape(closing), "", text)
    match = re.search(r"(?<!:):(?!:)", text)
    if not match:
        return set()
    return {name.replace("@", "") for name in re.findall(r"(?:global::)?(?:@?\w+\.)*@?\w+(?=\s*(?:,|$))", text[match.end():])}


class TypeLookup:
    """Prove (a)-(c) from both revisions; retain (d) on unknown inventory."""

    def __init__(self, snapshots, roots, files, support_names=()):
        self.changed = {path for file in files for path in (file.old_path, file.new_path)}
        roots = {(scope or "", name) for scope, name in roots}
        for file in files:
            old_hunks = {line.hunk for line in file.lines if line.side == "-" and (target := class_name(line.text))
                         and (namespace(file.before) or "", target[1]) in roots}
            for line in file.lines:
                target = class_name(line.text) if line.side == "+" else None
                if target and line.hunk in old_hunks:
                    roots.add((namespace(file.after) or "", target[1]))
            if support_source(file.new_path, file.after, support_names):
                roots.update((namespace(file.after) or "", name) for name in support_names)
        self.relevant = set()
        self.types = {}
        self.type_paths = {}
        base_lists = []
        self.unproven = set()
        self.unknown_entries = []
        self.global_aliases = {}
        self.imports = []
        self.import_unknowns = []
        self.alias_entries = []
        self.global_namespaces = set()
        split_names = {name.rsplit(".", 1)[-1] for _, name in roots} | set(support_names)
        for sources in snapshots:
            self.global_namespaces.update(imported_namespaces(sources, global_only=True))
            for path, lines in sources.items():
                clean = declaration_lines(code_lines(lines))
                items = declarations(clean)
                identities = [(item, type_identity(lines, item)) for item in items if item.kind == "type"]
                split_scope = any(identity in roots for _, identity in identities)
                text = "\n".join(clean)
                for pattern in (TYPE, DELEGATE):
                    for match in pattern.finditer(text):
                        name = match[2] if pattern is TYPE else match[1]
                        name = name.removeprefix("@")
                        number = text.count("\n", 0, match.start()) + 1
                        if not any(item.name == name and item.line == number and item.kind in {"type", "delegate_type", "unknown"}
                                   for item in items):
                            self.unknown_entries.append((path, Declaration(name, "<unknown>", number, "unknown"), lines))
                for line in clean:
                    match = re.fullmatch(r"global using (@?\w+) = (.+);", line)
                    if match:
                        self.global_aliases.setdefault(match[1].removeprefix("@"), set()).update(base_names("class _ : " + match[2]))
                # Multiple type headers on one line cannot prove lexical scope.
                if any(len(TYPE.findall(line)) + len(DELEGATE.findall(line)) > 1 or line.lstrip().startswith("#")
                       for line in clean):
                    self.unproven.add((path, lines))
                depth = 0
                for line in clean:
                    depth += line.count("{") - line.count("}")
                    if depth < 0:
                        self.unproven.add((path, lines))
                if depth:
                    self.unproven.add((path, lines))
                proven_classes = {(item.line, item.name) for item, identity in identities if identity is not None}
                unproven_split_scope = any(
                    match[2].removeprefix("@") in split_names and ((path, lines) in self.unproven or
                        (text.count("\n", 0, match.start()) + 1, match[2].removeprefix("@")) not in proven_classes)
                    for match in TYPE.finditer(text) if match[1] == "class")
                for globally, target, alias, number, owner in name_imports(clean):
                    if globally or split_scope or unproven_split_scope or owner == "<extern alias>":
                        if target:
                            # A matching class name does not prove a namespace.
                            # Keep its import, but never guess its lookup scope.
                            imports = self.import_unknowns if unproven_split_scope else self.imports
                            imports.append((path, target.strip(), number, lines))
                        else:
                            self.alias_entries.append((path, Declaration(alias, owner, number, "unknown"), lines))
                for item in items:
                    if item.kind != "type":
                        continue
                    identity = type_identity(lines, item)
                    if identity is None:
                        continue
                    header = " ".join(clean[item.line - 1:]).split("{", 1)[0].split(";", 1)[0]
                    self.types.setdefault(identity, set())
                    self.type_paths.setdefault(identity, set()).add(path)
                    base_lists.append((identity, base_names(header), lines))
                    if identity in roots:
                        parts = identity[1].split(".")
                        self.relevant.update((identity[0], ".".join(parts[:i])) for i in range(1, len(parts) + 1))
        for identity, names, lines in base_lists:
            for base in names:
                self.types[identity].update(self.resolve_base(base, identity, lines))
        for path, target, number, lines in self.imports:
            # Unlike a base-name hint, a static import must actually resolve;
            # an unrelated project namesake cannot prove an external target.
            identity = namespace(lines) or "", "<imports>"
            found = self.resolve_base(target.replace("@", ""), identity, lines, strict=True)
            if found:
                self.relevant.update(found)
            else:
                self.import_unknowns.append((path, target, number, lines))
        # Lexical and inherited lookup can continue through project base types.
        pending = list(self.relevant)
        while pending:
            identity = pending.pop()
            for candidate in self.types.get(identity, ()):
                if candidate not in self.relevant:
                    self.relevant.add(candidate)
                    pending.append(candidate)

    def resolve_base(self, base, identity, lines, seen=frozenset(), *, strict=False):
        if base in seen:
            return set()
        clean = declaration_lines(code_lines(lines))
        aliases = dict(self.global_aliases)
        for line in clean:
            match = re.fullmatch(r"(?:global )?using (@?\w+) = (.+);", line)
            if match:
                aliases[match[1].removeprefix("@")] = base_names("class _ : " + match[2])
        if base in aliases:
            return {candidate for target in aliases[base]
                    for candidate in self.resolve_base(target, identity, lines, seen | {base}, strict=strict)}
        # Resolve known lexical/namespace/imported identities first. If lookup
        # is unproven, a declaration of that base name is required as evidence;
        # an external or generic base alone never makes an unrelated type block.
        qualified = {}
        for candidate in self.types:
            qualified.setdefault(".".join(part for part in candidate if part), set()).add(candidate)
        if base.startswith("global::"):
            found = qualified.get(base[8:], set())
        else:
            parts = ".".join(part for part in identity if part).split(".")
            found = set()
            for length in range(len(parts) - 1, -1, -1):
                target = ".".join([*parts[:length], base])
                if target in qualified:
                    found = qualified[target]
                    break
            if not found:
                imports = imported_namespaces({"source": lines}) | (self.global_namespaces if strict else set())
                found = {candidate for prefix in imports
                         for candidate in qualified.get(prefix + "." + base, ())}
        if strict:
            return found
        return found or {candidate for candidate in self.types
                         if candidate[1].rsplit(".", 1)[-1] == base.rsplit(".", 1)[-1].removeprefix("global::")}

    def rule(self, path, item, lines):
        if item.kind not in {"type", "delegate_type"} or (path, lines) in self.unproven:
            return "d"
        if not item.nested:
            return "a"
        identity = type_identity(lines, item)
        if identity is None or item.declaring_type.startswith("<"):
            return "d"
        owner = identity[0], item.declaring_type
        if owner not in self.types:
            return "d"
        if owner in self.relevant:
            return "b"
        return "c" if self.type_paths[owner] & self.changed else None


class BindingGuard:
    def __init__(self, sources, support_names, moved_types, destinations, extra_facts=(), project_sources=(), converted_paths=(), *, files=(), roots=()):
        all_sources = dict(project_sources)
        all_sources.update(sources)
        self.facts = type_facts(all_sources)
        for name, kind in extra_facts:
            self.facts.setdefault(name, set()).add(kind)
        self.aliases = alias_names(all_sources)
        self.global_imports = imported_namespaces(all_sources, global_only=True)
        self.sources = all_sources
        self.bases = declaring_bases(all_sources)
        before_sources = dict(source for file in files for source in file.base_sources)
        before_sources.update((file.old_path, file.before) for file in files if file.status != "A")
        origins = {(file.old_path, member.start) for file in files
                   for member in member_spans(file.before, code_lines(file.before), True)
                   if member.name in moved_types and (file.new_path in converted_paths or
                       any(line.side == "-" and line.number == member.start for line in file.lines))}
        lookup_roots = set(roots) | {(namespace(file.before) or "", item.declaring_type) for file in files
            for item in declarations(code_lines(file.before)) if item.kind in {"type", "delegate_type"}
            and (file.old_path, item.line) in origins}
        self.lookup = TypeLookup((all_sources, before_sources), lookup_roots, files, support_names)
        destinations = set(destinations) | {(file.new_path, member.start) for file in files
            if file.new_path in converted_paths for member in member_spans(
                file.after, code_lines(file.after), True, frozenset(support_names))}
        self.scope_facts = {}
        self.entries = []
        self.type_only = set()
        for path, lines in sorted(all_sources.items()):
            has_support = support_source(path, lines, support_names) or path in converted_paths
            excluded = frozenset(moved_types) if has_support else frozenset()
            for item in declarations(code_lines(lines), frozenset(support_names) if has_support else frozenset(), excluded):
                if (path, item.line) in destinations:
                    continue
                if item.kind not in {"type", "delegate_type", "unknown"} and path not in sources and not item.in_support:
                    continue
                entry = path, item, lines
                self.entries.append(entry)
                if path not in sources and not item.in_support:
                    self.type_only.add(entry)
        for path, lines in sorted(before_sources.items()):
            for item in declarations(code_lines(lines)):
                if item.kind not in {"type", "delegate_type", "unknown"} or (path, item.line) in origins:
                    continue
                if (path, item, lines) not in self.entries:
                    self.entries.append((path, item, lines))
                    self.type_only.add((path, item, lines))
        for entry in self.lookup.unknown_entries + self.lookup.alias_entries:
            if entry not in self.entries:
                self.entries.append(entry)
                self.type_only.add(entry)
        self.collisions: dict[tuple[str, str, int, str], dict[str, object]] = {}

    def check(self, member, source) -> list[int]:
        moved = moved_kind(member, source, visible_facts(self.facts, source, global_imports=self.global_imports), self.aliases)
        reasons = []
        unknowns = [(path, Declaration(member.name, "<using static " + target + ">", number, "unknown"), lines)
                    for path, target, number, lines in self.lookup.import_unknowns] if moved == "type" else []
        for path, item, lines in self.entries + unknowns:
            if item.name != member.name:
                continue
            if moved != "type" and (path, item, lines) in self.type_only:
                continue
            rule = self.lookup.rule(path, item, lines) if moved == "type" else "d"
            if rule is None:
                continue
            remaining = item.kind
            if item.kind == "value":
                scope_key = path, item.declaring_type
                if scope_key not in self.scope_facts:
                    known = inherited_lookup_known(self.facts, self.bases, lines, item.declaring_type,
                                                   self.global_imports, self.aliases)
                    self.scope_facts[scope_key] = visible_facts(self.facts, lines, item.declaring_type, self.global_imports) if known else {}
                remaining = value_kind(item.type_name, self.scope_facts[scope_key], self.aliases)
            if item.nested and not item.in_support and ((moved == "method" and remaining == "non_invocable")
                                or (moved == "constant" and remaining == "method")):
                continue
            reason = (f"same-named member remains in {path}: member {item.name}, "
                      f"declaring type {item.declaring_type}, line {item.line}; name binding may change")
            key = item.name, path, item.line, item.declaring_type
            if key not in self.collisions:
                self.collisions[key] = {"id": len(self.collisions), "name": item.name, "file": path,
                                        "declaring_type": item.declaring_type, "line": item.line,
                                        "kind": remaining, "rule": rule, "reason": reason}
            reasons.append(self.collisions[key]["id"])
        return reasons


def collection_key(attribute: str) -> str | None:
    value = attribute.split("(", 1)[1].rsplit(")", 1)[0].strip()
    pattern = r'"(?:\\.|[^"\\])*"|/\*[\s\S]*?\*/|//[^\n]*'
    value = re.sub(pattern, lambda match: match[0] if match[0].startswith('"') else " ", value).strip()
    if match := re.fullmatch(r"nameof\s*\(\s*(?:global::)?([\w.@]+)\s*\)", value):
        return match[1].rsplit(".", 1)[-1].removeprefix("@")
    match = re.fullmatch(r'"([\w .-]*)"', value)
    return match[1] if match else None


def collection_uses(lines):
    """Include ordinary, internal and nested classes, not just split shapes."""
    raw, clean = "\n".join(lines), "\n".join(code_lines(lines))
    clean = re.sub(r"(?m)///[^\n]*", lambda match: " " * len(match[0]), clean)
    pattern = (r"\[\s*(?:[^\]]*?,\s*)?(?P<attribute>(?:global::)?(?:Xunit\.)?Collection(?:Attribute)?\s*\(.*?\))"
               r"\s*(?:,[^\]]*)?\]\s*(?:\[[^\]]*\]\s*)*" + MODIFIERS
               + r"(?:class|record(?!\s+struct)(?:\s+class)?)\s+(?P<name>@?\w+)")
    for match in re.finditer(pattern, clean, re.S):
        attribute = "[" + raw[match.start("attribute"):match.end("attribute")] + "]"
        yield match["name"].removeprefix("@"), attribute


def captured_collections(base_sources, source_identities, new_collections):
    """New definitions cannot change implicit collections of unrelated classes."""
    if not new_collections:
        return []
    try:
        from scripts.split_equivalence_source import namespace
    except ModuleNotFoundError:
        from split_equivalence_source import namespace
    failures = []
    for path, lines in sorted(base_sources.items()):
        for name, attribute in collection_uses(lines):
            if (namespace(lines), name) in source_identities:
                continue
            key = collection_key(attribute)
            if key is None or any(key == collection_key(item) for item in new_collections):
                failures.append({"classes": [name], "file": path, "collection": attribute,
                                 "reason": "new definition captures a base collection outside the split" if key is not None else
                                           "base collection argument cannot be classified outside the split"})
    return failures
