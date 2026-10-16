$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$gacPath = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
$gac64Path = "C:\Windows\Microsoft.NET\assembly\GAC_64"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "90HX Studio 绿色免安装版构建流程" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$targetExe = "D:\app\90HX Studio.exe"
Write-Host "正在编译免安装可执行程序: $targetExe ..." -ForegroundColor Yellow

$argsList = @(
    "/target:winexe",
    "/optimize+",
    "/win32manifest:D:\app\build_source\app.manifest",
    "/win32icon:D:\app\app.ico",
    "/resource:D:\app\build_source\src\MainWindow.xaml,MainWindow.xaml",
    "/resource:D:\app\build_source\src\SplashWindow.xaml,SplashWindow.xaml",
    "/resource:D:\app\build_source\assets\wechat_pay.png,wechat_pay.png",
    "/resource:D:\app\build_source\assets\alipay.png,alipay.png",
    "/resource:D:\app\assets.zip,assets.zip",
    ("/out:" + $targetExe),
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/r:System.IO.Compression.dll",
    ("/r:" + $gacPath + "\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll"),
    ("/r:" + $gac64Path + "\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll"),
    ("/r:" + $gacPath + "\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll"),
    ("/r:" + $gacPath + "\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"),
    ("/r:" + $gacPath + "\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll"),
    ("/r:" + $gacPath + "\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll"),
    "D:\app\build_source\src\SystemDetector.cs",
    "D:\app\build_source\src\NativeCore.cs",
    "D:\app\build_source\src\VirusOverlay.cs",
    "D:\app\build_source\src\App.cs",
    "D:\app\build_source\src\AssemblyInfo.cs"
)
& $csc $argsList
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] 主程序编译失败！" -ForegroundColor Red
    exit 1
}

Write-Host "==================================================" -ForegroundColor Green
Write-Host "[OK] 构建成功！双击即可直接使用：90HX Studio.exe" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Get-Item -LiteralPath $targetExe | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
