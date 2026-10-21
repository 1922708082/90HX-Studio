# 把 web/ 下的模块化源文件合并成可运行的单文件 index.html
# 由 build.ps1 在编译前调用；也可单独运行用于本地预览。

$ErrorActionPreference = 'Stop'
$root = 'D:\app\build_source\web'

$cssOrder   = @('01-base.css', '02-layout.css', '03-components.css', '04-pages.css', '05-overrides.css')
$pageOrder  = @('device', 'unlock', 'spoof', 'power', 'dlss', 'overlay', 'dualgpu', 'maintenance', 'logs', 'update')

$tpl = Get-Content (Join-Path $root 'index.template.html') -Raw -Encoding UTF8

$cssParts = @()
foreach ($f in $cssOrder) {
    $p = Join-Path $root ('css\' + $f)
    if (-not (Test-Path $p)) { throw "缺少样式文件: $p" }
    $cssParts += ("/* =====================  $f  ===================== */")
    $cssParts += (Get-Content $p -Raw -Encoding UTF8)
}

$pageParts = @()
foreach ($n in $pageOrder) {
    $p = Join-Path $root ('pages\' + $n + '.html')
    if (-not (Test-Path $p)) { throw "缺少页面文件: $p" }
    $pageParts += (Get-Content $p -Raw -Encoding UTF8)
}

$jsPath = Join-Path $root 'js\app.js'
if (-not (Test-Path $jsPath)) { throw "缺少脚本: $jsPath" }
$jsText = Get-Content $jsPath -Raw -Encoding UTF8

$out = $tpl.Replace('{{CSS}}', ($cssParts -join "`r`n"))
$out = $out.Replace('{{PAGES}}', ($pageParts -join "`r`n"))
$out = $out.Replace('{{JS}}', $jsText)

$outPath = Join-Path $root 'index.html'
[System.IO.File]::WriteAllText($outPath, $out, (New-Object System.Text.UTF8Encoding($false)))

$lines = ($out -split "`n").Count
Write-Host ("[WEB] 已合并 index.html  {0} 行 / {1:N0} 字节" -f $lines, (Get-Item $outPath).Length)
