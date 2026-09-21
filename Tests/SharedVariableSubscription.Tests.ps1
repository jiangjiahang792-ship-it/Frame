# 使用真实程序集验证共享变量订阅，不连接现场设备。
param([string]$BuildDirectory = 'bin/x64/Release', [string]$SolutionPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExecutable = Join-Path $validationDirectory 'SharedVariableSubscription.Tests.exe'
$references = @(
    (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe'),
    (Join-Path $validationDirectory 'Newtonsoft.Json.dll'),
    (Join-Path $validationDirectory 'SunnyUI.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll'
) | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'SharedVariableSubscription.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '共享变量订阅测试编译失败。' }
& $testExecutable $SolutionPath
if ($LASTEXITCODE -ne 0) { throw '共享变量订阅测试失败。' }
