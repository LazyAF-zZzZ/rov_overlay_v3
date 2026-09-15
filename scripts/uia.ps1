# Drives one running RovOverlayTool process through UI Automation, the way a person would:
# finds a control by the text on it and presses it. Never touches any other process.
#
#   -Action click           press the button showing -Text, in the main window (not in a dialog)
#   -Action click-dialog    press the button showing -Text, inside an open dialog
#   -Action capture         PrintWindow the main window to -Out (PNG)
#   -Action capture-dialog  PrintWindow the open dialog to -Out (PNG)
#   -Action windows         list this process's windows, dialogs included
#
# A window with an Owner is not a child of the desktop in the UI Automation tree: it is
# listed underneath its owner. So dialogs are looked for inside the main window too.
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$Action,
    [string]$Text,
    [string]$Out,
    [int]$TimeoutSeconds = 8
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class RovShot {
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static void Save(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    using (var bmp = new Bitmap(r.R - r.L, r.B - r.T)) {
      using (var g = Graphics.FromImage(bmp)) {
        var hdc = g.GetHdc();
        PrintWindow(h, hdc, 2);   // PW_RENDERFULLCONTENT, or WPF draws black
        g.ReleaseHdc(hdc);
      }
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
}
'@

$AE = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
$root = $AE::RootElement
$pidCondition = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $ProcessId)
$windowCondition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)

function Get-TopWindows {
    @($root.FindAll($Scope::Children, $pidCondition))
}

function Get-MainWindow {
    Get-TopWindows | Where-Object { $_.Current.Name -eq 'ROV Overlay Tool' } | Select-Object -First 1
}

function Get-Dialogs {
    $found = @()
    $main = Get-MainWindow
    if ($main) { $found += @($main.FindAll($Scope::Descendants, $windowCondition)) }
    $found += @(Get-TopWindows | Where-Object { $_.Current.Name -ne 'ROV Overlay Tool' })
    $found
}

# Not "$scope": PowerShell variable names ignore case, and a parameter called $scope hides
# the script's $Scope (the TreeScope type) inside this function, turning Descendants into null.
function Find-Named($within, [string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    $within.FindFirst($Scope::Descendants, $condition)
}

# The text found may be the button itself (string content) or a TextBlock inside it.
function Invoke-Around($element) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current = $element
    while ($null -ne $current -and $current.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) {
        $current = $walker.GetParent($current)
    }
    if ($null -eq $current) { throw "no button around '$($element.Current.Name)'" }
    if (-not $current.Current.IsEnabled) { throw "button '$($element.Current.Name)' is disabled" }
    $current.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Wait-For([scriptblock]$find, [string]$what) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $found = & $find
        if ($null -ne $found) { return $found }
        Start-Sleep -Milliseconds 200
    }
    throw "timed out looking for $what"
}

switch ($Action) {
    'windows' {
        Get-TopWindows | ForEach-Object { "top:    '{0}' hwnd {1}" -f $_.Current.Name, $_.Current.NativeWindowHandle }
        Get-Dialogs | ForEach-Object { "dialog: '{0}' hwnd {1}" -f $_.Current.Name, $_.Current.NativeWindowHandle }
    }
    'click' {
        # Search the main window but skip anything inside a dialog, which may carry the same text.
        $element = Wait-For {
            $main = Get-MainWindow
            if (-not $main) { return $null }
            $condition = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Text)
            foreach ($hit in @($main.FindAll($Scope::Descendants, $condition))) {
                $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
                $up = $walker.GetParent($hit)
                $insideDialog = $false
                while ($null -ne $up -and -not [System.Windows.Automation.Automation]::Compare($up, $main)) {
                    if ($up.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window) { $insideDialog = $true; break }
                    $up = $walker.GetParent($up)
                }
                if (-not $insideDialog) { return $hit }
            }
            return $null
        } "'$Text' in the main window"
        Invoke-Around $element
        "clicked '$Text'"
    }
    'click-dialog' {
        $element = Wait-For {
            foreach ($dialog in Get-Dialogs) {
                $hit = Find-Named $dialog $Text
                if ($hit) { return $hit }
            }
            return $null
        } "'$Text' in a dialog"
        Invoke-Around $element
        "clicked '$Text' in a dialog"
    }
    'capture' {
        $main = Wait-For { Get-MainWindow } 'the main window'
        [RovShot]::Save([IntPtr]$main.Current.NativeWindowHandle, $Out)
        "captured $Out"
    }
    'capture-dialog' {
        $dialog = Wait-For { Get-Dialogs | Select-Object -First 1 } 'a dialog'
        [RovShot]::Save([IntPtr]$dialog.Current.NativeWindowHandle, $Out)
        "captured dialog $Out"
    }
    'has-text' {
        # Any element in the main window whose name (the text a TextBlock shows) contains -Text.
        $null = Wait-For {
            $main = Get-MainWindow
            if (-not $main) { return $null }
            foreach ($hit in @($main.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition))) {
                if ($hit.Current.Name -like "*$Text*") { return $hit }
            }
            return $null
        } "text containing '$Text'"
        "found text containing '$Text'"
    }
    'has-value' {
        # A text field in the main window whose current text contains -Text.
        $editCondition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
        $null = Wait-For {
            $main = Get-MainWindow
            if (-not $main) { return $null }
            foreach ($field in @($main.FindAll($Scope::Descendants, $editCondition))) {
                try {
                    $value = $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
                    if ($value -like "*$Text*") { return $field }
                } catch { }
            }
            return $null
        } "a field containing '$Text'"
        "found a field containing '$Text'"
    }
    default { throw "unknown action $Action" }
}
