"""Keep the host's GitHub Actions marker out of the repository-script tests."""

import pytest


@pytest.fixture(autouse=True)
def _without_github_actions_marker(monkeypatch):
    # A GitHub-hosted runner sets GITHUB_ACTIONS=true, and console scrubbing
    # follows it. A test that needs the marker sets it explicitly.
    monkeypatch.delenv("GITHUB_ACTIONS", raising=False)
