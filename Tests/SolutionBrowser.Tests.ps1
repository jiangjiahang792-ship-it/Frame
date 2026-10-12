param([string]$BuildDirectory = 'bin/SolutionBrowserVerify')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$test = Join-Path $build 'SolutionBrowser.Tests.exe'
$assembly = Join-Path $build '机器视觉AI检测系统V1.0.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$assembly" "/out:$test" (Join-Path $PSScriptRoot 'SolutionBrowser.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '方案列表回归编译失败。' }
Copy-Item -LiteralPath ($assembly + '.config') -Destination ($test + '.config') -Force
Push-Location $build
try {
    & $test
    if ($LASTEXITCODE -ne 0) { throw '方案列表回归失败。' }
}
finally { Pop-Location }
