param([string]$BuildDirectory = 'bin/Station2RenameValidation')
$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path -Parent $PSScriptRoot) $BuildDirectory
$test = Join-Path $build 'StartupDeviceConnectionBatch.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$test" "/reference:$(Join-Path $build '机器视觉AI检测系统V1.0.exe')" /reference:System.Core.dll (Join-Path $PSScriptRoot 'StartupDeviceConnectionBatch.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '设备并发恢复测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($test + '.config') -Force
& $test
if ($LASTEXITCODE -ne 0) { throw '设备并发恢复测试失败。' }
