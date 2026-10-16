[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "显卡型号深度修改工具 (CMP 90HX -> RTX 3080)"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "       显卡型号深度修改工具 (CMP 90HX -> RTX 3080)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[错误] 请以管理员身份运行此脚本！" -ForegroundColor Red
    pause
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# 1. 备份原始注册表配置
Write-Host "[1/7] 正在备份当前显卡配置..." -ForegroundColor Cyan
$classPath = "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001"
$enumPath = "HKLM\SYSTEM\CurrentControlSet\Enum\PCI\VEN_10DE&DEV_220D&SUBSYS_155510DE&REV_A1\4&1babdf5b&0&0009"
if (-not (Test-Path "$scriptDir\backup_class0001.reg")) {
    & reg export "$classPath" "$scriptDir\backup_class0001.reg" /y | Out-Null
}
if (-not (Test-Path "$scriptDir\backup_enum.reg")) {
    & reg export "$enumPath" "$scriptDir\backup_enum.reg" /y | Out-Null
}
Write-Host "    备份文件已就绪。" -ForegroundColor Green

# 2. 修改 Class 显卡类信息 (任务管理器、DxDiag、DirectX)
Write-Host "[2/7] 正在修改系统显卡驱动类信息 (任务管理器与DxDiag)..." -ForegroundColor Cyan
$classKey = "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001"
Set-ItemProperty -Path $classKey -Name "DriverDesc" -Value "NVIDIA GeForce RTX 3080"
Set-ItemProperty -Path $classKey -Name "HardwareInformation.AdapterString" -Value "NVIDIA GeForce RTX 3080"
Set-ItemProperty -Path $classKey -Name "HardwareInformation.ChipType" -Value "NVIDIA GeForce RTX 3080"

# 3. 修改 Video 服务项与 Enum 设备项 (设备管理器与系统属性)
Write-Host "[3/7] 正在修改设备管理器友好名称与硬件属性..." -ForegroundColor Cyan
$videoPaths = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Control\Video" -Recurse -ErrorAction SilentlyContinue | Where-Object {
    (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).Driver -eq "{4d36e968-e325-11ce-bfc1-08002be10318}\0001"
}
foreach ($vp in $videoPaths) {
    Set-ItemProperty -Path $vp.PSPath -Name "DeviceDesc" -Value "NVIDIA GeForce RTX 3080"
}
$enumKey = "HKLM:\SYSTEM\CurrentControlSet\Enum\PCI\VEN_10DE&DEV_220D&SUBSYS_155510DE&REV_A1\4&1babdf5b&0&0009"
if (Test-Path $enumKey) {
    Set-ItemProperty -Path $enumKey -Name "FriendlyName" -Value "NVIDIA GeForce RTX 3080"
    Set-ItemProperty -Path $enumKey -Name "DeviceDesc" -Value "NVIDIA GeForce RTX 3080"
}

# 4. 修改 DirectX 适配器描述与系统图形设置缓存
Write-Host "[4/7] 正在修改 DirectX 缓存与 Windows 高性能图形设置..." -ForegroundColor Cyan
Get-ChildItem "HKLM:\SOFTWARE\Microsoft\DirectX" -ErrorAction SilentlyContinue | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
    if ($p.Description -like "*CMP 90HX*" -or $p.DeviceId -eq 8717) {
        Set-ItemProperty -Path $_.PSPath -Name "Description" -Value "NVIDIA GeForce RTX 3080"
        Write-Host "    已更新 DirectX 适配器: $($_.PSChildName)" -ForegroundColor Green
    }
}

# 5. 修改 nvlddmkm 服务 Video 键
Write-Host "[5/7] 正在更新显卡驱动底层服务配置..." -ForegroundColor Cyan
$nvVideoKey = "HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkm\Video"
if (Test-Path $nvVideoKey) {
    Set-ItemProperty -Path $nvVideoKey -Name "DeviceDesc" -Value "NVIDIA GeForce RTX 3080"
}

# 6. 同步更新 INF 驱动定义 (系统 INF 与 DriverStore)
Write-Host "[6/7] 正在同步系统与驱动存储区中的 INF 文件..." -ForegroundColor Cyan
$infPath = "C:\Windows\INF\oem39.inf"
if (Test-Path $infPath) {
    $content = [System.IO.File]::ReadAllText($infPath, [System.Text.Encoding]::Default)
    if ($content -match 'NVIDIA CMP 90HX \(RainCandy Technology\)') {
        $content = $content.Replace('NVIDIA CMP 90HX (RainCandy Technology)', 'NVIDIA GeForce RTX 3080')
        [System.IO.File]::WriteAllText($infPath, $content, [System.Text.Encoding]::Default)
    }
}

$driverStoreInf = "C:\Windows\System32\DriverStore\FileRepository\nvac.inf_amd64_786a7910b35feb43\nvac.inf"
if (Test-Path $driverStoreInf) {
    takeown /f "$driverStoreInf" | Out-Null
    icacls "$driverStoreInf" /grant "Administrators:F" /c | Out-Null
    $infContent = [System.IO.File]::ReadAllText($driverStoreInf, [System.Text.Encoding]::Default)
    if ($infContent -match 'NVIDIA CMP 90HX \(RainCandy Technology\)') {
        $infContent = $infContent.Replace('NVIDIA CMP 90HX (RainCandy Technology)', 'NVIDIA GeForce RTX 3080')
        [System.IO.File]::WriteAllText($driverStoreInf, $infContent, [System.Text.Encoding]::Default)
    }
}

# 7. 更新 NVIDIA App 本地缓存
Write-Host "[7/7] 正在同步更新 NVIDIA App 本地配置缓存..." -ForegroundColor Cyan
$userDirs = Get-ChildItem "C:\Users" -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
foreach ($u in $userDirs) {
    $shim = "$u\AppData\Local\NVIDIA Corporation\NVIDIA app\NvBackend\SHIM.json"
    if (Test-Path $shim) {
        $raw = [System.IO.File]::ReadAllText($shim, [System.Text.Encoding]::UTF8)
        $raw = $raw.Replace('"LongName":"NVIDIA CMP 90HX"', '"LongName":"NVIDIA GeForce RTX 3080"')
        $raw = $raw.Replace('"Description":"NVIDIA CMP 90HX (RainCandy Technology)"', '"Description":"NVIDIA GeForce RTX 3080"')
        $raw = $raw.Replace('NVIDIA CMP 90HX', 'NVIDIA GeForce RTX 3080')
        [System.IO.File]::WriteAllText($shim, $raw, [System.Text.Encoding]::UTF8)
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " [√] 显卡型号已全部修改同步完成！" -ForegroundColor Green
Write-Host " 目标型号: NVIDIA GeForce RTX 3080" -ForegroundColor Yellow
Write-Host ""
Write-Host " 生效说明:" -ForegroundColor Cyan
Write-Host " 1. 设备管理器 / 任务管理器已实时更新。"
Write-Host " 2. Windows 图形高性能设置需在重启电脑后重新加载显示。"
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""