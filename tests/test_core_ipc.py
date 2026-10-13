"""Protocol test for KagiBox-core (core/kagibox_core.py).

Run: .venv\\Scripts\\python.exe tests\\test_core_ipc.py [path\\to\\KagiBox-core.exe]
Without an argument the source version is started with the current Python.
Uses a temporary data folder; the real vault is never touched.
"""
from __future__ import annotations

import json
import os
import queue
import shutil
import subprocess
import sys
import tempfile
import threading
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


class Core:
    def __init__(self, data_dir: Path, exe: str | None):
        cmd = [exe] if exe else [sys.executable, str(ROOT / "core" / "kagibox_core.py")]
        env = {**os.environ, "KAGIBOX_DATA_DIR": str(data_dir), "UPDATE_CHECK_ENABLED": "false"}
        self.proc = subprocess.Popen(
            cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            env=env, creationflags=subprocess.CREATE_NO_WINDOW,
        )
        self.messages: "queue.Queue[dict]" = queue.Queue()
        self.next_id = 0
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        for raw in self.proc.stdout:
            line = raw.decode("utf-8").strip()
            if line:
                self.messages.put(json.loads(line))

    def wait_event(self, name: str, timeout=15) -> dict:
        while True:
            msg = self.messages.get(timeout=timeout)
            if msg.get("event") == name:
                return msg.get("payload", {})

    def call(self, method: str, **args):
        self.next_id += 1
        req = {"id": self.next_id, "method": method, "args": args}
        self.proc.stdin.write((json.dumps(req, ensure_ascii=False) + "\n").encode("utf-8"))
        self.proc.stdin.flush()
        while True:
            msg = self.messages.get(timeout=30)
            if msg.get("id") == self.next_id:
                return msg


def main() -> int:
    exe = sys.argv[1] if len(sys.argv) > 1 else None
    data = Path(tempfile.mkdtemp(prefix="kagibox_ipc_"))
    try:
        core = Core(data, exe)
        ready = core.wait_event("ready")
        assert ready["version"] == (ROOT / "changelog" / "VERSION").read_text(encoding="utf-8").strip(), ready
        assert Path(ready["data_dir"]) == data, ready

        assert core.call("ping")["ok"] == "pong"
        assert core.call("vault_exists")["ok"] is False
        assert core.call("create_vault", master_password="短い")["ok"]["error"] == "too_short"
        assert core.call("create_vault", master_password="ipc test master 日本語")["ok"]["ok"] is True
        added = core.call("add_account", data={"service": "テスト銀行", "username": "u", "password": "p@ss-1234567"})["ok"]
        assert added["ok"], added
        listed = core.call("list_accounts")["ok"]
        assert [a["service"] for a in listed] == ["テスト銀行"], listed
        assert all("password" not in a for a in listed), "password leaked"
        assert core.call("reveal_password", item_id=added["id"])["ok"]["password"] == "p@ss-1234567"

        # refused / malformed requests get an error reply and do not kill the core
        assert "error" in core.call("shutdown")
        assert "error" in core.call("_guard")
        assert "error" in core.call("unlock_vault", wrong_arg=1)

        assert core.call("lock_vault")["ok"]["ok"] is True
        assert core.call("list_accounts")["ok"] == {"ok": False, "error": "vault_locked"}
        assert core.call("unlock_vault", master_password="wrong")["ok"]["error"] == "invalid_password"
        assert core.call("unlock_vault", master_password="ipc test master 日本語")["ok"]["ok"] is True
        info = core.call("get_app_info")["ok"]
        assert info["data_dir"] == str(data), info

        # a second core on the same data folder must refuse to start
        second = Core(data, exe)
        assert second.wait_event("fatal")["error"] == "already_running"
        assert second.proc.wait(timeout=15) == 3

        # closing stdin ends the core cleanly
        core.proc.stdin.close()
        assert core.proc.wait(timeout=15) == 0
        print("CORE IPC TESTS PASSED" + (f" ({exe})" if exe else " (source)"))
        return 0
    finally:
        shutil.rmtree(data, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
