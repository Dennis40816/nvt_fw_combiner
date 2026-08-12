#!/usr/bin/env python3
"""Create one evidence-backed 0.10.x refactor progress snapshot."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PLAN_PATH = Path("docs/governance/0.10.x-ticket-dependency-plan.md")
PLAN_TABLE_HEADER = "| Depth | Wave | Issue | Approved outcome | Blocked by |"
PLAN_TABLE_SEPARATOR = "| ---: | --- | ---: | --- | --- |"
PLAN_TABLE_END_HEADING = "## Stable execution ordering"
PRELOAD_TABLE_HEADER = (
    "| Depth | Preload wave | Issue | Approved outcome | Blocked by |"
)
ISSUE_ROW = re.compile(
    r"^\|\s*(?P<depth>\d+)\s*\|\s*(?P<wave>[^|]+?)\s*\|\s*"
    r"#(?P<number>\d+)\s*\|\s*(?P<outcome>[^|]+?)\s*\|\s*"
    r"(?P<blocked_by>[^|]+?)\s*\|$"
)
ISSUE_REFERENCE = re.compile(r"#(\d+)")
GROUP_WEIGHTS = {
    "baseline": 5.0,
    "headlessCanonicalFoundation": 50.0,
    "deferredUi": 15.0,
    "deletion": 10.0,
    "coreConvergence": 15.0,
    "integration": 5.0,
}


@dataclass(frozen=True)
class PlannedTicket:
    number: int
    depth: int
    wave: str
    outcome: str
    blockers: tuple[int, ...]
    group: str


def _run(*args: str, cwd: Path) -> str:
    environment = os.environ.copy()
    environment["PYTHONUTF8"] = "1"
    environment["GH_PAGER"] = "cat"
    environment["NO_COLOR"] = "1"
    result = subprocess.run(
        args,
        cwd=cwd,
        env=environment,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
    )
    return result.stdout.strip()


def _repository_root(start: Path) -> Path:
    return Path(_run("git", "rev-parse", "--show-toplevel", cwd=start))


def _group_for_wave(wave: str) -> str:
    if wave == "Baseline":
        return "baseline"
    if wave == "Canonical pilot" or wave.startswith("Headless "):
        return "headlessCanonicalFoundation"
    if wave == "Deferred UI":
        return "deferredUi"
    if wave in {"Runtime deletion", "Compatibility deletion"}:
        return "deletion"
    if wave == "Core convergence":
        return "coreConvergence"
    if wave == "Integration":
        return "integration"
    if wave == "Preload lifecycle":
        return "preloadLifecycle"
    raise ValueError(f"Unknown dependency-plan wave: {wave!r}")


def parse_plan(plan_path: Path) -> list[PlannedTicket]:
    lines = plan_path.read_text(encoding="utf-8").splitlines()
    header_indexes = [
        index for index, line in enumerate(lines) if line.strip() == PLAN_TABLE_HEADER
    ]
    if len(header_indexes) != 1:
        raise ValueError(
            "Dependency plan must contain exactly one canonical ticket-table header."
        )

    header_index = header_indexes[0]
    separator_index = header_index + 1
    if (
        separator_index >= len(lines)
        or lines[separator_index].strip() != PLAN_TABLE_SEPARATOR
    ):
        raise ValueError(
            "Dependency-plan ticket table must use the canonical separator row."
        )

    end_indexes = [
        index
        for index, line in enumerate(lines[separator_index + 1 :], separator_index + 1)
        if line.strip() == PLAN_TABLE_END_HEADING
    ]
    if len(end_indexes) > 1:
        raise ValueError(
            "Dependency plan must not contain duplicate ticket-table end headings."
        )
    end_index = end_indexes[0] if end_indexes else len(lines)
    table_lines = list(
        enumerate(lines[separator_index + 1 : end_index], separator_index + 2)
    )
    while table_lines and not table_lines[-1][1].strip():
        table_lines.pop()

    tickets: list[PlannedTicket] = []
    seen: set[int] = set()
    for line_number, line in table_lines:
        if not line.strip():
            raise ValueError(
                f"Blank line inside dependency-plan ticket table at line {line_number}."
            )
        match = ISSUE_ROW.match(line)
        if match is None:
            raise ValueError(
                f"Malformed dependency-plan ticket row at line {line_number}: {line!r}"
            )
        number = int(match.group("number"))
        if number in seen:
            raise ValueError(f"Duplicate dependency-plan issue #{number}.")
        seen.add(number)
        wave = match.group("wave").strip()
        tickets.append(
            PlannedTicket(
                number=number,
                depth=int(match.group("depth")),
                wave=wave,
                outcome=match.group("outcome").strip(),
                blockers=tuple(
                    int(value)
                    for value in ISSUE_REFERENCE.findall(match.group("blocked_by"))
                ),
                group=_group_for_wave(wave),
            )
        )
    if not tickets:
        raise ValueError(f"No dependency-plan tickets found in {plan_path}.")

    preload_header_indexes = [
        index
        for index, line in enumerate(lines)
        if line.strip() == PRELOAD_TABLE_HEADER
    ]
    if len(preload_header_indexes) != 1:
        raise ValueError("Dependency plan must contain exactly one preload table.")
    preload_table_end_index = -1
    preload_header_index = preload_header_indexes[0]
    preload_separator_index = preload_header_index + 1
    if (
        preload_separator_index >= len(lines)
        or lines[preload_separator_index].strip() != PLAN_TABLE_SEPARATOR
    ):
        raise ValueError("Preload ticket table must use the canonical separator row.")
    preload_ticket_count = 0
    for line_index, line in enumerate(
        lines[preload_separator_index + 1 :], preload_separator_index + 1
    ):
        if not line.strip():
            preload_table_end_index = line_index
            break
        line_number = line_index + 1
        match = ISSUE_ROW.match(line)
        if match is None:
            raise ValueError(
                f"Malformed preload ticket row at line {line_number}: {line!r}"
            )
        number = int(match.group("number"))
        if number in seen:
            raise ValueError(f"Duplicate dependency-plan issue #{number}.")
        seen.add(number)
        wave = match.group("wave").strip()
        tickets.append(
            PlannedTicket(
                number=number,
                depth=int(match.group("depth")),
                wave=wave,
                outcome=match.group("outcome").strip(),
                blockers=tuple(
                    int(value)
                    for value in ISSUE_REFERENCE.findall(match.group("blocked_by"))
                ),
                group=_group_for_wave(wave),
            )
        )
        preload_ticket_count += 1
    else:
        preload_table_end_index = len(lines)
    if preload_ticket_count == 0:
        raise ValueError(
            "Dependency-plan preload table must contain at least one ticket."
        )

    for line_index, line in enumerate(lines[end_index + 1 :], end_index + 1):
        if preload_header_indexes[0] <= line_index < preload_table_end_index:
            continue
        if ISSUE_ROW.match(line) or (
            line.lstrip().startswith("|") and ISSUE_REFERENCE.search(line)
        ):
            raise ValueError(
                "Dependency-plan ticket row appears after the canonical table "
                f"terminator at line {line_index + 1}: {line!r}"
            )

    planned_numbers = {ticket.number for ticket in tickets}
    for ticket in tickets:
        unknown_blockers = sorted(set(ticket.blockers) - planned_numbers)
        if unknown_blockers:
            blockers = ", ".join(f"#{number}" for number in unknown_blockers)
            raise ValueError(
                f"Dependency-plan issue #{ticket.number} references unknown blocker(s): "
                f"{blockers}."
            )
    return tickets


def _load_live_issues(root: Path, repository: str) -> list[dict[str, Any]]:
    raw = _run(
        "gh",
        "issue",
        "list",
        "--repo",
        repository,
        "--state",
        "all",
        "--limit",
        "1000",
        "--json",
        "number,title,state,stateReason,labels,closedAt,url",
        cwd=root,
    )
    value = json.loads(raw)
    if not isinstance(value, list):
        raise ValueError("GitHub issue query did not return a JSON array.")
    return value


def _load_fixture_issues(path: Path) -> list[dict[str, Any]]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, list):
        raise ValueError("Issue fixture must contain a JSON array.")
    return value


def _is_completed(issue: dict[str, Any] | None) -> bool:
    return (
        issue is not None
        and str(issue.get("state", "")).upper() == "CLOSED"
        and str(issue.get("stateReason", "")).upper() == "COMPLETED"
    )


def _labels(issue: dict[str, Any]) -> set[str]:
    values = issue.get("labels", [])
    return {
        str(value.get("name", ""))
        for value in values
        if isinstance(value, dict) and value.get("name")
    }


def _percentage(completed: int, total: int) -> float:
    return round((completed / total) * 100.0, 1) if total else 0.0


def build_snapshot(
    *,
    root: Path,
    plan_path: Path,
    repository: str,
    tickets: list[PlannedTicket],
    issues: list[dict[str, Any]],
    state_source: str,
    queried_at: datetime,
) -> dict[str, Any]:
    issue_by_number: dict[int, dict[str, Any]] = {}
    data_issues: list[str] = []
    for issue in issues:
        number = int(issue["number"])
        if number in issue_by_number:
            data_issues.append(f"GitHub returned duplicate issue #{number}.")
        issue_by_number[number] = issue

    missing = [
        ticket.number for ticket in tickets if ticket.number not in issue_by_number
    ]
    if missing:
        data_issues.append(
            "Dependency-plan issues missing from GitHub state: "
            + ", ".join(f"#{number}" for number in missing)
        )

    ticket_completed = sum(
        _is_completed(issue_by_number.get(ticket.number)) for ticket in tickets
    )
    group_rows: list[dict[str, Any]] = []
    weighted_total = 0.0
    for group, weight in GROUP_WEIGHTS.items():
        group_tickets = [ticket for ticket in tickets if ticket.group == group]
        group_completed = sum(
            _is_completed(issue_by_number.get(ticket.number))
            for ticket in group_tickets
        )
        completion = _percentage(group_completed, len(group_tickets))
        contribution = (
            round(weight * group_completed / len(group_tickets), 2)
            if group_tickets
            else 0.0
        )
        weighted_total += contribution
        group_rows.append(
            {
                "group": group,
                "completed": group_completed,
                "total": len(group_tickets),
                "percent": completion,
                "weight": weight,
                "weightedContribution": contribution,
            }
        )

    completed_numbers = {
        ticket.number
        for ticket in tickets
        if _is_completed(issue_by_number.get(ticket.number))
    }
    frontier: list[dict[str, Any]] = []
    blocked: list[dict[str, Any]] = []
    for ticket in tickets:
        issue = issue_by_number.get(ticket.number)
        if issue is None or _is_completed(issue):
            continue
        unmet = [
            number for number in ticket.blockers if number not in completed_numbers
        ]
        ready = "ready-for-agent" in _labels(issue)
        row = {
            "number": ticket.number,
            "depth": ticket.depth,
            "title": issue.get("title", ticket.outcome),
            "wave": ticket.wave,
            "blockers": list(ticket.blockers),
            "unmetBlockers": unmet,
            "readyForAgent": ready,
            "url": issue.get("url"),
        }
        if ready and not unmet:
            frontier.append(row)
        else:
            blocked.append(row)

    plan_bytes = plan_path.read_bytes()
    confidence = "high" if not data_issues and state_source == "github-live" else "low"
    foundation = next(
        row for row in group_rows if row["group"] == "headlessCanonicalFoundation"
    )
    preload_tickets = [
        ticket for ticket in tickets if ticket.group == "preloadLifecycle"
    ]
    preload_completed = sum(
        _is_completed(issue_by_number.get(ticket.number)) for ticket in preload_tickets
    )
    return {
        "schemaVersion": 1,
        "repository": repository,
        "version": "0.10.x",
        "generatedAtUtc": queried_at.astimezone(timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "sources": {
            "githubState": state_source,
            "dependencyPlan": (
                plan_path.relative_to(root).as_posix()
                if plan_path.is_relative_to(root)
                else str(plan_path)
            ),
            "dependencyPlanSha256": hashlib.sha256(plan_bytes).hexdigest(),
            "gitHead": _run("git", "rev-parse", "HEAD", cwd=root),
        },
        "metrics": {
            "ticketCompletion": {
                "completed": ticket_completed,
                "total": len(tickets),
                "percent": _percentage(ticket_completed, len(tickets)),
            },
            "headlessCanonicalFoundation": {
                "completed": foundation["completed"],
                "total": foundation["total"],
                "percent": foundation["percent"],
            },
            "unifiedPreloadLifecycle": {
                "completed": preload_completed,
                "total": len(preload_tickets),
                "percent": _percentage(preload_completed, len(preload_tickets)),
            },
            "weightedTotal": {"percent": round(weighted_total, 1)},
        },
        "groups": group_rows,
        "frontier": sorted(frontier, key=lambda row: (row["depth"], row["number"])),
        "blocked": sorted(blocked, key=lambda row: row["number"]),
        "dataIssues": data_issues,
        "confidence": confidence,
    }


def render_markdown(snapshot: dict[str, Any]) -> str:
    metrics = snapshot["metrics"]
    tickets = metrics["ticketCompletion"]
    foundation = metrics["headlessCanonicalFoundation"]
    preload = metrics["unifiedPreloadLifecycle"]
    weighted = metrics["weightedTotal"]
    confidence = str(snapshot["confidence"]).capitalize()
    lines = [
        f"Ticket completion: {tickets['completed']}/{tickets['total']} "
        f"({tickets['percent']:.1f}%)",
        "Headless canonical foundation: "
        f"{foundation['completed']}/{foundation['total']} "
        f"({foundation['percent']:.1f}%)",
        "Unified preload lifecycle: "
        f"{preload['completed']}/{preload['total']} "
        f"({preload['percent']:.1f}%)",
        f"Weighted total: {weighted['percent']:.1f}%",
        f"Confidence: {confidence}",
        "",
        "Executable frontier:",
    ]
    if snapshot["frontier"]:
        lines.extend(
            f"- #{row['number']} {row['title']}" for row in snapshot["frontier"]
        )
    else:
        lines.append("- None")
    lines.extend(
        [
            "",
            "Evidence:",
            f"- Git head: `{snapshot['sources']['gitHead']}`",
            "- Dependency plan SHA-256: "
            f"`{snapshot['sources']['dependencyPlanSha256']}`",
            f"- GitHub state: `{snapshot['sources']['githubState']}` "
            f"at {snapshot['generatedAtUtc']}",
        ]
    )
    if snapshot["dataIssues"]:
        lines.extend(["", "Data issues:"])
        lines.extend(f"- {issue}" for issue in snapshot["dataIssues"])
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", help="GitHub owner/name; defaults to current repo.")
    parser.add_argument("--plan", type=Path, help="Dependency-plan path override.")
    parser.add_argument(
        "--issues-json",
        type=Path,
        help="Offline/test issue-state fixture; disclosed as non-live.",
    )
    parser.add_argument(
        "--format",
        choices=("json", "markdown"),
        default="markdown",
    )
    args = parser.parse_args(argv)

    root = _repository_root(Path.cwd())
    plan_path = args.plan.resolve() if args.plan else root / PLAN_PATH
    tickets = parse_plan(plan_path)
    repository = args.repo or _run(
        "gh",
        "repo",
        "view",
        "--json",
        "nameWithOwner",
        "--jq",
        ".nameWithOwner",
        cwd=root,
    )
    if args.issues_json:
        issues = _load_fixture_issues(args.issues_json)
        state_source = f"fixture:{args.issues_json.resolve()}"
    else:
        issues = _load_live_issues(root, repository)
        state_source = "github-live"
    snapshot = build_snapshot(
        root=root,
        plan_path=plan_path,
        repository=repository,
        tickets=tickets,
        issues=issues,
        state_source=state_source,
        queried_at=datetime.now(timezone.utc),
    )
    if args.format == "json":
        print(json.dumps(snapshot, indent=2, ensure_ascii=False))
    else:
        print(render_markdown(snapshot))
    return 0 if not snapshot["dataIssues"] else 2


if __name__ == "__main__":
    sys.exit(main())
