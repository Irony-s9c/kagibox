"""KagiBox-core: KagiBox's processing without a window.

The WinUI app starts this as a child process and talks to it with one JSON
object per line (UTF-8):
  request  {"id": 1, "method": "unlock_vault", "args": {"master_password": "..."}}
  reply    {"id": 1, "ok": <result>}   or   {"id": 1, "error": "<message>"}
  event    {"event": "auto-locked", "payload": {"reason": "idle_timeout"}}

Events: "ready" {"version", "data_dir"}, "auto-locked" {"reason"},
"autotype-requested" {}, "fatal" {"error"} (then the process exits).
When stdin closes, the vault key and the clipboard are wiped and the process
exits with code 0.
"""
from __future__ import annotations

import json
import os
import sys
import threading
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from backend import config  # noqa: E402
from backend.service import KagiBoxService  # noqa: E402

# Methods the UI may call. Everything else (internal helpers, shutdown) is refused.
ALLOWED_METHODS = frozenset(
    {
        "vault_exists", "create_vault", "unlock_vault", "lock_vault", "is_unlocked",
        "list_accounts", "add_account", "update_account", "delete_account", "toggle_favorite",
        "copy_text", "reveal_password", "copy_password", "open_url", "generate_password", "estimate_strength",
        "run_health_check", "export_vault", "import_vault", "request_autotype",
        "get_settings", "update_settings", "get_app_info", "open_data_folder",
        "check_for_update", "get_app_version", "download_and_apply_update",
    }
)

EXIT_ALREADY_RUNNING = 3


class Channel:
    """Writes JSON lines to the UI. Thread-safe (events come from other threads)."""

    def __init__(self, stream):
        self._stream = stream
        self._lock = threading.Lock()

    def send(self, message: dict) -> None:
        line = json.dumps(message, ensure_ascii=False, separators=(",", ":"))
        with self._lock:
            try:
                self._stream.write(line + "\n")
                self._stream.flush()
            except (OSError, ValueError):
                pass  # the UI is gone; stdin EOF will end the process

    def event(self, name: str, payload: dict | None = None) -> None:
        self.send({"event": name, "payload": payload or {}})


def dispatch(service: KagiBoxService, request: dict) -> dict:
    req_id = request.get("id")
    method = request.get("method")
    args = request.get("args") or {}
    if method == "ping":
        return {"id": req_id, "ok": "pong"}
    if method not in ALLOWED_METHODS or not isinstance(args, dict):
        return {"id": req_id, "error": f"unknown method: {method}"}
    try:
        with service._lock:
            result = getattr(service, method)(**args)
        return {"id": req_id, "ok": result}
    except TypeError:
        config.log_error(f"bad args for {method}")
        return {"id": req_id, "error": f"bad arguments for {method}"}
    except Exception as exc:
        config.log_error(f"{method} failed")
        return {"id": req_id, "error": f"{type(exc).__name__}"}


def main() -> int:
    # Our protocol owns the real stdout; stray prints from libraries go to stderr.
    out = open(sys.stdout.fileno(), "w", encoding="utf-8", newline="\n", closefd=False)
    sys.stdout = sys.stderr
    stdin = open(sys.stdin.fileno(), "r", encoding="utf-8-sig", newline=None, closefd=False)
    channel = Channel(out)

    data_dir = config.default_data_dir()
    data_dir.mkdir(parents=True, exist_ok=True)
    config.setup_error_log(data_dir)
    config.load_env(data_dir)

    mutex = config.acquire_single_instance(data_dir)
    if mutex is None:
        channel.event("fatal", {"error": "already_running"})
        return EXIT_ALREADY_RUNNING

    service = KagiBoxService(data_dir, emit=channel.event)
    channel.event("ready", {"version": service.get_app_version(), "data_dir": str(data_dir)})
    try:
        service.start_background_services()
    except Exception:
        config.log_error("start background services")

    for line in stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
            if not isinstance(request, dict):
                raise ValueError("not an object")
        except ValueError:
            config.log_error("invalid request line")
            continue
        channel.send(dispatch(service, request))

    service.shutdown()
    return 0


if __name__ == "__main__":
    try:
        code = main()
    except Exception:
        config.log_error("fatal")
        code = 1
    # Background threads (auto-lock poller, keyboard hook) must not keep us alive.
    os._exit(code)
