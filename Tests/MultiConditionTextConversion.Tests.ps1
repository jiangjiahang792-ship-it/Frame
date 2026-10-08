# 使用真实程序集验证比较结果和首次机会异常数量。
param([string]$BuildDirectory = 'bin/x64/Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'MultiConditionTextConversion.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" "/reference:$(Join-Path $build '机器视觉AI检测系统V1.0.exe')" (Join-Path $PSScriptRoot 'MultiConditionTextConversion.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '多条件文本转换测试编译失败。' }
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '多条件文本转换测试失败。' }
} finally { Pop-Location }
