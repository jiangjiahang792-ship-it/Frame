# 复用两次独立试验的方法，统一比较整栓；不连接设备或修改生产框架。
param([string]$BuildDirectory='bin/NewTerminal12Validation')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$build=Join-Path $root $BuildDirectory
$refs=@('机器视觉AI检测系统V1.0.exe','Newtonsoft.Json.dll','OpenCvSharp.dll','SunnyUI.dll') | ForEach-Object {'/reference:'+(Join-Path $build $_)}
$output=Join-Path $build 'PlugWholeComparison.Tests.exe'
$sources=@('PlugWholeComparison.Tests.cs','PlugGoodPair.Tests.cs','PlugContourComparison.Tests.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /main:PlugWholeComparison /platform:x64 /langversion:7.3 "/out:$output" @refs /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources
if($LASTEXITCODE -ne 0){throw '统一整栓试验编译失败。'}
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($output+'.config') -Force
Push-Location $build
try {
    & $output $root
    if($LASTEXITCODE -ne 0){throw '统一整栓试验运行失败。'}
} finally {Pop-Location}
