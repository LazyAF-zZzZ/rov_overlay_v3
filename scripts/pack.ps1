# Builds an installable ROV Overlay Tool and, optionally, publishes it as the update
# feed users receive.
#
#   .\scripts\pack.ps1 -Version 3.0.0
#   .\scripts\pack.ps1 -Version 3.0.1-beta.1 -Channel beta
#   .\scripts\pack.ps1 -Version 3.0.1 -Publish          # uploads to GitHub Releases
#
# What ends up in the bundle: the WPF app (self-contained, so no .NET to install), the
# Node backend with only its production packages, and node.exe itself. Nothing on the
# machine that runs this script is required on the machine that installs it.
#
# Everything is staged into a fresh folder rather than packed from the repo, because the
# repo's backend/node_modules carries development packages, and public/images holds
# whatever teams the builder happened to upload while testing. Those are the builder's
# own data and must never be shipped: see EXCLUDED below and the guard in
# backend/tests/media.test.ts.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [ValidateSet('stable', 'beta')][string]$Channel = 'stable',
    [switch]$Publish,
    [string]$RepoUrl = 'https://github.com/LazyAF-zZzZ/rov_overlay_v3'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $root 'publish'
$app = Join-Path $stage 'app'
$releases = Join-Path $root 'releases'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$vpk = Join-Path $env:USERPROFILE '.dotnet\tools\vpk.exe'
$nodeExe = (Get-Command node).Source

# vpk's own name for the default channel is 'win'; 'beta' is ours.
$vpkChannel = if ($Channel -eq 'beta') { 'beta' } else { 'win' }

# Images the operator uploaded. They live in the same folders as the app's own art, so
# they are removed by name after the copy rather than filtered during it.
$uploadDirs = @('team-logos', 'skins')

# The HTML operator pages v2 shipped. Every one has a native screen now, so shipping them
# would hand users a second way to drive the app that nobody maintains.
#
# The overlays OBS loads are NOT here and must never be: overlay, overlay-1440, result,
# overlay-prev, overlay-standings, overlay-matchup, overlay-team-drafts, overlay-team-card, overlay-teams,
# overlay-analytics.
#
# Two more pages stay on purpose, because nothing replaced them:
#   sfx-test.html  a sound check, a troubleshooting tool rather than an operator screen.
#   guide.html     the manual. The native Guide screen has a button that opens it in a
#                  browser, which is how it gets read on a second monitor while the app
#                  itself is showing the control panel.
# Guarded by backend/tests/packaging.test.ts.
$operatorPages = @(
    'home.html', 'control.html', 'teams.html', 'team.html', 'tournament.html',
    'tournament-drafts.html', 'bracket.html', 'analytics.html', 'design.html',
    'hotkeys.html'
)

Write-Host "ROV Overlay Tool $Version ($Channel)" -ForegroundColor Cyan

# --- 1. A clean stage -------------------------------------------------------------
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $app -Force | Out-Null

# --- 2. The desktop app -----------------------------------------------------------
Write-Host '  building the app…'
& $dotnet publish (Join-Path $root 'desktop\RovOverlay.Desktop\RovOverlay.Desktop.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:PublishSingleFile=false `
    -o $app | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# --- 3. The backend ---------------------------------------------------------------
# TypeScript is compiled from the repo, then only the output is copied.
Write-Host '  building the backend…'
Push-Location (Join-Path $root 'backend')
try {
    & npm run build | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'npm run build failed' }
}
finally { Pop-Location }

$backend = Join-Path $app 'backend'
New-Item -ItemType Directory -Path $backend -Force | Out-Null
foreach ($item in @('build', 'public', 'server.js', 'package.json', 'package-lock.json')) {
    Copy-Item (Join-Path $root "backend\$item") -Destination $backend -Recurse -Force
}

# The operator's own uploads, stripped after the copy. An installer that shipped these
# would hand every user the logos of whichever teams the builder was testing with.
foreach ($dir in $uploadDirs) {
    $path = Join-Path $backend "public\images\$dir"
    if (Test-Path $path) {
        Get-ChildItem $path -File | Where-Object { $_.Name -ne 'README.md' } | Remove-Item -Force
    }
}

# The replaced operator pages. Their routes stay and answer with a line saying where the
# screen went, so an old bookmark explains itself instead of failing.
foreach ($page in $operatorPages) {
    $path = Join-Path $backend "public\$page"
    if (Test-Path $path) { Remove-Item $path -Force }
}

# Production packages only: a clean install from the lockfile, not a copy of the repo's
# node_modules, which still carries v2's Electron packages.
Write-Host '  installing production packages…'
Push-Location $backend
try {
    & npm ci --omit=dev --no-audit --no-fund | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
}
finally { Pop-Location }
Remove-Item (Join-Path $backend 'package-lock.json') -Force

# --- 4. Node itself ---------------------------------------------------------------
# The backend is what serves the overlays to OBS, so the user cannot be asked to install
# Node first. BackendHost looks here before it looks at PATH.
Write-Host "  bundling node ($nodeExe)…"
New-Item -ItemType Directory -Path (Join-Path $backend 'runtime') -Force | Out-Null
Copy-Item $nodeExe -Destination (Join-Path $backend 'runtime\node.exe') -Force

# --- 5. Pack ----------------------------------------------------------------------
# Release notes appear in the installer and on the Releases page. A version without a
# notes file still packs; it just ships without them.
$notesArgs = @()
$notesFile = Join-Path $root "docs\release-notes\$Version.md"
if (Test-Path $notesFile) {
    $notesArgs = @('--releaseNotes', $notesFile)
    Write-Host "  notes: docs\release-notes\$Version.md"
}
else {
    Write-Host "  no release notes at docs\release-notes\$Version.md" -ForegroundColor Yellow
}

Write-Host '  packing…'
& $vpk pack `
    --packId RovOverlayTool3 `
    --packVersion $Version `
    --packDir $app `
    --packTitle 'ROV Overlay Tool' `
    --packAuthors 'LazyAF' `
    --mainExe RovOverlayTool.exe `
    --icon (Join-Path $root 'backend\public\images\app-icon.ico') `
    --instLicense (Join-Path $root 'LICENSE.md') `
    --channel $vpkChannel `
    --outputDir $releases @notesArgs
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

Write-Host "  done: $releases" -ForegroundColor Green

# --- 6. Publish -------------------------------------------------------------------
# Uploading is the moment users start receiving this build, so it is never automatic.
if ($Publish) {
    Write-Host '  uploading to GitHub Releases…'
    & $vpk upload github `
        --repoUrl $RepoUrl `
        --channel $vpkChannel `
        --outputDir $releases `
        --tag "v$Version" `
        --releaseName "ROV Overlay Tool $Version" `
        --publish true `
        --token $env:GITHUB_TOKEN
    if ($LASTEXITCODE -ne 0) { throw 'vpk upload failed' }
    Write-Host '  published.' -ForegroundColor Green
}
else {
    Write-Host "  not published. Add -Publish when this build is meant to reach users." -ForegroundColor Yellow
}
