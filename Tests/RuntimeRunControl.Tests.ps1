param([string]$BuildDirectory = 'bin/x64/DesignerRunControlVerify')
# 只运行无设备测试入口，不启动生产主程序。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = (Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1).FullName
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'RuntimeRunControl.Tests.exe'
$references = @($application, (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll')
$arguments = $references | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @arguments (Join-Path $PSScriptRoot 'RuntimeRunControl.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Run control tests failed to compile.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Run control tests failed.' }
