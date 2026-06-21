$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Log  = Join-Path $Root "build-log.txt"

Start-Transcript -Path $Log -Force
Write-Host "=== KagiBox Build ==="
Write-Host ""

# --- Load signing key from signing.ps1 (gitignored, not committed) ---
$signingFile = Join-Path $Root "signing.ps1"
if (Test-Path $signingFile) {
    . $signingFile
    Write-Host "[info] Signing key loaded from signing.ps1"
} else {
    Write-Host "[warn] signing.ps1 not found - updater signature will be skipped"
    Write-Host "       Create signing.ps1 with: " '$env:TAURI_SIGNING_PRIVATE_KEY = "..."'
}
Write-Host ""

# --- Node.js ---
Write-Host "[check] Node.js..."
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] Node.js not found."
    Write-Host "        Install from: https://nodejs.org"
    Stop-Transcript
    Start-Process notepad $Log
    exit 1
}
Write-Host "        $(node --version)"

# --- npm ---
Write-Host "[check] npm..."
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] npm not found."
    Write-Host "        Install from: https://nodejs.org"
    Stop-Transcript
    Start-Process notepad $Log
    exit 1
}
Write-Host "        $(npm --version)"

# --- Rust ---
Write-Host "[check] Rust..."
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Host "[INFO] Rust not found. Downloading rustup..."
    $rustup = Join-Path $env:TEMP "rustup-init.exe"
    try {
        Invoke-WebRequest "https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe" -OutFile $rustup
        & $rustup -y --default-toolchain stable
        Remove-Item $rustup -Force
        $env:PATH = "$env:USERPROFILE\.cargo\bin;$env:PATH"
    } catch {
        Write-Host "[ERROR] Rust install failed: $_"
        Write-Host "        Install manually from: https://rustup.rs"
        Stop-Transcript
        Start-Process notepad $Log
        exit 1
    }
    if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] cargo not found after install."
        Write-Host "        Close this window, open a new terminal, and re-run build.bat"
        Stop-Transcript
        Start-Process notepad $Log
        exit 1
    }
}
Write-Host "        $(cargo --version)"

Write-Host ""

# --- npm install ---
Write-Host "[1/2] npm install..."
Set-Location $Root
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] npm install failed (exit $LASTEXITCODE)"
    Stop-Transcript
    Start-Process notepad $Log
    exit 1
}
Write-Host ""

# --- tauri build ---
Write-Host "[2/2] Building installer (first run takes several minutes)..."
npx tauri build
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] tauri build failed (exit $LASTEXITCODE)"
    Write-Host ""
    Write-Host "Common causes:"
    Write-Host "  - Microsoft C++ Build Tools missing"
    Write-Host "    Fix: winget install Microsoft.VisualStudio.2022.BuildTools"
    Write-Host "  - WebView2 runtime missing (usually pre-installed on Win10/11)"
    Stop-Transcript
    Start-Process notepad $Log
    exit 1
}

# --- Copy installer into project folder and clean up build cache ---
Write-Host ""
Write-Host "=== Build complete ==="
$BundleDir = Join-Path $Root "target\release\bundle\nsis"
$Dest      = Join-Path $Root "installer"
New-Item -ItemType Directory -Path $Dest -Force | Out-Null

if (Test-Path $BundleDir) {
    Get-ChildItem $BundleDir | ForEach-Object {
        Copy-Item $_.FullName -Destination $Dest -Force
        Write-Host "Installer: $Dest\$($_.Name)"
    }
} else {
    Write-Host "[warn] NSIS output not found: $BundleDir"
}

# --- Publish GitHub Release ---
Write-Host ""
Write-Host "Publishing GitHub release..."
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "[warn] gh CLI not found. Skipping GitHub release."
    Write-Host "       Install from: https://cli.github.com"
} else {
    $confPath = Join-Path $Root "src-tauri\tauri.conf.json"
    $conf = Get-Content $confPath -Raw | ConvertFrom-Json
    $version = $conf.version
    $tag = "v$version"

    $zipFile = Get-ChildItem $BundleDir -Filter "*.nsis.zip"    | Select-Object -First 1
    $sigFile = Get-ChildItem $BundleDir -Filter "*.nsis.zip.sig" | Select-Object -First 1
    $exeFile = Get-ChildItem $BundleDir -Filter "*.exe"          | Select-Object -First 1

    if ($zipFile -and $sigFile) {
        $sig     = (Get-Content $sigFile.FullName -Raw).Trim()
        $pubDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $dlBase  = "https://github.com/Irony-s9c/kagibox/releases/download/$tag"

        $latestObj = [ordered]@{
            version  = $version
            notes    = "KagiBox $tag"
            pub_date = $pubDate
            platforms = [ordered]@{
                "windows-x86_64" = [ordered]@{
                    signature = $sig
                    url       = "$dlBase/$($zipFile.Name)"
                }
            }
        }
        $latestJson = $latestObj | ConvertTo-Json -Depth 5
        $latestPath = Join-Path $Dest "latest.json"
        Set-Content -Path $latestPath -Value $latestJson -Encoding utf8
        Write-Host "latest.json: $latestPath"

        # Delete existing release for this tag if any
        gh release delete $tag --yes --repo Irony-s9c/kagibox 2>$null; $true

        $uploadArgs = @(
            $exeFile.FullName,
            $zipFile.FullName,
            $sigFile.FullName,
            $latestPath
        )
        gh release create $tag --repo Irony-s9c/kagibox `
            --title "KagiBox $tag" `
            --notes "KagiBox $tag release" `
            @uploadArgs
        if ($LASTEXITCODE -eq 0) {
            Write-Host "GitHub release published: $tag"
        } else {
            Write-Host "[warn] GitHub release failed (exit $LASTEXITCODE)"
        }
    } else {
        Write-Host "[warn] Updater artifacts not found (.nsis.zip / .sig). Skipping release."
        Write-Host "       Make sure tauri.conf.json has: createUpdaterArtifacts: true"
    }
}

# Clean up bulk build artifacts (keep installer, delete intermediate files)
Write-Host ""
Write-Host "Cleaning build cache..."
$TargetBase = Join-Path $Root "target"
Remove-Item (Join-Path $TargetBase "debug")              -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\build")      -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\deps")       -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\examples")   -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\incremental")-Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Done."

Write-Host ""
Write-Host "Log: $Log"
Write-Host ""

Stop-Transcript
