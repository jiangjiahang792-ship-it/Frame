# 编译真实程序集上的自动订阅回归，仅创建临时窗体，不连接设备。
param([string]$BuildDirectory = 'bin/ContourMatchValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectoryPath = Join-Path $projectRoot $BuildDirectory
$compilerPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $buildDirectoryPath $_) }
$testPath = Join-Path $buildDirectoryPath 'SubscriptionAutoSelection.Tests.exe'
& $compilerPath /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'SubscriptionAutoSelection.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '自动订阅验证程序编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildDirectoryPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
Push-Location $buildDirectoryPath
try {
    & $testPath
    if ($LASTEXITCODE -ne 0) { throw '自动订阅验证失败。' }
} finally { Pop-Location }
