"""Global-hotkey autotype: Ctrl+Alt+V captures the currently focused window,
shows the KagiBox picker, then types "username -> Tab -> password -> Enter"
back into that original window.
"""
from __future__ import annotations

import time
from typing import Callable, Optional

import keyboard

DEFAULT_HOTKEY = "ctrl+alt+v"


def capture_foreground_window() -> Optional[int]:
    try:
        import win32gui

        return win32gui.GetForegroundWindow()
    except ImportError:
        return None


def focus_window(hwnd: Optional[int]) -> None:
    if hwnd is None:
        return
    try:
        import win32con
        import win32gui

        if win32gui.IsIconic(hwnd):
            win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
        win32gui.SetForegroundWindow(hwnd)
        time.sleep(0.15)
    except ImportError:
        pass


def perform_autotype(username: str, password: str, target_hwnd: Optional[int] = None) -> None:
    focus_window(target_hwnd)
    if username:
        keyboard.write(username)
    keyboard.send("tab")
    if password:
        keyboard.write(password)
    keyboard.send("enter")


class HotkeyListener:
    """Registers the global hotkey; on_trigger receives the hwnd that was
    focused at the moment the hotkey fired, so the caller can restore focus
    to it later once the user has picked an account in the KagiBox UI."""

    def __init__(self, on_trigger: Callable[[Optional[int]], None], hotkey: str = DEFAULT_HOTKEY):
        self.on_trigger = on_trigger
        self.hotkey = hotkey
        self._registered = False

    def start(self) -> None:
        if self._registered:
            return
        keyboard.add_hotkey(self.hotkey, self._handle)
        self._registered = True

    def stop(self) -> None:
        if self._registered:
            keyboard.remove_hotkey(self.hotkey)
            self._registered = False

    def _handle(self) -> None:
        hwnd = capture_foreground_window()
        self.on_trigger(hwnd)
