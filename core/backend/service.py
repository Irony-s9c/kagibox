"""KagiBox's processing, independent of any UI toolkit.

Used by the headless core (core/kagibox_core.py), which the WinUI app drives
over JSON lines.

The contract that matters for security review:
  - list_accounts() never includes the "password" field
  - reveal_password/copy_password/request_autotype
    are the ONLY calls that touch a decrypted secret, and only for the single
    item asked for
  - lock()/on idle/on sleep wipes the in-memory key immediately

Things only a UI can do (file dialogs, showing / minimizing the window) stay in
the UI. The service tells the UI about asynchronous happenings through
emit(event, payload): "auto-locked" {"reason"} and "autotype-requested" {}.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import tempfile
import threading
import webbrowser
from pathlib import Path
from typing import Any, Callable, Optional
from urllib.parse import urlsplit

import pyperclip

from . import config, health_check, password_generator, updater
from .autolock import AutoLockMonitor
from .autotype import DEFAULT_HOTKEY, HotkeyListener, perform_autotype
from .vault import InvalidMasterPasswordError, VaultLockedError, VaultManager

APP_VERSION = config.read_app_version()
CLIPBOARD_CLEAR_SECONDS = 30
MIN_MASTER_PASSWORD_LENGTH = 8

EmitFn = Callable[[str, dict], None]


class KagiBoxService:
    def __init__(self, data_dir: Path, emit: Optional[EmitFn] = None):
        self._data_dir = data_dir
        self._emit_fn = emit
        self._vault = VaultManager(data_dir / "vault.kagibox")
        self._settings = config.SettingsStore(data_dir / "settings.json")
        self._clipboard_timer: Optional[threading.Timer] = None
        self._pending_autotype_hwnd: Optional[int] = None
        # Calls from the UI and the auto-lock / hotkey threads must not touch the
        # vault at the same time.
        self._lock = threading.RLock()

        self._autolock = AutoLockMonitor(on_lock=self._handle_auto_lock)
        self._autolock.set_idle_timeout_minutes(self._settings.all()["idle_lock_minutes"])
        self._hotkey_listener = HotkeyListener(on_trigger=self._handle_hotkey)
        self._hotkey_ok = False
        self._services_started = False

    def start_background_services(self) -> None:
        # Start only once, even if called again.
        if self._services_started:
            return
        self._services_started = True
        self._autolock.start()
        try:
            self._hotkey_listener.start()
            self._hotkey_ok = True
        except Exception:
            pass  # keyboard hook can fail without admin rights on some setups; app still usable manually

    def shutdown(self) -> None:
        """Called when the UI goes away: wipe the key and anything we copied."""
        with self._lock:
            self._autolock.stop()
            try:
                self._hotkey_listener.stop()
            except Exception:
                pass
            self._vault.lock()
            self._clear_clipboard_now()

    # -- vault lifecycle ---------------------------------------------------
    def vault_exists(self) -> bool:
        return self._vault.exists()

    def create_vault(self, master_password: str) -> dict:
        if self._vault.exists():
            return {"ok": False, "error": "already_exists"}
        if len(master_password or "") < MIN_MASTER_PASSWORD_LENGTH:
            return {"ok": False, "error": "too_short"}
        self._vault.create(master_password)
        self._autolock.notify_unlocked()
        return {"ok": True}

    def unlock_vault(self, master_password: str) -> dict:
        try:
            self._vault.unlock(master_password)
            self._autolock.notify_unlocked()
            return {"ok": True}
        except InvalidMasterPasswordError:
            return {"ok": False, "error": "invalid_password"}

    def lock_vault(self) -> dict:
        self._vault.lock()
        self._autolock.notify_locked()
        self._clear_clipboard_now()
        return {"ok": True}

    def is_unlocked(self) -> bool:
        return self._vault.is_unlocked

    # -- accounts -----------------------------------------------------------
    def list_accounts(self) -> list[dict]:
        return self._guard(lambda: self._vault.list_accounts())

    def add_account(self, data: dict) -> dict:
        return self._guard(lambda: {"ok": True, "id": self._vault.add_account(data)})

    def update_account(self, item_id: str, data: dict) -> dict:
        return self._guard(lambda: (self._vault.update_account(item_id, data), {"ok": True})[1])

    def delete_account(self, item_id: str) -> dict:
        return self._guard(lambda: (self._vault.delete_account(item_id), {"ok": True})[1])

    def toggle_favorite(self, item_id: str) -> dict:
        return self._guard(lambda: {"ok": True, "favorite": self._vault.toggle_favorite(item_id)})

    # -- generic clipboard copy ---------------------------------------------
    def copy_text(self, text: str, sensitive: bool = False) -> dict:
        """For text the UI already shows (username, a freshly generated
        password on the generator screen). sensitive=True clears the
        clipboard after CLIPBOARD_CLEAR_SECONDS like a copied password."""
        pyperclip.copy(text or "")
        if sensitive:
            self._schedule_clipboard_clear()
        return {"ok": True}

    # -- on-demand secret reveal / copy -------------------------------------
    def reveal_password(self, item_id: str) -> dict:
        return self._guard(lambda: {"ok": True, "password": self._vault.get_password(item_id)})

    def copy_password(self, item_id: str) -> dict:
        def _do():
            pw = self._vault.get_password(item_id)
            if not pw:
                return {"ok": False, "error": "empty"}
            pyperclip.copy(pw)
            self._schedule_clipboard_clear()
            return {"ok": True}

        return self._guard(_do)

    # -- open login page ---------------------------------------------------
    def open_url(self, url: str) -> dict:
        """Opens only http(s) URLs in the default browser. A bare host such as
        "example.com" is treated as https://example.com."""
        url = (url or "").strip()
        if not url:
            return {"ok": False, "error": "empty"}
        # "javascript:...", "file:..." etc. are rejected; "host:8080" is not a scheme.
        scheme = re.match(r"^([a-z][a-z0-9+.-]*):(?!\d)", url, re.IGNORECASE)
        if scheme and scheme.group(1).lower() not in ("http", "https"):
            return {"ok": False, "error": "unsupported_url"}
        if "://" not in url:
            url = "https://" + url
        parts = urlsplit(url)
        if parts.scheme.lower() not in ("http", "https") or not parts.netloc:
            return {"ok": False, "error": "unsupported_url"}
        webbrowser.open(url)
        return {"ok": True}

    # -- password generator ---------------------------------------------
    def generate_password(self, options: dict) -> dict:
        pw = password_generator.generate_password(
            length=int(options.get("length", 16)),
            use_upper=bool(options.get("use_upper", True)),
            use_lower=bool(options.get("use_lower", True)),
            use_digits=bool(options.get("use_digits", True)),
            use_symbols=bool(options.get("use_symbols", True)),
            exclude_ambiguous=bool(options.get("exclude_ambiguous", True)),
        )
        return {"password": pw, "strength": password_generator.estimate_strength(pw)}

    def estimate_strength(self, password: str) -> dict:
        return password_generator.estimate_strength(password)

    # -- health check ------------------------------------------------------
    def run_health_check(self) -> dict:
        return self._guard(lambda: health_check.run(self._vault.all_items_for_health_check()))

    # -- import / export -----------------------------------------------
    def export_vault(self, file_path: str) -> dict:
        def _do():
            blob = self._vault.export_encrypted_blob()
            Path(file_path).write_text(json.dumps(blob), encoding="utf-8")
            return {"ok": True}

        return self._guard(_do)

    def import_vault(self, file_path: str, master_password: str) -> dict:
        """Replaces the current vault with the backup file, then unlocks the
        imported vault with its own master password so the in-memory state
        matches the file on disk (otherwise the next save would silently
        overwrite the imported data with the old in-memory items)."""
        try:
            raw = json.loads(Path(file_path).read_text(encoding="utf-8"))
            self._vault.import_encrypted_blob(raw, master_password)
        except InvalidMasterPasswordError:
            return {"ok": False, "error": "invalid_password"}
        except Exception:
            return {"ok": False, "error": "invalid_file"}
        try:
            self._vault.lock()
            self._vault.unlock(master_password)
            self._autolock.notify_unlocked()
        except Exception:
            self._vault.lock()
            return {"ok": False, "error": "unlock_failed"}
        return {"ok": True}

    # -- autotype ------------------------------------------------------
    def request_autotype(self, item_id: str) -> dict:
        """Types into the window that had the focus when the hotkey fired.
        The UI must get out of the way (minimize / hide) before calling this."""

        def _do():
            item = self._vault.get_item_full(item_id)
            perform_autotype(item.get("username", ""), item.get("password", ""), self._pending_autotype_hwnd)
            return {"ok": True}

        return self._guard(_do)

    def _handle_hotkey(self, hwnd: Optional[int]) -> None:
        self._pending_autotype_hwnd = hwnd
        self._emit("autotype-requested", {})

    # -- settings / info -------------------------------------------------
    def get_settings(self) -> dict:
        return self._settings.all()

    def update_settings(self, values: dict) -> dict:
        new = self._settings.update(values)
        self._autolock.set_idle_timeout_minutes(new["idle_lock_minutes"])
        return new

    def get_app_info(self) -> dict:
        return {
            "version": APP_VERSION,
            "autotype_hotkey": DEFAULT_HOTKEY.replace("ctrl", "Ctrl").replace("alt", "Alt").replace("+v", "+V"),
            "autotype_available": self._hotkey_ok,
            "clipboard_clear_seconds": CLIPBOARD_CLEAR_SECONDS,
            "data_dir": str(self._data_dir),
            "update_check_allowed": config.update_check_allowed_by_env(),
        }

    def open_data_folder(self) -> dict:
        self._data_dir.mkdir(parents=True, exist_ok=True)
        os.startfile(str(self._data_dir))  # noqa: S606 - opens our own folder in Explorer
        return {"ok": True}

    # -- update check ------------------------------------------------------
    def check_for_update(self, manual: bool = False) -> dict:
        base = {"available": False, "latest_version": APP_VERSION, "current_version": APP_VERSION, "release_notes": ""}
        if not manual and (not self._settings.all()["check_updates_on_start"] or not config.update_check_allowed_by_env()):
            return {**base, "skipped": True}
        info = updater.check_for_update(config.github_repo(), APP_VERSION, config.github_token())
        return {
            **base,
            "available": info.available,
            "latest_version": info.latest_version,
            "release_notes": info.release_notes,
        }

    def get_app_version(self) -> str:
        return APP_VERSION

    def download_and_apply_update(self) -> dict:
        """Downloads the installer for the latest release and launches it in
        /SILENT mode (progress bar only; installer.iss restarts KagiBox when
        done). The caller must then quit the app so the installer can
        overwrite its files."""
        info = updater.check_for_update(config.github_repo(), APP_VERSION, config.github_token())
        if not info.available or not info.download_url:
            return {"ok": False, "error": "no_update"}

        installer_path = Path(tempfile.gettempdir()) / f"KagiBox-Setup-{info.latest_version}.exe"
        try:
            updater.download_installer(info.download_url, installer_path, info.sha256)
        except updater.ChecksumMismatchError:
            config.log_error("update installer checksum mismatch")
            return {"ok": False, "error": "checksum_mismatch"}
        except Exception:
            config.log_error("update download failed")
            return {"ok": False, "error": "download_failed"}

        try:
            subprocess.Popen(
                [str(installer_path), "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS"],
                close_fds=True,
            )
        except Exception:
            return {"ok": False, "error": "launch_failed"}

        self._vault.lock()
        self._clear_clipboard_now()
        return {"ok": True}

    # -- internal ------------------------------------------------------
    def _emit(self, event: str, payload: dict) -> None:
        if self._emit_fn is not None:
            try:
                self._emit_fn(event, payload)
            except Exception:
                config.log_error(f"emit {event}")

    def _handle_auto_lock(self, reason: str) -> None:
        with self._lock:
            self._vault.lock()
            self._clear_clipboard_now()
        self._emit("auto-locked", {"reason": reason})

    def _schedule_clipboard_clear(self) -> None:
        if self._clipboard_timer is not None:
            self._clipboard_timer.cancel()
        self._clipboard_timer = threading.Timer(CLIPBOARD_CLEAR_SECONDS, self._clear_clipboard_now)
        self._clipboard_timer.daemon = True
        self._clipboard_timer.start()

    def _clear_clipboard_now(self) -> None:
        try:
            pyperclip.copy("")
        except Exception:
            pass

    def _guard(self, fn):
        try:
            return fn()
        except VaultLockedError:
            return {"ok": False, "error": "vault_locked"}
        except KeyError:
            return {"ok": False, "error": "not_found"}
