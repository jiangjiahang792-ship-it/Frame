param()
# 编译纯显示模型、渲染器及控制器，不加载生产程序集或访问设备。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeImageViewportVerify'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
$sources = @('RuntimeLayoutDocument.cs', 'RuntimeLayoutSerializer.cs', 'RuntimeLayoutSession.cs',
    'RuntimeDataBinding.cs', 'RuntimeDataValue.cs', 'RuntimeLog.cs', 'RuntimeLogRenderer.cs',
    'RuntimeRunControl.cs', 'RuntimeWidgetClipboard.cs', 'RuntimeImageViewport.cs', 'RuntimeWidgetRenderer.cs',
    'RuntimeTuningController.cs', 'RuntimeTuningWidget.cs', 'RuntimeTuningWidget.Designer.cs',
    'RuntimeLiveWidgetRenderer.cs', 'RuntimeLiveViewController.cs', 'RuntimeDesignCanvas.cs',
    'RuntimeDesignCanvas.ImageInteraction.cs', 'RuntimeDesignCanvas.Designer.cs',
    'RuntimeWidgetSurfaceCache.cs', 'RuntimeLayoutAlignment.cs', 'RuntimeRunControlWidget.cs', 'RuntimeRunControlWidget.Designer.cs') |
    ForEach-Object { Join-Path $root ('Forms/RuntimeDesigner/' + $_) } |
    Where-Object { Test-Path -LiteralPath $_ }
$references = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') |
    ForEach-Object { '/reference:' + $_ }
$output = Join-Path $build 'RuntimeImageViewport.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'RuntimeImageViewport.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '图像视口后端专项测试编译失败。' }
Copy-Item -LiteralPath $json -Destination $build -Force
& $output
if ($LASTEXITCODE -ne 0) { throw '图像视口后端专项测试失败。' }
