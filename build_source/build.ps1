$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$gacPath = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
$gac64Path = "C:\Windows\Microsoft.NET\assembly\GAC_64"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "HX家族多功能工具箱 v6.2 绿色免安装版构建流程" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 先把 web/ 下的模块化源文件（css/ + pages/ + js/）合并成单文件 index.html，
# 再交给编译器内嵌。改了任何 css/pages/js 都必须经过这一步。
$mergeScript = "D:\app\build_source\scripts\merge-web.ps1"
if (Test-Path $mergeScript) {
    & $mergeScript
    if (-not $?) { Write-Host "[ERROR] 前端合并失败" -ForegroundColor Red; exit 1 }
} else {
    Write-Host "[WARN] 未找到 merge-web.ps1，跳过合并" -ForegroundColor Yellow
}

$targetExe = "D:\app\90HX Studio.exe"
Write-Host "正在编译免安装可执行程序: $targetExe ..." -ForegroundColor Yellow

$argsList = @(
    "/target:winexe",
    "/optimize+",
    "/warn:4",
    "/warnaserror+",
    "/win32manifest:D:\app\build_source\app.manifest",
    "/win32icon:D:\app\app.ico",
    "/resource:D:\app\build_source\src\MainWindow.xaml,MainWindow.xaml",
    "/resource:D:\app\build_source\src\SplashWindow.xaml,SplashWindow.xaml",
    "/resource:D:\app\build_source\assets\wechat_pay.png,wechat_pay.png",
    "/resource:D:\app\build_source\assets\alipay.png,alipay.png",
    "/resource:D:\app\build_source\web\index.html,index.html",
    "/resource:D:\app\build_source\lib\webview2\Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.Core.dll",
    "/resource:D:\app\build_source\lib\webview2\Microsoft.Web.WebView2.Wpf.dll,Microsoft.Web.WebView2.Wpf.dll",
    "/resource:D:\app\build_source\lib\webview2\x64\WebView2Loader.dll,wv2loader64.dll",
    "/resource:D:\app\build_source\lib\webview2\x86\WebView2Loader.dll,wv2loader32.dll",
    "/resource:D:\app\assets.zip,assets.zip",
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
    "/r:D:\app\build_source\lib\webview2\Microsoft.Web.WebView2.Core.dll",
    "/r:D:\app\build_source\lib\webview2\Microsoft.Web.WebView2.Wpf.dll",
    "D:\app\build_source\src\SystemDetector.cs",
    "D:\app\build_source\src\GpuHardwareManager.cs",
    "D:\app\build_source\src\PcieUnlockManager.cs",
    "D:\app\build_source\src\GpuSpoofManager.cs",
    "D:\app\build_source\src\EcoPowerManager.cs",
    "D:\app\build_source\src\EcosystemPatchManager.cs",
    "D:\app\build_source\src\NativeCore.cs",
    "D:\app\build_source\src\VirusOverlay.cs",
    "D:\app\build_source\src\NvmlFan.cs",
    "D:\app\build_source\src\PowerPresets.cs",
    "D:\app\build_source\src\HardwareBridge.cs",
    "D:\app\build_source\src\WebShell.cs",
    "D:\app\build_source\src\App.cs",
    "D:\app\build_source\src\AssemblyInfo.cs"
)
& $csc $argsList
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] 主程序编译失败！" -ForegroundColor Red
    exit 1
}

Write-Host "==================================================" -ForegroundColor Green
Write-Host "[OK] 构建成功！正在同步二进制文件..." -ForegroundColor Green
Copy-Item $targetExe "D:\app\HX家族多功能工具箱.exe" -Force
Write-Host "[OK] 已同步至: D:\app\HX家族多功能工具箱.exe" -ForegroundColor Green

$desktopDir = "C:\Users\19227\Desktop"
if (Test-Path $desktopDir) {
    Copy-Item $targetExe "$desktopDir\90HX Studio.exe" -Force
    Copy-Item $targetExe "$desktopDir\HX家族多功能工具箱.exe" -Force
    Write-Host "[OK] 已成功同步至桌面: $desktopDir\90HX Studio.exe 与 HX家族多功能工具箱.exe" -ForegroundColor Green
}

Write-Host "==================================================" -ForegroundColor Green
Get-Item -LiteralPath $targetExe | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

