# Architecture Decision Record Lifecycle

ADR files are immutable decision history, but their current authority must be
unambiguous. Do not delete an ADR merely because a later decision replaced it,
and do not use `Ignored` as a status: it does not say whether the decision was
rejected, implemented and retired, or still used by compatibility callers.

Use these lifecycle states:

- `Proposed` — under review and not yet implementation authority.
- `Accepted` — current authority within its stated scope.
- `Deprecated` — still present for compatibility, but no new caller may adopt
  it; the ADR must name the migration target and deletion gate.
- `Partially superseded` — a later ADR replaced named decisions while the
  explicitly listed remainder stays current.
- `Superseded` — retained as history only; the status must link the successor.
- `Historical` — records a completed milestone or program without defining
  current implementation authority.

When a later ADR changes an earlier one:

1. the later ADR declares `Supersedes` or `Amends`;
2. the earlier ADR receives the reciprocal `Superseded by` or `Amended by`
   link;
3. partial replacement names both the retired and retained decisions;
4. callers migrate under the later ADR's evidence and compatibility gates
   before the earlier ADR becomes fully `Superseded`; and
5. a status-only cleanup never changes firmware behavior, ranges, processor
   authority, schemas, or release claims.

Rejected alternatives remain inside the deciding ADR's `Rejected options`
section; they do not require separate `Ignored` ADR files.
