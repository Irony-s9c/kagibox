$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

Write-Host "=== KagiBox Dev Mode ==="
Write-Host ""

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] Node.js not found. Install from: https://nodejs.org"
    Read-Host "Press Enter to close"
    exit 1
}

if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] Rust not found. Run build.bat first."
    Read-Host "Press Enter to close"
    exit 1
}


Set-Location $Root

if (-not (Test-Path (Join-Path $Root "node_modules"))) {
    Write-Host "[INFO] node_modules not found. Running npm install..."
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] npm install failed"
        Read-Host "Press Enter to close"
        exit 1
    }
}

Write-Host "Starting dev server..."
npx tauri dev
