$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Path $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-Order {
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

$processSource = Get-ProjectSource 'Process.cs'

Assert-Contains $processSource 'private async Task<CameraCallbackFastPathResult> TryRunCameraCallbackFastPath(NodeBase nodeImage)' 'Process must expose a camera-callback fast path helper.'
Assert-Contains $processSource 'CanUseCameraCallbackFastPath(nodeImage, out fastStartNode)' 'Fast path must be guarded by an explicit eligibility check.'
Assert-Contains $processSource 'fastStartNode.NodeType != NodeType.ImageRotate' 'Fast path must initially be limited to image-source -> image-rotate chains.'
Assert-Contains $processSource 'RunConnectedNode(fastStartNode, null)' 'Fast path must run the first downstream node directly instead of re-entering the graph through the skipped image source.'
Assert-Contains $processSource 'RunConnectedNodes(null, null, resumeNode)' 'Fast path must resume the normal graph runtime from the first node downstream.'
Assert-Contains $processSource 'TryGetSingleDefaultNextNode(nodeImage, out fastStartNode)' 'Fast path must require a single default downstream node from the skipped image source.'
Assert-Contains $processSource 'CountDefaultNextNodes(fastStartNode, out ignoredResumeNode) > 1' 'Fast path must fall back when the directly-run node has multiple default outputs.'
Assert-Contains $processSource 'CameraCallbackFastPathResult.NotHandled' 'Fast path must report when the normal graph runtime should handle the flow.'

Assert-Order $processSource 'if (!TryPrepareSkippedNodeRunResult(nodeImage))' 'TryRunCameraCallbackFastPath(nodeImage)' 'Fast path must run only after the callback image-source result has been prepared.'
Assert-Order $processSource 'TryRunCameraCallbackFastPath(nodeImage)' 'RunConnectedNodes(nodeImage, null, nodeImage)' 'Normal graph runtime must remain as the fallback after the fast path.'

$cameraSource = Get-ProjectSource 'Device\Camera\CameraHik.cs'
Assert-Contains $cameraSource 'ConcurrentQueue<RawFrameIngressFailure> _rawFrameIngressFailures' 'Raw ingress failures must preserve each frame routing identity in a queue.'
Assert-Contains $cameraSource '_rawFrameIngressFailures.Enqueue(failure)' 'SDK callback must enqueue every raw ingress failure without blocking.'
Assert-Contains $cameraSource 'while (_rawFrameIngressFailures.TryDequeue(out ingressFailure))' 'Conversion worker must report every queued ingress failure independently.'
Assert-NotContains $cameraSource 'CompareExchange(ref _rawFrameIngressFailure' 'A single first-failure slot can hide a later production failure behind a preview failure.'

Write-Host 'Camera callback fast path regression checks passed.'
