# 编译并运行离线相机参数验证，使用项目真实程序集。
param([string]$BuildDirectory = 'bin/CameraTriggerModeValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $outputDirectory $_) }
$testPath = Join-Path $outputDirectory 'CameraTriggerMode.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'CameraTriggerMode.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '相机模式测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $outputDirectory '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
Push-Location $outputDirectory
try {
    & $testPath (Join-Path $projectRoot 'artifacts/CameraTriggerModeValidation')
    if ($LASTEXITCODE -ne 0) { throw '相机模式验证失败。' }
} finally { Pop-Location }
