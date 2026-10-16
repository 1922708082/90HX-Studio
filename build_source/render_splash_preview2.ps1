# Splash transparent preview renderer (simulated desktop + glass card)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$W = 1280; $H = 720
$root = New-Object System.Windows.Controls.Grid

# wallpaper gradient
$bg = New-Object System.Windows.Controls.Border
$lb = New-Object System.Windows.Media.LinearGradientBrush
$lb.StartPoint = [System.Windows.Point]::new(0, 0)
$lb.EndPoint = [System.Windows.Point]::new(1, 1)
$lb.GradientStops.Add([System.Windows.Media.GradientStop]::new([System.Windows.Media.Color]::FromRgb(0x2E, 0x3D, 0x6B), 0))
$lb.GradientStops.Add([System.Windows.Media.GradientStop]::new([System.Windows.Media.Color]::FromRgb(0x0E, 0x14, 0x28), 1))
$bg.Background = $lb
[void]$root.Children.Add($bg)

# wallpaper deco circles for depth
$c1 = New-Object System.Windows.Shapes.Ellipse
$c1.Width = 560; $c1.Height = 560
$c1.Fill = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(0x30, 0x4D, 0x8B, 0xFF))
$c1.HorizontalAlignment = 'Right'; $c1.VerticalAlignment = 'Top'
$c1.Margin = [System.Windows.Thickness]::new(0, -180, -140, 0)
[void]$root.Children.Add($c1)

# fake desktop icons
$iconNames = @('This PC', 'GPU-Z', 'Files', 'Notes', 'Studio', 'Tools')
for ($i = 0; $i -lt 6; $i++) {
    $sp = New-Object System.Windows.Controls.StackPanel
    $sp.HorizontalAlignment = 'Left'; $sp.VerticalAlignment = 'Top'
    $sp.Margin = [System.Windows.Thickness]::new(36, (30 + $i * 100), 0, 0)
    $b = New-Object System.Windows.Controls.Border
    $b.Width = 46; $b.Height = 46
    $b.CornerRadius = [System.Windows.CornerRadius]::new(11)
    $b.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(0x58, 0xFF, 0xFF, 0xFF))
    $b.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(0x30, 0xFF, 0xFF, 0xFF))
    $b.BorderThickness = [System.Windows.Thickness]::new(1)
    $b.HorizontalAlignment = 'Center'
    $t = New-Object System.Windows.Controls.TextBlock
    $t.Text = $iconNames[$i]; $t.FontSize = 10
    $t.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(0xD0, 0xFF, 0xFF, 0xFF))
    $t.HorizontalAlignment = 'Center'; $t.Margin = [System.Windows.Thickness]::new(0, 6, 0, 0)
    [void]$sp.Children.Add($b); [void]$sp.Children.Add($t)
    [void]$root.Children.Add($sp)
}

# ---- glass card ----
$card = New-Object System.Windows.Controls.Border
$card.Width = 460; $card.Height = 330
$card.CornerRadius = [System.Windows.CornerRadius]::new(26)
$card.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(0xDE, 0x0B, 0x0C, 0x0F))
$card.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(0x1F, 0xFF, 0xFF, 0xFF))
$card.BorderThickness = [System.Windows.Thickness]::new(1)
$card.HorizontalAlignment = 'Center'; $card.VerticalAlignment = 'Center'
$sh = New-Object System.Windows.Media.Effects.DropShadowEffect
$sh.Color = [System.Windows.Media.Colors]::Black; $sh.BlurRadius = 46; $sh.ShadowDepth = 0; $sh.Opacity = 0.6
$card.Effect = $sh

$cardGrid = New-Object System.Windows.Controls.Grid

$panel = New-Object System.Windows.Controls.StackPanel
$panel.HorizontalAlignment = 'Center'; $panel.VerticalAlignment = 'Center'

$num = New-Object System.Windows.Controls.TextBlock
$num.Text = '90'; $num.FontSize = 96
$num.FontWeight = [System.Windows.FontWeights]::ExtraBold
$num.FontFamily = New-Object System.Windows.Media.FontFamily('Segoe UI, Microsoft YaHei UI')
$num.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0xDF, 0xF3, 0xFF))
$num.HorizontalAlignment = 'Center'
$glow = New-Object System.Windows.Media.Effects.DropShadowEffect
$glow.Color = [System.Windows.Media.Color]::FromRgb(0x4F, 0xC3, 0xF7); $glow.BlurRadius = 36; $glow.ShadowDepth = 0; $glow.Opacity = 0.9
$num.Effect = $glow
[void]$panel.Children.Add($num)

$barTrack = New-Object System.Windows.Controls.Border
$barTrack.Width = 156; $barTrack.Height = 4
$barTrack.CornerRadius = [System.Windows.CornerRadius]::new(2)
$barTrack.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x22, 0x24, 0x2B))
$barTrack.HorizontalAlignment = 'Center'
$barTrack.Margin = [System.Windows.Thickness]::new(0, 30, 0, 0)
$barFill = New-Object System.Windows.Controls.Border
$barFill.Width = 156; $barFill.Height = 4
$barFill.CornerRadius = [System.Windows.CornerRadius]::new(2)
$barFill.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x4F, 0xC3, 0xF7))
$barTrack.Child = $barFill
[void]$panel.Children.Add($barTrack)

$sfx = New-Object System.Windows.Controls.TextBlock
$sfx.Text = 'HX  ·  STUDIO'; $sfx.FontSize = 16
$sfx.FontWeight = [System.Windows.FontWeights]::SemiBold
$sfx.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x4F, 0xC3, 0xF7))
$sfx.HorizontalAlignment = 'Center'; $sfx.Margin = [System.Windows.Thickness]::new(0, 22, 0, 0)
[void]$panel.Children.Add($sfx)

$foot = New-Object System.Windows.Controls.TextBlock
$foot.Text = 'GA102 旗舰解锁控制台'
$foot.FontSize = 11.5
$foot.FontFamily = New-Object System.Windows.Media.FontFamily('Microsoft YaHei UI, Microsoft YaHei')
$foot.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x8A, 0x8E, 0x98))
$foot.HorizontalAlignment = 'Center'; $foot.Margin = [System.Windows.Thickness]::new(0, 14, 0, 0)
[void]$panel.Children.Add($foot)

[void]$cardGrid.Children.Add($panel)

# glass gloss on top
$gloss = New-Object System.Windows.Controls.Border
$gloss.CornerRadius = [System.Windows.CornerRadius]::new(25, 25, 0, 0)
$gloss.Height = 96; $gloss.VerticalAlignment = 'Top'
$gb = New-Object System.Windows.Media.LinearGradientBrush
$gb.StartPoint = [System.Windows.Point]::new(0, 0)
$gb.EndPoint = [System.Windows.Point]::new(0, 1)
$gb.GradientStops.Add([System.Windows.Media.GradientStop]::new([System.Windows.Media.Color]::FromArgb(0x14, 0xFF, 0xFF, 0xFF), 0))
$gb.GradientStops.Add([System.Windows.Media.GradientStop]::new([System.Windows.Media.Color]::FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1))
$gloss.Background = $gb
[void]$cardGrid.Children.Add($gloss)

$card.Child = $cardGrid
[void]$root.Children.Add($card)

# watermark
$note = New-Object System.Windows.Controls.TextBlock
$note.Text = 'PREVIEW - transparent splash (simulated desktop)'
$note.FontSize = 11
$note.HorizontalAlignment = 'Right'; $note.VerticalAlignment = 'Bottom'
$note.Margin = [System.Windows.Thickness]::new(0, 0, 24, 16)
$note.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(0x77, 0xFF, 0xFF, 0xFF))
[void]$root.Children.Add($note)

$root.Width = $W; $root.Height = $H
$root.Measure([System.Windows.Size]::new($W, $H))
$root.Arrange([System.Windows.Rect]::new(0, 0, $W, $H))
$root.UpdateLayout()

$rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($W, $H, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$rtb.Render($root)
$enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
$fs = [System.IO.File]::Create('D:\app\build_source\ui_review\preview_splash_transparent.png')
$enc.Save($fs); $fs.Close()
Write-Host 'OK preview_splash_transparent.png'
