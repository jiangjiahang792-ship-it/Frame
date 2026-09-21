# 编译并运行真实保存工作池的非法文件名落盘回归。
param([string]$BuildDirectory = 'bin/x64/Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildPath = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testPath = Join-Path $buildPath 'ImageSaveInvalidName.Tests.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'OpenCvSharp.dll', 'SunnyUI.dll', 'Newtonsoft.Json.dll') | ForEach-Object { '/reference:' + (Join-Path $buildPath $_) }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'ImageSaveInvalidName.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '非法存图名称测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
$resultPath = Join-Path $projectRoot ('Tests/Results/ImageSaveInvalidName/' + [Guid]::NewGuid().ToString('N'))
& $testPath $resultPath
if ($LASTEXITCODE -ne 0) { throw '非法存图名称测试失败。' }
