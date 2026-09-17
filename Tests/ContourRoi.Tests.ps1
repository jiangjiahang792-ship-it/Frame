# 验证组合区域和真实算法，独立于正在运行的发布程序。
param([string]$BuildDirectory = 'bin/ContourRoiValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testBuildPath = Join-Path $projectRoot $BuildDirectory
$compilerPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $testBuildPath $_) }
$testPath = Join-Path $testBuildPath 'ContourRoi.Tests.exe'
& $compilerPath /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ContourRoi.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '组合区域验证编译失败。' }
Copy-Item -LiteralPath (Join-Path $testBuildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
Push-Location $testBuildPath
try {
    & $testPath (Join-Path $projectRoot 'artifacts/ContourRoiValidation')
    if ($LASTEXITCODE -ne 0) { throw '组合区域验证失败。' }
} finally { Pop-Location }
