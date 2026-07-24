# Agent Skill Inventory

Status: Reviewed `0.10.0` M1 adoption inventory.

## Upstream provenance

- Source: [`mattpocock/skills`](https://github.com/mattpocock/skills)
- Pinned commit: `ed37663cc5fbef691ddfecd080dff42f7e7e350d`
- Upstream commit date: 2026-07-21
- License: MIT; preserved at
  [`third-party/mattpocock-skills/LICENSE`](../../third-party/mattpocock-skills/LICENSE)
- Local form: NFC-adapted copies under `.agents/skills/<name>/`

Only the 22 reviewed active skills are adopted. Upstream `deprecated`,
`in-progress`, `personal`, and `misc` directories remain inventory-only and are
not copied into the active repository skill set.

## Repository-native skills

All repository-native skills are retained as authoritative, model-invoked
skills. Their descriptions remain the routing triggers.

| Skill | Disposition | Authority |
| --- | --- | --- |
| `composition-experience-change` | Keep | Experience/persona authoring and access |
| `crc-worker-contract` | Keep | CRC/header worker and staged transform protocol |
| `dotnet-bootstrap` | Keep | SDK, packages, restore, solution bootstrap |
| `firmware-profile-authoring` | Keep | IC facts, profiles, regions, mappings, processors |
| `github-review-polling` | Keep | Exact-head read-only Codex review monitoring |
| `golden-regression` | Keep | Golden bytes, hashes, provenance, promotion |
| `nfc-architecture-change` | Keep | Layering, contracts, ports/adapters, ADRs |
| `polytail` | Keep | Mandatory completion and review quality gate |
| `release-readiness` | Keep | Version, packaging, release evidence and smoke |
| `supervised-branch-development` | Keep | Version/feature branch multi-agent coordination |
| `ui-experience-change` | Keep | Avalonia UI, ViewModels, localization, accessibility |

## Adopted upstream skills

### Engineering, user-invoked

| Skill | Disposition | Local routing |
| --- | --- | --- |
| `ask-matt` | Adopt/adapt | Router for the adopted workflow set |
| `grill-with-docs` | Adopt/adapt | Grilling plus canonical NFC domain/ADR updates |
| `triage` | Adopt/adapt | Uses the owner-gated GitHub configuration |
| `improve-codebase-architecture` | Adopt/adapt | Requires `$nfc-architecture-change` |
| `setup-matt-pocock-skills` | Adopt/replace setup behavior | Audits this preconfigured integration; does not overwrite NFC authority |
| `to-spec` | Adopt/adapt | Publishes only under explicit tracker authority |
| `to-tickets` | Adopt/adapt | Tracer-bullet tickets with explicit blocking edges |
| `implement` | Adopt/adapt | Uses NFC branch/test/phase-commit gates |
| `wayfinder` | Adopt/adapt | Missing GitHub labels/dependencies fail closed |

### Engineering, model-invoked

| Skill | Disposition | Local routing |
| --- | --- | --- |
| `prototype` | Adopt/adapt | Isolated question-answering artifact; no implicit branch/commit |
| `diagnosing-bugs` | Adopt/adapt | Diagnose first; route to the matching NFC authority |
| `research` | Adopt/adapt | Read-only unless a durable artifact is requested |
| `tdd` | Adopt/adapt | Narrow loop beneath the canonical test ladder |
| `domain-modeling` | Adopt/merge | Uses NFC canonical docs instead of `CONTEXT.md` |
| `codebase-design` | Adopt/adapt | Deep-module vocabulary beneath NFC architecture |
| `code-review` | Adopt/merge | General review plus mandatory `$polytail` |
| `resolving-merge-conflicts` | Adopt/replace unsafe steps | Preserves changes and validates operation/branch intent |

### Productivity

| Skill | Invocation | Disposition | Local routing |
| --- | --- | --- | --- |
| `grill-me` | User | Adopt | Explicit wrapper for `$grilling` |
| `handoff` | User | Adopt/adapt | Redacted OS-temp handoff only |
| `teach` | User | Adopt/adapt | Dedicated teaching workspace only |
| `writing-great-skills` | User | Adopt/adapt | Codex `openai.yaml` invocation model |
| `grilling` | Model | Adopt | One decision question at a time |

## Compatibility decisions

1. Every `SKILL.md` frontmatter contains only `name` and `description`.
2. User-invoked skills set `policy.allow_implicit_invocation: false` in
   `agents/openai.yaml`; model-invoked skills omit that restriction.
3. Codex invocation syntax is `$skill-name`, not upstream slash syntax.
4. [`agent-skill-routing.md`](agent-skill-routing.md) is the single NFC
   precedence and authority overlay; individual imported skills point to it
   instead of duplicating firmware rules.
5. `SPEC.md`, existing ADR/architecture/contract documents, profiles, schemas,
   tests, and repository-native skills remain canonical. No parallel
   `CONTEXT.md` is introduced.
6. Upstream steps that implied unconditional branches, commits, broad staging,
   GitHub writes, external execution, or always-resolve conflict behavior are
   replaced with NFC authority-preserving gates.
7. `$tdd`, `$prototype`, and `$code-review` cannot weaken independent golden
   expectations, the canonical test ladder, `$polytail`, or R2/R3 human gates.

## Inbound-link migration

- Root `AGENTS.md` points to the routing and inventory contracts.
- Imported skill-to-skill invocations use `$name`.
- Shared GitHub behavior points to
  [`agent-issue-tracker.md`](agent-issue-tracker.md).
- Shared NFC authority and verification behavior points to the routing contract
  rather than being copied across 22 skills.
- Existing links to repository-native skills remain valid; no native skill is
  renamed or deleted.

## Representative workflow validation

| Workflow | Required evidence |
| --- | --- |
| Router | `$ask-matt` names only installed active skills and the NFC authority layer |
| Diagnose | `$diagnosing-bugs` ends at evidence unless a fix was requested |
| Implement/review | `$implement` uses the test ladder; `$code-review` applies `$polytail` |
| Domain/architecture | No imported skill creates `CONTEXT.md` or bypasses `$nfc-architecture-change` |
| Conflict recovery | Wrong-branch operations may be safely aborted; unrelated files are not broadly staged |
| Tracker workflows | Missing triage/wayfinder labels stop before GitHub mutation |
| Invocation | All user-invoked skills are explicit in `agents/openai.yaml` |

Repository validation locks the installed set, frontmatter shape, metadata
presence, and invocation classification. The skill creator validator is run on
every adopted skill, followed by the canonical repository gates.
