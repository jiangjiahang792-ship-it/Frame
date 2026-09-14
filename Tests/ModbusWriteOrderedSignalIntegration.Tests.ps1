# 保留原测试入口名；普通Modbus写入已改为直接执行，不再验证旧的工件排队。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$buildDirectory = if ($env:TDJS_TEST_BUILD_DIRECTORY) { $env:TDJS_TEST_BUILD_DIRECTORY } else { 'bin\x64\Debug' }
$build = Join-Path $root $buildDirectory
$source = Get-Content -LiteralPath (Join-Path $root 'Node/5-EquipmentCommunication/ModbusWrite/NodeModbusWrite.cs') -Raw -Encoding UTF8
if ($source.Contains('IOrderedExternalSignalNode') -or $source.Contains('SubmitOrderedSignal') -or
    $source.Contains('lock (') -or $source.Contains('SemaphoreSlim') -or $source.Contains('AcquireAsync')) {
    throw 'Modbus直接写入节点不能新增队列或发送锁。'
}
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'ModbusDirectWrite.Tests.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'HslCommunication.dll', 'Newtonsoft.Json.dll', 'SunnyUI.dll') |
    ForEach-Object { '/reference:' + (Join-Path $build $_) }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ModbusDirectWrite.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Modbus直接写入测试编译失败。' }
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Modbus直接写入测试失败。' }
} finally { Pop-Location }
