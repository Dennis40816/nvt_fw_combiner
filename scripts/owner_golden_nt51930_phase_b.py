"""Exact NT51930 Phase B evidence record used by owner-golden validation."""

from __future__ import annotations

from typing import Any


def nt51930_phase_b_result() -> dict[str, Any]:
    """Return the exact support-neutral NT51930 INX route result."""
    return {
        "status": "v1-v2-parity-routed-owner-delta-classified",
        "routeProfileId": "nt51930-ctrlram-replace-fw130-cascade3",
        "routeProfileVersion": "0.1.0",
        "ownerExpectedSha256": "676a4b3fb1a302b9bee4b2cea795e17189d70b6d4dd20a45b3fef603afabb1a8",
        "standardMergeReconstructionSha256": "f831e6348af02d9cb8ad833433b165764c495c17b385b996a6fb270dbcddb08d",
        "legacyOutputSha256": "6725c501f66a064c200612f2a1569f13f76f71cab51f4366b4c4f6e7e73ff48f",
        "v2OutputSha256": "6725c501f66a064c200612f2a1569f13f76f71cab51f4366b4c4f6e7e73ff48f",
        "differenceCounts": {
            "legacyToV2": 0,
            "ownerToLegacy": 4397,
            "ownerToV2": 4397,
            "ownerToStandardMergeReconstruction": 4097,
            "headerCrc": 8,
            "headerCopy": 1,
            "diffDlmRegion": 4388,
            "nfNormalMpVnRegions": 0,
        },
        "ownerDifferenceRanges": [
            {
                "start": "0x7100",
                "endExclusive": "0x7104",
                "differenceBytes": 4,
                "classification": "header-crc-word",
            },
            {
                "start": "0x7118",
                "endExclusive": "0x711C",
                "differenceBytes": 4,
                "classification": "header-crc-word",
            },
            {
                "start": "0x28FD8",
                "endExclusive": "0x28FD9",
                "differenceBytes": 1,
                "classification": "header-copy-byte",
            },
            {
                "start": "0x2F200",
                "endExclusive": "0x3F000",
                "differenceBytes": 4388,
                "classification": "declared-diffdlm-replacement-and-postbuild",
            },
        ],
        "baseEvidence": {
            "kind": "owner-final-as-immutable-reference-sentinel",
            "reason": (
                "The official system supplied a final expected output but no independent "
                "pre-replacement FlashCode. The Standard Merge DP+TP reconstruction is "
                "separately hash-pinned and differs from the owner final at [0x6000,0x7000) "
                "plus byte 0x3FFFF."
            ),
            "ownerExpectedFullByteParityClaimed": False,
        },
        "physicalInputs": {
            "nfComposite": {
                "size": 577,
                "sha256": "2e79b9cdc060442190e31c9e3c3a11f82ee6e76407d3db73d90add907dde148e",
                "outputPrefixDifferenceBytes": 0,
            },
            "normal": {"consumedBytes": 11264, "outputDifferenceBytes": 0},
            "mp": {"consumedBytes": 13312, "outputDifferenceBytes": 0},
            "vn": {"consumedBytes": 6494, "outputDifferenceBytes": 0},
            "diffDlm": {"consumedBytes": 65024, "outputDifferenceBytes": 4087},
            "nfDiffSourceInventory": {
                "count": 29,
                "numericOrder": "0..28",
                "compositeDerivationClaimed": False,
            },
        },
        "processorId": "nfc.nt51930.ctrlram-postbuild-fw1.x",
        "toolBindingId": "legacy-combiner-1.13.0",
        "selectedTool": {
            "sha256": "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf",
            "registration": "repository-hash-pinned",
        },
        "suppliedToolObservation": {
            "sha256": "291c2c1cc5b75c59680818497ddb863718ff1930b1f000c61a27e1c4eac9dec3",
            "selected": False,
            "executed": False,
        },
        "sessionCount": 1,
        "commandCount": 1,
        "orderedArguments": [
            [
                "NT51930BASED_NORMAL_MODE",
                "CRC8",
                "output/nt51930_fw.bin",
                "output/nt51930_fw.bin",
                "BIN/NF_Ctrlram.bin",
                "0x0",
                "0x1FC00",
                "6736",
                "BIN/Normal_Ctrlram.bin",
                "0x0",
                "0x21650",
                "11264",
                "BIN/MP_Ctrlram.bin",
                "0x0",
                "0x24250",
                "13312",
                "BIN/VN_Ctrlram.bin",
                "0x0",
                "0x27650",
                "6494",
                "output/nt51930_fw.bin",
                "0x7000",
                "0x28FB0",
                "256",
                "BIN/DiffDLM.bin",
                "0x0",
                "0x2F200",
                "65024",
            ]
        ],
        "legacyReadAuthority": [{"start": "0x0", "endExclusive": "0x40000"}],
        "v2ReadAuthority": [{"start": "0x0", "endExclusive": "0x40000"}],
        "effectiveWriteAuthority": [
            {"start": "0x7100", "endExclusive": "0x7104"},
            {"start": "0x7118", "endExclusive": "0x711C"},
            {"start": "0x1FC00", "endExclusive": "0x1FE41"},
            {"start": "0x21650", "endExclusive": "0x24250"},
            {"start": "0x24250", "endExclusive": "0x27650"},
            {"start": "0x27650", "endExclusive": "0x28FAE"},
            {"start": "0x28FB0", "endExclusive": "0x290B0"},
            {"start": "0x2F200", "endExclusive": "0x3F000"},
        ],
        "baseMetadata": {
            "commonFwVersion": "1.3.0",
            "chipCount": 3,
            "pid": "0x110D",
            "topology": "cascade",
        },
        "postbuildBatSha256": "7641ef3b25442d31048d1831714f822a225d9f40875ab059c5b6cb669ead2b08",
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
