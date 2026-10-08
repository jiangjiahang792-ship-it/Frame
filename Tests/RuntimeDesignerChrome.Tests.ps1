param([string]$BuildDirectory = 'bin/x64/RuntimeDesignerChromeVerify')
# 独立编译设计器源码，不引用或启动生产主程序。
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$build = Join-Path $projectRoot $BuildDirectory
New-Item -ItemType Directory -Force -Path $build | Out-Null
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$json = Join-Path $projectRoot 'packages/Newtonsoft.Json.13.0.3/lib/net45/Newtonsoft.Json.dll'
$testExe = Join-Path $build 'RuntimeDesignerChrome.Tests.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Forms/RuntimeDesigner') -Filter '*.cs' | Where-Object { $_.Name -notin @('RuntimeBindingCatalog.cs', 'RuntimeBindingDataSource.cs', 'RuntimeFrameworkRunController.cs', 'RuntimeRoiSnapshot.cs', 'RuntimeFrameworkLogSource.cs') } | ForEach-Object { $_.FullName })
$references = @($json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExe" @references @sources (Join-Path $PSScriptRoot 'RuntimeDesignerChrome.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '窗口外观独立测试编译失败。' }
Copy-Item -LiteralPath $json -Destination $build -Force
& $testExe
if ($LASTEXITCODE -ne 0) { throw '窗口外观独立测试失败。' }
