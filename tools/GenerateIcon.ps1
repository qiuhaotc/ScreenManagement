Add-Type -AssemblyName System.Drawing

$accent   = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
$standClr = [System.Drawing.Color]::FromArgb(255, [int](37*0.8), [int](99*0.8), [int](235*0.8))

function New-RoundedPath([int]$x, [int]$y, [int]$w, [int]$h, [int]$r) {
    if ($r -lt 1) { $r = 1 }
    $d = $r * 2
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x,        $y,        $d, $d, 180, 90)
    $p.AddArc($x+$w-$d,  $y,        $d, $d, 270, 90)
    $p.AddArc($x+$w-$d,  $y+$h-$d,  $d, $d,   0, 90)
    $p.AddArc($x,        $y+$h-$d,  $d, $d,  90, 90)
    $p.CloseFigure()
    return $p
}

function New-MonitorBitmap([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode          = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode        = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality     = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    [double]$sf = $S / 32.0

    # Helper
    function Rnd([double]$v, [int]$mn = 0) { [int][Math]::Max($mn, [Math]::Round($v)) }

    # --- monitor frame ---
    $fx = Rnd ($sf*1);  $fy = Rnd ($sf*1)
    $fw = Rnd ($sf*29) 1; $fh = Rnd ($sf*21) 1
    $fr = Rnd ($sf*3)  1
    $fb = New-Object System.Drawing.SolidBrush($accent)
    $fp = New-RoundedPath $fx $fy $fw $fh $fr
    $g.FillPath($fb, $fp)
    $fp.Dispose(); $fb.Dispose()

    # --- screen (derived from frame to guarantee visible bezel at all sizes) ---
    $padH = Rnd ($sf * 3) 1   # left/right bezel thickness
    $padB = Rnd ($sf * 5) 1   # bottom bezel thickness (larger, for stand area)
    $sx   = $fx + $padH;  $sy = $fy + $padH
    $sw   = [int][Math]::Max(1, $fw - 2 * $padH)
    $sh   = [int][Math]::Max(1, $fh - $padH - $padB)
    $sb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 12, 20, 40))
    $g.FillRectangle($sb, $sx, $sy, $sw, $sh)
    $sb.Dispose()

    # --- screen highlight ---
    $gh = Rnd ($sf*5) 1
    $gb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(55, 255, 255, 255))
    $g.FillRectangle($gb, $sx, $sy, $sw, $gh)
    $gb.Dispose()

    # --- content lines (32px+) ---
    if ($S -ge 32) {
        $lh = Rnd ($sf*2) 1
        $lb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(100, $accent.R, $accent.G, $accent.B))
        $g.FillRectangle($lb, (Rnd ($sf*6)), (Rnd ($sf*12)), (Rnd ($sf*14) 1), $lh)
        $g.FillRectangle($lb, (Rnd ($sf*6)), (Rnd ($sf*15)), (Rnd ($sf*9)  1), (Rnd $sf 1))
        $lb.Dispose()
    }

    # --- stand ---
    $stb = New-Object System.Drawing.SolidBrush($standClr)
    $g.FillRectangle($stb, (Rnd ($sf*14)), (Rnd ($sf*22)), (Rnd ($sf*4) 1), (Rnd ($sf*4) 1))

    # --- base ---
    $bx = Rnd ($sf*9);  $by = Rnd ($sf*26)
    $bw = Rnd ($sf*14) 1; $bh = Rnd ($sf*4) 2; $br = Rnd ($sf*2) 1
    $bp = New-RoundedPath $bx $by $bw $bh $br
    $g.FillPath($stb, $bp)
    $bp.Dispose(); $stb.Dispose()

    $g.Dispose()
    return $bmp
}

$out   = "$PSScriptRoot\..\src\ScreenManagement.UI\app.ico"
$sizes = @(16, 32, 48, 256)

$pngList = [System.Collections.Generic.List[byte[]]]::new()
foreach ($sz in $sizes) {
    $bmp = New-MonitorBitmap $sz
    $ms  = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $pngList.Add($bytes)
    Write-Host "  $($sz)x$($sz) -> $($bytes.Length) bytes PNG"
    $ms.Dispose(); $bmp.Dispose()
}

# Write ICO (PNG-in-ICO, Vista+)
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type: ICO
$bw.Write([uint16]$sizes.Count)  # image count

$offset = [uint32](6 + 16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $bw.Write([byte]$(if ($sz -eq 256) { 0 } else { $sz }))  # width
    $bw.Write([byte]$(if ($sz -eq 256) { 0 } else { $sz }))  # height
    $bw.Write([byte]0)     # color count
    $bw.Write([byte]0)     # reserved
    $bw.Write([uint16]1)   # planes
    $bw.Write([uint16]32)  # bpp
    $bw.Write([uint32]$pngList[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += [uint32]$pngList[$i].Length
}
foreach ($d in $pngList) {
    $bw.Write($d, 0, $d.Length)
}
$bw.Flush()
$bw.Dispose()
$fs.Dispose()

$total = (Get-Item $out).Length
Write-Host "ICO written: $out ($total bytes)"
