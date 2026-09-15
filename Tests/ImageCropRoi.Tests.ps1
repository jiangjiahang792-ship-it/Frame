# 在独立构建目录验证真实图像控件和裁剪输出，不连接现场设备。
param([string]$BuildDirectory = 'bin/ImageCropValidation', [string]$ReferenceImage = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationDirectory = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExecutable = Join-Path $validationDirectory 'ImageCropRoi.Tests.exe'
$references = @(
    (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe'),
    (Join-Path $validationDirectory 'OpenCvSharp.dll'),
    (Join-Path $validationDirectory 'Newtonsoft.Json.dll'),
    (Join-Path $validationDirectory 'SunnyUI.dll'),
    'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll'
) | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExecutable" @references (Join-Path $PSScriptRoot 'ImageCropRoi.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '图像裁剪专项测试编译失败。' }
# 测试程序与主程序使用相同的程序集绑定策略。
Copy-Item -LiteralPath (Join-Path $validationDirectory '机器视觉AI检测系统V1.0.exe.config') -Destination ($testExecutable + '.config') -Force
Push-Location $validationDirectory
try {
    if ($ReferenceImage) { & $testExecutable $ReferenceImage }
    else { & $testExecutable }
    if ($LASTEXITCODE -ne 0) { throw '图像裁剪专项测试失败。' }
} finally { Pop-Location }
