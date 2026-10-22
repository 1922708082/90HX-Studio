# 把 web/ 下的模块化源文件合并成可运行的单文件 index.html
# 由 build.ps1 在编译前调用；也可单独运行用于本地预览。

$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path -Parent $PSScriptRoot) 'web'

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

# 构建戳：便于确认运行时用到的前端是哪一次合并出来的
$stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
$out = $out.Replace('{{BUILD_STAMP}}', $stamp)
if ($out -notmatch 'hx-build') {
    $out = $out.Replace('<head>', '<head>' + "`r`n  <meta name=""hx-build"" content=""" + $stamp + """ />")
}

# 原子写入：先写 .tmp 再 Move 覆盖。
# 直接 WriteAllText 时若进程持有文件或中途失败，会留下半截 index.html，
# 而它恰好就是要内嵌进 exe 的那份。
$outPath = Join-Path $root 'index.html'
$tmpPath = $outPath + '.tmp'
[System.IO.File]::WriteAllText($tmpPath, $out, (New-Object System.Text.UTF8Encoding($false)))
if (Test-Path $outPath) { [System.IO.File]::Delete($outPath) }
[System.IO.File]::Move($tmpPath, $outPath)

$lines = ($out -split "`n").Count
Write-Host ("[WEB] 已合并 index.html  {0} 行 / {1:N0} 字节" -f $lines, (Get-Item $outPath).Length)
