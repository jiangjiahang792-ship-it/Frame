param([string]$OutputDirectory = 'bin/x64/RuntimeInlinePagesDemo')

# 仅编译显示层及纯内存模拟入口，不链接生产方案、设备和通信实现。
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
$output = Join-Path $build '运行界面多页签体验.exe'
$references = @($json, 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') |
    ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:winexe /platform:x64 /langversion:7.3 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'MultipleWindowsProgram.cs')
if ($LASTEXITCODE -ne 0) { throw '运行界面多页签体验编译失败。' }
Copy-Item -LiteralPath $json -Destination $build -Force
Write-Output $output
