"""Exact NT51932 Phase B evidence record used by owner-golden validation."""

from __future__ import annotations

from typing import Any


def nt51932_phase_b_result() -> dict[str, Any]:
    """Return the exact support-neutral NT51932 cascade-3 route result."""
    return {
        "status": "v1-v2-parity-routed-owner-crc-delta-classified",
        "routeProfileId": "nt51932-ctrlram-replace-fw200-cascade3",
        "routeProfileVersion": "0.1.0",
        "ownerExpectedSha256": "3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd",
        "standardMergeReconstructionSha256": "3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd",
        "legacyOutputSha256": "0e59a2fbaab16979745b3543564b18f49c9d4eb7912bdea2e61383e31e662566",
        "v2OutputSha256": "0e59a2fbaab16979745b3543564b18f49c9d4eb7912bdea2e61383e31e662566",
        "differenceCounts": {
            "legacyToV2": 0,
            "ownerToLegacy": 16,
            "ownerToV2": 16,
            "headerCrc": 16,
            "replacementPayload": 0,
        },
        "allowedDifferenceRanges": [
            {
                "start": "0x7100",
                "endExclusive": "0x7104",
                "classification": "header-crc-word",
            },
            {
                "start": "0x7118",
                "endExclusive": "0x711C",
                "classification": "header-crc-word",
            },
            {
                "start": "0x27FF0",
                "endExclusive": "0x27FF4",
                "classification": "header-copy-crc-word",
            },
            {
                "start": "0x28008",
                "endExclusive": "0x2800C",
                "classification": "header-copy-crc-word",
            },
        ],
        "baseEvidence": {
            "kind": "standard-merge-dp-and-tp-reconstruction",
            "fullByteOwnerExpectedParity": True,
        },
        "routeAdmission": {
            "metadataTuple": "NT51932/PID-0x5601/Common-FW-2.0.0/cascade-3",
            "exactReferenceSha256": "3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd",
            "differentBaseWithSameMetadata": "legacy-v1-fallback",
        },
        "physicalInputs": {
            "nfComposite": {
                "size": 1758,
                "sha256": "a76cc832e496b3866fa6fc73c749098b72c1549ce8ccf7bb0d2675b8e519b99b",
                "outputPrefixDifferenceBytes": 0,
            },
            "normal": {"consumedBytes": 18944, "outputDifferenceBytes": 0},
            "vn": {"consumedBytes": 4120, "outputDifferenceBytes": 0},
            "diffDlm": {
                "consumedBytes": 35840,
                "sourceToOutputDifferenceBytes": 4094,
                "classification": "embedded-fwconfig-backup-preserved-from-reference",
                "sourceDifferenceRuns": [
                    {"start": "0x2F00", "endExclusive": "0x3071"},
                    {"start": "0x3072", "endExclusive": "0x310A"},
                    {"start": "0x310B", "endExclusive": "0x3F00"},
                ],
                "differingSourceByte": "0xFF",
            },
            "nfDiffSourceInventory": {
                "count": 16,
                "numericOrder": "0..15",
                "directCompositeEquals": "NF_Diff_0.bin",
                "compositeDerivationClaimed": False,
            },
        },
        "diffNfMerge": {
            "executableSha256": "f611af7e315d46341e15cd7140eb3962f6ac05d337121e5554022ef5e69a2bbe",
            "runtimeRegistered": False,
            "executed": False,
            "derivationClaimed": False,
            "routeInput": "direct-owner-NF_Ctrlram.bin",
        },
        "processorId": "nfc.nt51932.ctrlram-postbuild-v1",
        "toolBindingId": "legacy-combiner-1.13.0",
        "selectedTool": {
            "sha256": "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf",
            "registration": "repository-hash-pinned",
        },
        "sessionCount": 1,
        "commandCount": 2,
        "orderedArguments": [
            [
                "NT51932BASED_NORMAL_MODE",
                "CRC8",
                "output/nt51932_fw.bin",
                "output/nt51932_fw.bin",
                "BIN/Normal_Ctrlram.bin",
                "0x0",
                "0x21B90",
                "18944",
                "BIN/DiffDLM.bin",
                "0x0",
                "0x2D100",
                "35840",
                "BIN/VN_Ctrlram.bin",
                "0x0",
                "0x26590",
                "6496",
                "BIN/NF_Ctrlram.bin",
                "0x0",
                "0x1FC00",
                "8080",
                "output/nt51932_fw.bin",
                "0x7000",
                "0x27EF0",
                "512",
            ],
            [
                "NT51932BASED_NORMAL_MODE",
                "CRC8",
                "output/nt51932_fw.bin",
                "output/nt51932_fw.bin",
                "output/nt51932_fw.bin",
                "0x7000",
                "0x27EF0",
                "512",
            ],
        ],
        "legacyReadAuthority": [{"start": "0x0", "endExclusive": "0x40000"}],
        "v2ReadAuthority": [{"start": "0x0", "endExclusive": "0x40000"}],
        "effectiveWriteAuthority": [
            {"start": "0x7100", "endExclusive": "0x7104"},
            {"start": "0x7118", "endExclusive": "0x711C"},
            {"start": "0x1FC00", "endExclusive": "0x202DE"},
            {"start": "0x21B90", "endExclusive": "0x26590"},
            {"start": "0x26590", "endExclusive": "0x275A8"},
            {"start": "0x27EF0", "endExclusive": "0x280F0"},
            {"start": "0x2D100", "endExclusive": "0x35D00"},
        ],
        "baseMetadata": {
            "commonFwVersion": "2.0.0",
            "chipCount": 3,
            "pid": "0x5601",
            "topology": "cascade",
        },
        "postbuildBatSha256": "9b570db204df0849f9962f09f9800e6e442a86d38d6dacd0988b32e18f0a514f",
        "insertSidScope": "out-of-scope-pre-step-nonblocking",
        "inputArtifactsUnchanged": True,
        "reportIdentityParity": True,
        "wrongShapeBehavior": "legacy-v1-fallback",
        "reviewEvidence": {
            "ownerInputGate": "closed-by-existing-owner-decision",
            "independentR3Findings": {"p0": 0, "p1": 0, "p2": 0, "p3": 0},
            "promotionPolicy": "support-neutral-no-promotion",
        },
        "runtimeSupportPromotion": False,
    }
