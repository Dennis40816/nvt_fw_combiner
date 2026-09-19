# Toolchain runtime trust probe v1

Status: admitted implementation contract, 2026-09-20; not release evidence.
Owner decisions and page reference: [handoff](../ui/v1.1.9-toolchain-runtime-handoff.md).
Configuration publication: [ADR 0073](../adr/0073-toolchain-runtime-selection.md).

## Authority

The host may inspect an explicitly chosen VC++ runtime as data. It must not
load, execute, install or select that DLL during inspection. The probe is an
internal entry point of the same trusted application host, not a user-selected
executable or arbitrary command. Desktop and CLI invoke the same Bootstrap
entry point before starting ordinary application work.

The parent captures a stable bounded file snapshot through the local-file
adapter and creates a host-owned temporary copy. It retains the source path
and SHA-256. The probe locks its copy against writes/deletion, checks the
expected hash, and verifies that same file. The probe working directory is the
trusted host directory, never the candidate/user-DLL directory. Neither that
directory nor the user path enters PATH or another DLL search override.

## Transport and lifetime

Arguments, passed as separate tokens with shell execution disabled:

```text
--internal-runtime-trust-v1 <candidate-copy-path> <expected-sha256> <request-guid>
```

The source path is data, not a command. The child emits exactly one JSON object
with `ProtocolVersion` = 1, `RequestId`, `Sha256`, and `IssueCode` (null only on
success). SHA-256 is 64 hexadecimal characters; request identity is one GUID.
Unknown/missing fields, malformed or multiple objects, mismatched identity,
nonzero exit, or an absent response reject the inspection. Diagnostics are not
used as verification evidence. Serialization uses the runtime's source-generated
JSON context and bounded parent output capture.

The parent limits the trusted probe to 30 seconds, runs it off the dispatcher,
and uses the existing process runner's cancellation/termination path. The
probe starts no child processes. Cancellation and timeout never mean Verified;
real child-exit tests are required before claiming reclamation. A failed kill
or unresolved exit is an explicit failure, not successful cleanup. This
contract does not grant new lifetime guarantees to unrelated runner consumers.

## Signature and publisher

Windows `WinVerifyTrust` uses `WINTRUST_ACTION_GENERIC_VERIFY_V2`, no UI,
FILE choice, VERIFY state and unconditional CLOSE of acquired state. Require
return value zero with whole-chain revocation and
`WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT` plus `WTD_DISABLE_MD2_MD4`.
Do not use cache-only retrieval, revocation-ignore, hash-only or signature-only
flags. Unknown/unavailable/revoked trust and platform errors reject the result.

Network retrieval is allowed for Windows trust evaluation. Windows may use
still-valid revocation cache data; success is not a claim that every call made
a fresh network request. Unconfirmed revocation is rejected. Authenticode's
verified timestamp semantics remain intact; a second current-time certificate
chain check must not reinterpret a legitimately timestamped signature.

Read the actual verified signer from WinTrust provider state, not arbitrary
file metadata or a guessed first certificate. Its ASN.1 Organization attribute
must identify `Microsoft Corporation`. The display CN need not have that name
(for example, Windows Software Compatibility Publisher is legitimate).
The selected DLL's CompanyName, filename and version are never trust evidence.

Reference: [Microsoft WINTRUST_DATA](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-wintrust_data).

## Compatibility and resource limits

Candidate bytes are bounded to 8 MiB and must be native AMD64 PE32+ DLL bytes.
Required VCRUNTIME140 imports are read from the manifest/hash-verified Combiner
snapshot using existing tool discovery. No expected import contract is a
failure, not vacuous compatibility. Named and ordinal imports must all resolve
to nonzero, in-bounds exports. Required forwarded exports are rejected with
`runtime.export.forwarder-unsupported`; unrelated forwarders do not reject the
whole candidate. This conservative first version does not claim every newer
Microsoft runtime is compatible.

PE table iteration is capped at 65,536 entries and strings at 512 bytes;
arithmetic and RVA/range reads are checked before allocation or access.
Malformed, out-of-range and truncated data produce typed rejection without
loading the image. Version/architecture metadata is informational.

## Evidence boundary

The complete candidate result is separate from saved selection and Build
readiness. Save/Reload must revalidate exact selected identity. Actual execution
must use an independently admitted owned deployment lease and the same captured
configuration generation. No silent bundled fallback is permitted.

Unit fakes prove decision behavior, not Microsoft trust or DLL search order.
Required Windows evidence includes invalid signature, changed hash, non-Microsoft
publisher, real timeout/cancel/exit, and a complete successful candidate probe.
Full Toolchain acceptance additionally needs actual Combiner image-load trace
matching the selected deployed DLL plus unchanged approved output bytes.
