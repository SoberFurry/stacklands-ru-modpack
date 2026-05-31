chcp 65001 | Out-Null
Add-Type -AssemblyName System.Drawing

$outDir = "D:\Пользователи\Пользовател\Desktop\Mod\Icons_Generated"
New-Item -ItemType Directory -Force $outDir | Out-Null

function New-Icon {
    param([string]$name, [scriptblock]$draw)
    $size = 24
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 25, 20))
    & $draw $g $brush $size
    $path = Join-Path $outDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "  Created: $name"
}

# ── icon-all.png ── 3 horizontal bars (hamburger)
New-Icon "icon-all.png" {
    param($g, $b, $s)
    $g.FillRectangle($b, 3, 5,  18, 3)
    $g.FillRectangle($b, 3, 11, 18, 3)
    $g.FillRectangle($b, 3, 17, 18, 3)
}

# ── icon-new.png ── 5-pointed filled star
New-Icon "icon-new.png" {
    param($g, $b, $s)
    $cx = 12.0; $cy = 12.0; $outer = 10.0; $inner = 4.2
    $pts = for ($i = 0; $i -lt 10; $i++) {
        $angle = [Math]::PI * $i / 5 - [Math]::PI / 2
        $r = if ($i % 2 -eq 0) { $outer } else { $inner }
        [System.Drawing.PointF]::new($cx + $r * [Math]::Cos($angle), $cy + $r * [Math]::Sin($angle))
    }
    $g.FillPolygon($b, $pts)
}

# ── icon-reset.png ── circular arrow: arc + arrowhead
New-Icon "icon-reset.png" {
    param($g, $b, $s)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 30, 25, 20), 3.0)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    # Draw C-shape arc (3/4 circle, clockwise)
    $g.DrawArc($pen, 3, 3, 18, 18, -60, 300)
    # Arrowhead at the open end (roughly top-right)
    $arrowPts = @(
        [System.Drawing.PointF]::new(18.0, 3.5),
        [System.Drawing.PointF]::new(21.0, 8.5),
        [System.Drawing.PointF]::new(14.5, 7.5)
    )
    $g.FillPolygon($b, $arrowPts)
    $pen.Dispose()
}

# ── icon-make.png ── downward arrow with plus (create/craft)
New-Icon "icon-make.png" {
    param($g, $b, $s)
    # Arrow pointing down
    $g.FillRectangle($b, 10, 2, 4, 14)
    $arrowPts = @(
        [System.Drawing.PointF]::new(12.0, 22.0),
        [System.Drawing.PointF]::new(5.0, 13.0),
        [System.Drawing.PointF]::new(19.0, 13.0)
    )
    $g.FillPolygon($b, $arrowPts)
}

# ── icon-usedin.png ── arrow going up-right (used in recipe)
New-Icon "icon-usedin.png" {
    param($g, $b, $s)
    # Arrow pointing up
    $g.FillRectangle($b, 10, 8, 4, 14)
    $arrowPts = @(
        [System.Drawing.PointF]::new(12.0, 2.0),
        [System.Drawing.PointF]::new(5.0,  11.0),
        [System.Drawing.PointF]::new(19.0, 11.0)
    )
    $g.FillPolygon($b, $arrowPts)
}

# ── icon-open.png ── open book / list (opened recipes)
New-Icon "icon-open.png" {
    param($g, $b, $s)
    # Book shape: two pages
    $g.FillRectangle($b, 2,  4, 9, 16)
    $g.FillRectangle($b, 13, 4, 9, 16)
    # Spine
    $spineBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 230, 210))
    $g.FillRectangle($spineBrush, 10, 3, 4, 18)
    $spineBrush.Dispose()
    # Lines on pages
    $lineBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 230, 210))
    $g.FillRectangle($lineBrush, 4, 8, 5, 1.5)
    $g.FillRectangle($lineBrush, 4, 12, 5, 1.5)
    $g.FillRectangle($lineBrush, 4, 16, 5, 1.5)
    $g.FillRectangle($lineBrush, 15, 8, 5, 1.5)
    $g.FillRectangle($lineBrush, 15, 12, 5, 1.5)
    $g.FillRectangle($lineBrush, 15, 16, 5, 1.5)
    $lineBrush.Dispose()
}

Write-Host ""
Write-Host "All icons created in: $outDir" -ForegroundColor Green
