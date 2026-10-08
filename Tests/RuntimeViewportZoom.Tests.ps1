param([ValidateSet('', 'windows', 'zoom')][string]$Case = '')
# 独立输出纯界面代码，不构建主程序；编辑器仅在屏幕外显示且不激活。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeViewportZoomVerify'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Forms/RuntimeDesigner') -Filter '*.cs' | Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } | ForEach-Object { $_.FullName })
$references = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
$output = Join-Path $build 'RuntimeViewportZoom.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /debug:full /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'RuntimeViewportZoom.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '视口缩放专项测试编译失败。' }
Copy-Item -LiteralPath $json -Destination $build -Force
if ($Case) { & $output $Case } else { & $output }
if ($LASTEXITCODE -ne 0) { throw '视口缩放专项测试失败。' }
