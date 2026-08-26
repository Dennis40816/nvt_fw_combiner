from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def test_stable_package_couples_one_version_scoped_launcher() -> None:
    package = (ROOT / "scripts" / "package.ps1").read_text(encoding="utf-8-sig")

    assert "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj" in package
    assert "launcher/NvtFwCombiner.Launcher.exe" in package
    assert "schemaVersion = if ($IncludeManagedLauncher) { '1.2' } else { '1.1' }" in package
    assert "$Manifest.versionManagementProtocolVersion = 1" in package
    assert "role = 'launcher'" in package
    assert "NvtFwCombiner.Bootstrap.exe" not in package


def test_release_smoke_rejects_bootstrap_in_update_and_checks_launcher_identity() -> None:
    smoke = (ROOT / "scripts" / "smoke-release.ps1").read_text(encoding="utf-8-sig")

    assert "Immutable Bootstrap must remain outside every version update package." in smoke
    assert "Release manifest launcher identity is inconsistent." in smoke
    assert "Version 1.0.0 and newer require the managed launcher contract." in smoke


def test_managed_lab_publishes_only_the_immutable_bootstrap_at_root() -> None:
    lab = (ROOT / "scripts" / "create-managed-installation-lab.ps1").read_text(encoding="utf-8-sig")

    assert "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj" in lab
    assert "Move-Item -LiteralPath $PublishedBootstrap -Destination $Bootstrap" in lab
    assert "--bootstrap $Bootstrap" in lab
    assert "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj" not in lab
