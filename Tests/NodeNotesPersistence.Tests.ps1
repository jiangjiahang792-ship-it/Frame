# 在独立输出目录验证实际方案保存与节点恢复，不加载生产方案或连接设备。
param([string]$BuildDirectory = 'bin/NotesValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = Join-Path $build '机器视觉AI检测系统V1.0.exe'
$output = Join-Path $build 'NodeNotesPersistence.Tests.exe'
$references = @($application, (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll') |
    ForEach-Object { '/reference:' + $_ }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 "/out:$output" @references (Join-Path $PSScriptRoot 'NodeNotesPersistence.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '备注持久化回归编译失败。' }
Copy-Item -LiteralPath ($application + '.config') -Destination ($output + '.config') -Force
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '备注持久化回归失败。' }
} finally { Pop-Location }
