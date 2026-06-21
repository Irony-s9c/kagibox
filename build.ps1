$ErrorActionPreference = "Stop"
$Root    = $PSScriptRoot

# Ensure gh CLI is on PATH
$ghDefault = "C:\Program Files\GitHub CLI"
if ((Test-Path "$ghDefault\gh.exe") -and ($env:PATH -notlike "*GitHub CLI*")) {
    $env:PATH = "$ghDefault;$env:PATH"
}
$LogDir  = Join-Path $Root "build-logs"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
$LogDate = (Get-Date -Format "yyyyMMdd-HHmmss")
$Log     = Join-Path $LogDir "build-$LogDate.txt"

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

# --- Version bump ---
$confPath = Join-Path $Root "src-tauri\tauri.conf.json"
$conf = Get-Content $confPath -Raw | ConvertFrom-Json
$currentVersion = $conf.version

Write-Host "Current version: $currentVersion"
Stop-Transcript  # Read-Host does not log to transcript cleanly; pause it briefly
$input = Read-Host "New version (Enter = keep $currentVersion)"
Start-Transcript -Path $Log -Append -Force

if ([string]::IsNullOrWhiteSpace($input)) {
    $newVersion = $currentVersion
} else {
    $newVersion = $input.Trim()
}

if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "[ERROR] Invalid version format. Use X.Y.Z (e.g. 0.2.0)"
    Stop-Transcript
    Start-Process notepad $Log
    exit 1
}

if ($newVersion -ne $currentVersion) {
    Write-Host "Bumping version: $currentVersion -> $newVersion"
    $confRaw = [System.IO.File]::ReadAllText($confPath)
    $confRaw = $confRaw -replace '"version"\s*:\s*"[^"]*"', """version"": ""$newVersion"""
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($confPath, $confRaw, $utf8NoBom)
    Write-Host "tauri.conf.json updated."
} else {
    Write-Host "Version unchanged: $currentVersion"
}

# Rename log file to include version
$newLog = Join-Path $LogDir "build-v$newVersion-$LogDate.txt"
Stop-Transcript
Rename-Item -Path $Log -NewName (Split-Path $newLog -Leaf) -ErrorAction SilentlyContinue
$Log = $newLog
Start-Transcript -Path $Log -Append -Force

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
Write-Host "[1/3] npm install..."
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
Write-Host "[2/3] Building installer (first run takes several minutes)..."
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

# --- Copy installer ---
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

# --- [3/3] Git commit + push + GitHub Release ---
Write-Host ""
Write-Host "[3/3] Publishing update..."

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "[warn] gh CLI not found. Skipping GitHub release."
    Write-Host "       Install from: https://cli.github.com"
} else {
    $tag = "v$newVersion"

    # git commit & push if version changed
    if ($newVersion -ne $currentVersion) {
        Write-Host "Committing version bump..."
        git -C $Root add "src-tauri/tauri.conf.json"
        git -C $Root commit -m "bump version to $newVersion"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[warn] git commit failed. Continuing..."
        }
        git -C $Root push
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[warn] git push failed. Release notes may be incomplete."
        } else {
            Write-Host "Pushed to GitHub."
        }
    }

    # Tauri v2 generates .exe + .exe.sig (not .nsis.zip)
    $exeFile = Get-ChildItem $BundleDir -Filter "*$newVersion*x64-setup.exe" | Where-Object { $_.Name -notlike "*.sig" } | Select-Object -First 1
    $sigFile = Get-ChildItem $BundleDir -Filter "*$newVersion*x64-setup.exe.sig" | Select-Object -First 1

    if ($exeFile -and $sigFile) {
        $sig     = (Get-Content $sigFile.FullName -Raw).Trim()
        $pubDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $dlBase  = "https://github.com/Irony-s9c/kagibox/releases/download/$tag"

        $latestObj = [ordered]@{
            version  = $newVersion
            notes    = "KagiBox $tag"
            pub_date = $pubDate
            platforms = [ordered]@{
                "windows-x86_64" = [ordered]@{
                    signature = $sig
                    url       = "$dlBase/$($exeFile.Name)"
                }
            }
        }
        $latestPath = Join-Path $Dest "latest.json"
        $utf8NoBom  = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($latestPath, ($latestObj | ConvertTo-Json -Depth 5), $utf8NoBom)
        Write-Host "latest.json created."

        # Delete existing release for this tag if any
        gh release delete $tag --yes --repo Irony-s9c/kagibox 2>$null; $true

        $uploadArgs = @(
            $exeFile.FullName,
            $sigFile.FullName,
            $latestPath
        )
        gh release create $tag --repo Irony-s9c/kagibox `
            --title "KagiBox $tag" `
            --generate-notes `
            @uploadArgs
        if ($LASTEXITCODE -eq 0) {
            Write-Host "GitHub release published: $tag"
        } else {
            Write-Host "[warn] GitHub release failed (exit $LASTEXITCODE)"
        }
    } else {
        Write-Host "[warn] Updater artifacts not found (*$newVersion*x64-setup.exe / .sig)"
        Write-Host "       Make sure tauri.conf.json has: createUpdaterArtifacts: true"
    }
}

# --- Clean up build cache ---
Write-Host ""
Write-Host "Cleaning build cache..."
$TargetBase = Join-Path $Root "target"
Remove-Item (Join-Path $TargetBase "debug")               -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\build")       -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\deps")        -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\examples")    -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\incremental") -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Done."

Write-Host ""
Write-Host "Log: $Log"
Write-Host ""

Stop-Transcript
