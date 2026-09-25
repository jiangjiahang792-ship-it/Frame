$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$root = Split-Path -Parent $PSScriptRoot
$cameraSourcePath = Join-Path $root 'Device\Camera\CameraHik.cs'
$cameraSource = Get-Content -LiteralPath $cameraSourcePath -Raw -Encoding UTF8

Assert-Contains `
    $cameraSource `
    'int liveCameraCount = Solution.Instance.CameraDevices.Count + Solution.Instance.Camera3DDevices.Count;' `
    '原始帧预算必须读取当前实际相机数量，避免资源档案相机数落后于五工位方案。'

Assert-Contains `
    $cameraSource `
    'return Math.Max(1, Math.Max(profileCameraCount, liveCameraCount));' `
    '原始帧预算必须取资源档案相机数和当前相机数的较大值，不能让旧档案按4台相机给5台方案分配缓冲。'

Write-Host '海康相机原始帧预算相机数量回归检查通过。'
