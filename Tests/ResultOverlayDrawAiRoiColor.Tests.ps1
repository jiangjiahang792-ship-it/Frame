# 编译并运行真实程序集的离线颜色回归，不加载方案设备。
param(
    [string]$BuildDirectory = 'bin/RoiColorValidation',
    [string]$SolutionPath = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExecutable = Join-Path $validationDirectory 'ResultOverlayDrawAiRoiColor.Tests.exe'
$references = @(
    (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe'),
    (Join-Path $validationDirectory 'Newtonsoft.Json.dll'),
    (Join-Path $validationDirectory 'OpenCvSharp.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll'
) | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'ResultOverlayDrawAiRoiColor.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'AI矩形颜色专项编译失败。' }
Push-Location $validationDirectory
try {
    if ($SolutionPath) { & $testExecutable $SolutionPath }
    else { & $testExecutable }
    if ($LASTEXITCODE -ne 0) { throw 'AI矩形颜色专项失败。' }
} finally { Pop-Location }
