param()
# 只编译多选模型与编码器，不读取系统剪贴板、不运行生产程序集。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeMultiSelectionBackendVerify'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
$sources = @('RuntimeLayoutDocument.cs', 'RuntimeLayoutSerializer.cs', 'RuntimeLayoutSession.cs',
    'RuntimeDataBinding.cs', 'RuntimeLog.cs', 'RuntimeRunControl.cs', 'RuntimeWidgetClipboard.cs') |
    ForEach-Object { Join-Path $root ('Forms/RuntimeDesigner/' + $_) }
$references = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') |
    ForEach-Object { '/reference:' + $_ }
$output = Join-Path $build 'RuntimeMultiSelectionBackend.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'RuntimeMultiSelectionBackend.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '多选后端专项测试编译失败。' }
Copy-Item -LiteralPath $json -Destination $build -Force
& $output
if ($LASTEXITCODE -ne 0) { throw '多选后端专项测试失败。' }
