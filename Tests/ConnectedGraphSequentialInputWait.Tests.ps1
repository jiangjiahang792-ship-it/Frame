param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    if ($Text -notlike "*$Expected*") {
        throw $Message
    }
}

function Assert-BeforeText {
    param(
        [string]$Text,
        [string]$First,
        [string]$Second,
        [string]$Message
    )

    $firstIndex = $Text.IndexOf($First)
    $secondIndex = $Text.IndexOf($Second)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw $Message
    }
}

$processPath = Join-Path $ProjectRoot 'Process.cs'
$processSource = Get-Content -LiteralPath $processPath -Raw -Encoding UTF8

Assert-ContainsText `
    -Text $processSource `
    -Expected 'HasPendingIncomingConnection(node, visitedNodeIds, failedNodeIds, pendingNodes)' `
    -Message 'Sequential graph runtime must check unfinished incoming connections before executing a node.'

Assert-BeforeText `
    -Text $processSource `
    -First 'HasPendingIncomingConnection(node, visitedNodeIds, failedNodeIds, pendingNodes)' `
    -Second 'visitedNodeIds.Add(node.ID)' `
    -Message 'Incoming connection wait must happen before the node is marked visited.'

Assert-ContainsText `
    -Text $processSource `
    -Expected 'IsPendingOrReachableFromPendingNodes(pendingNodes, upstreamNodeId)' `
    -Message 'Sequential graph runtime must wait for upstream nodes that are reachable from the pending queue.'

Write-Host 'Connected graph sequential input wait check passed.'
