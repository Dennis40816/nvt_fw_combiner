"""Retired symbols and live admission instructions cannot silently return."""
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
SYMBOLS = re.compile(
    r"capability_reuse|CAPABILITY_REUSE|_record_changed_in_commits_after|"
    r"_read_commit_path_batch|_is_tree_transparent_containment_merge|trusted_initial_base"
)
INSTRUCTIONS = re.compile(
    r"design-active|final-complete|evidence commit|capability-reuse record|capability record|"
    r"external-authority attestation|trusted (?:initial )?(?:capability )?checkpoint|"
    r"latest (?:evidence )?checkpoint|R[0-3] (?:capability )?record|"
    r"(?:own|separate|executor) record|finaliz\w* .{0,40}record|change-records/", re.I
)
GOVERNANCE_OWNERS = {
    "development-execution-workflow.md", "branch-version-and-release-governance.md",
    "agent-skill-routing.md", "agent-model-routing.md", "agent-issue-tracker.md",
}

# Exact historical sentences, bound to their original canonical documents.
EXCEPTIONS: set[tuple[str, str]] = {
    ('docs/adr/0021-code-size-ratchet-accepted-artifact-amendment.md', 'This increment has no transferable headroom; independent admission and fixed-head review are recorded in the batch capability record.'),
    ('docs/adr/0057-v0916-black-box-parity-certification.md', 'H3 may modify only the predeclared final-record set; H4 may add only the predeclared external-authority attestation set and is the exact parity package-source head.'),
    ('docs/adr/0072-event-buffer-format-configuration.md', 'Record 12 remains `design-active` until the frozen integration boundary.'),
    ('docs/adr/0072-event-buffer-format-configuration.md', 'Records 11–13 remain `design-active` until the frozen integration boundary; scoped tests do not satisfy the remaining firmware-owner evidence or certify the entire candidate.'),
    ('docs/adr/0077-prebuilt-profile-catalog.md', 'Records and reviews follow the governance in force at each admission (today, capability-reuse records bound to the latest evidence checkpoint; the WS-GOV reset may replace the record mechanism, not the substantive reviews and evidence).'),
    ('docs/adr/0079-test-architecture.md', '| T1 | This ADR, the reciprocal "Amended by" line in ADR 0027 and a pointer in `tests/README.md` (`TEST-ARCH-ADR-1113-01`) | R2 | capability-reuse record, independent design and exact-head reviews, the owner\'s explicit acceptance of the complete text with the measured targets filled in (board decision 81) | board decisions 69 to 76 and 81; batch 2a merged |'),
    ('docs/adr/0080-governance-reset.md', 'Around that core, the capability-reuse record system ([ADR 0054](0054-finalize-capability-reuse-records.md), [0059](0059-trusted-initial-capability-checkpoint.md), [0061](0061-tree-transparent-merge-history-normalization.md), [0070](0070-bounded-local-r1-continuation.md), [0071](0071-final-integration-path-ownership.md); contract [`capability-reuse-record.md`](../governance/capability-reuse-record.md)) became the main cost and failure source of the development flow.'),
    ('docs/adr/0080-governance-reset.md', '- **History replay.** For every sealed final record, every external-authority attestation, the trusted checkpoint and each retired legacy record, `scripts/validate_repository.py` (`_record_changed_in_commits_after`) walks `git rev-list --ancestry-path <revision>..HEAD` and reads each commit with `git diff-tree -m`.'),
    ('docs/adr/0080-governance-reset.md', 'At `1.1.x` `e6e991af3` the repository holds 359 records, all final-complete, and 112 attestations (327 and 103 at `v1.1.11`). - **Release re-run conflict, 1.1.12.** Release pull request #449 merged into `main` as `405603dbe`, then the release run stopped on unresolved P1 review threads.'),
    ('docs/adr/0080-governance-reset.md', 'The next release pull request (#455) failed the "PR head contains the exact reviewed base" check, and merging `main` into `1.1.12` failed the history audit: that merge lists the newly finalized records against `405603dbe`, which is not an ancestor of the tree-equal side.'),
    ('docs/adr/0080-governance-reset.md', 'An admission must name the latest checkpoint; one made on an older checkpoint cannot be reconciled after the newer one is brought in and must be rewritten (board working rules, 2026-09-26).'),
    ('docs/adr/0080-governance-reset.md', 'On 2026-09-01 the history scan used the whole 600-second lane limit, and v1.1.0 shipped under waiver `REL-110-FULL-VERIFY-OWNER-WAIVER-01`. - **Ceremony that is red by design.** A reviewed head fails the final gate until a separate direct-child evidence commit.'),
    ('docs/adr/0080-governance-reset.md', 'Removes the merge sensitivity, but keeps the direct-child evidence commit, the red reviewed head and the checkpoint serialization. 3.'),
    ('docs/adr/0080-governance-reset.md', 'Not pursued: the checkpoint also fixes path coverage, the path-state digest and the reconciliation ancestry, so it needs a separate admission base and integration base and new ownership rules, while history replay, the evidence commit and re-review stay.'),
    ('docs/adr/0080-governance-reset.md', 'The validator stops parsing, validating and replaying capability-reuse records, external-authority attestations, the trusted checkpoint and retired legacy records.'),
    ('docs/adr/0080-governance-reset.md', 'The existing evidence is **frozen in place** (O-1, board decision 51, which reverses the 2026-09-25 choice to move it to an archive folder): `docs/governance/change-records/`, `docs/governance/external-authority-attestations/`, `docs/governance/waivers/` and `docs/governance/trusted-initial-capability-checkpoint.v1.json` keep their paths. **Pin construction (one order):** G1-B adds one `README.md` naming each of the three directories historical, and nothing else under the frozen paths.'),
    ('docs/adr/0080-governance-reset.md', 'An owner bypass or a paused ruleset (board decisions 77 and 78, item 8) is outside them and follows its own recorded procedure.'),
    ('docs/adr/0080-governance-reset.md', 'It passes the governance gates still in force on that base: for G1-A, the current record rules (its capability-reuse record, the base validator, the existing required checks, the release-owner attestation for the workflow); for the release pull request, the release gates of ADR 0033 and the release policy.'),
    ('docs/adr/0080-governance-reset.md', "- Merges between `main`, the trunk and release branches stop failing on sealed evidence; a stopped release follows one of three written recovery paths. - Structure validation stops growing with history, and parallel workstreams stop realigning on each other's checkpoints. - The reviewed head is the mergeable head; there is no evidence commit. - The human approval is GitHub's own last-push code-owner review; the roles and evidence a change needs are checked on every push and description edit; what neither enforces is a named procedure (decision 105)."),
    ('docs/adr/0080-governance-reset.md', '| Per-record finalization, batch coverage, evidence commit, JSON attestation | removed; pull request evidence and exact-head approvals instead |'),
    ('docs/adr/0080-governance-reset.md', '| A reviewed head that fails the final gate because of `design-active` | removed |'),
    ('docs/adr/0080-governance-reset.md', 'Decision 105 accepts this; the self-change check, the bootstrap approval and the pre-merge verification carry it, and each merge costs an approval snapshot, a comparison of the base authority code and one re-run of the check. - Path-mapped CI can miss a cross-cutting interaction until the next full run. - While G1-A runs beside the record gate, it adds cost: the old review binds the implementation head, while the new review record and the GitHub approval bind the final pull request head, which includes the evidence commit, so an R3 pull request of that period is reviewed and approved twice.'),
    ('docs/adr/0080-governance-reset.md', "The cost ends with G1-B. - In the same period every pull request that adds or finalizes a capability-reuse record or an external-authority attestation changes a frozen evidence path of item 4 and is therefore governance R3 for the authority check: it declares `governance-owner` with its change statement, and the owner's approval names that role."),
    ('docs/adr/0080-governance-reset.md', 'The cost ends with G1-B, after which no new record or attestation is added. - Keeping the WS-AI changes in G1-B (decision 101) delays the retirement: G1-B cannot fix its head until the WS-AI port is designed and reviewed; its larger diff lengthens the fixed-head review and makes a base move (rebase, re-pin, re-review, new authorization) more likely; until then every seal still realigns the other workstreams, evidence commits continue and each structure run still replays the history (250-310 s locally for this draft).'),
    ('docs/adr/0080-governance-reset.md', 'The owner chose this knowingly; this ADR does not narrow G1-B. - After work lands under the new rules, returning to the record system needs a new trusted checkpoint and new admissions (ADR 0059 activation), not a revert.'),
    ('docs/adr/0080-governance-reset.md', 'Until then every change, G1-A included, follows the current rules: records, admission against the latest checkpoint, finalization, evidence commits and attestations.'),
    ('docs/adr/0080-governance-reset.md', "D4's identical-tree observation is recorded before G1-A is declared in force, D6 before the G1-B cutover. - **G1-A (R3, admitted under the current rules with its own record):** the authority policy and schema, the authority check and its workflow, the CODEOWNERS consistency test, the pull request template and the procedures of decision 105; its deliverables, owner actions and acceptance are listed in [G1-A delivery and acceptance](#g1-a-delivery-and-acceptance)."),
    ('docs/adr/0080-governance-reset.md', "Both gates run side by side. - **G1-B (the cutover, R3), required for the retirement:** - the validator's record code and its tests removed (item 1), and the record references in `test_code_size_policy.py`, `test_skill_inventory_validation.py` and `test_v0916_parity_contracts.py` updated; - the three READMEs, the pin file and the pin check (item 2); - `capability-reuse-record.md` Historical; ADRs 0054, 0059, 0061, 0070 and 0071 Superseded; ADR 0080 added to `docs/adr/` as Accepted; - every live instruction to create or finalize records replaced: root `AGENTS.md` (the gate pointer, the R3 roles of decision 102, the single-writer rule), the execution workflow (admission through pull request fields, the single-writer procedure, review and approval records, waivers from `docs/policies/polytail.md`), branch governance (item 10 after G1-B), the record text of `docs/ci/pull-request-ci.md` and `CONTRIBUTING.md`, and the record steps of the skills `implement`, `nfc-architecture-change`, `polytail` and `supervised-branch-development`, which would otherwise tell agents to add files under frozen paths; - the transition inventory (step 3) and the cutover authorization (step 2). - **G1-B, carried by decision 101 (not required for the retirement):** the WS-AI changes of item 18 (skill renames with the `nfc-` prefix, the `nfc-review` merge, 23 to 18 skills, `.claude/` projections, the dual-runtime agent documents, the hybrid interview style, and the skill inventory and routing changes they need) and the size-policy consolidation of item 17."),
    ('docs/adr/0080-governance-reset.md', 'It names: - the base SHA, the G1-B head SHA and the expected frozen pins (three tree IDs, one blob ID); - the transition inventory (step 3); - **the obligations it replaces for G1-B itself:** its capability-reuse `design-active` admission and design-review field, its `final-complete` finalization with path coverage and `pathStateDigest`, the direct-child evidence commit, the binding to the latest checkpoint, and the external-authority attestation files.'),
    ('docs/adr/0080-governance-reset.md', '| Active (`design-active`) | The record is dropped on rebase; its admission facts move to the pull request; the new gates apply. |'),
    ('docs/adr/0080-governance-reset.md', '| Blocked, or owner evidence still owed | Stays an open gate in the pull request or the board; the migration never closes it. The inventory lists each item, the owner or Golden evidence it owes and the pull request or board row that carries it. At `e6e991af3` all 359 records on the trunk line are final-complete, and the six open inherited authorities of the trusted checkpoint carry approving attestations. |'),
    ('docs/adr/0080-governance-reset.md', "It is admitted under the current rules with its own capability-reuse record bound to the latest checkpoint and an approved design review; its final evidence needs the release-owner attestation for the workflow, and its governance-owner approval is the owner's GitHub approval of its last push naming that role, since the record system has no governance attestation type."),
}


def sentences(text):
    for paragraph in re.split(r"\n\s*\n", text):
        if paragraph.lstrip().startswith("|"):
            yield from (" ".join(line.split()) for line in paragraph.splitlines())
            continue
        normalized = " ".join(paragraph.split())
        yield from re.split(r"(?<=[.!?])\s+(?=[A-Z`])", normalized)


def live_path(path, text):
    name = Path(path).name
    if path.startswith("docs/adr/"):
        status = re.search(r"^(?:- )?Status:\s*\*{0,2}(\w+)", text, re.M)
        return status is not None and status[1] in {"Proposed", "Accepted"}
    return (name == "AGENTS.md" or path in {"CONTRIBUTING.md", ".github/pull_request_template.md",
            "docs/ci/pull-request-ci.md"} or path.startswith(("docs/policies/", ".agents/skills/", ".claude/"))
            or path.startswith(".codex/agents/") and path.endswith(".toml")
            or path.startswith("docs/contracts/") and path.endswith(".md")
            or path.startswith("docs/governance/") and name in GOVERNANCE_OWNERS)


def live_findings(sources):
    return [(path, sentence) for path, text in sources.items() if live_path(path, text)
            for sentence in sentences(text) if INSTRUCTIONS.search(sentence)
            and (path, sentence) not in EXCEPTIONS]


def test_no_retired_executable_symbols():
    findings = []
    for folder in ("scripts", "tests"):
        for path in (ROOT / folder).rglob("*.py"):
            if path.resolve() == Path(__file__).resolve():
                continue
            for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
                if SYMBOLS.search(line):
                    findings.append(f"{path.relative_to(ROOT)}:{number}: {line}")
    assert findings == []


def test_no_live_retired_admission_instructions():
    paths = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode("utf-8").split("\0")
    sources = {path: (ROOT / path).read_text(encoding="utf-8") for path in paths
               if path and (ROOT / path).is_file()
               and (live_path(path, "") or path.startswith("docs/adr/") and path.endswith(".md"))}
    assert live_findings(sources) == []


def test_new_instruction_in_exception_document_is_reported():
    sentence = "Finalize the design-active record before the evidence commit."
    path = "docs/adr/0080-governance-reset.md"
    assert (path, sentence) in live_findings({path: "Status: Accepted\n\n" + sentence})


def test_exception_sentence_moved_to_another_document_is_reported():
    _, sentence = next(iter(sorted(EXCEPTIONS)))
    assert live_findings({"AGENTS.md": sentence}) == [("AGENTS.md", sentence)]


def test_skill_scripts_and_claude_files_are_in_live_scope():
    sentence = "Finalize the design-active record before the evidence commit."
    for path in (".agents/skills/example/check.py", ".agents/skills/example/check.sh", ".claude/rules.yml"):
        assert live_findings({path: sentence}) == [(path, sentence)]
