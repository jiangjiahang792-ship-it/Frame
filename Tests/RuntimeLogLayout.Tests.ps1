param([string]$BuildDirectory = 'bin/x64/RuntimeLogVerify', [switch]$CompileSources)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = Join-Path $build '机器视觉AI检测系统V1.0.exe'
$sources = @()
if ($CompileSources) {
    $build = Join-Path $root 'bin/x64/RuntimeLogLayoutVerify'
    New-Item -ItemType Directory -Force -Path $build | Out-Null
    $application = $null
    $json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
    Copy-Item -LiteralPath $json -Destination $build -Force
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Forms/RuntimeDesigner') -Filter '*.cs' |
        Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } |
        ForEach-Object { $_.FullName })
}
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'RuntimeLogLayout.Tests.exe'
$references = @($application, (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | Where-Object { $_ } | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'RuntimeLogLayout.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '日志布局测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '日志布局测试失败。' }
