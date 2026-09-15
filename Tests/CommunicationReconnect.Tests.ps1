# 编译并执行独立本机模拟测试，不连接现场设备。
param([string]$BuildDirectory = 'bin/CommunicationReconnectValidation', [switch]$SignalsOnly, [switch]$SerialOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'HslCommunication.dll', 'Newtonsoft.Json.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'CommunicationReconnect.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'CommunicationReconnect.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '重连测试编译失败。' }
Push-Location $build
try {
    if ($SignalsOnly) { & $output signals } elseif ($SerialOnly) { & $output serial } else { & $output }
    if ($LASTEXITCODE -ne 0) { throw '重连测试失败。' }
} finally { Pop-Location }
