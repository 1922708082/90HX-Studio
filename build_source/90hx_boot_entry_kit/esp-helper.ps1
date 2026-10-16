# ESP file checker (mount-free) - ASCII only
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File esp-helper.ps1 list

param([string]$Action = 'find')

$espPart = Get-Partition -ErrorAction SilentlyContinue | Where-Object { $_.GptType -eq '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}' } | Select-Object -First 1
$vol = $null
if ($espPart) { $vol = ($espPart.AccessPaths | Where-Object { $_ -like '\\?\Volume*' } | Select-Object -First 1) }
if (-not $vol) {
    $wv = Get-CimInstance Win32_Volume -ErrorAction SilentlyContinue | Where-Object { $_.SystemVolume -eq $true } | Select-Object -First 1
    if ($wv) { $vol = $wv.DeviceID }
}
if (-not $vol) { Write-Host 'ESP-NOT-FOUND'; exit 2 }
$vol = $vol.TrimEnd('\') + '\'

if ($Action -eq 'list') {
    Write-Host ("       ESP volume: " + $vol)
    foreach ($f in @('NVPermissiveEFI.efi', 'shellx64.efi', 'startup.nsh')) {
        $fp = $vol + 'EFI\NVPermissive\' + $f
        if ([System.IO.File]::Exists($fp)) { Write-Host ("       FOUND   " + $fp) } else { Write-Host ("       MISSING " + $fp) }
    }
} else {
    Write-Host $vol
}
