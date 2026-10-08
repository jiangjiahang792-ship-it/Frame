param()
# 仅编译后端服务与替身，不加载生产程序集、不打开窗体或执行学习。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeTuningBackendVerify'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$service = Join-Path $root 'Forms/SolRunParam/ProcessTuningService.cs'
$sources = @((Join-Path $PSScriptRoot 'RuntimeTuningBackend.Tests.cs'))
if (Test-Path -LiteralPath $service) { $sources += $service }
$output = Join-Path $build 'RuntimeTuningBackend.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 /reference:System.Core.dll /reference:System.Windows.Forms.dll "/out:$output" @sources
if ($LASTEXITCODE -ne 0) { throw 'Runtime tuning backend test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Runtime tuning backend tests failed.' }
