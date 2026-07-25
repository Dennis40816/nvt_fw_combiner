---
name: research
description: Research a question from high-trust primary sources when the user needs external facts, official documentation, source evidence, or delegated reading legwork.
---

For NFC repository work, apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before acting.

# Research

1. State the exact question, decision it informs, scope, and required freshness.
2. Prefer primary sources: official documentation, specifications, first-party
   APIs, source code, and owner-provided evidence. Trace important claims to the
   source that owns them and distinguish fact, inference, and unresolved gap.
3. Use a bounded background agent only when the current request or calling
   workflow authorizes delegation and useful independent work can continue.
   Give it the same source, confidentiality, and authority constraints.
4. Return a concise cited answer. Write a durable Markdown artifact only when
   the user requested one or the approved workflow explicitly requires one;
   otherwise keep the repository unchanged.
5. Never copy private firmware, credentials, proprietary payloads, or
   redistribution-restricted evidence into Git. Record provenance and hashes
   where the repository policy permits only a reference.

Completion requires every material claim to have a primary source or an
explicit uncertainty label, with no external or repository state changed beyond
the request.
