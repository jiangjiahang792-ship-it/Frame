# 仅编译真实节点窗体的现场图预览入口，控件继续使用生产 Designer。
param([string]$BuildDirectory = 'bin/x64/Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$buildPath = Join-Path $projectRoot $BuildDirectory
$compilerPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $buildPath $_) }
$executable = Join-Path $buildPath 'ContourLivePreview.exe'
& $compilerPath /nologo /target:winexe /platform:x64 /langversion:7.3 "/out:$executable" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'ContourLivePreview.cs')
if ($LASTEXITCODE -ne 0) { throw '现场图预览编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($executable + '.config') -Force
Write-Output $executable
