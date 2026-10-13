# Install test: installs installer_output\KagiBox-Setup-<ver>.exe silently for the current user into a
# temporary folder (no admin, Program Files untouched), checks that the installer starts KagiBox
# afterwards (the same thing happens after an in-app update), closes it, uninstalls silently and checks
# that the files are gone. A throwaway data folder is used; the real vault is never touched.
# Usage: powershell -ExecutionPolicy Bypass -File scripts\install-test.ps1
# (No screenshot here: the look is checked with scripts\smoke-test.ps1.)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$ver = (Get-Content (Join-Path $root "changelog\VERSION") -Raw).Trim()
$setup = Join-Path $root "installer_output\KagiBox-Setup-$ver.exe"
if (-not (Test-Path $setup)) { throw "$setup not found. Build the installer first." }

Add-Type -AssemblyName System.Windows.Forms
$screens = [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Sort-Object { $_.Bounds.X }
$target = if ($screens) { @($screens)[-1].WorkingArea } else { [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea }

$id = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$dir = Join-Path $env:TEMP "kagibox_install_$id"
$data = Join-Path $env:TEMP "kagibox_install_data_$id"
New-Item -ItemType Directory -Force $data | Out-Null

# Inherited by the KagiBox.exe that the installer starts at the end.
$env:KAGIBOX_DATA_DIR = $data
$env:KAGIBOX_WINDOW_POS = "$($target.X + 60),$($target.Y + 40)"
$env:UPDATE_CHECK_ENABLED = "false"

$result = [ordered]@{}
# Wait for the installer process only: Start-Process -Wait would also wait for the KagiBox it starts.
$p = Start-Process $setup -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CURRENTUSER", "/DIR=`"$dir`"", "/NOICONS", "/TASKS=`"`"" -PassThru
$p.WaitForExit()
$result["installer exit code"] = $p.ExitCode
$result["KagiBox.exe installed"] = Test-Path (Join-Path $dir "KagiBox.exe")
$result["KagiBox-core.exe installed"] = Test-Path (Join-Path $dir "KagiBox-core.exe")
$result["KagiBox.pri installed"] = Test-Path (Join-Path $dir "KagiBox.pri")
$result["uninstaller present"] = Test-Path (Join-Path $dir "unins000.exe")

# The installer starts KagiBox when it finishes.
$app = $null
$deadline = (Get-Date).AddSeconds(20)
while (-not $app -and (Get-Date) -lt $deadline) {
    $app = Get-Process -Name KagiBox -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$dir*" } | Select-Object -First 1
    if (-not $app) { Start-Sleep -Milliseconds 300 }
}
$result["app started after install"] = [bool]$app
if ($app) {
    Start-Sleep -Seconds 8
    $app.Refresh()
    $result["app alive after 8s"] = -not $app.HasExited
    [void]$app.CloseMainWindow()
    if (-not $app.WaitForExit(8000)) { Stop-Process -Id $app.Id -Force }
    Start-Sleep -Milliseconds 800
    $core = Get-Process -Name KagiBox-core -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$dir*" }
    $result["core exited with the app"] = -not $core
}

$log = Join-Path $data "logs\error.log"
$result["error.log lines"] = if (Test-Path $log) { @(Get-Content $log).Count } else { 0 }

$un = Start-Process (Join-Path $dir "unins000.exe") -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -PassThru
$un.WaitForExit()
# The uninstaller hands over to a temporary copy of itself; give it time to finish removing files.
$deadline = (Get-Date).AddSeconds(30)
while ((Test-Path (Join-Path $dir "KagiBox.exe")) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
Start-Sleep -Seconds 2
$result["uninstaller exit code"] = $un.ExitCode
$result["files removed"] = -not (Test-Path (Join-Path $dir "KagiBox.exe"))

$result.GetEnumerator() | ForEach-Object { Write-Host ("{0}: {1}" -f $_.Key, $_.Value) }
if (Test-Path $log) { Write-Host "--- error.log"; Get-Content $log }
Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
