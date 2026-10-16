$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$csc = Join-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319' 'csc.exe'
$gacMsil = 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL'
$gac64 = 'C:\Windows\Microsoft.NET\assembly\GAC_64'

$outExe = 'D:\app\build_source\RenderSplash.exe'

$argsList = @(
    '/target:exe',
    '/optimize+',
    ('/out:' + $outExe),
    '/r:System.dll',
    ('/r:' + $gacMsil + '\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll'),
    ('/r:' + $gac64 + '\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll'),
    ('/r:' + $gacMsil + '\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll'),
    ('/r:' + $gacMsil + '\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll'),
    'D:\app\build_source\RenderSplash.cs'
)

$out = & $csc $argsList 2>&1
$out | Out-File -FilePath 'D:\app\build_source\compile_log.txt' -Encoding utf8
if ($LASTEXITCODE -ne 0) {
    exit 1
}

& $outExe
