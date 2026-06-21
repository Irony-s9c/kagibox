$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "Node.js が見つかりません: https://nodejs.org"
    Read-Host "Enter で閉じる"; exit 1
}
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Host "Rust が見つかりません: https://rustup.rs"
    Read-Host "Enter で閉じる"; exit 1
}

Set-Location $Root
if (-not (Test-Path (Join-Path $Root "node_modules"))) { npm install }

npx tauri dev
