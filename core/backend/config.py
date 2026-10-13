"""Paths, .env loading and persisted (non-secret) user settings.

.env is looked up next to KagiBox.exe (installed / frozen build), in the
project root (source run), and in the data folder (%LOCALAPPDATA%\\KagiBox),
because Program Files is not user-writable. Real environment variables always
win over .env values. No secret is ever hardcoded here.
"""
from __future__ import annotations

import ctypes
import hashlib
import json
import logging
import os
import sys
import traceback
from logging.handlers import RotatingFileHandler
from pathlib import Path
from typing import Any

DEFAULT_GITHUB_REPO = "Irony-s9c/kagibox"

# Source layout: <project>/core/backend/config.py
_PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent


def app_dir() -> Path:
    """Folder that contains the running exe (frozen) or the project root (source)."""
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return _PROJECT_ROOT


def resource_dir() -> Path:
    """Folder that contains bundled read-only files (changelog/VERSION)."""
    if getattr(sys, "frozen", False):
        return Path(getattr(sys, "_MEIPASS", app_dir()))
    return _PROJECT_ROOT


# ---------------------------------------------------------------------------
# Error log: <data dir>\logs\error.log, rotated at about 5 MB. Never raises.

_logger = logging.getLogger("kagibox")


def setup_error_log(data_dir: Path) -> None:
    if _logger.handlers:
        return
    try:
        log_dir = data_dir / "logs"
        log_dir.mkdir(parents=True, exist_ok=True)
        handler = RotatingFileHandler(log_dir / "error.log", maxBytes=5 * 1024 * 1024, backupCount=2, encoding="utf-8", delay=True)
        handler.setFormatter(logging.Formatter("%(asctime)s [%(levelname)s] core %(message)s", "%Y-%m-%d %H:%M:%S"))
        _logger.addHandler(handler)
        _logger.setLevel(logging.ERROR)
    except Exception:
        pass


def log_error(context: str) -> None:
    """Logs the exception being handled (if any) with a short context."""
    try:
        detail = traceback.format_exc() if sys.exc_info()[0] is not None else ""
        _logger.error("%s%s", context, ("\n" + detail.rstrip()) if detail else "")
    except Exception:
        pass


def default_data_dir() -> Path:
    # The vault must live somewhere the current user can always write to;
    # Program Files is admin-only once installed. KAGIBOX_DATA_DIR lets test
    # runs point at a scratch folder so they never touch the real vault.
    base = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "KagiBox"
    return Path(os.environ.get("KAGIBOX_DATA_DIR", str(base)))


def single_instance_mutex_name(data_dir: Path) -> str:
    """Two running copies would each hold their own in-memory vault and
    overwrite each other's saves, so only one instance per data folder is
    allowed (shared by the legacy UI and KagiBox-core)."""
    digest = hashlib.sha1(str(data_dir).lower().encode("utf-8")).hexdigest()[:16]
    return "Local\\KagiBox-SingleInstance-" + digest


def acquire_single_instance(data_dir: Path) -> object | None:
    """Returns the mutex handle, or None when another instance already owns it."""
    kernel32 = ctypes.windll.kernel32
    handle = kernel32.CreateMutexW(None, False, single_instance_mutex_name(data_dir))
    if kernel32.GetLastError() == 183:  # ERROR_ALREADY_EXISTS
        return None
    return handle


def _parse_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    try:
        text = path.read_text(encoding="utf-8-sig")
    except OSError:
        return values
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        values[key.strip()] = value
    return values


def load_env(data_dir: Path) -> None:
    """Fills os.environ from the first .env files found, without overriding
    variables that are already set."""
    for candidate in (app_dir() / ".env", data_dir / ".env"):
        for key, value in _parse_env_file(candidate).items():
            # An empty value ("GITHUB_REPO=" from .env.example) means "not set",
            # so it must not hide a value from the next .env file.
            if value and not os.environ.get(key):
                os.environ[key] = value


def github_repo() -> str:
    return os.environ.get("GITHUB_REPO", "").strip() or DEFAULT_GITHUB_REPO


def github_token() -> str | None:
    return os.environ.get("GITHUB_TOKEN", "").strip() or None


def update_check_allowed_by_env() -> bool:
    return os.environ.get("UPDATE_CHECK_ENABLED", "true").strip().lower() not in ("0", "false", "no", "off")


def read_app_version() -> str:
    # changelog/VERSION is the single source of truth shared with
    # installer.iss (MyAppVersion) and the UI project.
    # core/KagiBox-core.spec bundles it at the same relative path.
    try:
        return (resource_dir() / "changelog" / "VERSION").read_text(encoding="utf-8").strip()
    except OSError:
        return "0.0.0"


# ---------------------------------------------------------------------------
# Non-secret UI settings, stored as plain JSON next to the vault.

IDLE_LOCK_CHOICES = (0, 1, 5, 15, 30, 60)  # minutes; 0 = off

DEFAULT_SETTINGS: dict[str, Any] = {
    "idle_lock_minutes": 5,
    "check_updates_on_start": True,
}


class SettingsStore:
    def __init__(self, path: Path):
        self.path = path
        self._data = dict(DEFAULT_SETTINGS)
        try:
            loaded = json.loads(path.read_text(encoding="utf-8"))
            if isinstance(loaded, dict):
                self._data.update(self._sanitize(loaded))
        except (OSError, ValueError):
            pass

    @staticmethod
    def _sanitize(values: dict[str, Any]) -> dict[str, Any]:
        out: dict[str, Any] = {}
        if "idle_lock_minutes" in values:
            try:
                minutes = int(values["idle_lock_minutes"])
            except (TypeError, ValueError):
                minutes = DEFAULT_SETTINGS["idle_lock_minutes"]
            out["idle_lock_minutes"] = minutes if minutes in IDLE_LOCK_CHOICES else DEFAULT_SETTINGS["idle_lock_minutes"]
        if "check_updates_on_start" in values:
            out["check_updates_on_start"] = bool(values["check_updates_on_start"])
        return out

    def all(self) -> dict[str, Any]:
        return dict(self._data)

    def update(self, values: dict[str, Any]) -> dict[str, Any]:
        self._data.update(self._sanitize(values or {}))
        self.path.parent.mkdir(parents=True, exist_ok=True)
        tmp = self.path.with_suffix(".tmp")
        tmp.write_text(json.dumps(self._data, indent=2), encoding="utf-8")
        os.replace(tmp, self.path)
        return self.all()
