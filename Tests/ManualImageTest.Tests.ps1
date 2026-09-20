# 编译并运行真实程序集的手动图像测试回归，不连接现场设备。
param([string]$BuildDirectory = 'bin/CameraTriggerModeValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $outputDirectory $_) }
$testPath = Join-Path $outputDirectory 'ManualImageTest.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ManualImageTest.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '手动图像测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $outputDirectory '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
Push-Location $outputDirectory
try {
    & $testPath
    if ($LASTEXITCODE -ne 0) { throw '手动图像测试回归失败。' }
} finally { Pop-Location }
