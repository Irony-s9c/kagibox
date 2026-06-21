$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

$ghDefault = "C:\Program Files\GitHub CLI"
if ((Test-Path "$ghDefault\gh.exe") -and ($env:PATH -notlike "*GitHub CLI*")) {
    $env:PATH = "$ghDefault;$env:PATH"
}

$LogDir  = Join-Path $Root "build-logs"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
$LogDate = (Get-Date -Format "yyyyMMdd-HHmmss")
$Log     = Join-Path $LogDir "build-$LogDate.txt"

Start-Transcript -Path $Log -Force

$signingFile = Join-Path $Root "signing.ps1"
if (Test-Path $signingFile) { . $signingFile }

$confPath = Join-Path $Root "src-tauri\tauri.conf.json"
$conf = Get-Content $confPath -Raw | ConvertFrom-Json
$currentVersion = $conf.version

Write-Host "Current version: $currentVersion"
Stop-Transcript
$input = Read-Host "New version (Enter = keep $currentVersion)"
Start-Transcript -Path $Log -Append -Force

$newVersion = if ([string]::IsNullOrWhiteSpace($input)) { $currentVersion } else { $input.Trim() }

if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Invalid version: $newVersion"
    Stop-Transcript; Start-Process notepad $Log; exit 1
}

if ($newVersion -ne $currentVersion) {
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    $confRaw = [System.IO.File]::ReadAllText($confPath)
    $confRaw = $confRaw -replace '"version"\s*:\s*"[^"]*"', """version"": ""$newVersion"""
    [System.IO.File]::WriteAllText($confPath, $confRaw, $utf8NoBom)
    Write-Host "$currentVersion -> $newVersion"
}

$newLog = Join-Path $LogDir "build-v$newVersion-$LogDate.txt"
Stop-Transcript
Rename-Item -Path $Log -NewName (Split-Path $newLog -Leaf) -ErrorAction SilentlyContinue
$Log = $newLog
Start-Transcript -Path $Log -Append -Force

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "Node.js が見つかりません: https://nodejs.org"
    Stop-Transcript; Start-Process notepad $Log; exit 1
}
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Host "Rust が見つかりません: https://rustup.rs"
    Stop-Transcript; Start-Process notepad $Log; exit 1
}

Set-Location $Root
npm install
if ($LASTEXITCODE -ne 0) { Stop-Transcript; Start-Process notepad $Log; exit 1 }

Write-Host ""
Write-Host "ビルド中..."
npx tauri build
if ($LASTEXITCODE -ne 0) {
    Write-Host "ビルド失敗 (exit $LASTEXITCODE)"
    Stop-Transcript; Start-Process notepad $Log; exit 1
}

$BundleDir = Join-Path $Root "target\release\bundle\nsis"
$Dest      = Join-Path $Root "installer"
New-Item -ItemType Directory -Path $Dest -Force | Out-Null

Get-ChildItem $BundleDir -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName -Destination $Dest -Force
}

if (Get-Command gh -ErrorAction SilentlyContinue) {
    $tag     = "v$newVersion"
    $exeFile = Get-ChildItem $BundleDir -Filter "*$newVersion*x64-setup.exe" | Where-Object { $_.Name -notlike "*.sig" } | Select-Object -First 1
    $sigFile = Get-ChildItem $BundleDir -Filter "*$newVersion*x64-setup.exe.sig" | Select-Object -First 1

    if ($exeFile -and $sigFile) {
        if ($newVersion -ne $currentVersion) {
            git -C $Root add "src-tauri/tauri.conf.json"
            git -C $Root commit -m "v$newVersion"
            git -C $Root push
        }

        $sig     = (Get-Content $sigFile.FullName -Raw).Trim()
        $pubDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $dlBase  = "https://github.com/Irony-s9c/kagibox/releases/download/$tag"
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)

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
        [System.IO.File]::WriteAllText($latestPath, ($latestObj | ConvertTo-Json -Depth 5), $utf8NoBom)

        gh release delete $tag --yes --repo Irony-s9c/kagibox 2>$null; $true
        gh release create $tag --repo Irony-s9c/kagibox --title "KagiBox $tag" --generate-notes $exeFile.FullName $sigFile.FullName $latestPath
    }
}

$TargetBase = Join-Path $Root "target"
Remove-Item (Join-Path $TargetBase "debug")               -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\build")       -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\deps")        -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\examples")    -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $TargetBase "release\incremental") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "完了"
Stop-Transcript
