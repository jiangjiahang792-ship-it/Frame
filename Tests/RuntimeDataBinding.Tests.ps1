param([string]$BuildDirectory = 'bin/x64/DesignerBevelVerify')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = Get-ChildItem -LiteralPath $build -Filter '*.exe' | Where-Object { $_.Name -like '*V1.0.exe' } | Select-Object -First 1
if ($null -eq $application) { throw 'Build the application first.' }
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExe = Join-Path $build 'RuntimeDataBinding.Tests.exe'
$references = @($application.FullName, (Join-Path $build 'Newtonsoft.Json.dll'), (Join-Path $build 'OpenCvSharp.dll'), (Join-Path $build 'OpenCvSharp.Extensions.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExe" @references (Join-Path $PSScriptRoot 'RuntimeDataBinding.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Binding test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Binding tests failed.' }
