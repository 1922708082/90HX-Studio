param([switch]$SkipContractAudit)
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$gacPath = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
$gac64Path = "C:\Windows\Microsoft.NET\assembly\GAC_64"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

# 路径全部相对脚本自身推导，不写死盘符——否则别人克隆到别的盘就编译不了
$srcRoot  = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = Split-Path -Parent $srcRoot

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "HX家族多功能工具箱 v6.2 绿色免安装版构建流程" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 先把 web/ 下的模块化源文件（css/ + pages/ + js/）合并成单文件 index.html，
# 再交给编译器内嵌。改了任何 css/pages/js 都必须经过这一步。
$mergeScript = "$srcRoot\scripts\merge-web.ps1"
if (Test-Path $mergeScript) {
    & $mergeScript
    if (-not $?) { Write-Host "[ERROR] 前端合并失败" -ForegroundColor Red; exit 1 }
} else {
    Write-Host "[ERROR] 未找到 scripts/merge-web.ps1，无法合并前端，构建中止（继续会内嵌上一次的 index.html）" -ForegroundColor Red
    exit 1
}

# 契约门禁：比对「前端发的/等的」与「后端实现的/发的」
# 历史上多次出现「页面等某个事件、宿主从来不推」导致整页空白，
# 这条检查让它在构建时就暴露，而不是等用户点进去发现。
$auditScript = "$srcRoot\scripts\audit_contract.py"
# 解释器路径不能写死到某台机器的用户目录：换机/换人后 Test-Path 直接 false，
# 整段检查会静默跳过，门禁等于不存在。按 环境变量 → 本机默认 → PATH 依次查找。
$pyExe = $null
foreach ($c in @($env:PYTHON, (Join-Path $env:USERPROFILE ".workbuddy\binaries\python\versions\3.13.12\python.exe"))) {
    if ($c -and (Test-Path $c)) { $pyExe = $c; break }
}
if (-not $pyExe) {
    $pc = Get-Command python -ErrorAction SilentlyContinue
    if ($pc) { $pyExe = $pc.Source }
}
if (-not $pyExe) {
    if ($SkipContractAudit) {
        Write-Host "==================================================" -ForegroundColor Yellow
        Write-Host "[SKIP] 已指定 -SkipContractAudit，契约检查跳过（本次构建不校验前后端契约）" -ForegroundColor Yellow
        Write-Host "==================================================" -ForegroundColor Yellow
    } else {
        # 不能静默降级：没有解释器就等于门禁不存在，
        # 而「页面等一个永远不发的事件」正是历史上最难排查的一类。
        Write-Host "==================================================" -ForegroundColor Red
        Write-Host "[ERROR] 未找到 Python，契约检查无法执行，构建中止" -ForegroundColor Red
        Write-Host "        安装 Python 或设置 PYTHON 环境变量；" -ForegroundColor Red
        Write-Host "        确实需要跳过请显式加 -SkipContractAudit 参数。" -ForegroundColor Red
        Write-Host "==================================================" -ForegroundColor Red
        exit 1
    }
}
if ($pyExe -and -not (Test-Path $auditScript)) {
    Write-Host "[WARN] 未找到 scripts/audit_contract.py，契约检查跳过" -ForegroundColor Yellow
}
if ($pyExe -and (Test-Path $auditScript)) {
    $auditOut = & $pyExe $auditScript 2>&1
    # 审计输出按小节切片。
    # 不能直接用 Select-String "    - "：那是全文匹配，会把 [3] 的行也列进来
    # （[3] 目前是识别器的假阳性，混进来会让「缺 4 个动作」看起来像「缺 13 个」）。
    $lines2 = @($auditOut | ForEach-Object { "$_" })
    function Get-SectionItems([string]$startTag, [string]$endTag) {
        $i1 = -1; $i2 = $lines2.Count
        for ($i = 0; $i -lt $lines2.Count; $i++) {
            if ($i1 -lt 0 -and $lines2[$i] -match $startTag) { $i1 = $i; continue }
            if ($i1 -ge 0 -and $lines2[$i] -match $endTag) { $i2 = $i; break }
        }
        if ($i1 -lt 0) { return @() }
        return @($lines2[($i1 + 1)..([Math]::Max($i1 + 1, $i2 - 1))] | Where-Object { $_ -match '    - ' })
    }

    # [1] 前端会发、后端未实现：只告警（这类可能是排期中，硬拦会让构建永远失败）
    $missing = ($lines2 | Where-Object { $_ -match '^\[1\] 前端会发' } | Select-Object -First 1)
    if ($missing -and $missing -notmatch ": 0$") {
        Write-Host "[契约告警] 有动作前端会发但后端未实现（点了会报 NOT_SUPPORTED）：" -ForegroundColor Yellow
        Get-SectionItems '^\[1\]' '^\[2\]' | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
    }

    $waiting = ($lines2 | Where-Object { $_ -match '^\[2\] 前端在等' } | Select-Object -First 1)
    if ($waiting -and $waiting -notmatch ": 0$") {
        Write-Host "--------------------------------------------------" -ForegroundColor Red
        Write-Host "[契约错误] 有页面在等待宿主从不发出的事件，构建中止：" -ForegroundColor Red
        Get-SectionItems '^\[2\]' '^\[3\]' | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Write-Host "--------------------------------------------------" -ForegroundColor Red
        exit 1
    }
}

$targetExe = "$repoRoot\90HX Studio.exe"
Write-Host "正在编译免安装可执行程序: $targetExe ..." -ForegroundColor Yellow

$argsList = @(
    "/target:winexe",
    "/optimize+",
    "/warn:4",
    "/warnaserror+",
    "/win32manifest:$srcRoot\app.manifest",
    "/win32icon:$repoRoot\app.ico",
    "/resource:$srcRoot\src\MainWindow.xaml,MainWindow.xaml",
    "/resource:$srcRoot\src\SplashWindow.xaml,SplashWindow.xaml",
    "/resource:$srcRoot\assets\wechat_pay.png,wechat_pay.png",
    "/resource:$srcRoot\assets\alipay.png,alipay.png",
    "/resource:$srcRoot\web\index.html,index.html",
    "/resource:$srcRoot\lib\webview2\Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.Core.dll",
    "/resource:$srcRoot\lib\webview2\Microsoft.Web.WebView2.Wpf.dll,Microsoft.Web.WebView2.Wpf.dll",
    "/resource:$srcRoot\lib\webview2\x64\WebView2Loader.dll,wv2loader64.dll",
    "/resource:$srcRoot\lib\webview2\x86\WebView2Loader.dll,wv2loader32.dll",
    "/resource:$repoRoot\assets.zip,assets.zip",
    ("/out:" + $targetExe),
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/r:System.IO.Compression.dll",
    "/r:System.Management.dll",
    ("/r:" + $gacPath + "\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll"),
    ("/r:" + $gac64Path + "\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll"),
    ("/r:" + $gacPath + "\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll"),
    ("/r:" + $gacPath + "\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"),
    ("/r:" + $gacPath + "\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll"),
    ("/r:" + $gacPath + "\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll"),
    "/r:$srcRoot\lib\webview2\Microsoft.Web.WebView2.Core.dll",
    "/r:$srcRoot\lib\webview2\Microsoft.Web.WebView2.Wpf.dll",
    "$srcRoot\src\SystemDetector.cs",
    "$srcRoot\src\GpuHardwareManager.cs",
    "$srcRoot\src\PcieUnlockManager.cs",
    "$srcRoot\src\GpuSpoofManager.cs",
    "$srcRoot\src\EcoPowerManager.cs",
    "$srcRoot\src\EcosystemPatchManager.cs",
    "$srcRoot\src\NativeCore.cs",
    "$srcRoot\src\VirusOverlay.cs",
    "$srcRoot\src\AppSettings.cs",
    "$srcRoot\src\SpoofCatalog.cs",
    "$srcRoot\src\Diagnostics.cs",
    "$srcRoot\src\GameScanner.cs",
    "$srcRoot\src\DlssPack.cs",
    "$srcRoot\src\UpdateChecker.cs",
    "$srcRoot\src\UpdateInstaller.cs",
    "$srcRoot\src\NvmlFan.cs",
    "$srcRoot\src\PowerPresets.cs",
    "$srcRoot\src\HardwareBridge.cs",
    "$srcRoot\src\WebShell.cs",
    "$srcRoot\src\App.cs",
    "$srcRoot\src\AssemblyInfo.cs"
)
& $csc $argsList
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] 主程序编译失败！" -ForegroundColor Red
    exit 1
}

Write-Host "==================================================" -ForegroundColor Green
Write-Host "[OK] 构建成功！正在同步二进制文件..." -ForegroundColor Green
Copy-Item $targetExe "$repoRoot\HX家族多功能工具箱.exe" -Force
Write-Host "[OK] 已同步至: $repoRoot\HX家族多功能工具箱.exe" -ForegroundColor Green

$desktopDir = "C:\Users\19227\Desktop"
if (Test-Path $desktopDir) {
    Copy-Item $targetExe "$desktopDir\90HX Studio.exe" -Force
    Copy-Item $targetExe "$desktopDir\HX家族多功能工具箱.exe" -Force
    Write-Host "[OK] 已成功同步至桌面: $desktopDir\90HX Studio.exe 与 HX家族多功能工具箱.exe" -ForegroundColor Green
}

Write-Host "==================================================" -ForegroundColor Green
Get-Item -LiteralPath $targetExe | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

