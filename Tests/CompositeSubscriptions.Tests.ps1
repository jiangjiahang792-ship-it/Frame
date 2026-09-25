# 使用方案副本恢复实际控件，不反序列化设备、不启动模型推理。
param([string]$BuildDirectory = 'bin/CompositeSubscriptionValidation',
      [string]$SchemePath = "$PSScriptRoot/Fixtures/CompositeSubscriptions/Fakra.Sol", [switch]$Preview)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildPath = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testPath = Join-Path $buildPath 'CompositeSubscriptions.Tests.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'OpenCvSharp.dll', 'SunnyUI.dll', 'Newtonsoft.Json.dll') | ForEach-Object { '/reference:' + (Join-Path $buildPath $_) }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'CompositeSubscriptions.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '现场方案订阅测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
if ($Preview) { & $testPath $SchemePath --preview } else { & $testPath $SchemePath }
if ($LASTEXITCODE -ne 0) { throw '现场方案订阅测试失败。' }
