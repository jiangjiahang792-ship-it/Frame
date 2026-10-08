param([string]$BuildDirectory = 'bin/x64/RuntimeInlinePagesVerify', [switch]$CompileSources)
# 仅编译独立屏幕外测试入口，禁止启动生产主程序。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$sources = @()
if ($CompileSources) {
    # 独立输出避免覆盖主代理的产品构建，仅编译无设备依赖的设计器代码。
    $build = Join-Path $root 'bin/x64/RuntimeRunControlRefreshVerify'
    New-Item -ItemType Directory -Force -Path $build | Out-Null
    $json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Forms/RuntimeDesigner') -Filter '*.cs' | Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } | ForEach-Object { $_.FullName })
    $assemblies = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll')
    Copy-Item -LiteralPath $json -Destination $build -Force
} else {
    $application = (Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1).FullName
    $assemblies = @($application, (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll')
}
$references = $assemblies | ForEach-Object { '/reference:' + $_ }
$output = Join-Path $build 'RuntimeRunControlRefresh.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /debug:full /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'RuntimeRunControlRefresh.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '运行按钮刷新专项测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '运行按钮刷新专项测试失败。' }
