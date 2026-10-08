param()
# 仅编译纯几何和本机配置存储；测试文件全部位于独立临时目录。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeWindowPlacementVerify'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$sources = @((Join-Path $PSScriptRoot 'RuntimeWindowPlacement.Tests.cs'))
$model = Join-Path $root 'Forms/RuntimeDesigner/RuntimeWindowPlacement.cs'
$store = Join-Path $root 'Forms/RuntimeDesigner/RuntimeWindowPlacementStore.cs'
$defines = @()
if ((Test-Path -LiteralPath $model) -and (Test-Path -LiteralPath $store)) {
    $sources += $model, $store
    $defines += '/define:PLACEMENT_IMPLEMENTED'
}
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
$output = Join-Path $build 'RuntimeWindowPlacement.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /langversion:7.3 /reference:System.Core.dll /reference:System.Drawing.dll "/reference:$json" "/out:$output" @defines @sources
if ($LASTEXITCODE -ne 0) { throw 'Window placement test compilation failed.' }
Copy-Item -LiteralPath $json -Destination $build -Force
& $output
if ($LASTEXITCODE -ne 0) { throw 'Window placement tests failed.' }
