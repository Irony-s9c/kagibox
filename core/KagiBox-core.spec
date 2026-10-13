# -*- mode: python ; coding: utf-8 -*-
# KagiBox-core.exe: the processing without a window, started by the WinUI app.
# Build: .venv\Scripts\python.exe -m PyInstaller --noconfirm core\KagiBox-core.spec
#        --distpath build\core-dist --workpath build\core-work
# Console subsystem on purpose: the UI talks to it over stdin / stdout and
# starts it with CreateNoWindow, so no console window appears.
# The runtime files go to "core-lib" (not "_internal") so they can sit next to
# the WinUI app's own files without mixing.
import os

CORE = SPECPATH
ROOT = os.path.dirname(CORE)

a = Analysis(
    [os.path.join(CORE, 'kagibox_core.py')],
    pathex=[CORE],
    binaries=[],
    datas=[(os.path.join(ROOT, 'changelog', 'VERSION'), 'changelog')],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['tkinter'],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='KagiBox-core',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=[os.path.join(ROOT, 'images', 'icon.ico')],
    contents_directory='core-lib',
)
coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    upx_exclude=[],
    name='KagiBox-core',
)
