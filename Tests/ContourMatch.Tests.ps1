# 编译并验证真实程序集与原生DLL，输出少量界面截图，不连接现场设备。
param([string]$BuildDirectory = 'bin/ContourMatchValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectoryPath = Join-Path $projectRoot $BuildDirectory
$compilerPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $buildDirectoryPath $_) }
$testPath = Join-Path $buildDirectoryPath 'ContourMatch.Tests.exe'
& $compilerPath /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ContourMatch.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '轮廓模板验证程序编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildDirectoryPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
Push-Location $buildDirectoryPath
try {
    & $testPath (Join-Path $projectRoot 'artifacts/ContourMatchValidation')
    if ($LASTEXITCODE -ne 0) { throw '轮廓模板匹配验证失败。' }
} finally { Pop-Location }
