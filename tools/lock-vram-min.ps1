# ============================================================
#  把显存频率锁到本卡支持的最低档（并保持住，不随负载浮动）
#
#  用法：
#    双击「锁定显存最低频.bat」            -> 锁到最低档
#    双击「解锁显存频率.bat」              -> 解除锁定
#    powershell -File lock-vram-min.ps1 -Status    -> 只看当前状态
#    powershell -File lock-vram-min.ps1 -DryRun    -> 只显示会做什么，不改
#
#  原理：nvidia-smi -lmc <min>,<max> 是把显存时钟钉死在区间内，
#        两个值给同一个数 = 完全锁死，不会自己升频。
#        这跟「设功耗墙」不一样：功耗墙只是限制上限，负载一高还是会冲上去。
#
#  注意：
#   · 必须管理员权限，脚本会自己弹 UAC
#   · 重启后失效，需要重新运行（驱动不会记住）
#   · 锁显存不影响核心频率；核心要锁另外用 nvidia-smi -lgc
# ============================================================

param(
    [switch]$Reset,
    [switch]$ResetCore,
    [switch]$NoPause,
    [switch]$Status,
    [switch]$DryRun,
    [int]$MemClock = 0
)

$ErrorActionPreference = 'Stop'

function Write-Line($text, $color = 'Gray') { Write-Host $text -ForegroundColor $color }

# ---------- 找 nvidia-smi ----------
$smi = $null
$candidates = @(
    (Join-Path $env:SystemRoot 'System32\nvidia-smi.exe'),
    (Join-Path $env:ProgramFiles 'NVIDIA Corporation\NVSMI\nvidia-smi.exe')
)
foreach ($c in $candidates) { if (Test-Path $c) { $smi = $c; break } }
if (-not $smi) {
    $cmd = Get-Command nvidia-smi -ErrorAction SilentlyContinue
    if ($cmd) { $smi = $cmd.Source }
}
if (-not $smi) {
    Write-Line "[X] 找不到 nvidia-smi.exe，请先装 NVIDIA 驱动" 'Red'
    if (-not $DryRun) { if (-not $NoPause) { Read-Host "按回车退出" | Out-Null } }
    exit 1
}

# ---------- 读本卡支持的最低显存档位 ----------
function Get-MinMemClock {
    $out = & $smi -q -d SUPPORTED_CLOCKS 2>$null
    $vals = @()
    foreach ($line in $out) {
        $m = [regex]::Match($line, '^\s*Memory\s*:\s*(\d+)\s*MHz')
        if ($m.Success) { $vals += [int]$m.Groups[1].Value }
    }
    if ($vals.Count -eq 0) { return 405 }   # 兜底：Turing 常见最低档
    return ($vals | Measure-Object -Minimum).Minimum
}

function Get-CurrentMemClock {
    $v = & $smi --query-gpu=clocks.mem --format=csv,noheader,nounits 2>$null | Select-Object -First 1
    if ($v) { return [int]($v.Trim()) }
    return -1
}

$minMem = if ($MemClock -gt 0) { $MemClock } else { Get-MinMemClock }
$curMem = Get-CurrentMemClock

# ---------- 状态 ----------
if ($Status) {
    Write-Line "==================================================" 'Cyan'
    & $smi --query-gpu=index,name,clocks.mem,clocks.gr,pstate --format=csv
    Write-Line "本卡支持的最低显存档位: $minMem MHz" 'Yellow'
    Write-Line "==================================================" 'Cyan'
    if (-not $DryRun) { if (-not $NoPause) { Read-Host "按回车退出" | Out-Null } }
    exit 0
}

# ---------- 试运行：不改任何东西 ----------
if ($DryRun) {
    Write-Line "=========== 试运行（不会改动任何设置）===========" 'Yellow'
    Write-Line ("当前显存频率 : {0} MHz" -f $(if ($curMem -ge 0) { $curMem } else { '未知' }))
    Write-Line ("将锁定到     : {0} MHz（最低档，min=max）" -f $minMem)
    Write-Line ("对应命令     : nvidia-smi -lmc {0},{0}" -f $minMem)
    Write-Line "================================================" 'Yellow'
    exit 0
}

# ---------- 需要管理员：没有就自己拉起一个提权的自己 ----------
$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Line "需要管理员权限，正在申请提权（会弹 UAC，请点「是」）..." 'Yellow'
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($Reset) { $argList += '-Reset' }
    if ($ResetCore) { $argList += '-ResetCore' }
    if ($MemClock -gt 0) { $argList += @('-MemClock', "$MemClock") }
    try {
        Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs | Out-Null
    } catch {
        Write-Line "[X] 提权被取消，未做任何改动" 'Red'
        if (-not $NoPause) { Read-Host "按回车退出" | Out-Null }
    }
    exit
}

Write-Line "==================================================" 'Cyan'

# ---------- 只解除核心锁（显存锁保持）----------
if ($ResetCore) {
    Write-Line "正在解除核心频率锁（-rgc），显存锁不受影响..." 'Yellow'
    $rc = & $smi -rgc 2>&1
    Write-Line ($rc -join "`n")
    Start-Sleep -Milliseconds 800
    $st = & $smi --query-gpu=clocks.mem,clocks.gr --format=csv,noheader
    Write-Line ("当前 显存/核心: $st") 'Green'
    Write-Line "==================================================" 'Cyan'
    if (-not $NoPause) { Read-Host "按回车退出" | Out-Null }
    exit 0
}

# ---------- 解除锁定 ----------
if ($Reset) {
    Write-Line "正在解除显存频率锁定..." 'Yellow'
    $r = & $smi -rmc 2>&1
    Write-Line ($r -join "`n")
    Start-Sleep -Milliseconds 800
    $after = Get-CurrentMemClock
    Write-Line ("解除完成，当前显存频率: {0} MHz（会随负载自动变化）" -f $after) 'Green'
    Write-Line "==================================================" 'Cyan'
    if (-not $NoPause) { Read-Host "按回车退出" | Out-Null }
    exit 0
}

# ---------- 锁定 ----------
$grBefore = (& $smi --query-gpu=clocks.gr --format=csv,noheader,nounits 2>$null | Select-Object -First 1)
Write-Line ("锁定前  显存: {0} MHz   核心: {1} MHz" -f $curMem, $grBefore) 'Gray'
Write-Line "本脚本只锁显存，核心频率不加锁（核心会照常跟着负载升降）" 'DarkGray' 
Write-Line ("正在锁定到 {0} MHz（min=max，频率不会自己升上去）..." -f $minMem) 'Yellow'

$result = & $smi -lmc "$minMem,$minMem" 2>&1
Write-Line ($result -join "`n")

Start-Sleep -Milliseconds 1000
$after = Get-CurrentMemClock

if ($after -gt 0 -and [Math]::Abs($after - $minMem) -le 2) {
    Write-Line ("[OK] 已锁在 {0} MHz" -f $after) 'Green'
    # 跑一下让时钟有机会上升，再看一次是否仍是锁死值
    Start-Sleep -Milliseconds 1500
    $again = Get-CurrentMemClock
    if ($again -gt 0 -and [Math]::Abs($again - $minMem) -le 2) {
        Write-Line ("[OK] 复检仍是 {0} MHz，说明确实被钉住了" -f $again) 'Green'
    } else {
        Write-Line ("[!] 复检变成 {0} MHz，可能未完全锁死" -f $again) 'Yellow'
    }
} else {
    Write-Line ("[!] 当前读到 {0} MHz，与目标 {1} MHz 不一致" -f $after, $minMem) 'Yellow'
    Write-Line "    常见原因：驱动不支持锁显存 / 该卡被厂商限制 / 有其他工具（Afterburner）在接管" 'Yellow'
    Write-Line "    原始输出：" 'Gray'
    Write-Line ($result -join "`n") 'DarkGray'
}

Write-Line "==================================================" 'Cyan'
Write-Line "重启后锁定会失效，需要重新运行本脚本。" 'Gray'
if (-not $NoPause) { Read-Host "按回车退出" | Out-Null }
