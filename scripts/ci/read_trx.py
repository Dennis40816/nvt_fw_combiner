"""Read only public method identities and counters from VSTest TRX results."""
from __future__ import annotations

import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

if __package__:
    from .fixed_argparse import FixedArgumentParser
else:
    from fixed_argparse import FixedArgumentParser

INVALID_TRX = "TRX results missing or invalid."
NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
SAFE_NAME = re.compile(r"[A-Za-z0-9_.]+")


class _NoDtdBuilder(ET.TreeBuilder):
    def doctype(self, name: str, pubid: str | None, system: str | None) -> None:
        raise ValueError(INVALID_TRX)


def empty_results() -> dict:
    return dict(Completed=False, Total=0, Passed=0, Failed=0, Skipped=0,
                NotExecuted=0, FailedTests=[], ExecutedClasses=[], Results=[])


def _read_file(path: Path) -> dict:
    document = ET.parse(path, parser=ET.XMLParser(target=_NoDtdBuilder()))
    root = document.getroot()
    if root.tag != NS + "TestRun":
        raise ValueError(INVALID_TRX)
    summary = root.find(NS + "ResultSummary")
    counters = None if summary is None else summary.find(NS + "Counters")
    if counters is None:
        raise ValueError(INVALID_TRX)
    counts = {}
    for name in ("total", "passed", "failed"):
        value = counters.get(name, "")
        if re.fullmatch(r"[0-9]+", value) is None:
            raise ValueError(INVALID_TRX)
        counts[name] = int(value)
    definitions = {}
    for definition in root.findall(f"{NS}TestDefinitions/{NS}UnitTest"):
        method = definition.find(NS + "TestMethod")
        if method is None:
            raise ValueError(INVALID_TRX)
        # A nested class is written as Outer+Inner; the public identity uses a dot.
        class_name = method.get("className", "").split(",", 1)[0].strip().replace("+", ".")
        method_name = method.get("name", "").split("(", 1)[0].strip()
        if not class_name or not method_name:
            raise ValueError(INVALID_TRX)
        if not SAFE_NAME.fullmatch(class_name):
            class_name = ""
        if not class_name or not SAFE_NAME.fullmatch(method_name):
            method_name = ""
        definitions[definition.get("id", "")] = (
            class_name, f"{class_name}.{method_name}" if method_name else ""
        )
    results = []
    observed = dict(Passed=0, Failed=0, NotExecuted=0)
    for result in root.findall(f"{NS}Results/{NS}UnitTestResult"):
        class_name, method_name = definitions[result.get("testId", "")]
        outcome = result.get("outcome", "")
        if outcome in observed:
            observed[outcome] += 1
        else:
            outcome = "Incomplete"
        results.append(dict(ClassName=class_name, MethodName=method_name, Outcome=outcome))
    if (observed["Passed"] != counts["passed"] or observed["Failed"] != counts["failed"]
            or len(results) > counts["total"] or sum(observed.values()) > counts["total"]):
        raise ValueError(INVALID_TRX)
    return dict(Completed=summary.get("outcome") == "Completed", Total=counts["total"],
                Passed=counts["passed"], Failed=counts["failed"],
                Skipped=observed["NotExecuted"], Results=results)


def read_trx_results(results_directory: Path | str) -> dict:
    """Port of FreeformHelper Read-TrxResults; no display names or Output access."""
    try:
        candidate = Path(results_directory)
        files = (
            [candidate]
            if candidate.is_file() and candidate.suffix == ".trx"
            else sorted(candidate.rglob("*.trx"))
        )
        if not files:
            raise ValueError(INVALID_TRX)
        counts = empty_results()
        counts["Completed"] = True
        for path in files:
            row = _read_file(path)
            counts["Completed"] &= row["Completed"]
            for name in ("Total", "Passed", "Failed", "Skipped"):
                counts[name] += row[name]
            counts["Results"].extend(row["Results"])
        counts["NotExecuted"] = (
            counts["Total"] - counts["Passed"] - counts["Failed"] - counts["Skipped"]
        )
        counts["FailedTests"] = sorted(
            {r["MethodName"] for r in counts["Results"] if r["Outcome"] == "Failed" and r["MethodName"]}
        )
        counts["ExecutedClasses"] = sorted(
            {
                r["ClassName"]
                for r in counts["Results"]
                if r["Outcome"] in ("Passed", "Failed") and r["ClassName"]
            }
        )
        return counts
    except (OSError, ValueError, KeyError, ET.ParseError, LookupError):
        raise ValueError(INVALID_TRX) from None


def get_example_data_test_names(test_root: Path) -> set[str]:
    """Match upstream public declarations; NFC floors stay zero until measured."""
    pattern = re.compile(r"\[ExampleData(?:Fact|Theory)(?:\([^\]]*\))?\]\s*"
                         r"(?:\[[^\]]*\]\s*)*public\s+(?:async\s+)?(?:void|Task)\s+"
                         r"([A-Za-z0-9_]+)\s*\(")
    names = set()
    for path in test_root.rglob("*.cs"):
        text = path.read_text(encoding="utf-8-sig")
        methods = pattern.findall(text)
        if methods:
            namespace = re.search(r"\bnamespace\s+([A-Za-z0-9_.]+)", text)
            cls = re.search(r"\bclass\s+([A-Za-z0-9_]+)", text)
            if namespace and cls:
                names.update(f"{namespace[1]}.{cls[1]}.{method}" for method in methods)
    return names


def main() -> int:
    parser = FixedArgumentParser(description=__doc__)
    parser.add_argument("results_directory", type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(read_trx_results(args.results_directory), indent=2))
        return 0
    except ValueError:
        print(INVALID_TRX, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
