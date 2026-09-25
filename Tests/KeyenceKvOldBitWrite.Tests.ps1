# 使用真实项目程序集和随机本机端口验证DM字内布尔写入。
param([string]$BuildDirectory = 'bin/KeyenceNanoValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExecutable = Join-Path $validationDirectory 'KeyenceKvOldBitWrite.Tests.exe'
$mainExecutable = Get-ChildItem $validationDirectory -Filter '*AI*V1.0.exe' |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $mainExecutable) { throw 'Main executable not found.' }
$references = @(
    $mainExecutable,
    (Join-Path $validationDirectory 'HslCommunication.dll'),
    (Join-Path $validationDirectory 'SunnyUI.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll'
) | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'KeyenceKvOldBitWrite.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Keyence old bit write test compile failed.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'Keyence old bit write test failed.' }
