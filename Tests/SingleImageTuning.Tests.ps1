param([string]$BuildDirectory = 'bin/SingleImageTuningValidation', [switch]$Benchmark)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$assembly = Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1
$output = Join-Path $build 'SingleImageTuning.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/reference:$($assembly.FullName)" "/reference:$(Join-Path $build 'WeifenLuo.WinFormsUI.Docking.dll')" "/reference:$(Join-Path $build 'Newtonsoft.Json.dll')" "/out:$output" (Join-Path $PSScriptRoot 'SingleImageTuning.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
Push-Location $build
try {
    $testArgs = @()
    if ($Benchmark) { $testArgs += '--benchmark' }
    & $output @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Single image tuning tests failed.' }
}
finally { Pop-Location }
