"""Auto-lock on idle timeout, Windows session lock (Win+L), or sleep.

Two independent watchers feed the same on_lock callback:
  - an idle-time poller (GetLastInputInfo) for the "N minutes no activity" rule
  - a hidden message-only window subscribed to WM_WTSSESSION_CHANGE /
    WM_POWERBROADCAST for immediate lock/sleep detection

Both are best-effort OS integrations; if pywin32 isn't available the idle
poller still works standalone (sleep/lock detection is skipped gracefully).
"""
from __future__ import annotations

import ctypes
import threading
import time
from ctypes import wintypes
from typing import Callable

DEFAULT_IDLE_TIMEOUT_SECONDS = 5 * 60


class _LASTINPUTINFO(ctypes.Structure):
    _fields_ = [("cbSize", wintypes.UINT), ("dwTime", wintypes.DWORD)]


def get_idle_seconds() -> float:
    info = _LASTINPUTINFO()
    info.cbSize = ctypes.sizeof(_LASTINPUTINFO)
    if not ctypes.windll.user32.GetLastInputInfo(ctypes.byref(info)):
        return 0.0
    millis_since_boot = ctypes.windll.kernel32.GetTickCount()
    return (millis_since_boot - info.dwTime) / 1000.0


class AutoLockMonitor:
    def __init__(self, on_lock: Callable[[str], None], idle_timeout_seconds: int = DEFAULT_IDLE_TIMEOUT_SECONDS):
        self.on_lock = on_lock
        self.idle_timeout_seconds = idle_timeout_seconds
        self._stop_event = threading.Event()
        self._idle_thread: threading.Thread | None = None
        self._session_thread: threading.Thread | None = None
        # The vault starts locked, so there is nothing to auto-lock until
        # notify_unlocked() is called after a successful unlock.
        self._already_locked = True

    def set_idle_timeout_minutes(self, minutes: int) -> None:
        """0 disables the idle rule; Win+L / sleep still lock immediately."""
        self.idle_timeout_seconds = max(0, int(minutes)) * 60

    def start(self) -> None:
        self._stop_event.clear()
        self._idle_thread = threading.Thread(target=self._idle_loop, daemon=True)
        self._idle_thread.start()

        self._session_thread = threading.Thread(target=self._session_watch_loop, daemon=True)
        self._session_thread.start()

    def stop(self) -> None:
        self._stop_event.set()

    def notify_unlocked(self) -> None:
        self._already_locked = False

    def notify_locked(self) -> None:
        """Manual lock: stop the idle rule from re-locking (and resetting the
        lock screen) while the user is already on it."""
        self._already_locked = True

    def _trigger(self, reason: str) -> None:
        if self._already_locked:
            return
        self._already_locked = True
        self.on_lock(reason)

    def _idle_loop(self) -> None:
        while not self._stop_event.is_set():
            if self.idle_timeout_seconds > 0 and get_idle_seconds() >= self.idle_timeout_seconds:
                self._trigger("idle_timeout")
            time.sleep(2)

    def _session_watch_loop(self) -> None:
        try:
            import win32con
            import win32gui
            import win32ts
        except ImportError:
            return  # pywin32 not available; idle-only fallback

        WM_WTSSESSION_CHANGE = 0x02B1
        WTS_SESSION_LOCK = 0x7
        PBT_APMSUSPEND = 0x4

        def wnd_proc(hwnd, msg, wparam, lparam):
            if msg == WM_WTSSESSION_CHANGE and wparam == WTS_SESSION_LOCK:
                self._trigger("session_lock")
            elif msg == win32con.WM_POWERBROADCAST and wparam == PBT_APMSUSPEND:
                self._trigger("system_sleep")
            return win32gui.DefWindowProc(hwnd, msg, wparam, lparam)

        class_name = "KagiBoxSessionWatcher"
        wc = win32gui.WNDCLASS()
        wc.lpfnWndProc = wnd_proc
        wc.lpszClassName = class_name
        try:
            win32gui.RegisterClass(wc)
        except Exception:
            pass  # already registered from a previous instance in-process

        hwnd = win32gui.CreateWindow(class_name, class_name, 0, 0, 0, 0, 0, 0, 0, None, None)
        win32ts.WTSRegisterSessionNotification(hwnd, win32ts.NOTIFY_FOR_THIS_SESSION)

        while not self._stop_event.is_set():
            win32gui.PumpWaitingMessages()
            time.sleep(0.5)
