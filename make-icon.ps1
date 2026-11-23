# Makes app.ico from app-icon.svg (the gold-framed Launch Grid logo) and, for the small sizes (16 to 32), from
# app-icon-small.svg (the same logo simplified so it stays crisp: a thicker frame, lighter squares, a bolder arrow, no
# glints). Microsoft Edge, running hidden with a throwaway profile, draws each size, and the sizes are packed into one
# multi-size .ico. Only needed when the logo changes: the finished app.ico is kept with the source.
param([string]$Path = (Join-Path $PSScriptRoot 'app.ico'), [string]$Svg = (Join-Path $PSScriptRoot 'app-icon.svg'),
      [string]$SmallSvg = (Join-Path $PSScriptRoot 'app-icon-small.svg'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge is needed to draw the icon from app-icon.svg.' }

# 16, 20, 24 and 32 are the notification area at 100, 125, 150 and 200% scaling; the rest are shortcuts and Explorer.
$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$work = Join-Path ([IO.Path]::GetTempPath()) ('ready-launcher-icon-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $work | Out-Null
try {
    # Every size on one page, side by side, then cut apart.
    $svgUrl = ([Uri](Resolve-Path $Svg).Path).AbsoluteUri
    $smallUrl = if (Test-Path $SmallSvg) { ([Uri](Resolve-Path $SmallSvg).Path).AbsoluteUri } else { $svgUrl }
    $x = 0; $imgs = @(); $spots = @()
    foreach ($s in $sizes) {
        $src = if ($s -le 32) { $smallUrl } else { $svgUrl }
        $imgs += "<img src=`"$src`" style=`"position:absolute;left:${x}px;top:0;width:${s}px;height:${s}px`">"; $spots += $x; $x += $s + 8
    }
    $page = Join-Path $work 'icon.html'
    "<!doctype html><html><head><meta charset=`"utf-8`"><style>html,body{margin:0;background:transparent}</style></head><body>$($imgs -join '')</body></html>" |
        Set-Content $page -Encoding UTF8
    $sheet = Join-Path $work 'sheet.png'
    $edgeArgs = "--headless --disable-gpu --no-first-run --no-default-browser-check --disable-extensions --disable-sync " +
                "--allow-file-access-from-files --user-data-dir=`"$work\profile`" --hide-scrollbars --force-device-scale-factor=1 " +
                "--default-background-color=00000000 --window-size=$x,264 --screenshot=`"$sheet`" `"$(([Uri]$page).AbsoluteUri)`""
    $p = Start-Process -FilePath $edge -ArgumentList $edgeArgs -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit(60000) -or -not (Test-Path $sheet)) { throw 'Edge did not draw the icon.' }

    $all = [System.Drawing.Bitmap]::FromFile($sheet)
    $pngs = New-Object 'System.Collections.Generic.List[byte[]]'
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $one = $all.Clone((New-Object System.Drawing.Rectangle $spots[$i], 0, $s, $s), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $ms = New-Object System.IO.MemoryStream
        if ($s -ge 256) { $one.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png) }   # the big one compressed, as usual
        else {
            # Smaller sizes as a plain 32-bit picture, which every icon reader understands (some can't read PNG ones):
            # a bitmap header (twice the height: picture and mask), the rows bottom up, then a 1-bit mask.
            $bw = New-Object System.IO.BinaryWriter $ms
            $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
            $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2)); $bw.Write([uint16]1); $bw.Write([uint16]32)
            $bw.Write([uint32]0); $bw.Write([uint32]($s * $s * 4 + $maskRow * $s)); $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
            $data = $one.LockBits((New-Object System.Drawing.Rectangle 0, 0, $s, $s), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $px = New-Object byte[] ($data.Stride * $s)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
            $one.UnlockBits($data)
            for ($y = $s - 1; $y -ge 0; $y--) { $bw.Write($px, $y * $data.Stride, $s * 4) }
            for ($y = $s - 1; $y -ge 0; $y--) {
                $mask = New-Object byte[] $maskRow
                for ($x2 = 0; $x2 -lt $s; $x2++) { if ($px[$y * $data.Stride + $x2 * 4 + 3] -eq 0) { $mask[[int][Math]::Floor($x2 / 8)] = $mask[[int][Math]::Floor($x2 / 8)] -bor (0x80 -shr ($x2 % 8)) } }
                $bw.Write($mask)
            }
            $bw.Flush()
        }
        $one.Dispose()
        $pngs.Add($ms.ToArray())
    }
    $all.Dispose()

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
    Write-Host "Wrote $Path ($($sizes -join ', '))"
}
finally {
    Start-Sleep -Milliseconds 500
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
