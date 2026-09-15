# 在独立构建目录验证真实程序集，只连接随机本机模拟服务。
param([string]$BuildDirectory = 'bin/ResultSendValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExecutable = Join-Path $validationDirectory 'ResultSend.Tests.exe'
$references = @(
    (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe'),
    (Join-Path $validationDirectory 'HslCommunication.dll'),
    (Join-Path $validationDirectory 'Newtonsoft.Json.dll'),
    (Join-Path $validationDirectory 'SunnyUI.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll'
) | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'ResultSend.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '结果发送专项测试编译失败。' }
Push-Location $validationDirectory
try {
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw '结果发送专项测试失败。' }
} finally { Pop-Location }
