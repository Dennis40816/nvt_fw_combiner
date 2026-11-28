"""Keep physical test-data reads behind the two resolver owners.

The allowlist pins complete source lines and occurrence counts, so an identity
exception cannot silently admit a new read elsewhere in the same file.
"""

from collections import Counter
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]
TESTDATA_LITERAL = re.compile(r'''["'](?:\.\.[/\\])*testdata(?:[/\\]|["'])''', re.IGNORECASE)
SOURCE_SUFFIXES = {".cs", ".py", ".ps1", ".sh"}
EXEMPT_FILES = {
    'scripts/testdata_paths.py': 'Resolver owner.',
    'tests/NvtFwCombiner.TestSupport/RepositoryPaths.cs': 'Resolver owner.',
    'scripts/package.ps1': 'PR B',
    'scripts/smoke-release.ps1': 'PR B',
    'scripts/canonical_golden_validation.py': 'PR B',
    'scripts/output_baseline.py': 'G0-3 follow-up',
    'tests/scripts/test_output_baseline.py': 'G0-3 follow-up',
    'tests/scripts/test_testdata_path_guard.py': 'Guard policy inventory and in-memory probe strings only.',
}

# Each entry is (exact source line, occurrence count, reason).
IDENTITY_USES = {
    'scripts/ab_merge_fixture_validation.py': (
        ('and collection.get("legacyRoot") == "testdata/golden/ab-merge"', 1, 'Manifest legacyRoot identity; reads use the resolver.'),
    ),
    'scripts/diagnostic_golden_validation.py': (
        ('PurePosixPath("testdata") / path.relative_to(resolve_testdata_root(repository_root)).as_posix()', 1, 'Reconstructs a repository identity for inventory comparison.'),
    ),
    'scripts/intake_ic_reference.py': (
        ('DEFAULT_OUTPUT_ROOT = ROOT / "testdata" / "golden" / "owner-handoff"', 1, 'Intake output destination or storage description, not an input read.'),
        ('"defaultStorage": "testdata/golden/owner-handoff; ignored by Git by default",', 1, 'Intake output destination or storage description, not an input read.'),
    ),
    'scripts/release_source_pins.py': (
        ('GOLDEN_ALLOWLIST_PATH = "testdata/golden/release-canonical-v1.json"', 1, 'Snapshot keys and source-binding patterns; sync resolves their storage.'),
        ('rb"\\$CanonicalGoldenReleaseAllowlistPath\\s*=\\s*Join-Path\\s+\\$RepoRoot\\s+\'testdata/golden/release-canonical-v1\\.json\'",', 1, 'Snapshot keys and source-binding patterns; sync resolves their storage.'),
    ),
    'scripts/sync_derived.py': (
        ('path = resolve_testdata_root(root) if identity.parts[0] == "testdata" else root', 1, 'Tests the identity prefix before selecting the resolver.'),
        ('parts = identity.parts[1:] if identity.parts[0] == "testdata" else identity.parts', 1, 'Tests the identity prefix before selecting the resolver.'),
    ),
    'scripts/testdata_mirror_check.py': (
        ('REPOSITORY_TESTDATA_NAME = "testdata"', 1, 'Physical repository copy compared with the external mirror; the resolver must not redirect it.'),
    ),
    'scripts/validate_repository.py': (
        ('"testdata/golden/canonical/manifest.json",', 1, 'Required-file identities or tracked-payload policy; reads resolve separately.'),
        ('"testdata/golden/release-standard-merge-v1.json",', 1, 'Required-file identities or tracked-payload policy; reads resolve separately.'),
        ('"testdata/diagnostics/golden-evidence/README.md",', 1, 'Required-file identities or tracked-payload policy; reads resolve separately.'),
        ('"testdata/diagnostics/golden-evidence/manifest.json",', 1, 'Required-file identities or tracked-payload policy; reads resolve separately.'),
        ('PurePosixPath("testdata/golden/canonical"),', 1, 'Required-file identities or tracked-payload policy; reads resolve separately.'),
        ('PurePosixPath("testdata/golden/ctrlram-replace/fixtures"),', 1, 'Required-file identities or tracked-payload policy; reads resolve separately.'),
        ('"testdata/golden/AGENTS.md",', 1, 'Required-file identities or tracked-payload policy; reads resolve separately.'),
    ),
    'tests/NvtFwCombiner.Architecture.Tests/ProfileBoundaryTests.RetiredIc.cs': (
        ('"testdata/golden/canonical",', 1, 'Retired-IC repository identity; filesystem reads resolve separately.'),
        ('"testdata/golden/ctrlram-replace",', 1, 'Retired-IC repository identity; filesystem reads resolve separately.'),
        ('"testdata/golden/owner-handoff",', 1, 'Retired-IC repository identity; filesystem reads resolve separately.'),
        ('"testdata/public-synthetic",', 1, 'Retired-IC repository identity; filesystem reads resolve separately.'),
        ('["testdata/golden/canonical/manifest.json"] =', 1, 'Retired-IC repository identity; filesystem reads resolve separately.'),
        ('["testdata/golden/ctrlram-replace/manifest.20260717.json"] =', 1, 'Retired-IC repository identity; filesystem reads resolve separately.'),
        ('string absoluteRoot = RepositoryPaths.FromTestData(evidenceRoot["testdata/".Length..]);', 1, 'Retired-IC repository identity; filesystem reads resolve separately.'),
    ),
    'tests/NvtFwCombiner.GoldenRegression.Tests/CanonicalGoldenTestDataTests.cs': (
        ('private static readonly string[] LegacyPaths = ["testdata/golden/ctrlram-replace/fixtures/base.bin"];', 1, 'Synthetic manifest identity, not a fixture read.'),
        ('diagnosticsRoot = "testdata/diagnostics/golden-evidence",', 1, 'Synthetic manifest identity, not a fixture read.'),
    ),
    'tests/NvtFwCombiner.UiSmoke.Tests/RepositoryPathsTests.cs': (
        ('RepositoryPaths.FromRepositoryRoot("testdata"), RepositoryPaths.FindTestDataRoot()));', 1, 'Expected fallback identity or temporary-directory label in resolver coverage.'),
        ('string root = CreateDirectory("testdata");', 1, 'Expected fallback identity or temporary-directory label in resolver coverage.'),
    ),
    'tests/scripts/test_authority_check.py': (
        ('"Testdata/evil.bin",', 1, 'Case-sensitive authority-policy identity probe.'),
    ),
    'tests/scripts/test_authority_policy.py': (
        ('"profiles/**", "testdata/**", "refcode/**",', 1, 'Authority-policy path pattern.'),
    ),
    'tests/scripts/test_canonical_golden_validation.py': (
        ('"testdata/golden/legacy/provenance/synthetic.dat",', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('self.write_json(self.root / "testdata/golden/release-canonical-v1.json", self.release_allowlist)', 2, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('self.canonical = self.root / "testdata/golden/canonical"', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('"testdata/golden/standard-merge-gen-flash/inputs/51927/dp.bin",', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('"testdata/golden/standard-merge-gen-flash/expected/51927/flash.bin",', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('"diagnosticsRoot": "testdata/diagnostics/golden-evidence",', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('path = self.root / "testdata/golden/release-canonical-v1.json"', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('self.root / "testdata/golden/release-canonical-v1.json",', 14, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('oracle = self.root / "testdata/public-synthetic/oracle.json"', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('"oracleReference": "testdata/public-synthetic/oracle.json",', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('retired_manifest = self.root / "testdata/golden/ctrlram-replace/manifest.json"', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('"testdata/diagnostics/golden-evidence/payload.bin"', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
        ('self.root_manifest["diagnosticsRoot"] = "testdata/golden/canonical/diagnostics"', 1, 'Synthetic repository/manifest identity for the R3 validator; PR B.'),
    ),
    'tests/scripts/test_ctrlram_canonical_20260717.py': (
        ('"testdata/golden/ctrlram-replace/" + item["path"]', 1, 'Legacy artifact identity comparison.'),
    ),
    'tests/scripts/test_ctrlram_canonical_final_intake.py': (
        ('"testdata/golden/ctrlram-replace/fixtures/20260718/"', 1, 'Legacy artifact identity comparison.'),
    ),
    'tests/scripts/test_diagnostic_golden_separation.py': (
        ('self.diagnostics = self.root / "testdata/diagnostics/golden-evidence"', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('self.legacy = self.root / "testdata/golden/ctrlram-replace"', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('self.handoff = self.root / "testdata/golden/owner-handoff"', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('environment = patch.dict(os.environ, {"NFC_TESTDATA_ROOT": str(self.root / "testdata")})', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('"artifactRoot": "testdata/golden/ctrlram-replace/fixtures/20260717",', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('"inventoryManifest": "testdata/golden/ctrlram-replace/manifest.20260717.json",', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('"root": "testdata/golden/owner-handoff",', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('"testdata/golden/ctrlram-replace/fixtures/other",', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('"testdata/golden/ctrlram-replace/other.json",', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
        ('"testdata/golden/other-handoff",', 1, 'Synthetic repository layout or manifest identity, isolated from external test data.'),
    ),
    'tests/scripts/test_open_golden_example.py': (
        ('manifest = root / "testdata" / "golden" / "canonical" / "synthetic" / "provenance" / "case.json"', 1, 'Synthetic output tree and subprocess root declaration.'),
        ('with mock.patch.dict("os.environ", {"NFC_TESTDATA_ROOT": str(root / "testdata")}):', 1, 'Synthetic output tree and subprocess root declaration.'),
    ),
    'tests/scripts/test_predecessor_rolling.py': (
        ('"plan": {"canonicalInputAuthority": {"manifestPath": "testdata/golden/canonical/manifest.json",', 1, 'Pinned Git tree identity or in-memory Git fixture key.'),
        ('if path == "testdata/golden/canonical":', 1, 'Pinned Git tree identity or in-memory Git fixture key.'),
        ('"testdata/golden/canonical/manifest.json": encoded(world["manifest"]),', 1, 'Pinned Git tree identity or in-memory Git fixture key.'),
        ('"testdata/golden/canonical/case.json": encoded(world["case"])}', 1, 'Pinned Git tree identity or in-memory Git fixture key.'),
        ('files["testdata/golden/canonical/" + row["path"]] = PAYLOAD', 1, 'Pinned Git tree identity or in-memory Git fixture key.'),
        ('self.assertEqual(git_reader.entry("testdata/golden/canonical")[2], descriptor["canonicalRootTree"])', 1, 'Pinned Git tree identity or in-memory Git fixture key.'),
    ),
    'tests/scripts/test_predecessor_v0916.py': (
        ('canonical = {"repositoryCommit": INPUT_COMMIT, "manifestPath": "testdata/golden/canonical/manifest.json",', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('"testdata/golden/canonical/manifest.json": encoded(world["manifest"]),', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('"testdata/golden/canonical/case.json": encoded(world["case"])}', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('self.commits[INPUT_COMMIT]["testdata/golden/canonical/" + row["path"]] = TP if row["artifactId"] in ("tp-base", "replacement") else PAYLOAD', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('self.commits[CANDIDATE]["testdata/golden/canonical/manifest.json"] = b"not-json"', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('self.assertFalse(any(commit == CANDIDATE and "testdata/golden/" in path for commit, path in git.reads))', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('git.commits[INPUT_COMMIT].update({"testdata/golden/canonical/alias-case.json": encoded(case),', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('"testdata/golden/canonical/ldc-input.bin": PAYLOAD})', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
        ('git.commits[INPUT_COMMIT]["testdata/golden/canonical/dp-input.bin"] = CHANGED', 1, 'Pinned Git blob identity or in-memory Git fixture key.'),
    ),
    'tests/scripts/test_release_package_policy.py': (
        ('"repositoryPath": "testdata/golden/canonical/README.md",', 1, 'Release projection keys and diagnostic manifest identities.'),
        ('"repositoryPath": "testdata/golden/release-canonical-v1.json",', 1, 'Release projection keys and diagnostic manifest identities.'),
        ('"repositoryPath": "testdata/golden/canonical/" + relative_path,', 1, 'Release projection keys and diagnostic manifest identities.'),
        ('"diagnosticsRoot": "testdata/diagnostics/golden-evidence",', 1, 'Release projection keys and diagnostic manifest identities.'),
        ('"sourceManifest": "testdata/golden/canonical/manifest.json",', 1, 'Release projection keys and diagnostic manifest identities.'),
    ),
    'tests/scripts/test_release_smoke_policy.py': (
        ('"testdata/golden/canonical/NT51927/standard-merge/gen-flash/"', 1, 'Release projection identity; filesystem reads resolve separately.'),
    ),
    'tests/scripts/test_render_release_notes.py': (
        ('"testdata/golden/private.bin",', 1, 'Redaction-policy input string, not a file read.'),
    ),
    'tests/scripts/test_structure_entry_audit.py': (
        ('["git", "ls-files", "-z", "testdata/golden/canonical"], cwd=checkout,', 1, 'Git index pathspec identity, not a working-tree read.'),
    ),
    'tests/scripts/test_sync_derived.py': (
        ('self.allowlist = "testdata/golden/release-canonical-v1.json"', 1, 'Snapshot keys, source-binding patterns or synthetic output layout.'),
        ('environment = patch.dict(os.environ, {"CI": "", "GITHUB_ACTIONS": "", "NFC_TESTDATA_ROOT": str(self.root / "testdata")})', 1, 'Snapshot keys, source-binding patterns or synthetic output layout.'),
        ('golden = "testdata/golden/canonical/synthetic-reference.bin"', 1, 'Snapshot keys, source-binding patterns or synthetic output layout.'),
        ('b"$CanonicalGoldenReleaseAllowlistPath = Join-Path $RepoRoot \'testdata/golden/release-canonical-v1.json\'",', 1, 'Snapshot keys, source-binding patterns or synthetic output layout.'),
        ('b"$ApprovedCanonicalGoldenAllowlistPath = Join-Path $PSScriptRoot \'../testdata/golden/release-canonical-v1.json\'",', 1, 'Snapshot keys, source-binding patterns or synthetic output layout.'),
    ),
    'tests/scripts/test_testdata_paths.py': (
        ('assert resolve_testdata_root() == ROOT / "testdata"', 1, 'Resolver input identity or expected fallback path, not a data read.'),
        ('assert repository_path("testdata/golden/canonical/manifest.json") == (', 2, 'Resolver input identity or expected fallback path, not a data read.'),
        ('ROOT / "testdata/golden/canonical/manifest.json"', 1, 'Resolver input identity or expected fallback path, not a data read.'),
    ),
    'tests/scripts/test_v0916_parity_1x_amendment.py': (
        ('f"testdata/golden/canonical/NT51951/ctrlram-replace/fw2.0.0/cascade-2/{row[\'binding\'][\'inputCaseId\']}/provenance/case.json"', 1, 'Pinned Git manifest identity.'),
    ),
    'tests/scripts/test_v0916_parity_contracts.py': (
        ('"testdata/golden/canonical/NT51917/standard-merge/gen-flash/"', 1, 'Pinned Git blob identity.'),
    ),
}


def literal_uses(source):
    return Counter(line.strip() for line in source.splitlines() if TESTDATA_LITERAL.search(line))


def test_testdata_reads_use_resolvers():
    failures = []
    for tree in ("tests", "scripts"):
        for path in sorted((ROOT / tree).rglob("*")):
            if not path.is_file() or path.suffix not in SOURCE_SUFFIXES or any(
                part in {"bin", "obj", "__pycache__"} for part in path.parts
            ):
                continue
            relative = path.relative_to(ROOT).as_posix()
            if relative in EXEMPT_FILES:
                continue
            allowed = Counter({line: count for line, count, reason in IDENTITY_USES.get(relative, ())})
            actual = literal_uses(path.read_text(encoding="utf-8-sig"))
            if actual != allowed:
                failures.append(f"{relative}: unexpected={dict(actual - allowed)}, stale={dict(allowed - actual)}")
    assert not failures, "\n".join(failures)
    assert all((ROOT / relative).is_file() for relative in IDENTITY_USES)
    assert all(reason for entries in IDENTITY_USES.values() for _, _, reason in entries)


def test_guard_catches_split_and_joined_literal_paths():
    for source in (
        'root / "testdata/golden/canonical"',
        'Path.Combine(root,\n    "testdata", "golden")',
        "Join-Path $RepoRoot '../testdata/golden/manifest.json'",
        'root / "testdata" / "golden"',
    ):
        assert literal_uses(source), source
    assert not literal_uses('resolve_testdata_root() / "golden/canonical"')
