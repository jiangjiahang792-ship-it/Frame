param([string]$OutputDirectory = 'bin/x64/RuntimeScreenProbe')
# 仅编译显示层，不链接生产设备、方案或运行调度入口。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$build = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Path $build -Force | Out-Null
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Forms/RuntimeDesigner') -Filter '*.cs' |
    Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs',
        'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } |
    ForEach-Object { $_.FullName })
$references = @($json, 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') |
    ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:winexe /platform:x64 /langversion:7.3 "/out:$build/RuntimeScreenProbe.exe" @references @sources (Join-Path $PSScriptRoot 'ScreenProbeProgram.cs')
if ($LASTEXITCODE -ne 0) { throw '跨屏诊断工具编译失败。' }
Copy-Item -LiteralPath $json -Destination $build -Force
