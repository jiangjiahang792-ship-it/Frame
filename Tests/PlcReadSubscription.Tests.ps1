# 使用已编译的真实程序集验证添加、恢复和本机上位链路通信，不启动主程序。
param([string]$BuildDirectory = 'bin/KeyenceNanoValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExecutable = Join-Path $validationDirectory 'PlcReadSubscription.Tests.exe'
$references = @(
    (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe'),
    (Join-Path $validationDirectory 'HslCommunication.dll'),
    (Join-Path $validationDirectory 'Newtonsoft.Json.dll'),
    (Join-Path $validationDirectory 'SunnyUI.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll'
) | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'PlcReadSubscription.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'PLC订阅专项测试编译失败。' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'PLC订阅专项测试失败。' }
