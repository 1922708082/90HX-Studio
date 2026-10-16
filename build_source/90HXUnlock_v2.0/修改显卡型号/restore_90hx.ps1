[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "恢复显卡原始型号 (还原为 CMP 90HX)"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "       恢复显卡原始型号 (还原为 CMP 90HX)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[错误] 请以管理员身份运行此脚本！" -ForegroundColor Red
    pause
    exit 1
}

# 1. 还原 Class 显卡类信息
Write-Host "[1/6] 正在还原显卡类驱动信息..." -ForegroundColor Cyan
$classKey = "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001"
Set-ItemProperty -Path $classKey -Name "DriverDesc" -Value "NVIDIA CMP 90HX (RainCandy Technology)"
Set-ItemProperty -Path $classKey -Name "HardwareInformation.AdapterString" -Value "NVIDIA CMP 90HX"
Set-ItemProperty -Path $classKey -Name "HardwareInformation.ChipType" -Value "NVIDIA CMP 90HX"

# 2. 还原 Video 与 Enum 设备项
Write-Host "[2/6] 正在还原设备管理器与硬件属性..." -ForegroundColor Cyan
$videoPaths = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Control\Video" -Recurse -ErrorAction SilentlyContinue | Where-Object {
    (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).Driver -eq "{4d36e968-e325-11ce-bfc1-08002be10318}\0001"
}
foreach ($vp in $videoPaths) {
    Set-ItemProperty -Path $vp.PSPath -Name "DeviceDesc" -Value "@oem39.inf,%nvidia_dev.220d%;NVIDIA CMP 90HX (RainCandy Technology)"
}
$enumKey = "HKLM:\SYSTEM\CurrentControlSet\Enum\PCI\VEN_10DE&DEV_220D&SUBSYS_155510DE&REV_A1\4&1babdf5b&0&0009"
if (Test-Path $enumKey) {
    Remove-ItemProperty -Path $enumKey -Name "FriendlyName" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $enumKey -Name "DeviceDesc" -Value "@oem39.inf,%nvidia_dev.220d%;NVIDIA CMP 90HX (RainCandy Technology)"
}

# 3. 还原 DirectX 适配器描述
Write-Host "[3/6] 正在还原 DirectX 缓存描述..." -ForegroundColor Cyan
Get-ChildItem "HKLM:\SOFTWARE\Microsoft\DirectX" -ErrorAction SilentlyContinue | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
    if ($p.DeviceId -eq 8717) {
        Set-ItemProperty -Path $_.PSPath -Name "Description" -Value "NVIDIA CMP 90HX (RainCandy Technology)"
    }
}

# 4. 还原 nvlddmkm 服务 Video
Write-Host "[4/6] 正在还原底层驱动服务配置..." -ForegroundColor Cyan
$nvVideoKey = "HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkm\Video"
if (Test-Path $nvVideoKey) {
    Set-ItemProperty -Path $nvVideoKey -Name "DeviceDesc" -Value "@oem39.inf,%nvidia_dev.220d%;NVIDIA CMP 90HX (RainCandy Technology)"
}

# 5. 还原 INF 驱动文件
Write-Host "[5/6] 正在还原驱动 INF 文件..." -ForegroundColor Cyan
$infPath = "C:\Windows\INF\oem39.inf"
if (Test-Path $infPath) {
    $content = [System.IO.File]::ReadAllText($infPath, [System.Text.Encoding]::Default)
    if ($content -match 'NVIDIA GeForce RTX 3080') {
        $content = $content.Replace('NVIDIA GeForce RTX 3080', 'NVIDIA CMP 90HX (RainCandy Technology)')
        [System.IO.File]::WriteAllText($infPath, $content, [System.Text.Encoding]::Default)
    }
}
$driverStoreInf = "C:\Windows\System32\DriverStore\FileRepository\nvac.inf_amd64_786a7910b35feb43\nvac.inf"
if (Test-Path $driverStoreInf) {
    takeown /f "$driverStoreInf" | Out-Null
    icacls "$driverStoreInf" /grant "Administrators:F" /c | Out-Null
    $infContent = [System.IO.File]::ReadAllText($driverStoreInf, [System.Text.Encoding]::Default)
    if ($infContent -match 'NVIDIA GeForce RTX 3080') {
        $infContent = $infContent.Replace('NVIDIA GeForce RTX 3080', 'NVIDIA CMP 90HX (RainCandy Technology)')
        [System.IO.File]::WriteAllText($driverStoreInf, $infContent, [System.Text.Encoding]::Default)
    }
}

# 6. 还原 NVIDIA App 缓存
Write-Host "[6/6] 正在还原 NVIDIA App 本地配置缓存..." -ForegroundColor Cyan
$userDirs = Get-ChildItem "C:\Users" -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
foreach ($u in $userDirs) {
    $shim = "$u\AppData\Local\NVIDIA Corporation\NVIDIA app\NvBackend\SHIM.json"
    if (Test-Path $shim) {
        $raw = [System.IO.File]::ReadAllText($shim, [System.Text.Encoding]::UTF8)
        $raw = $raw.Replace('"LongName":"NVIDIA GeForce RTX 3080"', '"LongName":"NVIDIA CMP 90HX"')
        $raw = $raw.Replace('"Description":"NVIDIA GeForce RTX 3080"', '"Description":"NVIDIA CMP 90HX (RainCandy Technology)"')
        [System.IO.File]::WriteAllText($shim, $raw, [System.Text.Encoding]::UTF8)
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " [√] 显卡已成功完全还原为原始出厂设置！" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""