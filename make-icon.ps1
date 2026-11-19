# Draws the launcher's app icon (olive tile with a 2x2 grid) and writes a multi-size .ico.
param([string]$Path = (Join-Path $PSScriptRoot 'app.ico'))
Add-Type -AssemblyName System.Drawing

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$sizes = @(16, 24, 32, 48, 64, 256)
$pngs = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $m = [single]($s * 0.04)
    $bg = New-RoundRect $m $m ($s - 2 * $m) ($s - 2 * $m) ($s * 0.22)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($s, $s)), ([System.Drawing.Color]::FromArgb(255, 222, 204, 48)), ([System.Drawing.Color]::FromArgb(255, 150, 132, 0))
    $g.FillPath($brush, $bg)
    $cell = [single]($s * 0.26); $gap = [single]($s * 0.08)
    $start = [single](($s - (2 * $cell + $gap)) / 2)
    foreach ($i in 0..1) {
        foreach ($j in 0..1) {
            $p = New-RoundRect ($start + $i * ($cell + $gap)) ($start + $j * ($cell + $gap)) $cell $cell ($cell * 0.28)
            $g.FillPath([System.Drawing.Brushes]::White, $p)
        }
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs.Add($ms.ToArray())
}

$fs = [System.IO.File]::Create($Path)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($b in $pngs) { $w.Write($b) }
$w.Close()
Write-Host "Wrote $Path"
