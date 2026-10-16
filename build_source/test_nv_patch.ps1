$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$csc = Join-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319' 'csc.exe'
$gacMsil = 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL'
$gac64 = 'C:\Windows\Microsoft.NET\assembly\GAC_64'

$argsList = @(
    '/target:exe',
    '/optimize+',
    '/out:D:\app\build_source\NvPatchTest.exe',
    '/r:System.dll',
    '/r:System.Core.dll',
    ('/r:' + $gacMsil + '\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll'),
    ('/r:' + $gac64 + '\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll'),
    ('/r:' + $gacMsil + '\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll'),
    ('/r:' + $gacMsil + '\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll'),
    ('/r:' + $gacMsil + '\System.IO.Compression\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.dll'),
    ('/r:' + $gacMsil + '\System.IO.Compression.FileSystem\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.FileSystem.dll'),
    'D:\app\build_source\src\SystemDetector.cs',
    'D:\app\build_source\src\NativeCore.cs',
    'D:\app\build_source\NvPatchTest.cs'
)

$out = & $csc $argsList 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "=== COMPILE ERROR ===" -ForegroundColor Red
    $out
    exit 1
}
Write-Host "[OK] compiled, running..."
& 'D:\app\build_source\NvPatchTest.exe'
