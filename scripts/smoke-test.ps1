# Smoke test for the WinUI app: starts KagiBox.exe with a throwaway data folder, shows the window on the
# last secondary monitor without activating it, optionally saves a screenshot, reports error.log, closes it.
# The real vault (%LOCALAPPDATA%\KagiBox) is never touched.
#
# Usage: powershell -ExecutionPolicy Bypass -File scripts\smoke-test.ps1 [-Exe <KagiBox.exe>] [-Seconds 8]
#          [-Screenshot <png>] [-Seed] [-Unlock]
#   -Seed    create the sample vault from tests\seed_test_vault.py (master password test1234)
#   -Unlock  (Debug builds only) unlock it automatically to capture the unlocked screens
param(
    [string]$Exe = "",
    [int]$Seconds = 8,
    [string]$Screenshot = "",
    [switch]$Seed,
    [switch]$Unlock,
    # (Debug builds, with -Unlock) open a screen: generator / health / settings / edit / autotype
    [string]$Page = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Exe) {
    $Exe = Get-ChildItem (Join-Path $root "ui\bin") -Recurse -Filter "KagiBox.exe" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Exe -or -not (Test-Path $Exe)) { throw "KagiBox.exe not found. Build ui\KagiBox.csproj first." }
$Exe = (Resolve-Path $Exe).Path

$running = Get-Process -Name "KagiBox" -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $Exe }
if ($running) {
    Write-Host "[SKIP] This KagiBox.exe is already running."
    exit 2
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class SmokeWin {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    public static IntPtr Find(uint pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }
}
"@

$screens = [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Sort-Object { $_.Bounds.X }
$target = if ($screens) { @($screens)[-1].WorkingArea } else { [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea }

$data = Join-Path $env:TEMP ("kagibox_smoke_" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Force $data | Out-Null
if ($Seed) {
    $py = Join-Path $root ".venv\Scripts\python.exe"
    & $py (Join-Path $root "tests\seed_test_vault.py") $data | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create the sample vault." }
}

$env:KAGIBOX_DATA_DIR = $data
$env:KAGIBOX_WINDOW_POS = "$($target.X + 60),$($target.Y + 40)"
$env:UPDATE_CHECK_ENABLED = "false"
if ($Unlock) { $env:KAGIBOX_TEST_MASTER_PASSWORD = "test1234" }
if ($Page -and $Page -ne "vault") { $env:KAGIBOX_TEST_PAGE = $Page }
$p = Start-Process $Exe -PassThru
Remove-Item Env:\KAGIBOX_TEST_MASTER_PASSWORD -ErrorAction SilentlyContinue
Remove-Item Env:\KAGIBOX_TEST_PAGE -ErrorAction SilentlyContinue

# Keep the window off the primary monitor (no activation, no resize).
$SWP_NOSIZE = 0x0001; $SWP_NOZORDER = 0x0004; $SWP_NOACTIVATE = 0x0010
$deadline = (Get-Date).AddSeconds(20)
$moved = $false
while (-not $moved -and (Get-Date) -lt $deadline -and -not $p.HasExited) {
    $h = [SmokeWin]::Find([uint32]$p.Id)
    if ($h -ne [IntPtr]::Zero) {
        [void][SmokeWin]::SetWindowPos($h, [IntPtr]::Zero, $target.X + 60, $target.Y + 40, 0, 0, $SWP_NOSIZE -bor $SWP_NOZORDER -bor $SWP_NOACTIVATE)
        $moved = $true
    } else {
        Start-Sleep -Milliseconds 15
    }
}

Start-Sleep -Seconds $Seconds
if ($Screenshot -and $moved -and -not $p.HasExited) {
    $h = [SmokeWin]::Find([uint32]$p.Id)
    [void][SmokeWin]::ShowWindow($h, 4)  # SW_SHOWNOACTIVATE
    # Put it above other windows for the capture without taking the focus, so only KagiBox is captured.
    $HWND_TOPMOST = [IntPtr](-1); $HWND_NOTOPMOST = [IntPtr](-2); $SWP_NOMOVE = 0x0002
    [void][SmokeWin]::SetWindowPos($h, $HWND_TOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_NOACTIVATE)
    Start-Sleep -Seconds 2
    $r = New-Object SmokeWin+RECT
    [void][SmokeWin]::GetWindowRect($h, [ref]$r)
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $bmp.Save($Screenshot, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    [void][SmokeWin]::SetWindowPos($h, $HWND_NOTOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_NOACTIVATE)
    Write-Host "screenshot: $Screenshot"
}

$alive = -not $p.HasExited
Write-Host "exe: $Exe"
Write-Host "window on x=$($target.X): $moved"
Write-Host "alive after ${Seconds}s: $alive"
$log = Join-Path $data "logs\error.log"
if (Test-Path $log) { Write-Host "--- error.log"; Get-Content $log } else { Write-Host "no error.log" }

# Close like a user would (WM_CLOSE) so the core gets its stdin closed and exits cleanly.
if ($alive) {
    [void]$p.CloseMainWindow()
    if (-not $p.WaitForExit(8000)) { Write-Host "[WARN] did not exit in time; killing"; Stop-Process -Id $p.Id -Force }
}
Start-Sleep -Milliseconds 800
$leftCore = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and $_.CommandLine -match "kagibox_core|KagiBox-core" -and $_.CommandLine -notmatch "smoke" }
Write-Host "core processes left: $(@($leftCore).Count)"
Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
if (-not $alive) { exit 1 }
exit 0
