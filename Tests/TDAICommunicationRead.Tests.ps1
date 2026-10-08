param([string]$BuildDirectory = 'bin/TDAIReadValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$assembly = Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1
$output = Join-Path $build 'TDAICommunicationRead.Tests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/reference:$($assembly.FullName)" "/reference:$(Join-Path $build 'Newtonsoft.Json.dll')" "/out:$output" (Join-Path $PSScriptRoot 'TDAICommunicationRead.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '测试编译失败。' }
Copy-Item -LiteralPath ($assembly.FullName + '.config') -Destination ($output + '.config') -Force
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '通信读取测试失败。' }
}
finally { Pop-Location }
