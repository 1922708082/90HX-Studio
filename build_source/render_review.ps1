# UI Review headless renderer - all 9 pages @ 1080x760 (ASCII-only script)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$xamlPath = 'D:\app\build_source\src\MainWindow.xaml'
$outDir = 'D:\app\build_source\ui_review'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$content = [System.IO.File]::ReadAllText($xamlPath, [System.Text.Encoding]::UTF8)
$window = [System.Windows.Markup.XamlReader]::Parse($content)
$root = $window.Content

# ---- demo data (ASCII only) ----
$x = $window.FindName('TxtTopGpuModel'); if ($x) { $x.Text = 'NVIDIA CMP 90HX' }
$x = $window.FindName('TxtTopPcieStatus'); if ($x) { $x.Text = 'PCIe Gen2 x16 (Full)' }
$x = $window.FindName('TxtTopOverlayStatus'); if ($x) { $x.Text = 'Overlay Ready' }
$x = $window.FindName('TxtTopSensors'); if ($x) { $x.Text = '48 C / 82.9 W' }
$x = $window.FindName('TxtMiningGpuSummary'); if ($x) { $x.Text = '1x CMP 90HX (GA102)' }
$x = $window.FindName('LstMiningGpus'); if ($x) { [void]$x.Items.Add('[Slot 1] CMP 90HX - RTX 3080 [READY]') }
$x = $window.FindName('CmbUsbDrives'); if ($x) { [void]$x.Items.Add('E: (KINGSTON - FAT32)'); $x.SelectedIndex = 0 }
$x = $window.FindName('TxtLogBox')
if ($x) { $x.Text = "[22:15:01] 90HX Studio ready.`r`n[22:15:02] CMP 90HX detected (10de:220d), PCIe Gen2 x16.`r`n[22:15:03] USB drive found: E: (KINGSTON - FAT32).`r`n[22:15:05] assets.zip embedded OK.`r`n" }

$pages = @('PageHardware','PageSpoof','PageOverlay','PageMatrix','PageDualGpu','PageDlss','PagePower','PageLogs','PageSponsor')
$navs  = @('NavItemHardware','NavItemSpoof','NavItemOverlay','NavItemMatrix','NavItemDualGpu','NavItemDlss','NavItemPower','NavItemLogs','NavItemSponsor')

$width = 1200; $height = 760
for ($p = 0; $p -lt $pages.Length; $p++) {
    for ($i = 0; $i -lt $pages.Length; $i++) {
        $pg = $window.FindName($pages[$i])
        if ($pg -ne $null) { $pg.Visibility = $(if ($i -eq $p) {'Visible'} else {'Collapsed'}) }
        $nv = $window.FindName($navs[$i])
        if ($nv -ne $null) { $nv.IsChecked = ($i -eq $p) }
    }
    $root.Width = $width; $root.Height = $height
    $root.Measure((New-Object System.Windows.Size($width, $height)))
    $root.Arrange((New-Object System.Windows.Rect(0, 0, $width, $height)))
    $root.UpdateLayout()
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($root)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $fs = [System.IO.File]::Create((Join-Path $outDir ('page{0}.png' -f $p)))
    $enc.Save($fs); $fs.Close()
    Write-Host ('[OK] page{0}.png' -f $p)
}
Write-Host 'ALL RENDERS DONE'
