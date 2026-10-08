param([string]$Template = 'bin/x64/DesignerVerify/设计器体验.runtime-ui.json')
# 独立编译设计器并在内存中计时；模板只读。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/x64/RuntimeDesignerPerformanceVerify'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$json = Join-Path $root 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
$exe = Join-Path $build 'RuntimeDesignerPerformance.Tests.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Forms/RuntimeDesigner') -Filter '*.cs' | Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } | ForEach-Object { $_.FullName })
$references = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$exe" @references @sources (Join-Path $PSScriptRoot 'RuntimeDesignerPerformance.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Performance harness compilation failed.' }
Copy-Item -LiteralPath $json -Destination $build -Force
& $exe (Join-Path $root $Template)
if ($LASTEXITCODE -ne 0) { throw 'Performance harness failed.' }
