$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$log = 'D:\app\build_source\pack.log'
"=== pack start $(Get-Date -Format 'HH:mm:ss') ===" | Out-File $log -Encoding utf8

$framework = Join-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319' 'csc.exe'
$gacMsil = 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL'

$argsList = @(
    '/target:exe',
    '/optimize+',
    '/out:D:\app\build_source\Packager.exe',
    '/r:System.dll',
    '/r:System.Core.dll',
    ('/r:' + $gacMsil + '\System.IO.Compression\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.dll'),
    ('/r:' + $gacMsil + '\System.IO.Compression.FileSystem\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.FileSystem.dll'),
    'D:\app\build_source\Packager.cs'
)

$out = & $framework $argsList 2>&1
$out | Out-File $log -Append -Encoding utf8
if ($LASTEXITCODE -ne 0) {
    "[ERROR] Packager compile failed exit=$LASTEXITCODE" | Out-File $log -Append -Encoding utf8
    exit 1
}
"[OK] compiled, running packager ..." | Out-File $log -Append -Encoding utf8

$runOut = & 'D:\app\build_source\Packager.exe' 2>&1
$runOut | Out-File $log -Append -Encoding utf8
"pack exit=$LASTEXITCODE" | Out-File $log -Append -Encoding utf8
