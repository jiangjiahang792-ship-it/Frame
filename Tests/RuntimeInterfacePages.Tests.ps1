param([string]$BuildDirectory = 'bin/x64/RuntimeFrameworkVerify')
# 只引用正式程序集测试配置，不运行生产主程序。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = (Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1).FullName
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'RuntimeInterfacePages.Tests.exe'
$references = @($application, (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll') | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references (Join-Path $PSScriptRoot 'RuntimeInterfacePages.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Multiple runtime interface test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Multiple runtime interface tests failed.' }
