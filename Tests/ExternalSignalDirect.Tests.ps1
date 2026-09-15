# 编译真实节点回归，不连接现场设备。
param([string]$BuildDirectory = 'bin/ExternalSignalDirectValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$aiSource = Get-Content (Join-Path $root 'Node/5-EquipmentCommunication/AIResultSend/NodeSignalSend.cs') -Raw -Encoding UTF8
if ($aiSource.Contains('Task.Run(') -or -not $aiSource.Contains('await SendToModbus(')) { throw '旧AI信号发送不得后台投递后提前报告成功。' }
$tcpSource = Get-Content (Join-Path $root 'Node/5-EquipmentCommunication/TCPClient/NodeTCPClient.cs') -Raw -Encoding UTF8
if (-not $tcpSource.Contains('await SendRequest(')) { throw 'TCP节点必须等待实际发送任务。' }
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'HslCommunication.dll', 'Newtonsoft.Json.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'ExternalSignalDirect.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ExternalSignalDirect.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '外发直接执行测试编译失败。' }
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '外发直接执行测试失败。' }
} finally { Pop-Location }
