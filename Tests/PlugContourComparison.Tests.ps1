# 独立无闭运算试验；不修改生产程序与现场配置。
param([string]$BuildDirectory='bin/NewTerminal12Validation', [string]$SampleDirectory='outputs/PlugColorSamples-20261018')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$build=Join-Path $root $BuildDirectory
$refs=@('机器视觉AI检测系统V1.0.exe','Newtonsoft.Json.dll','OpenCvSharp.dll','SunnyUI.dll') | ForEach-Object {'/reference:'+(Join-Path $build $_)}
$output=Join-Path $build 'PlugContourComparison.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @refs /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'PlugContourComparison.Tests.cs')
if($LASTEXITCODE -ne 0){throw '轮廓对比试验编译失败。'}
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($output+'.config') -Force
Push-Location $build
try {
    & $output (Join-Path $root $SampleDirectory)
    if($LASTEXITCODE -ne 0){throw '轮廓对比试验执行失败。'}
} finally { Pop-Location }
