$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Path $sourcePath -Raw -Encoding UTF8
}

function Assert-DiagnosticMarkerUsesDebug {
    param(
        [string]$RelativePath,
        [string]$Marker
    )

    $source = Get-ProjectSource $RelativePath
    $searchIndex = 0
    $found = $false

    while ($true) {
        $markerIndex = $source.IndexOf($Marker, $searchIndex)
        if ($markerIndex -lt 0) {
            break
        }

        $found = $true
        $callStart = $source.LastIndexOf('LogHelper.AddLog(', $markerIndex)
        $callEnd = $source.IndexOf(');', $markerIndex)

        if ($callStart -lt 0 -or $callEnd -lt 0) {
            throw "$RelativePath marker '$Marker' is not inside a LogHelper.AddLog call."
        }

        $callText = $source.Substring($callStart, $callEnd - $callStart + 2)
        if (-not $callText.Contains('MsgLevel.Debug')) {
            throw "$RelativePath marker '$Marker' must use MsgLevel.Debug."
        }

        $searchIndex = $markerIndex + $Marker.Length
    }

    if (-not $found) {
        throw "$RelativePath is missing diagnostic marker '$Marker'."
    }
}

$diagnosticMarkers = @(
    @{ Path = 'Node\1-Acquisition\ImageSource\NodeImageSource.cs'; Markers = @(
        'Bitmap转Mat完毕'
    ) },
    @{ Path = 'Node\3-Detection\TDAI\NodeTDAI.cs'; Markers = @(
        'AI检测开始',
        'AI检测,图像获取完毕',
        'AI检测输入图像摘要',
        'AI检测模型检查开始',
        'AI检测模型检查通过',
        'AI检测,Modbus地址',
        'AI检测推理完毕',
        'AI检测结果运算完毕',
        'AI检测解析结果',
        'AI检测完毕'
    ) },
    @{ Path = 'Node\3-Detection\ColorDiscern\NodeColorDiscern.cs'; Markers = @(
        '节点({ID}.{NodeName}){step}'
    ) },
    @{ Path = 'Node\3-Detection\ColorDiscern\NodeParamFormColorDiscern.cs'; Markers = @(
        '节点({nodeName}){step}'
    ) },
    @{ Path = 'Node\7-ResultProcessing\ResultOverlayDraw\NodeResultOverlayDraw.cs'; Markers = @(
        '【性能诊断-ROI结果绘制】',
        '【慢诊断-ROI结果绘制】'
    ) },
    @{ Path = 'Node\1-Acquisition\ImageShow\NodeImageShow.cs'; Markers = @(
        '【性能诊断-图像显示】',
        '【慢诊断-图像显示】'
    ) },
    @{ Path = 'Node\7-ResultProcessing\ImageSave\NodeSaveImage.cs'; Markers = @(
        '【性能诊断-保存图像快速入队】',
        '【内存诊断-保存队列启动】',
        '【内存诊断-保存图像后台取图】',
        '【内存诊断-保存队列消费】',
        '【内存诊断-保存队列释放】',
        '【内存诊断-保存队列停止】'
    ) },
    @{ Path = 'Node\7-ResultProcessing\ImageSave\ParamFormSaveImage.cs'; Markers = @(
        '【内存诊断-保存图像取图】'
    ) }
)

foreach ($entry in $diagnosticMarkers) {
    foreach ($marker in $entry.Markers) {
        Assert-DiagnosticMarkerUsesDebug $entry.Path $marker
    }
}

Write-Host 'Diagnostic log level regression checks passed.'
