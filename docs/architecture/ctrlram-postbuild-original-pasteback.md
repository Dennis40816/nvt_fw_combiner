# CtrlRAM Postbuild Original Pasteback Investigation

Status: investigation note, not a production behavior decision.
Date: 2026-07-03.

The investigation compared staged legacy postbuild execution with canonical
CtrlRAM outputs to understand whether original information can be pasted back
without unintended changes. The current conclusion and follow-up evidence are
indexed by the [CtrlRAM postbuild investigation reference](ctrlram-postbuild-investigation-reference.md).

The confidential evidence is identified by the `postbuild-*` and
`tddi-flash-header-*` entries in the
[public reference manifest](../references/confidential-references.json). The
manifest records identity and integrity metadata; the private asset repository
resolves each SHA-256 to its source. The public postbuild catalog and profiles
remain the authority for production command data and allowed writes.

The investigation found that full postbuild self-pasteback may refresh header
integrity words even when the input regions are unchanged. A diagnostic that
expects exact output equality must account for the declared per-IC write
ranges. A narrower VN-only diagnostic may avoid those writes, but it does not
establish full postbuild parity. Clearing or restoring header bytes is not a
production default. One command shape cannot represent every reviewed IC and
firmware interval.

For NT51929 AB, the controlled address-strategy investigation did not admit
an AB Replace route or certify an independent output Golden. The later Single
family Backup correction is recorded in the
[delivery evidence](../ui/v1.1.10-delivery.md); it does not change that AB
evidence disposition. Production promotion still requires firmware-owner
review, declared write authority, and complete byte-level Golden evidence.

## NT51929 AB address strategy characterization — 2026-09-21

The controlled run characterized address handling but did not admit an AB
Replace route or establish independent complete-output parity.

### Recovered existing 1.13 source — 2026-09-21

The private source review explained the observed Backup and CRC behavior.
The accepted Single family correction is recorded in the linked delivery
evidence. AB support still requires its separate firmware and Golden gates.
