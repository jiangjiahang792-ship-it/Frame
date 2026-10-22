# 构建独立新版库；不覆盖旧 ShapeMatchNative.dll。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$OpenCvPackageRoot,
    [string]$CMakeExecutable = 'C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildRoot = Join-Path $projectRoot 'artifacts/NewContourMatchValidation/native-build'
$packageRoot = (Resolve-Path -LiteralPath $OpenCvPackageRoot).Path
& $CMakeExecutable -S (Join-Path $PSScriptRoot 'native') -B $buildRoot -G 'Visual Studio 17 2022' -A x64 "-DCMAKE_PREFIX_PATH=$packageRoot"
if ($LASTEXITCODE -ne 0) { throw '新版轮廓原生库配置失败。' }
& $CMakeExecutable --build $buildRoot --config Release --parallel 2
if ($LASTEXITCODE -ne 0) { throw '新版轮廓原生库编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildRoot 'bin/NewContourMatchNative.dll') -Destination (Join-Path $projectRoot 'Native/NewContourMatchNative.dll') -Force
Write-Host '新版原生库已更新。重新构建主工程后运行 Tests/NewContourMatch.Tests.ps1 验证。'
