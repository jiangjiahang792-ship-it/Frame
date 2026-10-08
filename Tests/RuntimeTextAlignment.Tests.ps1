param()
# 纯属性和位图测试，不启动生产主程序或访问PLC。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeTextAlignmentTests'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
Copy-Item -LiteralPath $json -Destination $build -Force
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Forms/RuntimeDesigner') -Filter '*.cs' |
    Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } |
    ForEach-Object { $_.FullName })
$references = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
$output = Join-Path $build 'RuntimeTextAlignment.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references @sources (Join-Path $PSScriptRoot 'RuntimeTextAlignment.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '文本对齐测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '文本对齐测试失败。' }
