param()
# 纯界面专项测试，不访问系统剪贴板、现场方案和设备。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeShortcutsTests'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
Copy-Item -LiteralPath $json -Destination $build -Force
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Forms/RuntimeDesigner') -Filter '*.cs' |
    Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } |
    ForEach-Object { $_.FullName })
$defines = @()
if (!(Test-Path (Join-Path $root 'Forms/RuntimeDesigner/RuntimeWidgetClipboard.cs'))) { $defines = @('/define:CLIPBOARD_CONTRACT_STUB') }
$references = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
$output = Join-Path $build 'RuntimeDesignerShortcuts.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @defines @references @sources (Join-Path $PSScriptRoot 'RuntimeDesignerShortcuts.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '设计器快捷键测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '设计器快捷键测试失败。' }
