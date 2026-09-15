# 使用真实项目程序集和随机本机端口验证DM字内布尔写入。
param([string]$BuildDirectory = 'bin/KeyenceNanoValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExecutable = Join-Path $validationDirectory 'KeyenceKvOldBitWrite.Tests.exe'
$references = @(
    (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe'),
    (Join-Path $validationDirectory 'HslCommunication.dll'),
    (Join-Path $validationDirectory 'SunnyUI.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll'
) | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'KeyenceKvOldBitWrite.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'DM位写入专项测试编译失败。' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'DM位写入专项测试失败。' }
