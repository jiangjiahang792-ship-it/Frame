param(
    [string]$ResultJson = "docs/manual-assets/real-workflow/capture-result.json"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ResultJson)) {
    throw "真实流程截图结果文件不存在：$ResultJson"
}

$results = Get-Content -LiteralPath $ResultJson -Encoding UTF8 -Raw | ConvertFrom-Json
if ($results.Count -ne 86) {
    throw "截图任务数量应为 86，实际为 $($results.Count)。"
}

$failed = @($results | Where-Object status -ne 'captured')
if ($failed.Count -gt 0) {
    $names = ($failed | ForEach-Object module) -join '、'
    throw "存在未完成截图：$names"
}

$canvas = $results | Where-Object module -eq '流程画布' | Select-Object -First 1
if ($null -eq $canvas) {
    throw '截图结果中缺少“流程画布”。'
}
if ([int]$canvas.node_count -lt 4) {
    throw "流程画布节点数量不足：$($canvas.node_count)。"
}
if ([int]$canvas.connection_count -lt 3) {
    throw "流程画布连接数量不足：$($canvas.connection_count)。"
}

foreach ($item in $results) {
    if (-not (Test-Path -LiteralPath $item.file)) {
        throw "模块[$($item.module)]的截图不存在：$($item.file)"
    }
}

Write-Output "MODULES=86 CAPTURED=86 CANVAS_NODES=$($canvas.node_count) CANVAS_CONNECTIONS=$($canvas.connection_count) ERRORS=0"




