# 运行本项目基准，程序集与生产使用同一构建目录。
param([string[]]$RunnerArguments, [string]$BuildDirectory = 'bin/x64/Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$buildPath = Join-Path $projectRoot $BuildDirectory
$compilerPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'System.Memory.dll') | ForEach-Object { '/reference:' + (Join-Path $buildPath $_) }
$executable = Join-Path $buildPath 'ContourDatasetRunner.exe'
& $compilerPath /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$executable" @references /reference:System.Core.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ContourDatasetRunner.cs')
if ($LASTEXITCODE -ne 0) { throw '基准编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($executable + '.config') -Force
& $executable @RunnerArguments
if ($LASTEXITCODE -ne 0) { throw '基准执行失败。' }
