"""Path-relative execution policy for comparator tests using fake hosts."""

import copy
import json
import struct
import zlib
from pathlib import Path

from scripts.predecessor_pdb_probe import OPTIONS_KIND


RUNTIME_VERSION = "10.0.11-servicing.26373.116+e2f47b0110ed922f21a1522da67279133ce28f32"
COMPILER_VERSION = "5.6.0-2.26377.103+e730f1db756d11c93f246830ba7b94ee6fcf4b94"
RUNTIME_LIST = "Microsoft.NETCore.App 10.0.11 [C:\\synthetic\\dotnet\\shared\\Microsoft.NETCore.App]\n"
HOST_INFO = "Host:\n  Version: 10.0.11\n  Architecture: x64\n  Commit: synthetic\n\n"


def synthetic_managed_pe(*, runtime=RUNTIME_VERSION, compiler=COMPILER_VERSION, options=None,
                         debug_type=17, debug_directory=True, corrupt_deflate=False):
    """Minimal PE32+ and genuine portable metadata with a compilation-options CDI."""
    if options is None:
        options = b"version\0" + b"2\0runtime-version\0" + runtime.encode() + b"\0compiler-version\0" + compiler.encode() + b"\0"
    size = len(options)
    prefix = bytes([size]) if size < 128 else bytes([0x80 | (size >> 8), size & 255])
    streams = {"#Pdb": bytes(32), "#~": struct.pack("<IBBBBQQIHHH", 0, 2, 0, 0, 1, 1 << 55, 0, 1, 39, 1, 1),
               "#Blob": b"\0" + prefix + options, "#GUID": OPTIONS_KIND}
    version = b"PDB v1.0\0\0\0\0"
    header = b"BSJB" + struct.pack("<HHII", 1, 1, 0, len(version)) + version + struct.pack("<HH", 0, len(streams))
    names = [(name.encode() + b"\0") for name in streams]
    names = [name + bytes((-len(name)) % 4) for name in names]
    offset = len(header) + sum(8 + len(name) for name in names)
    directory, content = b"", b""
    for (name, payload), encoded_name in zip(streams.items(), names):
        directory += struct.pack("<II", offset, len(payload)) + encoded_name
        content += payload
        offset += len(payload)
    pdb = header + directory + content
    compressor = zlib.compressobj(wbits=-15)
    compressed = compressor.compress(pdb) + compressor.flush()
    if corrupt_deflate:
        compressed = b"\x07"
    embedded = b"MPDB" + struct.pack("<I", len(pdb)) + compressed
    pe = bytearray(0x200 + 28 + len(embedded))
    pe[:2] = b"MZ"
    struct.pack_into("<I", pe, 0x3C, 0x80)
    pe[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<HH", pe, 0x84, 0x8664, 1)
    struct.pack_into("<H", pe, 0x94, 240)
    struct.pack_into("<H", pe, 0x98, 0x20B)
    struct.pack_into("<I", pe, 0x98 + 108, 16)
    if debug_directory:
        struct.pack_into("<II", pe, 0x98 + 112 + 6 * 8, 0x2000, 28)
    struct.pack_into("<IIII", pe, 0x188 + 8, len(pe) - 0x200, 0x2000, len(pe) - 0x200, 0x200)
    struct.pack_into("<IIII", pe, 0x200 + 12, debug_type, len(embedded), 0x201C, 0x21C)
    pe[0x21C:] = embedded
    return bytes(pe)


def write_synthetic_cli_graph(root, names=("NvtFwCombiner.Cli",)):
    """Declare and emit the fake graph's managed bytes; the apphost stays separate."""
    libraries = {name + "/1.0.0": {"type": "project"} for name in names}
    targets = {name + "/1.0.0": {"runtime": {name + ".dll": {}}} for name in names}
    (root / "NvtFwCombiner.Cli.deps.json").write_text(json.dumps({"runtimeTarget": {"name": "synthetic"},
                                                                "libraries": libraries, "targets": {"synthetic": targets}}), encoding="utf-8")
    for name in names:
        (root / (name + ".dll")).write_bytes(synthetic_managed_pe())


def compiler_identity(count=1):
    return {"runtimeVersion": RUNTIME_VERSION, "compilerVersion": COMPILER_VERSION, "verifiedAssemblyCount": count}


def published_inventory(tags=("v1.2.0", "v1.2.1"), *, collected="2026-10-02T00:00:00Z", pages=2):
    """Complete synthetic publication facts, with independently variable collection evidence."""
    return {"schemaVersion": "1.0", "kind": "predecessor-published-release-inventory",
            "repository": "Dennis40816/nvt_fw_combiner", "collectedAtUtc": collected,
            "complete": True, "pagesRead": pages,
            "releases": [{"id": index + 1, "tag": tag, "publishedAtUtc": "2026-10-01T00:00:00Z",
                          "draft": False, "prerelease": False, "complete": True}
                         for index, tag in enumerate(tags)]}


def written_1x_merge_report(*, committed, dp_sha256, tp_sha256, output_sha256):
    """The member shape of a written 1.x Standard Merge report; a Preview is `committed=False`.

    Structure only, as the 1.2.2 rehearsal saw it from a real CLI: profile sequences 100 and 200,
    mutation rows without a sequence, the same mutations and described output in Preview and Build,
    and input snapshots. Sizes, digests and names are synthetic; no firmware byte or path is here.
    """
    def span(start, end):
        return {"Start": start, "Length": end - start, "EndExclusive": end}

    def source(slot, size, digest):
        return {"AddressSpaceId": slot, "ArtifactId": slot, "Size": size, "Sha256": digest,
                "OriginalFileName": f"{slot}.bin",
                "ExecutionSnapshot": {"AcceptedRange": span(0, size), "AcceptedSize": size, "AcceptedSha256": digest,
                                      "IgnoredTrailingRange": None, "IgnoredTrailingBytes": 0}}

    def operation(operation_id, sequence, slot, start, end):
        return {"OperationId": operation_id, "Sequence": sequence, "Kind": "CopyRange", "Status": "Succeeded",
                "SourceSpaceId": slot, "SourceRange": span(start, end), "TargetSpaceId": "output-image",
                "TargetRange": span(start, end), "OverlapPolicy": "Reject", "ProcessorId": None,
                "ToolBindingId": None, "ProcessorAllowedReadRanges": [], "ProcessorAllowedWriteRanges": [],
                "ExecutedCommands": [], "Reason": f"synthetic {operation_id}",
                "Provenance": {"Kind": "built-in-profile", "SourceId": None, "SourceVersion": None}}

    def mutation(operation_id, start, end, after):
        return {"OperationId": operation_id, "Kind": "CopyRange", "TargetSpaceId": "output-image",
                "TargetRange": span(start, end), "ChangedByteCount": end - start, "BeforeSha256": "0" * 64,
                "AfterSha256": after, "Reason": f"synthetic {operation_id}"}

    return {
        "RunId": "synthetic-run", "ProfileId": "synthetic-standard-merge", "ProfileVersion": "0.7.0",
        "IcId": "synthetic", "ModeId": "standard-merge", "ExperienceId": "standard-merge", "CompositionKind": "Merge",
        "StartedAtUtc": "2026-10-02T00:00:00.0000000+00:00", "CompletedAtUtc": "2026-10-02T00:00:00.5000000+00:00",
        "Inputs": [source("dp-input", 8, dp_sha256), source("tp-input", 4, tp_sha256)],
        "Operations": [operation("copy-tp", 100, "tp-input", 0, 4), operation("copy-dp", 200, "dp-input", 4, 8)],
        "Mutations": [mutation("copy-tp", 0, 4, "1" * 64), mutation("copy-dp", 4, 8, "2" * 64)],
        "Issues": [],
        "Output": {"FileName": "output.bin", "Size": 8, "Sha256": output_sha256, "Committed": committed},
        "OutputDifferences": [], "CompilationFingerprint": "c" * 64, "MapId": "synthetic-map",
        "Validations": [{"RuleId": "dp-content-plausibility", "Stage": "InputLoad", "Status": "Passed",
                         "Severity": "Warning", "IssueCode": "DP_UNIFORM_CONTENT_WARNING"}],
        "OutputNaming": {"RendererKind": "normal-flashcode-v1", "Template": "{ic}_FlashCode.bin",
                         "AutomaticFileName": "synthetic_FlashCode.bin", "ActualFileName": "output.bin",
                         "IsExplicitOverride": True, "DateSource": "utc",
                         "ResolvedAtUtc": "2026-10-02T00:00:00.0000000+00:00"},
    }


def contract_for_fake_processes(contract, temporary_root: Path):
    """Copy the contract, changing only the fake host's temporary path bound."""
    result = copy.deepcopy(contract)
    result["environment"]["temporaryRootMaxLength"] = len(str(temporary_root.resolve())) + 16
    return result
