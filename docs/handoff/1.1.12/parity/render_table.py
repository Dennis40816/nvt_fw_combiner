"""Render the path-free v0.9.16 local comparison JSON as a Markdown table.

Usage (from the repository root):

    python docs/handoff/1.1.12/parity/render_table.py \
        --table-json docs/handoff/1.1.12/parity/v0916-local-comparison.json \
        --explanations docs/handoff/1.1.12/parity/explanations.json \
        --output docs/handoff/1.1.12/parity/v0916-local-comparison.md

`explanations.json` maps a route id to `{"explanation": ..., "basis":
"fact" | "hypothesis", "source": ...}`. Rows without an entry render the
default text for their result.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

DEFAULT_TEXT = {
    "equal": "No difference.",
    "not-covered": "Not covered: no canonical input.",
    "candidate-only": "Not compared: predecessor not run (see blocker).",
    "error": "Execution error; see the JSON row.",
    "different": "unexplained",
}
WORKFLOW = {"standard-merge": "Standard", "ab-merge": "AB", "ctrlram-replace": "CtrlRAM"}


def short(value: str | None) -> str:
    return f"`{value[:16]}`" if value else "-"


def ranges_text(row: dict[str, Any]) -> str:
    comparison = row.get("comparison")
    if not comparison:
        return "-"
    if comparison["equal"]:
        return "none"
    parts = [f"[0x{r['start']:X},0x{r['endExclusive']:X})" for r in comparison["ranges"]]
    text = ", ".join(parts)
    if comparison["rangesTruncated"]:
        text += f", ... ({comparison['rangeCount']} ranges)"
    size = ""
    if not comparison["sizeEqual"]:
        size = f"; sizes {comparison['baselineSize']} vs {comparison['candidateSize']}"
    return f"{comparison['differentByteCount']} bytes in {comparison['rangeCount']}: {text}{size}"


def output_hash(row: dict[str, Any], side: str) -> str | None:
    observation = row.get(side) or {}
    return (observation.get("output") or {}).get("sha256")


def render(table: dict[str, Any], explanations: dict[str, Any]) -> str:
    lines = [
        "| # | IC | Workflow | IC count | Map variant | Proof | Runnable | Result | Differing ranges (file offsets, half-open) | v0.9.16 sha256 | Candidate sha256 | Proposed explanation |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    ordered = sorted(
        table["routes"],
        key=lambda r: (r["icId"], r["workflowId"], r["icCountVariant"], r["mapVariant"]),
    )
    for number, row in enumerate(ordered, start=1):
        note = explanations.get(row["routeId"])
        if note:
            text = f"{note['explanation']} ({note['basis']}; {note['source']})"
        else:
            text = DEFAULT_TEXT[row["result"]]
        proof = "TP prefix" if row["proofKind"] == "tp-prefix-transitive" else "exact"
        lines.append(
            "| {n} | {ic} | {wf} | {count} | `{map}` | {proof} | {run} | {result} | {ranges} | {b} | {c} | {text} |".format(
                n=number,
                ic=row["icId"],
                wf=WORKFLOW[row["workflowId"]],
                count=row["icCountVariant"],
                map=row["mapVariant"],
                proof=proof,
                run="yes" if row["runnable"] else "no",
                result=row["result"],
                ranges=ranges_text(row),
                b=short(output_hash(row, "baseline")),
                c=short(output_hash(row, "candidate")),
                text=text,
            )
        )
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--table-json", type=Path, required=True)
    parser.add_argument("--explanations", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    table = json.loads(args.table_json.read_text(encoding="utf-8"))
    explanations = (
        json.loads(args.explanations.read_text(encoding="utf-8")) if args.explanations else {}
    )
    header = args.output.read_text(encoding="utf-8").split("<!-- table -->")[0] if args.output.exists() else ""
    body = render(table, explanations)
    content = f"{header}<!-- table -->\n\n{body}" if header else body
    args.output.write_text(content, encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
