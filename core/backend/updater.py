"""GitHub Releases check for the startup update notification.

Version comparison uses backend/semver.py (SemVer 2.0.0 precedence rules).
Do not replace it with packaging.version or a split(".") tuple compare.
"""
from __future__ import annotations

import hashlib
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

import requests

from .semver import is_newer, is_valid, tag_to_version

GITHUB_API_TIMEOUT_SECONDS = 6
DOWNLOAD_TIMEOUT_SECONDS = 120
DOWNLOAD_CHUNK_BYTES = 1024 * 256


@dataclass
class UpdateInfo:
    available: bool
    latest_version: str
    download_url: str = ""
    release_notes: str = ""
    # Hex SHA-256 of the installer as published by GitHub ("digest" of the asset), if available.
    sha256: str = ""


class ChecksumMismatchError(Exception):
    pass


def _headers(token: Optional[str]) -> dict:
    headers = {"Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return headers


def _pick_asset(assets: list) -> dict:
    """Prefer the KagiBox-Setup-*.exe asset; the release should only contain
    that one file, but never hand a non-.exe asset to the installer launcher."""
    exes = [a for a in assets if str(a.get("name", "")).lower().endswith(".exe")]
    for asset in exes:
        if str(asset.get("name", "")).lower().startswith("kagibox-setup-"):
            return asset
    return exes[0] if exes else {}


def _pick_installer_asset(assets: list) -> str:
    return _pick_asset(assets).get("browser_download_url", "")


def _asset_sha256(asset: dict) -> str:
    digest = str(asset.get("digest") or "")
    return digest.split(":", 1)[1].lower() if digest.lower().startswith("sha256:") else ""


def check_for_update(repo: str, current_version: str, token: Optional[str] = None) -> UpdateInfo:
    """repo is 'owner/name'. Network failures and invalid tags are swallowed
    to "no update available" -- update checks must never block or crash app
    startup."""
    no_update = UpdateInfo(available=False, latest_version=current_version)
    if not is_valid(current_version):
        return no_update
    url = f"https://api.github.com/repos/{repo}/releases/latest"
    try:
        resp = requests.get(url, timeout=GITHUB_API_TIMEOUT_SECONDS, headers=_headers(token))
        resp.raise_for_status()
        data = resp.json()
    except Exception:
        return no_update

    latest = tag_to_version(str(data.get("tag_name", "")))
    if latest is None:
        return no_update  # tag is not valid SemVer -> ignore this release
    asset = _pick_asset(data.get("assets", []) or [])
    download_url = asset.get("browser_download_url", "")
    return UpdateInfo(
        available=is_newer(latest, current_version) and bool(download_url),
        latest_version=latest,
        download_url=download_url,
        release_notes=data.get("body", "") or "",
        sha256=_asset_sha256(asset),
    )


def download_installer(download_url: str, dest_path: Path, expected_sha256: str = "") -> None:
    """Streams the release asset to disk. When GitHub published a SHA-256 for
    the asset, the download must match it (ChecksumMismatchError otherwise).
    Raises on any failure -- the caller decides how to surface that."""
    resp = requests.get(download_url, timeout=DOWNLOAD_TIMEOUT_SECONDS, stream=True)
    resp.raise_for_status()
    dest_path.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = dest_path.with_suffix(dest_path.suffix + ".part")
    sha = hashlib.sha256()
    with open(tmp_path, "wb") as f:
        for chunk in resp.iter_content(chunk_size=DOWNLOAD_CHUNK_BYTES):
            if chunk:
                f.write(chunk)
                sha.update(chunk)
    if expected_sha256 and sha.hexdigest().lower() != expected_sha256.lower():
        tmp_path.unlink(missing_ok=True)
        raise ChecksumMismatchError(download_url)
    tmp_path.replace(dest_path)
