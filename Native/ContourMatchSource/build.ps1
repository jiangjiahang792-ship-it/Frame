# 从已安装的静态 OpenCV 编译本节点的原生 DLL，不下载或修改桌面 Demo。
[CmdletBinding()]
param(
    # 包含 share/opencv4 的 x64 静态 OpenCV 安装目录。
    [Parameter(Mandatory = $true)][string]$OpenCvPackageRoot,
    # CMake 可执行文件，可指定 Visual Studio 自带版本的绝对路径。
    [string]$CMakeExecutable = 'cmake',
    # Visual Studio CMake 生成器名称。
    [string]$Generator = 'Visual Studio 17 2022',
    # 原生构建配置，默认发布版本。
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildRoot = Join-Path $projectRoot 'artifacts/ContourMatchValidation/native-build'
$packageRoot = (Resolve-Path -LiteralPath $OpenCvPackageRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'share/opencv4/OpenCVConfig.cmake'))) {
    throw '指定目录没有找到 share/opencv4/OpenCVConfig.cmake，请提供 x64 静态 OpenCV 安装目录。'
}
$cmakePath = (Get-Command $CMakeExecutable -ErrorAction Stop).Source
$ctestPath = Join-Path (Split-Path -Parent $cmakePath) 'ctest.exe'

# 外部程序失败时停止，避免用旧 DLL 覆盖工程中的原生库。
function Assert-NativeExit {
    param([string]$Operation)
    if ($LASTEXITCODE -ne 0) { throw "$Operation 失败，退出码：$LASTEXITCODE" }
}
& $cmakePath -S (Join-Path $PSScriptRoot 'native') -B $buildRoot -G $Generator -A x64 "-DCMAKE_PREFIX_PATH=$packageRoot" -DBUILD_TESTING=ON
Assert-NativeExit '配置原生库'
& $cmakePath --build $buildRoot --config $Configuration --parallel 2
Assert-NativeExit '编译原生库'
& $ctestPath --test-dir $buildRoot -C $Configuration --output-on-failure
Assert-NativeExit '验证原生 ABI'
$dllPath = Join-Path $buildRoot 'bin/ShapeMatchNative.dll'
Copy-Item -LiteralPath $dllPath -Destination (Join-Path $projectRoot 'Native/ShapeMatchNative.dll') -Force
Write-Host '原生库已编译、验证并更新到 Native/ShapeMatchNative.dll，请重新构建主工程。'
