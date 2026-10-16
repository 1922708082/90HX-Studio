# CMP 90HX Unlock boot entry installer v2 (mount-free)
# ASCII only. Launch via install-90hx-unlock-bootentry-v2.bat (as administrator).

$ErrorActionPreference = 'Continue'
$KIT = Split-Path -Parent $MyInvocation.MyCommand.Path
$LOG = Join-Path $KIT 'install_log2.txt'

function Log {
    param([string]$Msg)
    $line = '[' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '] ' + $Msg
    Add-Content -LiteralPath $LOG -Value $line
    Write-Host $Msg
}

Write-Host '============================================================'
Write-Host '  CMP 90HX Unlock Boot Entry Installer v2 (mount-free)'
Write-Host '  - no mountvol, no drive letter needed'
Write-Host '  - writes to ESP via its volume path directly'
Write-Host '============================================================'
Write-Host ''

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    Write-Host '[ERROR] Administrator rights required.'
    Write-Host '        Right-click install-90hx-unlock-bootentry-v2.bat - Run as administrator.'
    Log 'not admin, abort'
    exit 1
}
Log 'v2 install start (admin ok)'

foreach ($f in @('NVPermissiveEFI.efi', 'shellx64.efi', 'startup.nsh')) {
    $fp = Join-Path $KIT $f
    if (-not (Test-Path -LiteralPath $fp)) {
        Write-Host "[ERROR] missing file: $fp"
        Log "missing $f"
        exit 1
    }
}
Write-Host '[OK] kit files present'
Write-Host ''

$sb = $null
try { $sb = Confirm-SecureBootUEFI } catch { $sb = $null }
if ($sb -eq $true) {
    Write-Host '[WARN] Secure Boot is ON - unsigned EFI files will not run.'
    Write-Host '       Better disable Secure Boot in BIOS first.'
    $ans = Read-Host 'Continue anyway? (Y/N)'
    if ($ans -notmatch '^[Yy]') { Log 'user aborted (secure boot)'; exit 1 }
}

# ---- [1/8] BCD backup ----
$bakDir = Join-Path $KIT 'BCD_Backup'
if (-not (Test-Path -LiteralPath $bakDir)) { New-Item -ItemType Directory -Path $bakDir | Out-Null }
$bak = Join-Path $bakDir ('bcd_before_install_v2_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.bcd')
Write-Host "[1/8] Backing up BCD to: $bak"
$null = bcdedit /export "$bak"
if (-not (Test-Path -LiteralPath $bak)) {
    Write-Host '[ERROR] BCD backup failed - aborting (nothing changed).'
    Log 'bcd export failed, abort'
    exit 1
}
Log "bcd backup ok"

# ---- [2/8] locate ESP volume (mount-free) ----
Write-Host '[2/8] Locating ESP volume (no mountvol)...'
$espPart = Get-Partition | Where-Object { $_.GptType -eq '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}' } | Select-Object -First 1
$vol = $null
if ($espPart) { $vol = ($espPart.AccessPaths | Where-Object { $_ -like '\\?\Volume*' } | Select-Object -First 1) }
if (-not $vol) {
    $wv = Get-CimInstance Win32_Volume -ErrorAction SilentlyContinue | Where-Object { $_.SystemVolume -eq $true } | Select-Object -First 1
    if ($wv) { $vol = $wv.DeviceID }
}
if (-not $vol) {
    Write-Host '[ERROR] ESP volume not found.'
    Log 'ESP volume not found'
    exit 1
}
$vol = $vol.TrimEnd('\') + '\'
Write-Host "       ESP volume: $vol"
Log "esp volume: $vol"

$devStr = $null
$bm = bcdedit /enum '{bootmgr}' | Out-String
if ($bm -match '(?m)^\s*device\s+(partition=\S+)') { $devStr = $Matches[1] }
if (-not $devStr) {
    Write-Host '[ERROR] Could not read device string from {bootmgr} - aborting.'
    Log 'bootmgr device parse failed'
    exit 1
}
Write-Host "       bootmgr device: $devStr"
Log "device string: $devStr"

# ---- [3/8] copy files to ESP ----
Write-Host '[3/8] Copying files to ESP...'
$destDir = $vol + 'EFI\NVPermissive'
[System.IO.Directory]::CreateDirectory($destDir) | Out-Null
foreach ($f in @('NVPermissiveEFI.efi', 'shellx64.efi', 'startup.nsh')) {
    $src = Join-Path $KIT $f
    $dst = Join-Path $destDir $f
    [System.IO.File]::Copy($src, $dst, $true)
}
$ok = ([System.IO.File]::Exists((Join-Path $destDir 'NVPermissiveEFI.efi'))) -and ([System.IO.File]::Exists((Join-Path $destDir 'startup.nsh')))
if (-not $ok) {
    Write-Host '[ERROR] copy to ESP failed.'
    Log 'copy to ESP failed'
    exit 1
}
Write-Host "       deployed to $destDir"
Log "deployed to $destDir"

# ---- [4/8] create firmware boot entry ----
Write-Host '[4/8] Creating firmware boot entry (copy of {bootmgr})...'
$out = (bcdedit /copy '{bootmgr}' /d "CMP 90HX Unlock EFI Shell" | Out-String)
$guid = $null
if ($out -match '\{[0-9a-fA-F\-]{36}\}') { $guid = $Matches[0] }
if (-not $guid) {
    Write-Host '[ERROR] create boot entry failed.'
    Write-Host $out
    Log ('copy failed: ' + $out)
    exit 1
}
$null = bcdedit /set "$guid" device "$devStr"
$null = bcdedit /set "$guid" path '\EFI\NVPermissive\shellx64.efi'
$null = bcdedit /set '{fwbootmgr}' displayorder "$guid" '{bootmgr}'
Write-Host "       entry created: $guid"
Write-Host '       boot order: unlock entry 1st / Windows Boot Manager 2nd'
Log "entry created: $guid"

# ---- [5/8] remove older unlock entries (keep the new one) ----
$removed = 0
$fwTxt = bcdedit /enum '{fwbootmgr}' | Out-String
$cands = [regex]::Matches($fwTxt, '\{[0-9a-fA-F\-]{36}\}') | ForEach-Object { $_.Value } | Select-Object -Unique
foreach ($g in $cands) {
    if ($g -eq $guid) { continue }
    $d = bcdedit /enum "$g" | Out-String
    if ($d -match 'NVPermissive|Backup|90HX') {
        Write-Host "       removing old entry: $g"
        $null = bcdedit /delete "$g"
        $removed++
    }
}
Write-Host "[5/8] old unlock entries removed: $removed"
Log "old entries removed: $removed"

# ---- [6/8] optional: disable Windows boot menu ----
Write-Host ''
$ans = Read-Host '[6/8] Also disable Windows boot menu (displaybootmenu=no, timeout 0)? (Y/N)'
if ($ans -match '^[Yy]') {
    $null = bcdedit /set '{bootmgr}' displaybootmenu no
    $null = bcdedit /timeout 0
    Write-Host '       disabled (restore with: bcdedit /set {bootmgr} displaybootmenu yes)'
    Log 'bootmenu disabled'
}

# ---- [7/8] verify ----
Write-Host ''
Write-Host '[7/8] Verify - new boot entry:'
bcdedit /enum "$guid"
Write-Host '--- firmware boot order ---'
bcdedit /enum '{fwbootmgr}'

# ---- [8/8] done ----
Write-Host ''
Write-Host '============================================================'
Write-Host '  Install v2 finished. Boot flow after reboot:'
Write-Host '    firmware - EFI Shell - startup.nsh runs automatically'
Write-Host '    - NVPermissiveEFI.efi windows (unlock, takes a while)'
Write-Host '    - chain-loads Windows (no USB, no typing)'
Write-Host ''
Write-Host '  At boot, if you see "Press ESC in N seconds..."'
Write-Host '  DO NOT press ESC - wait, or press any other key.'
Write-Host ''
Write-Host '  If the auto script does not trigger, type manually:'
Write-Host '    NVPermissiveEFI.efi windows'
Write-Host '    bootmgfw.efi'
Write-Host '  or type exit to fall through to Windows.'
Write-Host '============================================================'
Log 'v2 install finished'
