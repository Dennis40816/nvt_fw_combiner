# v0.9.16 baseline executor v2

Status: in effect for predecessor `v0916-1x` under 1.1.12 board decisions 63
and 79. [JSON](v0916-baseline-executor-v2.json) and its
[closed schema](v0916-baseline-executor-v2.schema.json) own the complete pins.
This is non-certifying (`certification: none`, `terminal: false`). The
terminal plan keeps [executor v1](v0916-baseline-executor-v1.json) unchanged.

The [1.x amendment](v0916-parity-1x-amendment-v1.md) binds v2's raw byte size
and SHA-256. The comparator loads those exact bytes from the candidate Git
snapshot, verifies the binding and the v1 relation, and hands the parsed
contract to `V0916BaselineExecutorBuilder`. A status word alone cannot admit
execution. Activation is an R3 change requiring both the firmware owner and
the release owner's approval of the last push at its exact head.

The builder uses a fresh detached worktree of the annotated `v0.9.16` tag
object, exact peeled commit and source tree. It rejects dirty sources and
pre-existing `bin`/`obj`, then checks SDK `10.0.303`, `global.json`, seven
original CLI dependency locks and seven external-tool pins. It restores with
`--force-evaluate --runtime win-x64`; the base build keeps v1's arguments.
The shared compiler-host adapter preflights the selected dotnet installation's
exact runtime and architecture, applies `DOTNET_ROLL_FORWARD=Disable` only
to restore/build, appends `-p:UseSharedCompilation=false`, `-nodeReuse:false`
and `-p:RuntimeFrameworkVersion=10.0.11` once, and restores the environment on
success and failure. The `RuntimeFrameworkVersion` value must equal
`compilerHost.requiredRuntime.version`. No `-m:1` is added to v0.9.16.
The `-p:RuntimeFrameworkVersion=10.0.11` argument is an extension of decision
79 that waits for the release owner's decision; it is not in decision 79's list.

Since 1.1.13, the 1.x build runs the framework-dependent prebuilt profile
catalog generator, which requests runtime `10.0.0` and cannot start while
roll-forward is disabled. Fixing the runtime framework version to the pinned
patch lets it run on `10.0.11`. The self-contained CLI already takes runtime
`10.0.11` from the SDK, so its closure is unchanged; the v0.9.16 closure pin
is reproduced with the property: 431 files, 88367164 bytes, SHA-256
`18da112302672766db191872d6b7973ca05effa257293b09161904545eb2c3b0`.

After restore and again after build, exactly the seven declared locks must
have changed to their raw size/SHA-256 pins. Other tracked bytes, unchanged
locks and unauthorized new files are rejected. The restored packages are the
one exception: the tag's `NuGet.config` names the top-level folder `.packages`
as the package folder and Git ignores it. The folder must be absent before
restore, and its files are not compared; the pinned lock bytes carry each
package's version and content hash. A folder of that name anywhere else is an
unauthorized new file. The Windows NuGet bytes use
CRLF with no final newline; hashing never normalizes them. The complete
embedded unified diff alone is LF-normalized, 5937 bytes with SHA-256
`3b5d1b832174de820e53ba1aa71e7dd3b62f8e0680611d5fed123cc3474c3988`.
Every rewrite is explained by the empty `net10.0/win-x64` target and Windows
serialization; Bootstrap, Cli and Infrastructure also refresh first-party
project ranges from `[0.9.2, )` to `[0.9.16, )`. No package id, resolved version
or content hash changes. The eighth Presentation.Avalonia lock stays equal
to its Git blob. Report `lockFileSetSha256` still hashes the ordered inventory
of all eight original Git-blob locks; it is never the rewritten inventory.

The probe parses the embedded portable PDB of each first-party managed
assembly declared by the built CLI deps graph, verifies the runtime version,
and reports `runtimeVersion`, `compilerVersion` and `verifiedAssemblyCount`.
It uses the CustomDebugInformation compilation-options GUID and the
`runtime-version` and `compiler-version` keys. Missing, corrupt or ambiguous
metadata, empty graphs and mixed/unpinned versions are typed executor failures.

Seven managed assemblies, the CLI apphost and the complete closure must match
their pins: 431 files, 88367164 bytes, SHA-256
`18da112302672766db191872d6b7973ca05effa257293b09161904545eb2c3b0`.
The v1 relation binds its raw JSON (3730 bytes, SHA-256
`92e400212b5cdbb5e164b4d1401d59cdd1adbb0aef9a490be4777554d5b1e659`)
with `identical-runtime-closure` and zero differing files.

The payload-free P-0.5 sources are `build-v0916-rt11.json` (identity, SDK,
pre/post-restore pins, argv/environment and measure), `closure-v0916-rt11.json`
(managed pins and complete inventory), `lockdiff-v0916-rt11.diff` (full diff),
and `pdbinfo-v0916-rt11.json` (compiler options). The 10.0.12 host diagnostic
changes seven DLL embedded PDBs. A Git-archive diagnostic lacks SourceLink and
reduces their total size by 7168 bytes. Neither is an acceptable fallback.
The DLL/CLI/closure pins identify this Windows win-x64 recipe; Git identities
and original blob pins are never platform-normalized. Comparator rebuilds do
not establish identity with differently configured release packages.

R35-09 must exercise the actual builders and probe against real assemblies.
Synthetic host tests establish refusal behavior, not real-build parity.
