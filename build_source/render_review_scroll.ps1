# Scroll-render PageMatrix lower part to verify channel card selected state (ASCII-only)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$xamlPath = 'D:\app\build_source\src\MainWindow.xaml'
$outDir = 'D:\app\build_source\ui_review'
$content = [System.IO.File]::ReadAllText($xamlPath, [System.Text.Encoding]::UTF8)
$window = [System.Windows.Markup.XamlReader]::Parse($content)
$root = $window.Content

$x = $window.FindName('TxtMiningGpuSummary'); if ($x) { $x.Text = '1x CMP 90HX (GA102)' }
$x = $window.FindName('LstMiningGpus'); if ($x) { [void]$x.Items.Add('[Slot 1] CMP 90HX - RTX 3080 [READY]') }

$pages = @('PageHardware','PageSpoof','PageOverlay','PageMatrix','PageDualGpu','PageDlss','PagePower','PageLogs','PageSponsor')
$navs  = @('NavItemHardware','NavItemSpoof','NavItemOverlay','NavItemMatrix','NavItemDualGpu','NavItemDlss','NavItemPower','NavItemLogs','NavItemSponsor')
for ($i = 0; $i -lt $pages.Length; $i++) {
    $pg = $window.FindName($pages[$i])
    if ($pg -ne $null) { $pg.Visibility = $(if ($i -eq 3) {'Visible'} else {'Collapsed'}) }
    $nv = $window.FindName($navs[$i])
    if ($nv -ne $null) { $nv.IsChecked = ($i -eq 3) }
}

$width = 1200; $height = 760
$root.Width = $width; $root.Height = $height
$root.Measure((New-Object System.Windows.Size($width, $height)))
$root.Arrange((New-Object System.Windows.Rect(0, 0, $width, $height)))
$root.UpdateLayout()

# find the ScrollViewer inside PageMatrix
$pg = $window.FindName('PageMatrix')
$sv = $null
$n = [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($pg)
for ($i = 0; $i -lt $n; $i++) {
    $c = [System.Windows.Media.VisualTreeHelper]::GetChild($pg, $i)
    if ($c -is [System.Windows.Controls.ScrollViewer]) { $sv = $c; break }
}

if ($sv -ne $null) {
    $sv.ScrollToVerticalOffset(760)
    $root.UpdateLayout()
    $sv.UpdateLayout()
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($root)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $fs = [System.IO.File]::Create((Join-Path $outDir 'page3b_matrix_scrolled.png'))
    $enc.Save($fs); $fs.Close()
    Write-Host 'OK page3b_matrix_scrolled.png (offset=760)'
    # second scroll deeper
    $sv.ScrollToVerticalOffset(1500)
    $root.UpdateLayout()
    $sv.UpdateLayout()
    $rtb2 = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb2.Render($root)
    $enc2 = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc2.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb2))
    $fs2 = [System.IO.File]::Create((Join-Path $outDir 'page3c_matrix_bottom.png'))
    $enc2.Save($fs2); $fs2.Close()
    Write-Host 'OK page3c_matrix_bottom.png (offset=1500)'
} else {
    Write-Host 'ERROR: ScrollViewer not found'
}
