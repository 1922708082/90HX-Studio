$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$framework = Join-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319' 'csc.exe'
$gacMsil = 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL'
$gac64 = 'C:\Windows\Microsoft.NET\assembly\GAC_64'

$outExe = 'D:\app\build_source\RenderAllTabsV2.exe'

$argsList = @(
    '/target:exe',
    '/optimize+',
    ('/out:' + $outExe),
    '/r:System.dll',
    '/r:System.Core.dll',
    ('/r:' + $gacMsil + '\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll'),
    ('/r:' + $gac64 + '\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll'),
    ('/r:' + $gacMsil + '\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll'),
    ('/r:' + $gacMsil + '\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll'),
    'D:\app\build_source\RenderAllTabsV2.cs'
)

& $framework $argsList
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] 渲染器编译失败！" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] 渲染器编译成功，开始渲染..."
& $outExe
Write-Host "渲染完成 exit=$LASTEXITCODE"
