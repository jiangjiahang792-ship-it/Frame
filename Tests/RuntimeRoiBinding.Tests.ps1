param([string]$BuildDirectory = 'bin/x64/DesignerRunControlVerify')
# 只编译本轮源码与专项入口，不启动生产主程序或设备。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1
if ($null -eq $application) { throw 'A reference build is required; do not launch it.' }
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'RuntimeRoiBinding.SourceTests.exe'
$sources = @('RuntimeDataBinding.cs', 'RuntimeLayoutDocument.cs', 'RuntimeLayoutSerializer.cs',
    'RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeDataValue.cs', 'RuntimeRoiSnapshot.cs',
    'RuntimeLiveViewController.cs', 'RuntimeLiveWidgetRenderer.cs', 'RuntimeDesignCanvas.cs',
    'RuntimeDesignCanvas.Designer.cs', 'RuntimeDesignCanvas.ImageInteraction.cs', 'RuntimeImageViewport.cs',
    'RuntimeWidgetRenderer.cs', 'RuntimeWidgetSurfaceCache.cs', 'RuntimeLayoutAlignment.cs', 'RuntimeLayoutSession.cs', 'RuntimeWidgetClipboard.cs',
    'RuntimeRunControl.cs', 'RuntimeRunControlWidget.cs', 'RuntimeRunControlWidget.Designer.cs',
    'RuntimeTuningController.cs', 'RuntimeTuningWidget.cs', 'RuntimeTuningWidget.Designer.cs',
    'RuntimeLog.cs', 'RuntimeLogRenderer.cs') |
    ForEach-Object { Join-Path $root ('Forms/RuntimeDesigner/' + $_) }
$references = @($application.FullName, (Join-Path $build 'Newtonsoft.Json.dll'),
    (Join-Path $build 'OpenCvSharp.dll'), (Join-Path $build 'OpenCvSharp.Extensions.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 /nowarn:0436 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'RuntimeRoiBinding.ModuleStub.cs') (Join-Path $PSScriptRoot 'RuntimeRoiBinding.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'ROI source test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'ROI source tests failed.' }
