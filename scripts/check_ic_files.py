"""Validate the repository's structure C IC authoring sources offline.

Ported from the delivered check_c.py structural checks. Historical source-hash
and A/C conversion comparisons belong to the conversion run, not this validator.
Only repository-owned schemas, reference inventories and baseline contracts are
read. No firmware is executed and no byte-parity assertion is made.
"""

from __future__ import annotations

import argparse
import copy
import functools
import itertools
import json
import re
import sys
from pathlib import Path

if __package__:
    from .ic_files import YamlDocument, unique_pairs
else:
    from ic_files import YamlDocument, unique_pairs

ROOT = Path(__file__).resolve().parents[1]
HERE = ROOT / "profiles/ic-files"
REFERENCE_PATH = Path(__file__).with_name("ic_files_structure_refs.json")
ROUTES_PATH = Path(__file__).with_name("ic_files_routes.json")
GROUP_REFS = {
    "regions": "region-refs",
    "fields": "field-refs",
    "inputs": "input-refs",
    "integrity": "integrity-refs",
    "checks": "check-refs",
    "extents": "extent-refs",
}


def load_yaml(path):
    return YamlDocument(path, path.read_bytes()).root.plain()


@functools.lru_cache(maxsize=1)
def reference_inventory():
    return read(REFERENCE_PATH)


@functools.lru_cache(maxsize=1)
def source_identity_names():
    return frozenset(reference_inventory()["sourceIdentities"])


def require(ok, message):
    if not ok:
        raise ValueError(message)


def read(path):
    return json.loads(
        path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_pairs
    )


def walk(data, path="$", key=None):
    yield path, data, key
    if isinstance(data, dict):
        for k, v in data.items():
            yield from walk(v, path + "." + k, k)
    elif isinstance(data, list):
        for i, v in enumerate(data):
            yield from walk(v, f"{path}[{i}]", key)


def fallback_schema_check(document, schema):
    """Small schema-subset evaluator, used only if jsonschema cannot import."""

    def valid(data, rule):
        if isinstance(rule, bool):
            return rule
        if "$ref" in rule:
            return valid(data, schema["$defs"][rule["$ref"].split("/")[-1]])
        if "oneOf" in rule and sum(valid(data, x) for x in rule["oneOf"]) != 1:
            return False
        if "allOf" in rule and not all(valid(data, x) for x in rule["allOf"]):
            return False
        if "not" in rule and valid(data, rule["not"]):
            return False
        if (
            "if" in rule
            and valid(data, rule["if"])
            and "then" in rule
            and not valid(data, rule["then"])
        ):
            return False
        if "const" in rule and data != rule["const"]:
            return False
        if "enum" in rule and data not in rule["enum"]:
            return False
        types = {
            "object": isinstance(data, dict),
            "array": isinstance(data, list),
            "string": isinstance(data, str),
            "integer": isinstance(data, int) and not isinstance(data, bool),
            "boolean": isinstance(data, bool),
            "null": data is None,
        }
        if "type" in rule and not any(
            types.get(t, False)
            for t in ([rule["type"]] if isinstance(rule["type"], str) else rule["type"])
        ):
            return False
        if isinstance(data, dict):
            if any(k not in data for k in rule.get("required", [])) or len(
                data
            ) < rule.get("minProperties", 0):
                return False
            props = rule.get("properties", {})
            for k, v in data.items():
                sub = props.get(k, rule.get("additionalProperties", True))
                if not valid(v, sub):
                    return False
        if isinstance(data, list):
            if (
                not rule.get("minItems", 0)
                <= len(data)
                <= rule.get("maxItems", sys.maxsize)
            ):
                return False
            if rule.get("uniqueItems") and len(
                {json.dumps(x, sort_keys=True) for x in data}
            ) != len(data):
                return False
            if not all(valid(x, rule.get("items", True)) for x in data):
                return False
            if "contains" in rule and not any(valid(x, rule["contains"]) for x in data):
                return False
        if isinstance(data, str):
            if (
                len(data) < rule.get("minLength", 0)
                or "pattern" in rule
                and re.search(rule["pattern"], data) is None
            ):
                return False
        if isinstance(data, int) and not isinstance(data, bool):
            if data < rule.get("minimum", -sys.maxsize) or data > rule.get(
                "maximum", sys.maxsize
            ):
                return False
        return True

    require(valid(document, schema), "Hand-written structural schema check failed")


def hx(n):
    return f"0x{n:X}"


def key(v):
    return json.dumps(v, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def index(xs):
    out = {}
    for x in xs:
        if x["id"] in out:
            raise ValueError("Duplicate id " + x["id"])
        out[x["id"]] = {k: v for k, v in x.items() if k != "id"}
    return out


def matches(w, facts):
    if isinstance(w, list):
        return all(matches(x, facts) for x in w)
    f = w["fact"]
    k = w["kind"]
    if k == "present":
        return f in facts and facts[f] is not None
    if k == "absent":
        return f not in facts or facts[f] is None
    if f not in facts:
        raise ValueError("Pending selection fact " + f)
    v = facts[f]
    if k == "equals":
        return v == w["equals"]

    def cv(x):
        return tuple(int(y) for y in x.split(".")) if isinstance(x, str) else x

    return (
        ("at-least" not in w or cv(v) >= cv(w["at-least"]))
        and ("at-most" not in w or cv(v) <= cv(w["at-most"]))
        and ("below" not in w or cv(v) < cv(w["below"]))
    )


def source_name(value):
    value = re.sub(r"header-(\d+)-", r"header-bank\1-", value)
    value = value.replace("nt51927-927-", "nt51927-type927-")
    value = value.replace("@2-8-ic", "@ic-count2-through8")
    if value.startswith("gap-"):
        value = re.sub(r"(?<=-)([0-9][0-9a-f]*)(?=-|$)", r"offset\1", value)
    if value.startswith("header-dlm-crc-"):
        value = re.sub(r"(?<=-)([0-9]+)(?=-|$)", r"offset\1", value)
    return value


def document_policy(path, data):
    require(
        isinstance(data, dict)
        and list(data)[0] == "schema-version"
        and type(data["schema-version"]) is int
        and data["schema-version"] == 1,
        str(path) + ": schema-version: 1 must be the first key",
    )
    if str(path).replace("\\", "/").split("/")[0] in {
        "ic",
        "parts",
        "rules",
        "definitions",
    }:
        for location, node, _ in walk(data):
            if isinstance(node, dict):
                for member in ["id", "definition", "part", "rule"]:
                    value = node.get(member)
                    if isinstance(value, str):
                        require(
                            not re.search(
                                r"[0-9a-fA-F]{8,}|-\d+-|range-(?:pre|steps|post)-",
                                value,
                            ),
                            str(path) + ": nonsemantic id " + value + " at " + location,
                        )


def validate_identity_aliases(documents):
    declared = {
        node["id"]
        for document in documents.values()
        for _, node, _ in walk(document)
        if isinstance(node, dict) and isinstance(node.get("id"), str)
    }
    declared.update(
        document[member]
        for document in documents.values()
        for member in ["definition", "part", "rule"]
        if isinstance(document.get(member), str)
    )
    aliases = {}
    for path, document in documents.items():
        for item in document.get("aliases", []):
            old, new = item["previous-id"], item["current-id"]
            require(
                old in source_identity_names(),
                path + ": previous alias is not an immutable source declaration " + old,
            )
            require(
                old != new and source_name(old) == new,
                path + ": alias is not the published identity rename " + old,
            )
            require(
                new in declared,
                path + ": alias target is not a declared identity " + new,
            )
            require(
                new not in aliases or aliases[new] == old,
                "Ambiguous historical identity " + new,
            )
            aliases[new] = old
    return aliases


def restore_identities(value, aliases, member=None):
    """Restore typed identity positions only. Literal values never pass through an alias map."""
    identity_members = {
        "id",
        "space",
        "region",
        "parent",
        "within",
        "from-fields",
        "field",
        "other",
        "span",
        "series-ref",
        "group",
        "input",
        "root",
        "rule",
        "transforms",
        "primaries",
        "complement",
        "subject",
    }
    if isinstance(value, list):
        return [restore_identities(item, aliases, member) for item in value]
    if isinstance(value, dict):
        return {
            (
                aliases.get(name, name)
                if member in {"fields", "regions", "inputs", "integrity"}
                else name
            ): restore_identities(item, aliases, name)
            for name, item in value.items()
        }
    if isinstance(value, str) and (
        member in identity_members or (member == "fact" and value.startswith("fields."))
    ):
        if value in aliases:
            return aliases[value]
        if member in {"from-fields", "fact"}:
            return ".".join(aliases.get(piece, piece) for piece in value.split("."))
    return value


def unname(v):
    if isinstance(v, list):
        return [unname(x) for x in v]
    if not isinstance(v, dict):
        return v
    return {
        k: (index([unname(x) for x in x]) if k == "fields" else unname(x))
        for k, x in v.items()
    }


def self_space(v, sid):
    if isinstance(v, list):
        return [self_space(x, sid) for x in v]
    if isinstance(v, dict):
        return {
            k: (sid if k == "space" and x == "$self" else self_space(x, sid))
            for k, x in v.items()
        }
    return v


def tool_metadata(v, tools):
    """Include the separately owned tool version/architecture and complete method table."""
    if isinstance(v, list):
        return [tool_metadata(x, tools) for x in v]
    if not isinstance(v, dict):
        return v
    out = {k: tool_metadata(x, tools) for k, x in v.items()}
    if isinstance(out.get("tool"), str):
        name = out["tool"]
        require(name in tools, "Dangling tool metadata " + name)
        data = tools[name]
        out["tool"] = {
            "id": name,
            **{
                k: copy.deepcopy(data[k])
                for k in ["version", "architecture", "platform", "methods"]
                if k in data
            },
        }
    if out.get("kind") == "vendor-footer" and isinstance(out.get("file"), str):
        out["file"] = {
            "path": out["file"],
            "methods": copy.deepcopy(tools["integrity-methods"]["methods"]),
        }
    return out


class Tree:
    def __init__(self, base, force_fallback=False):
        self.base = base
        self.documents = {}
        self.defs = {}
        self.parts = {}
        self.ics = {}
        self.rules = {}
        self.invocations = {}
        self.tools = {}
        self.engine = "fallback schema evaluator"
        self.source_aliases = {}
        self.parameter_count = 0
        for typ, folder in [
            ("definition", "definitions"),
            ("part", "parts"),
            ("ic", "ic"),
            ("rule", "rules"),
            ("tool", "tools"),
        ]:
            schema = read(base / "schema" / f"{typ}.schema.json")
            document_policy("schema/" + typ + ".schema.json", schema)
            for branch in schema.get("oneOf", [schema]):
                require(
                    "schema-version" in branch["required"]
                    and branch["properties"]["schema-version"]["const"] == 1,
                    typ + ": schema must require version 1",
                )

            def validate(d, s=schema):
                fallback_schema_check(d, s)

            if not force_fallback:
                try:
                    from jsonschema import Draft202012Validator
                except ImportError:
                    pass
                else:
                    Draft202012Validator.check_schema(schema)
                    validator = Draft202012Validator(schema)
                    validate = validator.validate
                    self.engine = "jsonschema Draft 2020-12"
            for path in sorted(
                [*(base / folder).rglob("*.yaml"), *(base / folder).rglob("*.yml")]
            ):
                data = load_yaml(path)
                document_policy(path.relative_to(base).as_posix(), data)
                try:
                    validate(data)
                except Exception as error:
                    raise ValueError(
                        f"{path.relative_to(base)}: schema rejected: {str(error)[:750]}"
                    ) from error
                self.documents[path.relative_to(base).as_posix()] = data
        for path, data in self.documents.items():
            for p, node, _ in walk(data):
                if (
                    isinstance(node, list)
                    and node
                    and all(isinstance(x, dict) and "id" in x for x in node)
                ):
                    index(node)
                if isinstance(node, dict) and {"space", "start", "end"} <= node.keys():
                    require(
                        0 <= int(node["start"], 16) < int(node["end"], 16),
                        path + ": invalid half-open range " + p,
                    )
                if isinstance(node, dict) and "when" in node:
                    conditions = (
                        node["when"]
                        if isinstance(node["when"], list)
                        else [node["when"]]
                    )
                    for w in conditions:
                        require(
                            isinstance(w, dict), path + ": when must use object form"
                        )
                        require(
                            w["kind"] != "equals" or "equals" in w,
                            path + ": equals lacks comparison",
                        )
                        require(
                            w["kind"] != "interval"
                            or any(k in w for k in ["at-least", "at-most", "below"]),
                            path + ": empty interval",
                        )
                if isinstance(node, dict) and node.get("type") == "choice":
                    require(
                        node["default"] in node["values"],
                        path + ": choice default is not declared",
                    )
                if isinstance(node, dict) and isinstance(node.get("count"), dict):
                    c = node["count"]
                    require(
                        "max" not in c or c["min"] <= c["max"],
                        path + ": invalid binding count bounds",
                    )
            if path.startswith("definitions/"):
                did = data["definition"]
                require(did not in self.defs, "Duplicate definition " + did)
                self.defs[did] = data
            elif path.startswith("parts/"):
                require(
                    data["part"] not in {x["part"] for x in self.parts.values()},
                    "Duplicate part id " + data["part"],
                )
                self.parts[path] = data
            elif path.startswith("ic/"):
                for ic in data["ic"]:
                    require(ic not in self.ics, "Duplicate IC " + ic)
                    self.ics[ic] = path
            elif path.startswith("rules/"):
                rid = data["rule"]
                require(rid not in self.rules, "Duplicate rule " + rid)
                self.rules[rid] = data
            elif path.startswith("tools/"):
                tool = data["tool"]
                require(tool not in self.tools, "Duplicate tool " + tool)
                self.tools[tool] = data
                for inv in data["invocations"]:
                    iid = inv["id"]
                    require(iid not in self.invocations, "Duplicate invocation " + iid)
                    self.invocations[iid] = {
                        k: copy.deepcopy(v) for k, v in inv.items() if k != "id"
                    }
                    if tool == "legacy-combiner":
                        parameters = index(data["parameters"])
                        self.parameter_count = len(parameters)
                        require(
                            len({key(v) for v in parameters.values()})
                            == len(parameters),
                            "Duplicate shared parameter value",
                        )
                        for command in self.invocations[iid].get("commands", []):
                            refs = command.pop("parameter-refs")
                            for field, reference in refs.items():
                                require(
                                    reference in parameters,
                                    "Dangling command parameter " + reference,
                                )
                                item = copy.deepcopy(parameters[reference])
                                mode_scope = item.pop("for-mode", None)
                                if mode_scope is not None:
                                    require(
                                        field == "crc" and mode_scope == refs["mode"],
                                        "CRC parameter belongs to a different command mode",
                                    )
                                require(
                                    len(item) == 1,
                                    "Parameter must define one typed field",
                                )
                                name, value = next(iter(item.items()))
                                require(
                                    name == field,
                                    "Parameter reference has the wrong type " + field,
                                )
                                require(
                                    name not in command,
                                    "Conflicting command parameter " + name,
                                )
                                command[name] = copy.deepcopy(value)
                            require(
                                {"family", "mode"} <= command.keys(),
                                "Command requires family and mode parameters",
                            )
        self.source_aliases = validate_identity_aliases(self.documents)
        method_codes = [x["method"] for x in self.tools["integrity-methods"]["methods"]]
        require(
            len(method_codes) == len(set(method_codes)), "Duplicate footer method code"
        )
        for inv in self.invocations.values():
            if inv["kind"] == "vendor-footer":
                require(
                    inv["file"] in self.documents,
                    "Dangling method-table file " + inv["file"],
                )
                require(
                    inv["method"] in method_codes,
                    "Dangling footer method " + inv["method"],
                )
            else:
                require(inv["tool"] in self.tools, "Dangling tool " + inv["tool"])
            artifacts = {x["id"] for x in inv.get("artifacts", [])}
            for command in inv.get("commands", []):
                require(
                    set(command.get("artifact-order", [])) <= artifacts,
                    "Dangling staged artifact argument",
                )
        self.routes = read(ROUTES_PATH)
        self.cases = [reference_inventory()["rejectionCase"]]
        document_policy("routes.json", self.routes)
        self.layout_cache = {}
        self.check_distinct_rules()

    def check_distinct_rules(self):
        def resolve_ids(value):
            if isinstance(value, list):
                return [resolve_ids(x) for x in value]
            if not isinstance(value, dict):
                return value
            result = {}
            for name, item in value.items():
                if name == "run":
                    require(item in self.invocations, "Dangling invocation " + item)
                    result[name] = resolve_ids(self.invocations[item])
                else:
                    result[name] = resolve_ids(item)
            return result

        seen = {}
        for name, doc in self.rules.items():
            plans = sorted(
                [
                    resolve_ids({k: v for k, v in p.items() if k != "id"})
                    for p in doc["plans"]
                ],
                key=key,
            )
            signature = key(plans)
            require(
                signature not in seen,
                "Identical resolved rule plans must share one file: "
                + name
                + " / "
                + seen.get(signature, ""),
            )
            seen[signature] = name
        return len(seen)

    def layout(self, file, lid):
        k = (file, lid)
        if k in self.layout_cache:
            return self.layout_cache[k]
        doc = self.documents[file]
        layouts = index(doc["layouts"])
        require(lid in layouts, "Dangling layout " + lid)
        lay = copy.deepcopy(layouts[lid])
        lay["id"] = lid
        for entry in lay.get("include", []):
            path = entry["file"]
            require(
                path in self.parts and ".." not in Path(path).parts,
                "Dangling or escaping include " + path,
            )
            part = self.parts[path]
            if "fact-set" in entry:
                sets = index(part.get("fact-sets", []))
                require(
                    entry["fact-set"] in sets, "Dangling fact set " + entry["fact-set"]
                )
                part = sets[entry["fact-set"]]
            for g, refs in GROUP_REFS.items():
                available = index(part.get(g, []))
                present = index(lay.get(g, []))
                for name in entry.get(refs, []):
                    require(
                        name in available,
                        "Dangling include " + path + ":" + g + "." + name,
                    )
                    require(
                        name not in present,
                        "Conflicting include; whole-record override forbidden "
                        + lid
                        + ":"
                        + g
                        + "."
                        + name,
                    )
                    lay.setdefault(g, []).append(
                        {"id": name, **copy.deepcopy(available[name])}
                    )
                    present[name] = available[name]
        if "check-order" in lay:
            checks = index(lay["checks"])
            require(
                set(lay["check-order"]) == set(checks),
                "Check order must name every check once",
            )
            lay["checks"] = [{"id": n, **checks[n]} for n in lay["check-order"]]
        self.layout_cache[k] = lay
        return lay

    def located(self, placement):
        name = placement["id"]
        did = placement["definition"]
        require(did in self.defs, "Dangling definition " + did)
        f = copy.deepcopy(self.defs[did])
        f.pop("definition")
        f.pop("schema-version")
        f.pop("aliases", None)
        f = unname(f)
        f = self_space(f, name)
        f["required"] = placement["required"]
        at = copy.deepcopy(placement["at"])
        if f["kind"] == "group":
            require(
                isinstance(at, dict) and at.get("kind") == "group",
                "Group requires located member collection",
            )
            for x in at["locations"]:
                require(
                    x["id"] in f.get("fields", {}), "Dangling group location " + x["id"]
                )
                f["fields"][x["id"]]["at"] = x["at"]
        elif at is not None:
            f["at"] = at
        if f["kind"] in {"structure", "scalar"}:
            require(at is not None, "Located byte field requires a locator " + name)
        for k in ["bindings", "map-selection"]:
            if k in placement:
                f[k] = copy.deepcopy(placement[k])
        for _, v, _ in walk(f):
            if isinstance(v, dict) and v.get("kind") in {"structure", "scalar"}:
                require(
                    "at" in v, "A resolved byte field is missing its located binding"
                )
        return f

    def select(self, file, bid, facts):
        doc = self.documents[file]
        bs = index(doc["rules"])
        require(bid in bs, "Dangling IC rule binding " + bid)
        b = bs[bid]
        rid = b["rule"]
        require(rid in self.rules, "Dangling rule " + rid)
        plans = index(self.rules[rid]["plans"])
        # All plan alternatives must agree on setting types/defaults for their binding.
        facts = copy.deepcopy(facts)
        if "ic-count" in facts:
            require(
                type(facts["ic-count"]) is int and facts["ic-count"] > 0,
                "IC count must be a positive integer",
            )
        links = b["plan-bindings"]
        for link in links:
            require(link["plan"] in plans, "Dangling rule plan " + link["plan"])
            for setting in (
                plans[link["plan"]]["settings"] + plans[link["plan"]]["option-defaults"]
            ):
                name = "setting." + setting["id"]
                if "default" in setting:
                    facts.setdefault(name, setting["default"])
                if setting["type"] == "choice" and name in facts:
                    require(
                        facts[name] in setting["values"],
                        "Invalid choice " + setting["id"],
                    )
                if setting["type"] == "boolean" and name in facts:
                    require(
                        type(facts[name]) is bool, "Invalid boolean " + setting["id"]
                    )
                if setting["type"] == "hex-length":
                    require(name in facts, "Missing user-requested output length")
                    value = facts[name]
                    require(
                        isinstance(value, str)
                        and re.fullmatch(r"0x[0-9A-Fa-f]+", value),
                        "Output length must be a hexadecimal byte length",
                    )
                    n = int(value, 16)
                    require(
                        int(setting["min-length"], 16)
                        <= n
                        <= int(setting["max-length"], 16),
                        "Output length outside source bounds",
                    )
        chosen = []
        for group in b["variants"]:
            selected = [c for c in group["cases"] if matches(c["when"], facts)]
            require(
                len(selected) == 1,
                f"{file}:{bid}: expected exactly one layout name, got {len(selected)}",
            )
            chosen.append(selected[0]["use"])
        require(
            len(chosen) == 1, "A binding must select one layout, never merge records"
        )
        lid = chosen[0]
        selected = [x for x in links if x["layout"] == lid]
        require(len(selected) == 1, "Layout must identify one complete plan")
        link = selected[0]
        pid = link["plan"]
        plan = copy.deepcopy(plans[pid])
        plan.pop("id", None)
        names = index(self.rules[rid]["naming"])
        require(
            link["naming-ref"] in names, "Dangling naming rule " + link["naming-ref"]
        )
        return (
            self.layout(file, lid),
            plan,
            names[link["naming-ref"]],
            facts,
            (lid, rid, pid),
        )

    def resolve(self, file, bid, facts):
        lay, plan, naming, facts, identity = self.select(file, bid, facts)
        rs = index(lay["regions"])
        size = lay["size"]
        size = facts["setting." + size["setting"]] if isinstance(size, dict) else size
        extents = {x["id"]: x["length"] for x in lay.get("extents", [])}
        extents["output"] = size

        def expand(v):
            if isinstance(v, list):
                return [expand(x) for x in v]
            if not isinstance(v, dict):
                return copy.deepcopy(v)
            if set(v) == {"ic-count", "at"}:
                at = expand(v["at"])
                require(
                    {"space", "start", "end"} <= at.keys(),
                    "Expected backup start must resolve to a layout region",
                )
                return {"ic-count": v["ic-count"], "start": at["start"]}
            if set(v) == {"extent"}:
                require(v["extent"] in extents, "Dangling extent " + v["extent"])
                return extents[v["extent"]]
            if {"space", "region"} <= v.keys():
                require(v["region"] in rs, "Dangling region " + v["region"])
                r = rs[v["region"]]
                start = int(r["start"], 16) + int(v.get("offset", "0x0"), 16)
                length = (
                    int(v["length"], 16) if "length" in v else int(r["end"], 16) - start
                )
                require(
                    length > 0
                    and start >= int(r["start"], 16)
                    and start + length <= int(r["end"], 16),
                    "Region slice out of bounds " + v["region"],
                )
                # A complete-source seed extends to the explicitly supplied source envelope.
                if (
                    plan["initial"].get("root") == v["region"]
                    and "input" in plan["initial"]
                    and "length" not in v
                    and "offset" not in v
                ):
                    inputid = plan["initial"]["input"]
                    if inputid + ".length" in facts:
                        length = facts[inputid + ".length"]
                return {
                    "space": v["space"],
                    "start": hx(start),
                    "end": hx(start + length),
                }
            return {k: expand(x) for k, x in v.items()}

        out = {
            "regions": {
                x["id"]: {k: v for k, v in x.items() if k not in {"id", "origin"}}
                for x in lay["regions"]
                if x.get("origin", "map") == "map"
            },
            "fields": {x["id"]: expand(self.located(x)) for x in lay["fields"]},
            "inputs": index(lay["inputs"]),
            "checks": expand(lay["checks"]),
            "banks": copy.deepcopy(lay["banks"]),
            "settings": plan["settings"],
            "user-mappings": plan["user-mappings"],
            "option-defaults": plan["option-defaults"],
            "naming": copy.deepcopy(naming),
            "uses-inputs": copy.deepcopy(plan["uses-inputs"]),
            "sizes": copy.deepcopy(self.documents[file]["sizes"]),
        }
        if "topology" in self.documents[file]:
            out["topology"] = copy.deepcopy(self.documents[file]["topology"])
        out["initial"] = expand(plan["initial"])
        if "input" in out["initial"] and "size-without-source" in out["initial"]:
            out["initial"]["length"] = (
                hx(facts[out["initial"]["input"] + ".length"])
                if out["initial"]["input"] + ".length" in facts
                else out["initial"]["size-without-source"]
            )
        out["integrity"] = {}
        for x in lay["integrity"]:
            if "run" in x:
                require(x["run"] in self.invocations, "Dangling invocation " + x["run"])
                v = self.invocations[x["run"]]
            else:
                v = {k: v for k, v in x.items() if k != "id"}
            out["integrity"][x["id"]] = expand(v)
        active = dict(facts)
        for o in plan["option-defaults"]:
            active.setdefault("option." + o["id"], o["default"])
        for phase in ["pre", "steps", "post"]:
            out[phase] = [
                expand(s)
                for s in plan[phase]
                if "when" not in s or matches(s["when"], active)
            ]
        for k in ["bank-processing", "recompute"]:
            if k in lay:
                out[k] = expand(lay[k])

        # Expand tool references in executable use positions to compare their complete contracts.
        def run_contract(v):
            if isinstance(v, list):
                return [run_contract(x) for x in v]
            if not isinstance(v, dict):
                return v
            result = {}
            for k, x in v.items():
                if k in {"run", "finalize"} and isinstance(x, str):
                    require(x in self.invocations, "Dangling invocation " + x)
                    if k == "run" and "rule" in v:
                        parent, _, _, _, _ = self.select(file, v["rule"], facts)
                        aliases = [
                            s["id"] for s in parent["integrity"] if s.get("run") == x
                        ]
                        require(
                            len(aliases) == 1,
                            "Recompute must reference the selected existing local invocation",
                        )
                        local, _ = self.resolve(file, v["rule"], facts)
                        result[k] = local["integrity"][aliases[0]]
                    else:
                        result[k] = expand(self.invocations[x])
                else:
                    result[k] = run_contract(x)
            return result

        for phase in ["pre", "steps", "post", "recompute", "bank-processing"]:
            if phase in out:
                out[phase] = run_contract(out[phase])
        # Resolve explicit historical identities to the source vocabulary for the unchanged comparator.
        # Values, categories, array order and tool metadata remain item-for-item comparisons.
        return restore_identities(
            tool_metadata(out, self.tools), self.source_aliases
        ), identity

    def validate_links(self):
        checked = 0
        for file, doc in self.documents.items():
            if not file.startswith("ic/"):
                continue
            layouts = index(doc["layouts"])
            bindings = index(doc["rules"])
            for bid, b in bindings.items():
                require(
                    set(b["ic-refs"]) <= set(doc["ic"]),
                    "Rule binding refers to a different IC",
                )
                require(b["rule"] in self.rules, "Dangling rule " + b["rule"])
                rd = self.rules[b["rule"]]
                plans = index(rd["plans"])
                namings = index(rd["naming"])
                uses = {c["use"] for v in b["variants"] for c in v["cases"]}
                require(
                    uses == {x["layout"] for x in b["plan-bindings"]},
                    "Variant/plan layout references disagree " + bid,
                )
                require(
                    len({x["layout"] for x in b["plan-bindings"]})
                    == len(b["plan-bindings"]),
                    "Multiple complete plans for one layout " + bid,
                )
                for link in b["plan-bindings"]:
                    require(
                        link["layout"] in layouts, "Dangling layout " + link["layout"]
                    )
                    require(link["plan"] in plans, "Dangling plan " + link["plan"])
                    require(
                        link["naming-ref"] in namings,
                        "Dangling naming reference " + link["naming-ref"],
                    )
                    lay = self.layout(file, link["layout"])
                    rs = index(lay["regions"])
                    inputs = index(lay["inputs"])
                    fs = {x["id"]: self.located(x) for x in lay["fields"]}
                    plan = plans[link["plan"]]
                    checked += 1
                    require(
                        set(plan["uses-inputs"]) <= set(inputs),
                        "Rule uses undeclared input " + bid,
                    )
                    for mapping in plan["user-mappings"]:
                        require(
                            mapping["input"] in inputs,
                            "Dangling mapping input " + mapping["input"],
                        )
                        require(
                            mapping["root"] == "output-image" or mapping["root"] in rs,
                            "Dangling mapping root " + mapping["root"],
                        )
                        require(
                            set(mapping.get("allowed-regions", [])) <= set(rs),
                            "Dangling mapping region whitelist",
                        )
                    # All region references in all plans and tool contracts must exist in every bound layout.
                    surfaces = [plan] + list(fs.values()) + lay["checks"]
                    for x in lay["integrity"]:
                        if "run" in x:
                            require(
                                x["run"] in self.invocations,
                                "Dangling invocation " + x["run"],
                            )
                            surfaces.append(self.invocations[x["run"]])
                        else:
                            surfaces.append(x)
                    if "recompute" in lay:
                        r = lay["recompute"]
                        require(
                            r["rule"] in bindings,
                            "Dangling recompute rule " + r["rule"],
                        )
                        require(
                            r["run"] in self.invocations,
                            "Dangling recompute invocation " + r["run"],
                        )
                        target = bindings[r["rule"]]
                        require(
                            all(
                                p["kind"] == "ctrlram-replace"
                                for p in self.rules[target["rule"]]["plans"]
                            ),
                            "Recompute target must be CtrlRAM",
                        )
                        require(
                            any(
                                any(
                                    s.get("run") == r["run"]
                                    for s in self.layout(file, link["layout"])[
                                        "integrity"
                                    ]
                                )
                                for link in target["plan-bindings"]
                            ),
                            "Recompute must reuse an existing local invocation",
                        )
                    for bank in lay.get("bank-processing", []):
                        require(
                            bank["rule"] in bindings,
                            "Dangling bank rule " + bank["rule"],
                        )
                        require(
                            bank["bank"] in rs
                            and bank["bank"] in {b["id"] for b in lay["banks"]},
                            "Dangling bank " + bank["bank"],
                        )
                        if "finalize" in bank:
                            require(
                                bank["finalize"] in self.invocations,
                                "Dangling finalizer " + bank["finalize"],
                            )
                    transforms = {
                        s.get("id")
                        for phase in ["pre", "steps", "post"]
                        for s in plan[phase]
                        if s["kind"] == "transform" and s.get("encoding") == "u32le"
                    }
                    for surface in surfaces:
                        for path, v, _ in walk(surface):
                            if not isinstance(v, dict):
                                continue
                            if "region" in v:
                                require(
                                    v["region"] in rs,
                                    f"{file}:{bid}:{path}: dangling region {v['region']}",
                                )
                            if v.get("kind") in {"marker", "selected"}:
                                require(
                                    v["within"] in rs,
                                    "Dangling locator result region " + v["within"],
                                )
                            if v.get("kind") == "integrity":
                                require(
                                    v["run"] in self.invocations,
                                    "Dangling rule invocation " + v["run"],
                                )
                            if "placed-write" in v:
                                pw = v["placed-write"]
                                require(
                                    pw["marker"] in fs,
                                    "Dangling placement marker " + pw["marker"],
                                )
                                loc = fs[pw["marker"]].get("at", {})
                                require(
                                    loc.get("kind") == "marker"
                                    and loc.get("match") == "unique",
                                    "Placement requires a unique actual marker locator",
                                )
                            if (
                                v.get("covers") == "unknown"
                                or v.get("algorithm") == "unknown"
                            ):
                                require(
                                    v.get("reason"),
                                    "Unknown checksum facts require a reason",
                                )
                            if "relocation" in v:
                                require(
                                    v["input"] in inputs, "Dangling relocation input"
                                )
                                require(
                                    set(v["relocation"]["transforms"]) <= transforms,
                                    "Dangling relocation transform",
                                )
                    for f in fs.values():
                        self.check_metadata(f, inputs, transforms)

                    def has_field(reference):
                        pieces = reference.removeprefix("fields.").split(".")

                        def present(scope):
                            for part in pieces:
                                if part not in scope:
                                    return False
                                scope = scope[part].get("fields", {})
                            return True

                        return present(fs)

                    for f in fs.values():
                        for _, v, _ in walk(f):
                            if (
                                isinstance(v, dict)
                                and "from-fields" in v
                                and "input" not in v
                            ):
                                refs = (
                                    v["from-fields"]
                                    if isinstance(v["from-fields"], list)
                                    else [v["from-fields"]]
                                )
                                require(
                                    all(has_field(x) for x in refs),
                                    "Dangling decoded-field reference",
                                )
                    for token in namings[link["naming-ref"]]["tokens"]:
                        if "from-fields" in token:
                            reference = token["from-fields"]
                            root = reference.removeprefix("fields.").split(".")[0]
                            # Naming may consume optional decoded run metadata. Source A explicitly
                            # supplies placeholders for unavailable DP/TP version projections. These
                            # are runtime property paths, not file/region/definition declaration IDs.
                            optional_projection = (
                                root in fs
                                and fs[root]["kind"] == "group"
                                and isinstance(token["missing"], dict)
                                and "text" in token["missing"]
                                and reference
                                == {
                                    "dpcmi-version": "fields.version-info.dp",
                                    "firmware-config-tp-version": "fields.version-info.tp",
                                }.get(token.get("formatter"))
                            )
                            require(
                                has_field(reference) or optional_projection,
                                "Dangling naming field " + reference,
                            )
                    for rid, r in rs.items():
                        if "parent" in r:
                            require(
                                r["parent"] in rs,
                                "Dangling parent region " + r["parent"],
                            )
                            p = rs[r["parent"]]
                            require(
                                r["space"] == p["space"]
                                and int(p["start"], 16)
                                <= int(r["start"], 16)
                                < int(r["end"], 16)
                                <= int(p["end"], 16),
                                "Parent containment failure " + rid,
                            )
                        if (
                            isinstance(lay["size"], str)
                            and r.get("origin", "map") == "map"
                        ):
                            require(
                                int(r["end"], 16) <= int(lay["size"], 16),
                                "Region exceeds layout " + rid,
                            )
                    for bk in lay["banks"]:
                        require(
                            bk["id"] in rs and rs[bk["id"]]["start"] == bk["start"],
                            "Bank origin differs from region " + bk["id"],
                        )
                self.check_variant_overlap(bid, b["variants"][0]["cases"])
        return checked

    @staticmethod
    def check_metadata(f, inputs, transforms):
        fields = f.get("fields", {})
        ids = set(fields)
        spans = {x["id"] for x in f.get("spans", [])}
        series = {x["id"] for x in f.get("series", [])}
        groups = {x["id"] for x in f.get("groups", [])}
        kinds = {"field": ids, "span": spans, "series-ref": series, "group": groups}
        for r in f.get("relations", []):
            require(
                r["field"] in ids and r["other"] in ids, "Dangling metadata relation"
            )
        for s in f.get("semantics", []):
            if "id" in s:
                require(s["id"] in ids, "Dangling semantic field " + s["id"])
            if "span" in s:
                require(s["span"] in spans, "Dangling semantic span")
        for s in f.get("series", []):
            require(
                all(x["field"] in ids for x in s["entries"]), "Dangling series member"
            )
            indices = [x["index"] for x in s["entries"]]
            require(len(indices) == len(set(indices)), "Duplicate series index")
            for c in s.get("cases", []):
                require(
                    set(c["indices"]) <= set(indices), "Dangling active series index"
                )
        for g in f.get("groups", []):
            for m in g["members"]:
                for kind, n in m.items():
                    require(
                        n in (series if kind == "series-ref" else ids),
                        "Dangling group member " + n,
                    )
        for b in f.get("bindings", []):
            require(b["input"] in inputs, "Dangling metadata input " + b["input"])
            if "relocation" in b:
                require(b["relocation"]["group"] in groups, "Dangling relocation group")
                require(
                    len(set(b["relocation"]["transforms"]))
                    == len(b["relocation"]["transforms"]),
                    "Duplicate relocation transform",
                )
            else:
                require(
                    "relocation" not in b["uses"], "Relocation cannot be a read purpose"
                )
                for t in b["targets"]:
                    for kind, n in t.items():
                        require(n in kinds[kind], "Dangling binding target " + n)
        for b in f.get("map-selection", []):
            require(
                b["input"] in inputs and set(b["from-fields"]) <= ids,
                "Dangling map selection binding",
            )

    @staticmethod
    def check_variant_overlap(bid, cases):
        """An exact symbolic conjunction intersection catches ambiguities beyond sampled facts."""

        def disjoint(a, b):
            by = {}
            for w in (a if isinstance(a, list) else [a]) + (
                b if isinstance(b, list) else [b]
            ):
                by.setdefault(w["fact"], []).append(w)

            def cv(v):
                return (
                    tuple(int(x) for x in v.split("."))
                    if isinstance(v, str)
                    and v.count(".") == 2
                    and all(x.isdigit() for x in v.split("."))
                    else v
                )

            for ws in by.values():
                if any(w["kind"] == "absent" for w in ws) and any(
                    w["kind"] != "absent" for w in ws
                ):
                    return True
                eq = [w["equals"] for w in ws if w["kind"] == "equals"]
                if eq:
                    if any(x != eq[0] for x in eq):
                        return True
                    try:
                        if any(
                            "at-least" in w
                            and cv(eq[0]) < cv(w["at-least"])
                            or "at-most" in w
                            and cv(eq[0]) > cv(w["at-most"])
                            or "below" in w
                            and cv(eq[0]) >= cv(w["below"])
                            for w in ws
                        ):
                            return True
                    except TypeError:
                        raise ValueError("Incompatible fact comparison types")
                lo = [cv(w["at-least"]) for w in ws if "at-least" in w]
                hi = [cv(w["at-most"]) for w in ws if "at-most" in w]
                ex = [cv(w["below"]) for w in ws if "below" in w]
                if lo and (hi and max(lo) > min(hi) or ex and max(lo) >= min(ex)):
                    return True
            return False

        for a, b in itertools.combinations(cases, 2):
            require(disjoint(a["when"], b["when"]), bid + ": two variants can match")

    def check_routes(self):
        policy = read(ROOT / "docs/contracts/canonical-capability-policy-v1.json")[
            "routes"
        ]
        expected = {x["routeId"] for x in policy}
        routes = self.routes["routes"]
        canonical = [x["routeId"] for x in routes if "routeId" in x]
        require(len(canonical) == len(set(canonical)), "Duplicate route id")
        require(
            set(canonical) == expected and len(canonical) == 94,
            "routes.json must cover 94 exact routeId values once",
        )
        expected_ids = set(reference_inventory()["expectedRouteIds"])
        require(
            {x["expected"] for x in routes if "expected" in x} == expected_ids,
            "Expected route IDs changed",
        )
        require(len(routes) == 132, "Coverage matrix row count drift")
        require(
            len({x["id"] for x in routes}) == len(routes),
            "Duplicate new route identity",
        )
        require(
            len({x["id"] for x in self.routes["combinations"]})
            == len(self.routes["combinations"]),
            "Duplicate selectable combination ID",
        )
        by_id = {r["id"]: r for r in routes}
        for c in self.routes["combinations"]:
            require(c["route"] in by_id, "Dangling combination route mapping")
            require(
                c["ic"] == by_id[c["route"]]["ic"], "Combination maps to a different IC"
            )
            binding = next(
                b for b in self.documents[c["file"]]["rules"] if b["id"] == c["binding"]
            )
            require(
                c["ic"] in binding["ic-refs"], "Combination outside declared IC scope"
            )
            require(
                binding["rule"] == c["rule"]
                and any(
                    link["layout"] == c["layout"] and link["plan"] == c["plan"]
                    for link in binding["plan-bindings"]
                ),
                "Combination does not identify a selectable rule/layout plan",
            )
        for row in routes:
            for selection in row["selections"]:
                binding = next(
                    b
                    for b in self.documents[selection["file"]]["rules"]
                    if b["id"] == selection["binding"]
                )
                require(
                    row["ic"] in binding["ic-refs"],
                    "Route selected a binding of another IC",
                )
                _, _, _, _, identity = self.select(
                    selection["file"], selection["binding"], selection["facts"]
                )
                require(
                    identity
                    == (selection["layout"], selection["rule"], selection["plan"]),
                    "Route selection identity drift " + row["id"],
                )
        return len(canonical), len(expected_ids)


def differences(a, b, path="$"):
    if type(a) is not type(b):
        return [{"path": path, "a": a, "c": b}]
    if isinstance(a, dict):
        out = []
        for k in sorted(set(a) | set(b)):
            if k not in a:
                out.append({"path": path + "." + k, "a": "<missing>", "c": b[k]})
            elif k not in b:
                out.append({"path": path + "." + k, "a": a[k], "c": "<missing>"})
            else:
                out.extend(differences(a[k], b[k], path + "." + k))
        return out
    if isinstance(a, list):
        out = []
        for i in range(max(len(a), len(b))):
            if i >= len(a):
                out.append({"path": f"{path}[{i}]", "a": "<missing>", "c": b[i]})
            elif i >= len(b):
                out.append({"path": f"{path}[{i}]", "a": a[i], "c": "<missing>"})
            else:
                out.extend(differences(a[i], b[i], f"{path}[{i}]"))
        return out
    return [] if a == b else [{"path": path, "a": a, "c": b}]


def general_merge_checks(tree):
    policy = read(ROOT / "docs/contracts/canonical-capability-policy-v1.json")["routes"]
    recipes = {
        r["routeId"]: r
        for r in read(ROOT / "docs/references/g0-output-baseline/recipes.json")[
            "recipes"
        ]
    }
    checked = 0
    for route in policy:
        if route["workflowId"] != "general-merge":
            continue
        file = tree.ics[route["icId"]]
        recipe = recipes[route["routeId"]]
        facts = {"ic-count": 1, "setting.requested-output-length": hx(recipe["size"])}
        result, _ = tree.resolve(file, "general-merge", facts)
        profile = read(
            ROOT / reference_inventory()["generalMergeProfiles"][route["icId"]]
        )
        source_bound = profile["inputSlots"][0]["acceptance"]["lengthRule"]
        setting = result["settings"][0]
        require(
            (int(setting["min-length"], 16), int(setting["max-length"], 16))
            == (source_bound["minimumBytes"], source_bound["maximumBytes"]),
            "General Merge setting bounds differ from the source in-memory length contract",
        )
        require(
            result["naming"]["template"].replace("{ic}", route["icId"].lower())
            == profile["output"]["fileNameTemplate"],
            "General Merge filename differs from the source profile",
        )
        require(
            result["user-mappings"][0]["root"] == "output-image"
            and result["user-mappings"][0]["overlap"] == "reject",
            "General Merge must preserve the logical output space and reject overlap",
        )
        require(
            int(result["initial"]["length"], 16) == recipe["size"]
            and int(result["initial"]["fill"], 16) == recipe["fill"],
            "General Merge baseline output extent/fill changed",
        )
        require(
            recipe["size"]
            > max(x["targetStart"] + x["length"] for x in recipe["bindings"]),
            "Baseline no longer exercises extent independent of mapping ends",
        )
        for length in ["0x0", "0x80000000", "4", "0x+4", "0x4 "]:
            try:
                tree.resolve(
                    file,
                    "general-merge",
                    {"ic-count": 1, "setting.requested-output-length": length},
                )
            except (ValueError, KeyError):
                pass
            else:
                raise ValueError(
                    "Invalid General Merge output length accepted " + length
                )
        checked += 1
    require(checked == 10, "Expected ten canonical General Merge recipes")
    pending = [
        r
        for r in tree.routes["routes"]
        if r["workflow"].lower().replace(" ", "-").startswith("general-merge")
    ]
    require(
        len(pending) == 11 and all(r["status"] == "written" for r in pending),
        "General Merge approval status drift",
    )
    return checked


def rejection_cases(tree):
    rejected = []

    def reject(name, mutate, check):
        sample = copy.deepcopy(tree)
        sample.layout_cache = {}
        mutate(sample)
        try:
            check(sample)
        except (ValueError, KeyError):
            rejected.append(name)
        else:
            raise ValueError("Negative case accepted: " + name)

    first = next(
        (file, b, link)
        for file, d in tree.documents.items()
        if file.startswith("ic/")
        for b in d["rules"]
        for link in b["plan-bindings"]
        if tree.rules[b["rule"]]["plans"][0]["pre"]
        and tree.rules[b["rule"]]["plans"][0]["pre"][0]["kind"] == "check-non-uniform"
    )
    file, b, link = first
    rid = b["rule"]
    pid = link["plan"]

    def dangling(t):
        p = next(x for x in t.rules[rid]["plans"] if x["id"] == pid)
        p["pre"][0]["from"]["region"] = "absent-region"

    reject("dangling region id", dangling, lambda t: t.validate_links())

    def missing(t):
        # Freeze this resolved layout into the IC and remove one region used by its rule.
        lay = copy.deepcopy(t.layout(file, link["layout"]))
        lay.pop("include", None)
        p = next(x for x in t.rules[rid]["plans"] if x["id"] == pid)
        used = p["pre"][0]["from"]["region"]
        lay["regions"] = [r for r in lay["regions"] if r["id"] != used]
        doc = t.documents[file]
        doc["layouts"] = [lay if x["id"] == lay["id"] else x for x in doc["layouts"]]
        t.layout_cache = {}

    reject("layout missing a rule region", missing, lambda t: t.validate_links())

    def ambiguous(t):
        r = next(x for x in t.documents[file]["rules"] if x["id"] == b["id"])
        r["variants"][0]["cases"].append(copy.deepcopy(r["variants"][0]["cases"][0]))

    reject("two variants matching", ambiguous, lambda t: t.validate_links())
    reject(
        "duplicate route id",
        lambda t: t.routes["routes"].append(
            copy.deepcopy(next(x for x in t.routes["routes"] if "routeId" in x))
        ),
        lambda t: t.check_routes(),
    )
    case = next(c for c in tree.cases if c["binding"].find("ctrlram-replace") >= 0)
    expected, _ = tree.resolve(case["file"], case["binding"], case["facts"])
    modified = copy.deepcopy(tree)
    modified.layout_cache = {}
    lay, _, _, _, _ = modified.select(case["file"], case["binding"], case["facts"])
    invocation = next(
        s["run"]
        for s in lay["integrity"]
        if "run" in s and modified.invocations[s["run"]].get("commands")
    )
    modified.invocations[invocation]["commands"][0]["mode"] += "-negative-case"
    actual, _ = modified.resolve(case["file"], case["binding"], case["facts"])
    require(
        differences(expected, actual), "Equivalence accepted a changed command mode"
    )
    rejected.append("equivalence detects changed command mode")
    modified = copy.deepcopy(tree)
    modified.layout_cache = {}
    lay = copy.deepcopy(modified.layout(case["file"], case["layout"]))
    at = next(
        v
        for _, v, _ in walk(lay["fields"])
        if isinstance(v, dict) and {"space", "start", "end"} <= v.keys()
    )
    at["start"] = hx(int(at["start"], 16) + 1)
    modified.layout_cache[(case["file"], case["layout"])] = lay
    actual, _ = modified.resolve(case["file"], case["binding"], case["facts"])
    require(
        differences(expected, actual), "Equivalence accepted a changed field location"
    )
    rejected.append("equivalence detects changed field location")
    sample = next(
        (path, data) for path, data in tree.documents.items() if path.startswith("ic/")
    )
    path, data = sample
    for name, mutate in [
        ("missing schema version", lambda d: d.pop("schema-version")),
        (
            "schema version not first",
            lambda d: d.update({"schema-version": d.pop("schema-version")}),
        ),
        ("boolean schema version", lambda d: d.update({"schema-version": True})),
        (
            "hexadecimal id suffix",
            lambda d: d["layouts"][0].update({"id": "layout-deadbeef"}),
        ),
        (
            "sequence-number id",
            lambda d: d["layouts"][0].update({"id": "layout-12-output"}),
        ),
    ]:
        modified = copy.deepcopy(data)
        mutate(modified)
        try:
            document_policy(path, modified)
        except ValueError:
            rejected.append(name)
        else:
            raise ValueError("Negative document policy case accepted: " + name)
    schema = read(tree.base / "schema/tool.schema.json")
    raw = copy.deepcopy(tree.documents["tools/legacy-combiner.yaml"])
    command = raw["invocations"][0]["commands"][0]
    command["mode"] = "INLINE_PARAMETER_NOT_ALLOWED"
    for engine in ["fallback", "jsonschema"]:
        try:
            if engine == "fallback":
                fallback_schema_check(raw, schema)
            else:
                try:
                    from jsonschema import Draft202012Validator
                except ImportError:
                    continue
                Draft202012Validator(schema).validate(raw)
        except Exception:
            rejected.append(engine + " rejects inline command parameters")
        else:
            raise ValueError("Inline command parameter accepted: " + engine)

    def duplicate_rule(t):
        name = next(iter(t.rules))
        t.rules["duplicate-rule"] = copy.deepcopy(t.rules[name])

    reject(
        "duplicate resolved rule body",
        duplicate_rule,
        lambda t: t.check_distinct_rules(),
    )
    for name, old, new in [
        ("alias cannot conceal tool version", "1.13.0", "9.99.0"),
        ("alias cannot conceal CRC", "CRC8", "CRC8_CHANGED"),
        ("alias cannot conceal address", "0x40000", "0x40100"),
        (
            "alias cannot conceal naming template",
            "{ic}-general-merge.bin",
            "changed.bin",
        ),
    ]:
        modified = copy.deepcopy(tree.documents)
        modified["tools/legacy-combiner.yaml"].setdefault("aliases", []).append(
            {"previous-id": old, "current-id": new}
        )
        try:
            validate_identity_aliases(modified)
        except ValueError:
            rejected.append(name)
        else:
            raise ValueError("Nonidentity alias accepted: " + name)
    current, previous = next(iter(tree.source_aliases.items()))
    values = {
        "version": current,
        "crc": current,
        "start": current,
        "end": current,
        "template": current,
        "fill": current,
        "scalar": current,
        "addend": current,
        "text": current,
        "equals": current,
    }
    require(
        restore_identities(values, tree.source_aliases) == values,
        "Identity restoration rewrote a literal value position",
    )
    require(
        restore_identities(
            {"fields": {current: {"from-fields": "fields." + current}}},
            tree.source_aliases,
        )
        == {"fields": {previous: {"from-fields": "fields." + previous}}},
        "Identity restoration lost a typed declaration/reference",
    )
    rejected.append("identity aliases leave literal value positions unchanged")
    modified = copy.deepcopy(tree)
    modified.layout_cache = {}
    modified.tools["legacy-combiner"]["version"] = "9.99.0"
    actual, _ = modified.resolve(case["file"], case["binding"], case["facts"])
    require(
        differences(expected, actual), "Equivalence accepted a changed tool version"
    )
    rejected.append("equivalence detects changed tool version")
    modified = copy.deepcopy(tree)
    modified.layout_cache = {}
    command = next(
        command
        for command in modified.invocations[invocation]["commands"]
        if "crc" in command
    )
    command["crc"] += "-negative-case"
    actual, _ = modified.resolve(case["file"], case["binding"], case["facts"])
    require(differences(expected, actual), "Equivalence accepted a changed CRC token")
    rejected.append("equivalence detects changed CRC token")
    modified = copy.deepcopy(tree)
    modified.layout_cache = {}
    selected = modified.documents[case["file"]]["rules"]
    binding = next(x for x in selected if x["id"] == case["binding"])
    naming_id = next(
        x["naming-ref"]
        for x in binding["plan-bindings"]
        if x["layout"] == case["layout"]
    )
    naming = next(
        x for x in modified.rules[binding["rule"]]["naming"] if x["id"] == naming_id
    )
    naming["template"] += "-negative-case"
    actual, _ = modified.resolve(case["file"], case["binding"], case["facts"])
    require(
        differences(expected, actual), "Equivalence accepted a changed naming template"
    )
    rejected.append("equivalence detects changed naming template")
    return rejected


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=HERE)
    parser.add_argument("--force-fallback", action="store_true")
    args = parser.parse_args(argv)
    tree = Tree(args.root.resolve(), args.force_fallback)
    print("Engine:", tree.engine, "; restricted offline YAML reader")
    print("PASS schemas:", len(tree.documents), "documents / 5 document types")
    links = tree.validate_links()
    print(
        "PASS ID index, references, region use, metadata, bank/recompute bindings:",
        links,
        "rule/layout pairs",
    )
    print(
        "PASS routes: %d canonical routeId values once; %d exact expected IDs"
        % tree.check_routes()
    )
    print(
        "PASS schema-version first key and meaningful ids; shared tool parameters:",
        tree.parameter_count,
    )
    print(
        "PASS General Merge:",
        general_merge_checks(tree),
        "baseline request extents retained; 11 owner-approved typed settings",
    )
    rejected = rejection_cases(tree)
    print("PASS negative controls:", len(rejected), "; ".join(rejected))
    print(
        "RESULT: PASS (IC source structure/data only; no firmware execution or A/C conversion comparison)"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print("FAIL:", error, file=sys.stderr)
        raise SystemExit(1)
