param([string]$BuildDirectory='bin/Station2FixValidation')
$ErrorActionPreference='Stop'
# 在独立编译目录验证生产程序集，不连接相机、PLC或修改用户方案。
$testRoot=Split-Path -Parent $PSScriptRoot
$testBuild=Join-Path $testRoot $BuildDirectory
$testExe=Join-Path $testBuild 'PositionCorrectionTemplateIdentity.Tests.exe'
$testRefs=@('机器视觉AI检测系统V1.0.exe','Newtonsoft.Json.dll') | ForEach-Object { '/reference:'+(Join-Path $testBuild $_) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 "/out:$testExe" @testRefs /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'PositionCorrectionTemplateIdentity.Tests.cs')
if($LASTEXITCODE -ne 0){throw '模板基准测试编译失败'}
& $testExe
if($LASTEXITCODE -ne 0){throw '模板基准回归失败'}
