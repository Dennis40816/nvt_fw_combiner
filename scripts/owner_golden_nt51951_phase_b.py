"""Exact NT51951 Phase B evidence record used by owner-golden validation."""

from __future__ import annotations

from typing import Any


def nt51951_phase_b_result() -> dict[str, Any]:
    """Return the exact support-neutral NT51951 single route result."""
    return {
        "status": "v1-v2-parity-routed-combiner-version-crc-delta-classified",
        "routeProfileId": "nt51951-ctrlram-replace-fw200-single",
        "routeProfileVersion": "0.1.0",
        "ownerExpectedSha256": "c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69",
        "standardMergeReconstructionSha256": "c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69",
        "legacyOutputSha256": "64ffa21a36a3a9560ebe109b9b0c94edcbb37c69a0dcb0aa183da7542694d1ea",
        "v2OutputSha256": "64ffa21a36a3a9560ebe109b9b0c94edcbb37c69a0dcb0aa183da7542694d1ea",
        "differenceCounts": {
            "legacyToV2": 0,
            "ownerToLegacy": 16,
            "ownerToV2": 16,
            "headerCrc": 16,
            "replacementPayload": 0,
        },
        "allowedDifferenceRanges": [
            {
                "start": "0xA11C",
                "endExclusive": "0xA120",
                "classification": "header-crc-word",
            },
            {
                "start": "0xA130",
                "endExclusive": "0xA134",
                "classification": "header-crc-word",
            },
            {
                "start": "0x2D428",
                "endExclusive": "0x2D42C",
                "classification": "header-copy-crc-word",
            },
            {
                "start": "0x2D43C",
                "endExclusive": "0x2D440",
                "classification": "header-copy-crc-word",
            },
        ],
        "baseEvidence": {
            "kind": "standard-merge-dp-and-tp-reconstruction",
            "fullByteOwnerExpectedParity": True,
        },
        "routeAdmission": {
            "metadataTuple": "NT51951/PID-0x5901/Common-FW-2.0.0/single-1",
            "exactReferenceSha256": "c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69",
            "differentBaseWithSameMetadata": "legacy-v1-fallback",
        },
        "physicalInputs": {
            "nf": {
                "sourceBytes": 2284,
                "maximumBytes": 10768,
                "outputDifferenceBytes": 0,
            },
            "normal": {
                "sourceBytes": 655360,
                "consumedBytes": 23552,
                "outputDifferenceBytes": 0,
            },
            "vn": {
                "sourceBytes": 8444,
                "consumedBytes": 8444,
                "outputDifferenceBytes": 0,
            },
        },
        "combinerCompatibility": {
            "ownerExpectedProducedBy": "legacy-combiner-1.11",
            "selectedToolBindingId": "legacy-combiner-1.13.0",
            "fullByteParity": False,
            "classification": "crc-only-four-word-divergence",
            "ownerAuthorizedHypothesisResolved": True,
        },
        "processorId": "nfc.nt51951.ctrlram-postbuild-v1",
        "toolBindingId": "legacy-combiner-1.13.0",
        "selectedTool": {
            "sha256": "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf",
            "registration": "repository-hash-pinned",
        },
        "sessionCount": 1,
        "commandCount": 2,
        "orderedArguments": [
            [
                "NT51950BASED_NORMAL_MODE",
                "CRC8",
                "output/nt51951_fw.bin",
                "output/nt51951_fw.bin",
                "BIN/Normal_Ctrlram.bin",
                "0x0",
                "0x25610",
                "23552",
                "BIN/VN_Ctrlram.bin",
                "0x0",
                "0x2B210",
                "8444",
                "BIN/NF_Ctrlram.bin",
                "0x0",
                "0x22C00",
                "10768",
                "output/nt51951_fw.bin",
                "0xA000",
                "0x2D30C",
                "512",
            ],
            [
                "NT51950BASED_NORMAL_MODE",
                "CRC8",
                "output/nt51951_fw.bin",
                "output/nt51951_fw.bin",
                "output/nt51951_fw.bin",
                "0xA000",
                "0x2D30C",
                "512",
            ],
        ],
        "legacyReadAuthority": [{"start": "0x0", "endExclusive": "0x80000"}],
        "v2ReadAuthority": [{"start": "0x0", "endExclusive": "0x80000"}],
        "effectiveWriteAuthority": [
            {"start": "0xA11C", "endExclusive": "0xA120"},
            {"start": "0xA130", "endExclusive": "0xA134"},
            {"start": "0x22C00", "endExclusive": "0x234EC"},
            {"start": "0x25610", "endExclusive": "0x2B210"},
            {"start": "0x2B210", "endExclusive": "0x2D30C"},
            {"start": "0x2D30C", "endExclusive": "0x2D50C"},
        ],
        "baseMetadata": {
            "commonFwVersion": "2.0.0",
            "chipCount": 1,
            "pid": "0x5901",
            "topology": "single",
        },
        "commandAuthority": "registered-1.13-catalog-nt51950-alias-flow",
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
