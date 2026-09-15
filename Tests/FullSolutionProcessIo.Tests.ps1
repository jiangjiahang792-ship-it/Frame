$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$stressToolPath = Join-Path $PSScriptRoot 'FullSolutionComparison.Tests.ps1'
$stressTool = Get-Content -LiteralPath $stressToolPath -Raw -Encoding UTF8

Assert-True ($stressTool.Contains('GetProcessIoCounters')) '完整方案测试器必须使用Windows进程I/O计数器。'
Assert-True ($stressTool.Contains('ReadTransferKilobytes')) '逐轮CSV必须记录读取传输量。'
Assert-True ($stressTool.Contains('WriteTransferKilobytes')) '逐轮CSV必须记录写入传输量。'
Assert-True ($stressTool.Contains('ProcessIdsStable')) '正式同比必须校验进程树未重启。'
Assert-True ($stressTool.Contains('串行同比第${round}轮进程树数量异常')) '串行同比必须逐轮校验主程序和AI进程数量。'
Assert-True ($stressTool.Contains('FindVisibleConfirmButton')) '正常关闭必须按进程和确认文案定位原生按钮。'
Assert-True ($stressTool.Contains('if (-not $KeepProcess -and $startedByScript -and $null -ne $mainProcess)')) '测试工具只能自动关闭自己启动的视觉进程。'
Assert-True ($stressTool.Contains('Stop-TestProcessTree')) '程序正常关闭失败时必须清理本工具拥有的测试进程树。'
Assert-True ($stressTool.Contains('本轮结果不能作为有效同比')) '程序或工作进程异常退出必须使同比测试失败。'
Assert-True ($stressTool.Contains('MeasurementScope')) '结果必须说明进程I/O与物理磁盘流量的边界。'

$nativeCodeMatch = [regex]::Match(
    $stressTool,
    'Add-Type -TypeDefinition @"\r?\n(?<code>.*?)\r?\n"@ -Language CSharp',
    [Text.RegularExpressions.RegexOptions]::Singleline)
Assert-True $nativeCodeMatch.Success '无法提取测试器中的Windows资源探针。'
Add-Type -TypeDefinition $nativeCodeMatch.Groups['code'].Value -Language CSharp

$temporaryFile = Join-Path ([IO.Path]::GetTempPath()) ("TDJS-Vision-ProcessIo-{0}.bin" -f [Guid]::NewGuid().ToString('N'))
$process = Get-Process -Id $PID
try {
    $before = [FullSolutionGuiResourceProbe]::QueryProcessIoCounters($process.Handle)
    $payload = New-Object byte[] (4MB)
    [Random]::new(20260820).NextBytes($payload)
    [IO.File]::WriteAllBytes($temporaryFile, $payload)
    $readBack = [IO.File]::ReadAllBytes($temporaryFile)
    $after = [FullSolutionGuiResourceProbe]::QueryProcessIoCounters($process.Handle)

    Assert-True ($readBack.Length -eq $payload.Length) '动态I/O验证必须完整读回测试文件。'
    Assert-True ($after.WriteOperationCount -gt $before.WriteOperationCount) '写入操作计数必须增加。'
    Assert-True ($after.ReadOperationCount -gt $before.ReadOperationCount) '读取操作计数必须增加。'
    Assert-True (($after.WriteTransferCount - $before.WriteTransferCount) -ge $payload.Length) '写入传输字节必须覆盖测试文件大小。'
    Assert-True (($after.ReadTransferCount - $before.ReadTransferCount) -ge $payload.Length) '读取传输字节必须覆盖测试文件大小。'
}
finally {
    if (Test-Path -LiteralPath $temporaryFile) {
        Remove-Item -LiteralPath $temporaryFile -Force
    }
}

Write-Host '完整方案进程I/O检查通过：Windows累计计数、逐轮读写量、进程稳定性和结果口径均已接通。'
