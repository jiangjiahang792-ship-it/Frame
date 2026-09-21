# 使用真实程序集验证命名存图，不连接现场设备。
param([string]$BuildDirectory = 'bin/x64/Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildPath = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testPath = Join-Path $buildPath 'NamedImageSave.Tests.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'OpenCvSharp.dll', 'SunnyUI.dll', 'Newtonsoft.Json.dll') | ForEach-Object { '/reference:' + (Join-Path $buildPath $_) }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'NamedImageSave.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '命名存图测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
& $testPath
if ($LASTEXITCODE -ne 0) { throw '命名存图测试失败。' }
