param([string]$BuildDirectory = 'bin/StopLifecycleValidation', [string]$Mode = 'complete')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$test = Join-Path $build 'StopLifecycle.Tests.exe'
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$test" ('/reference:' + (Join-Path $build '机器视觉AI检测系统V1.0.exe')) /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'StopLifecycle.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '停止回归编译失败。' }
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($test + '.config') -Force
& $test $Mode
if ($LASTEXITCODE -ne 0) { throw "停止回归失败：$Mode" }
