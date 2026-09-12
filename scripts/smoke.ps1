# Does the app actually open a window?
#
# This exists because 3.0.0 shipped without doing so. Every check up to then rendered a
# screen with --snapshot, and --snapshot deliberately skips the first-run licence, so the
# one path a new user takes was the one path never run. The app installed, started, and
# sat there: no window, no error, a healthy-looking process.
#
#   .\scripts\smoke.ps1                       # the development build
#   .\scripts\smoke.ps1 -Installed            # the installed copy
#   .\scripts\smoke.ps1 -FreshLicence         # as a new user sees it, licence and all
#
# It fails loudly if no visible window appears, and says which windows it did find.

[CmdletBinding()]
param(
    [string]$Exe,
    [switch]$Installed,
    [switch]$FreshLicence,
    [int]$TimeoutSeconds = 40
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not $Exe) {
    $Exe = if ($Installed) {
        Join-Path $env:LOCALAPPDATA 'RovOverlayTool3\current\RovOverlayTool.exe'
    }
    else {
        Join-Path $root 'desktop\RovOverlay.Desktop\bin\Debug\net10.0-windows\RovOverlayTool.exe'
    }
}
if (-not (Test-Path $Exe)) { throw "not built: $Exe" }

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class RovSmokeProbe {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  // Visible, and big enough to be the real window rather than one of WPF's hidden
  // helpers - or a message box. "This app is already open" is a visible window too, and
  // a smoke test that accepts one passes while the app is broken.
  public static string Visible(uint target) {
    var sb = new StringBuilder();
    EnumWindows(delegate(IntPtr h, IntPtr p) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == target && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        int w = r.R - r.L, ht = r.B - r.T;
        if (w >= 800 && ht >= 500) {
          var t = new StringBuilder(256); GetWindowText(h, t, 256);
          sb.AppendLine(string.Format("{0}x{1} at ({2},{3}) '{4}'", w, ht, r.L, r.T, t.ToString()));
        }
      }
      return true;
    }, IntPtr.Zero);
    return sb.ToString();
  }
}
'@

# One app per machine (the single-instance mutex), so a copy already running would make
# the new one show "already open" and exit - which is not what this is testing.
$already = Get-Process RovOverlayTool -ErrorAction SilentlyContinue
if ($already) {
    throw "ROV Overlay Tool is already running (pid $($already.Id -join ', ')). Close it first."
}

$settings = Join-Path $env:APPDATA 'RovOverlayTool3\settings.json'
$backup = $null
if ($FreshLicence -and (Test-Path $settings)) {
    # Keep the operator's real preferences; put them back in the finally block.
    $backup = Get-Content $settings -Raw
    Remove-Item $settings -Force
    Write-Host '  settings set aside so the licence is asked for'
}

$errorLog = Join-Path $env:APPDATA 'RovOverlayTool3\startup-error.log'
if (Test-Path $errorLog) { Remove-Item $errorLog -Force }

Write-Host "smoke: $Exe"
$app = Start-Process -FilePath $Exe -PassThru
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$found = ''

try {
    while ((Get-Date) -lt $deadline) {
        if ($app.HasExited) { throw "the app exited on its own, code $($app.ExitCode)" }
        $found = [RovSmokeProbe]::Visible([uint32]$app.Id)
        if ($found) { break }
        Start-Sleep -Milliseconds 500
    }

    if (-not $found) {
        Write-Host '  FAILED: no visible window' -ForegroundColor Red
        if (Test-Path $errorLog) { Write-Host (Get-Content $errorLog -Raw) }
        else { Write-Host '  and nothing was logged, so it is hanging rather than failing' }
        throw 'no window appeared'
    }

    Write-Host '  windows:' -ForegroundColor Green
    $found.TrimEnd() -split "`r?`n" | ForEach-Object { Write-Host "    $_" }
    if (Test-Path $errorLog) {
        Write-Host '  but a startup error was logged:' -ForegroundColor Yellow
        Write-Host (Get-Content $errorLog -Raw)
        throw 'startup logged an error'
    }
    Write-Host '  ok' -ForegroundColor Green
}
finally {
    if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    if ($backup) { Set-Content -Path $settings -Value $backup -Encoding utf8; Write-Host '  settings restored' }
}
