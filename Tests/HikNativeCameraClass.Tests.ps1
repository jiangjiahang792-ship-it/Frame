param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$cameraFile = Join-Path $ProjectRoot "Device\Camera\CameraHik.cs"
$interfaceFile = Join-Path $ProjectRoot "Device\Camera\ICamera.cs"
$mainFormFile = Join-Path $ProjectRoot "FormMain.cs"
$projectFile = Join-Path $ProjectRoot "TDJS-Vision.csproj"
$vendorFile = Join-Path $ProjectRoot "Device\Camera\Vendor\MVCamera.cs"
$benchmarkVendorFile = Join-Path $ProjectRoot "tools\HikDualCameraMatBenchmark\Vendor\MVCamera.cs"

function Assert-TextContains {
    param([string]$Path, [string]$Pattern, [string]$Message)
    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-TextNotContains {
    param([string]$Path, [string]$Pattern, [string]$Message)
    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ($content -match $Pattern) {
        throw $Message
    }
}

Assert-TextContains $cameraFile "MV_CC_RegisterImageCallBackEx_NET" "CameraHik未使用官方原生图像回调。"
Assert-TextContains $cameraFile "MV_CC_ConvertPixelType_NET" "CameraHik未使用官方原生像素转换。"
Assert-TextContains $cameraFile "Marshal\.AllocHGlobal" "CameraHik未使用每相机预分配转换缓冲。"
Assert-TextContains $cameraFile "Mat\.FromPixelData" "CameraHik未从预分配内存建立Mat。"
Assert-TextContains $cameraFile "bufferView\.Clone" "CameraHik未创建回调返回后有效的独立Mat。"
Assert-TextContains $cameraFile "ReleaseConversionBuffer" "CameraHik缺少转换缓冲释放路径。"
Assert-TextNotContains $cameraFile "using MvCameraControl" "CameraHik仍引用托管MvCameraControl命名空间。"
Assert-TextNotContains $interfaceFile "MvCameraControl" "ICamera仍向业务层暴露托管SDK类型。"
Assert-TextNotContains $projectFile "MvCameraControl\.Net" "主工程仍引用或复制MvCameraControl.Net.dll。"

$mainFormContent = Get-Content -LiteralPath $mainFormFile -Raw -Encoding UTF8
$solutionResetIndex = $mainFormContent.IndexOf("solutionResourcesReleased = Solution.Instance.SolReset()", [StringComparison]::Ordinal)
$sdkFinalizeIndex = $mainFormContent.IndexOf("CameraHik.FinalizeSDK()", [StringComparison]::Ordinal)
if ($solutionResetIndex -lt 0 -or $sdkFinalizeIndex -lt 0 -or $solutionResetIndex -ge $sdkFinalizeIndex) {
    throw "主程序必须先释放方案和相机资源，再反初始化海康原生SDK。"
}

$vendorHash = (Get-FileHash -LiteralPath $vendorFile -Algorithm SHA256).Hash
$benchmarkVendorHash = (Get-FileHash -LiteralPath $benchmarkVendorFile -Algorithm SHA256).Hash
if ($vendorHash -ne $benchmarkVendorHash) {
    throw "主工程MVCamera.cs与已验证Demo版本不一致。"
}

Write-Output "海康原生类静态检查通过。"
