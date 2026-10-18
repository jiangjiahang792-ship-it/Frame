# 在已编译的独立输出目录中验证右下角文本框，不启动生产程序或连接设备。
param([string]$BuildDirectory = 'bin/CornerTextValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$testExecutable = Join-Path $validationDirectory 'ResultOverlayCornerText.Tests.exe'
$references = @(
    (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe'),
    (Join-Path $validationDirectory 'Newtonsoft.Json.dll'),
    'System.Drawing.dll', 'System.Windows.Forms.dll'
) | ForEach-Object { '/reference:' + $_ }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'ResultOverlayCornerText.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '右下角文本框测试编译失败。' }
Push-Location $validationDirectory
try {
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw '右下角文本框测试失败。' }
} finally { Pop-Location }
